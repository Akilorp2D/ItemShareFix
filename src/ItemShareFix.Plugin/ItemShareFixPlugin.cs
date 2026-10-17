using System;
using BepInEx;
using HarmonyLib;
using RoR2;
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
        public const string PluginVersion = "1.2.0";

        private Harmony? _harmony;
        private PluginConfig? _config;
        private ServerCoordinator? _server;
        private ClientPresentationCoordinator? _presentation;
        private bool _active;
        private int _riskOfOptionsLocalizationWarmupFrames;
        private UpstreamBridge? _catalogUpstream;
        private bool _dlcCatalogLogged;

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

                PresentationColdPrimer.PrimeCommandPresentationDuringInitialization(Logger);
                var upstream = new UpstreamBridge(compatibility.ItemShareAssembly!, compatibility.PickupShareApiAssembly!, Logger);
                _server = new ServerCoordinator(_config, upstream, Logger);
                _presentation = new ClientPresentationCoordinator(_config, upstream, Logger);
                _harmony = new Harmony(PluginGuid);
                RuntimePatches.Install(_harmony, compatibility, _server, _presentation);
                _active = true;
                _catalogUpstream = upstream;
                RoR2Application.onLoad += LogDlcPickupCatalog;
                if (RoR2Application.loadFinished) LogDlcPickupCatalog();
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

        private void LogDlcPickupCatalog()
        {
            if (_dlcCatalogLogged || !_active || _config == null) return;
            _dlcCatalogLogged = true;
            RoR2Application.onLoad -= LogDlcPickupCatalog;
            if (!_config.DiagnosticInfoEnabled) return;
            try
            {
                Logger.LogInfo("ISF_DLC4_CATALOG build=" + RoR2Application.GetBuildId() + " policy=upstream-authority");
                foreach (var itemIndex in ItemCatalog.allItems)
                {
                    var item = ItemCatalog.GetItemDef(itemIndex);
                    if (item == null || (item.tier != ItemTier.Cursed && item.tier != ItemTier.Page && item.tier != ItemTier.Hex)) continue;
                    var pickupIndex = PickupCatalog.FindPickupIndex(itemIndex);
                    var def = PickupCatalog.GetPickupDef(pickupIndex);
                    var tierDef = ItemTierCatalog.GetItemTierDef(item.tier);
                    var shareable = def != null && _catalogUpstream != null && _catalogUpstream.TryIsShareable(def, out var value)
                        ? value.ToString() : "unresolved";
                    Logger.LogInfo("ISF_DLC4_ITEM name=" + item.name + " token=" + item.nameToken
                        + " itemIndex=" + (int)itemIndex + " pickupIndex=" + pickupIndex.value
                        + " tier=" + item.tier + " tags=" + string.Join(",", item.tags ?? Array.Empty<ItemTag>())
                        + " expansion=" + (item.requiredExpansion != null ? item.requiredExpansion.name : "none")
                        + " pickupRules=" + (tierDef != null ? tierDef.pickupRules.ToString() : "unresolved")
                        + " upstreamShareable=" + shareable);
                }
                var book = DLC4Content.KeyItems.CursedBook;
                Logger.LogInfo("ISF_DLC4_BOOK name=" + (book != null ? book.name : "absent")
                    + " keyItemIndex=" + (book != null ? book.keyItemIndex.ToString() : "none"));
            }
            catch (Exception ex)
            {
                Logger.LogWarning("ISF_DLC4_CATALOG unavailable: " + ex.GetType().Name);
            }
        }

        private void FixedUpdate()
        {
            if (!_active || _config == null) return;
            _server?.Tick();
            if (!_config.Enabled.Value)
            {
                _presentation?.RestoreAll();
                return;
            }
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
            RoR2Application.onLoad -= LogDlcPickupCatalog;
            _active = false;
            try { _server?.Dispose(); } catch { }
            try { _presentation?.Dispose(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
                try { _server?.Dispose(); } catch { }
        }
    }
}
