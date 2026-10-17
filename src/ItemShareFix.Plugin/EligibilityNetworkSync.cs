using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ItemShareFix.Core;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemShareFix
{
    // Uses the game's reliable UNet channel. Requests/replies are presentation metadata only.
    internal sealed class EligibilityNetworkSync : IDisposable
    {
        internal const short RequestMessageId = 28761;
        internal const short SnapshotMessageId = 28762;
        internal const int MaxOptions = 4096;
        private const byte ProtocolVersion = 1;
        private readonly ServerCoordinator _server;
        private readonly PluginConfig _config;
        private readonly ManualLogSource _log;
        private readonly AuthoritativeEligibilityMirror<Component, PickupDef> _mirror = new AuthoritativeEligibilityMirror<Component, PickupDef>();
        private readonly NetworkMessageDelegate _requestHandler;
        private readonly NetworkMessageDelegate _snapshotHandler;
        private NetworkClient? _client;
        private Run? _run;
        private int _stage;
        private float _nextPrune;
        private bool _failureLogged;

        private sealed class Request : MessageBase
        {
            public uint RunId, ObjectId, Nonce;
            public int Stage;
            public byte Kind;
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(ProtocolVersion); writer.Write(RunId); writer.Write(Stage);
                writer.Write(Kind); writer.Write(ObjectId); writer.Write(Nonce);
            }
            public override void Deserialize(NetworkReader reader)
            {
                if (reader.ReadByte() != ProtocolVersion) throw new InvalidOperationException("Eligibility protocol mismatch.");
                RunId = reader.ReadUInt32(); Stage = reader.ReadInt32();
                Kind = reader.ReadByte(); ObjectId = reader.ReadUInt32(); Nonce = reader.ReadUInt32();
                if (Kind > 1 || ObjectId == 0 || Nonce == 0) throw new InvalidOperationException("Invalid eligibility request.");
            }
        }

        private sealed class Snapshot : MessageBase
        {
            public Request Header = new Request();
            public int[] Options = Array.Empty<int>();
            public uint[] Collectors = Array.Empty<uint>();
            public override void Serialize(NetworkWriter writer)
            {
                Header.Serialize(writer); writer.Write(Options.Length);
                foreach (var option in Options) writer.Write(option);
                writer.Write(Collectors.Length);
                foreach (var master in Collectors) writer.Write(master);
            }
            public override void Deserialize(NetworkReader reader)
            {
                Header.Deserialize(reader);
                var count = reader.ReadInt32();
                if (count < 0 || count > MaxOptions) throw new InvalidOperationException("Invalid eligibility option count.");
                Options = new int[count];
                for (var i = 0; i < count; i++) Options[i] = reader.ReadInt32();
                var collectorCount = reader.ReadInt32();
                if (collectorCount < 0 || collectorCount > MaxOptions) throw new InvalidOperationException("Invalid eligibility collector count.");
                Collectors = new uint[collectorCount];
                for (var i = 0; i < collectorCount; i++) Collectors[i] = reader.ReadUInt32();
            }
        }

        public EligibilityNetworkSync(ServerCoordinator server, PluginConfig config, ManualLogSource log)
        {
            _server = server; _config = config; _log = log;
            _requestHandler = OnRequest; _snapshotHandler = OnSnapshot;
            NetworkManagerSystem.onStartServerGlobal += RegisterServer;
            NetworkManagerSystem.onStartClientGlobal += RegisterClient;
            NetworkManagerSystem.onStopClientGlobal += StopClient;
            if (NetworkServer.active) RegisterServer();
            foreach (var client in NetworkClient.allClients) RegisterClient(client);
        }

        private void RegisterServer()
        {
            if (NetworkServer.handlers.TryGetValue(RequestMessageId, out var existing) && existing != _requestHandler)
            { LogFailure("Request message ID already owned by another handler."); return; }
            NetworkServer.RegisterHandler(RequestMessageId, _requestHandler);
        }
        private void RegisterClient(NetworkClient client)
        {
            if (client.handlers.TryGetValue(SnapshotMessageId, out var existing) && existing != _snapshotHandler)
            { LogFailure("Snapshot message ID already owned by another handler."); return; }
            client.RegisterHandler(SnapshotMessageId, _snapshotHandler);
            _client = client;
            _mirror.Clear();
        }
        private void StopClient() { _client = null; _mirror.Clear(); }

        public void Tick()
        {
            EnsureEpoch();
            if (Time.unscaledTime < _nextPrune) return;
            _nextPrune = Time.unscaledTime + 1f;
            _mirror.Prune(owner => owner != null);
        }
        private void EnsureEpoch()
        {
            var run = Run.instance;
            var stage = run != null ? run.stageClearCount : -1;
            if (ReferenceEquals(run, _run) && stage == _stage) return;
            _run = run; _stage = stage; _mirror.Clear();
        }
        public bool Contains(Component owner, PickupDef def)
        {
            EnsureEpoch();
            return _mirror.Contains(owner.GetInstanceID(), owner, def);
        }
        public bool TryCollected(Component owner, uint master, out bool collected)
        {
            EnsureEpoch();
            return _mirror.TryCollected(owner.GetInstanceID(), owner, master, out collected);
        }
        public bool HasRetainedOptions(Component owner)
        {
            EnsureEpoch();
            return _mirror.HasRetainedOptions(owner.GetInstanceID(), owner);
        }
        public void Observe(Component owner)
        {
            EnsureEpoch();
            if (NetworkServer.active || !_config.Enabled.Value || _run == null || _run.netId.Value == 0
                || _client == null || !_client.isConnected) return;
            if (_client.handlers.TryGetValue(SnapshotMessageId, out var handler) && handler != _snapshotHandler) return;
            var identity = owner.GetComponent<NetworkIdentity>();
            if (identity == null || identity.netId.Value == 0) return;
            var nonce = _mirror.Request(owner.GetInstanceID(), owner, Time.unscaledTime);
            if (nonce == 0) return;
            _client.Send(RequestMessageId, new Request
            {
                RunId = _run.netId.Value, Stage = _stage, ObjectId = identity.netId.Value,
                Kind = owner is PickupPickerController ? (byte)1 : (byte)0, Nonce = nonce
            });
        }

        private static Component? ResolveOwner(GameObject? obj, byte kind)
            => obj == null ? null : kind == 0 ? (Component?)obj.GetComponent<GenericPickupController>() : obj.GetComponent<PickupPickerController>();
        private bool MatchesEpoch(Request request)
            => _run != null && _run.netId.Value != 0 && request.RunId == _run.netId.Value && request.Stage == _stage;

        private void OnRequest(NetworkMessage message)
        {
            if (!NetworkServer.active || !_config.Enabled.Value || message.conn == null || !message.conn.isReady) return;
            try
            {
                EnsureEpoch();
                var request = message.ReadMessage<Request>();
                if (!MatchesEpoch(request)) return;
                var owner = ResolveOwner(NetworkServer.FindLocalObject(new NetworkInstanceId(request.ObjectId)), request.Kind);
                if (owner == null) return;
                var options = new List<int>();
                foreach (var def in _server.RetainedOptions(owner))
                {
                    if (KnownProgressionPickupCompatibility.IsExcluded(def)) continue;
                    options.Add(def.pickupIndex.value);
                    if (options.Count > MaxOptions) { LogFailure("Eligibility snapshot exceeds supported option count."); return; }
                }
                var collectors = _server.CollectedMasterIds(owner);
                if (collectors.Length > MaxOptions) { LogFailure("Eligibility collector count exceeds supported limit."); return; }
                message.conn.Send(SnapshotMessageId, new Snapshot { Header = request, Options = options.ToArray(), Collectors = collectors });
            }
            catch (Exception ex) { LogFailure("Request rejected: " + ex.GetType().Name); }
        }
        private void OnSnapshot(NetworkMessage message)
        {
            if (NetworkServer.active || _client == null || !ReferenceEquals(message.conn, _client.connection)) return;
            try
            {
                EnsureEpoch();
                var snapshot = message.ReadMessage<Snapshot>();
                if (!MatchesEpoch(snapshot.Header)) return;
                var owner = ResolveOwner(ClientScene.FindLocalObject(new NetworkInstanceId(snapshot.Header.ObjectId)), snapshot.Header.Kind);
                if (owner == null) return; // Next bounded request retries after the object has spawned.
                var allowed = new List<PickupDef>(snapshot.Options.Length);
                foreach (var index in snapshot.Options)
                {
                    var def = PickupCatalog.GetPickupDef(new PickupIndex(index));
                    if (def == null) return; // Atomic reject; never apply a partial/unknown snapshot.
                    if (!KnownProgressionPickupCompatibility.IsExcluded(def)) allowed.Add(def);
                }
                if (_mirror.Apply(owner.GetInstanceID(), owner, snapshot.Header.Nonce, allowed, snapshot.Collectors)) RuntimePatches.RequestPresentationRefresh();
            }
            catch (Exception ex) { LogFailure("Snapshot rejected: " + ex.GetType().Name); }
        }
        private void LogFailure(string reason)
        {
            if (_failureLogged) return;
            _failureLogged = true;
            _log.LogWarning("[ItemShareFix] eligibility presentation sync unavailable: " + reason);
        }
        public void Dispose()
        {
            NetworkManagerSystem.onStartServerGlobal -= RegisterServer;
            NetworkManagerSystem.onStartClientGlobal -= RegisterClient;
            NetworkManagerSystem.onStopClientGlobal -= StopClient;
            if (NetworkServer.active && NetworkServer.handlers.TryGetValue(RequestMessageId, out var serverHandler) && serverHandler == _requestHandler)
                NetworkServer.UnregisterHandler(RequestMessageId);
            foreach (var client in NetworkClient.allClients)
                if (client.handlers.TryGetValue(SnapshotMessageId, out var handler) && handler == _snapshotHandler) client.UnregisterHandler(SnapshotMessageId);
            _client = null; _mirror.Clear();
        }
    }
}
