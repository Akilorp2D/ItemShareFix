using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ItemShareFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.majai.pickupshareapi", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.majai.itemshare", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ItemShareFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.itemsharefix";
        public const string PluginName = "ItemShareFix";
        public const string PluginVersion = "1.1.0";

        private Harmony? _harmony;
        private PluginConfig? _config;
        private ServerCoordinator? _server;
        private ClientPresentationCoordinator? _presentation;
        private bool _active;
        private int _riskOfOptionsLocalizationWarmupFrames;

        private void Awake()
        {
            _config = new PluginConfig(Config);
            if (!_config.Enabled.Value)
            {
                Logger.LogInfo("[ItemShareFix] disabled by configuration.");
                return;
            }

            var compatibility = CompatibilityGuard.Probe(Logger);
            if (!compatibility.Supported)
            {
                Logger.LogError("[ItemShareFix] FAIL-CLOSED: " + compatibility.Reason);
                enabled = false;
                return;
            }

            try
            {
                if (RemoteOperationProbe.TryVerifyRuntimeShape(out var remoteOperationShapeEvidence))
                    Logger.LogInfo("[ItemShareFix] Remote Operation runtime shape PASS: " + remoteOperationShapeEvidence);
                else
                    Logger.LogWarning("[ItemShareFix] Remote Operation runtime shape unavailable; Support Drone classification will fail closed: " + remoteOperationShapeEvidence);

                var upstream = new UpstreamBridge(compatibility.ItemShareAssembly!, compatibility.PickupShareApiAssembly!, Logger);
                _server = new ServerCoordinator(_config, upstream, Logger);
                _presentation = new ClientPresentationCoordinator(_config, upstream, Logger);
                _harmony = new Harmony(PluginGuid);
                RuntimePatches.Install(_harmony, compatibility, _server, _presentation);
                _active = true;
                Logger.LogInfo("[ItemShareFix] world-space marker clustering active: adaptive LOD, Detailed default / Compact optional presentation, HUD/modal suppression enabled. Gameplay/share ownership remains unchanged.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[ItemShareFix] initialization failed closed: " + ex);
                try { _harmony?.UnpatchSelf(); } catch { }
                _presentation?.RestoreAll();
                _active = false;
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!_active || _config == null) return;
            if (!_config.Enabled.Value)
            {
                _presentation?.RestoreAll();
                return;
            }
            _server?.Tick();
            _presentation?.Tick();
        }

        private void Update()
        {
            if (_config != null)
            {
                if (_riskOfOptionsLocalizationWarmupFrames < 3) _riskOfOptionsLocalizationWarmupFrames++;
                if (_riskOfOptionsLocalizationWarmupFrames >= 3)
                {
                    OptionalRiskOfOptionsIntegration.TryRegister(_config, Logger);
                    OptionalRiskOfOptionsIntegration.TryRefreshLocalization(Logger);
                }
            }
            if (!_active || _presentation == null) return;
            _presentation.RecordUnityUpdate();
            _presentation.RenderFrame();
        }

        private void OnDestroy()
        {
            _active = false;
            try { _presentation?.Dispose(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
        }
    }
}
