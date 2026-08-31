using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using ItemShareFix.Core;

namespace ItemShareFix
{
    internal static class RuntimePatches
    {
        private static ServerCoordinator? _server;
        private static ClientPresentationCoordinator? _presentation;
        private static FieldInfo? _itemShareClaimsField;
        private static FieldInfo? _itemShareDistributedField;
        private static FieldInfo? _itemShareChoicesField;

        public static void Install(Harmony harmony, CompatibilityResult compatibility, ServerCoordinator server, ClientPresentationCoordinator presentation)
        {
            _server = server;
            _presentation = presentation;
            var itemShare = compatibility.ItemShareAssembly ?? throw new InvalidOperationException("ItemShare assembly missing from compatibility result.");
            var pluginType = itemShare.GetType("ItemShare.ItemSharePlugin", true)!;
            var providerType = itemShare.GetType("ItemShare.ItemShareStateProvider", true)!;
            _itemShareClaimsField = RequiredField(pluginType, "Claims");
            _itemShareDistributedField = RequiredField(pluginType, "Distributed");
            _itemShareChoicesField = RequiredField(pluginType, "Choices");

            Patch(harmony, Required(pluginType, "OnAttemptGrant", 3, typeof(void), isStatic: true), prefix: nameof(ItemShareAttemptGrantPrefix));
            Patch(harmony, Required(pluginType, "GrantIndividual", 5, typeof(void), isStatic: true), prefix: nameof(GrantIndividualPrefix), postfix: nameof(DistributionPostfix), finalizer: nameof(DistributionFinalizer));
            Patch(harmony, Required(pluginType, "GrantInstant", 5, typeof(void), isStatic: true), prefix: nameof(GrantInstantPrefix), postfix: nameof(DistributionPostfix), finalizer: nameof(DistributionFinalizer));
            Patch(harmony, Required(pluginType, "IsDown", 1, typeof(bool), isStatic: true), postfix: nameof(IsDownPostfix));
            Patch(harmony, Required(pluginType, "OnPickupSelected", 3, typeof(void), isStatic: true), prefix: nameof(ItemShareCommandSelectionPrefix), postfix: nameof(ItemShareCommandSelectionPostfix));
            Patch(harmony, Required(pluginType, "GiveDirect", 3, typeof(bool), isStatic: true), prefix: nameof(GiveDirectPrefix));
            Patch(harmony, Required(pluginType, "LocalPlayersHaveTaken", 1, typeof(bool), isStatic: true), postfix: nameof(LocalPlayersHaveTakenPostfix));
            Patch(harmony, Required(pluginType, "ApplyOrbVisibility", 2, typeof(void), isStatic: true), postfix: nameof(ApplyOrbVisibilityPostfix));
            Patch(harmony, Required(pluginType, "RefreshOrbVisibility", 0, typeof(void), isStatic: true), postfix: nameof(RefreshVisibilityPostfix));
            Patch(harmony, Required(providerType, "TransferOrbState", 2, typeof(bool), isStatic: false), postfix: nameof(TransferOrbStatePostfix));

            // Ordinary shared pickups remain one authoritative network object, but a
            // local collector that already owns this exact claim must no longer be a valid IInteractable
            // target. This is a participant/interactor-specific presentation decision only.
            Patch(harmony, Required(typeof(GenericPickupController), "GetInteractability", 1, typeof(Interactability), isStatic: false), postfix: nameof(PickupGetInteractabilityPostfix));

            if (compatibility.DisconnectMethod != null)
                Patch(harmony, compatibility.DisconnectMethod, prefix: nameof(NetworkDestroyObservationPrefix));

            InstallBlockingModalLifecyclePatches(harmony);
        }

        private static bool ItemShareAttemptGrantPrefix(object[] __args)
        {
            if (_server == null || !NetworkServer.active || __args.Length != 3) return true;
            if (__args[1] is not GenericPickupController self) return true;

            var pickup = self.pickup;
            var isTemporary = pickup.isTempItem;
            if (!isTemporary) return true;

            var instanceId = self.GetInstanceID();
            var upstreamAlreadyOwned = ContainsInstanceId(_itemShareClaimsField, instanceId)
                || ContainsInstanceId(_itemShareDistributedField, instanceId);
            var shareTemporaryItems = _server.ShareTemporaryItemsEnabled;
            var vanillaBypass = TemporarySharingPolicy.ShouldUseVanillaBypass(
                isTemporary,
                shareTemporaryItems,
                upstreamAlreadyOwned);

            if (!vanillaBypass)
            {
                _server.LogTemporaryPolicy("ordinary", instanceId, shareTemporaryItems, "itemshare",
                    upstreamAlreadyOwned ? "existing-upstream-state" : "policy-on");
                return true;
            }

            InvokeSuppliedOriginal(__args[0], self, __args[2]);
            _server.LogTemporaryPolicy("ordinary", instanceId, shareTemporaryItems, "vanilla-bypass", "preclaim");
            return false;
        }

        private static bool ItemShareCommandSelectionPrefix(object[] __args, out bool __state)
        {
            __state = false;
            if (_server == null || !NetworkServer.active || __args.Length != 3) return true;
            if (__args[1] is not PickupPickerController picker || __args[2] is not int choiceIndex) return true;
            if (picker.options == null || choiceIndex < 0 || choiceIndex >= picker.options.Length) return true;

            var option = picker.options[choiceIndex];
            if (!option.available) return true;
            var selectedPickup = option.pickup;
            var isTemporary = selectedPickup.isTempItem;
            if (!isTemporary) return true;

            var instanceId = picker.GetInstanceID();
            var upstreamAlreadyOwned = ContainsInstanceId(_itemShareChoicesField, instanceId);
            var shareTemporaryItems = _server.ShareTemporaryItemsEnabled;
            var vanillaBypass = TemporarySharingPolicy.ShouldUseVanillaBypass(
                isTemporary,
                shareTemporaryItems,
                upstreamAlreadyOwned);

            if (!vanillaBypass)
            {
                _server.LogTemporaryPolicy("command", instanceId, shareTemporaryItems, "itemshare",
                    upstreamAlreadyOwned ? "existing-upstream-state" : "policy-on");
                return true;
            }

            InvokeSuppliedOriginal(__args[0], picker, choiceIndex);
            _server.LogTemporaryPolicy("command", instanceId, shareTemporaryItems, "vanilla-bypass", "prechoices");
            __state = true;
            return false;
        }

        private static void GrantIndividualPrefix(object[] __args, out bool __state)
        {
            __state = false;
            if (_server == null) return;
            _server.BeginDistribution(__args, instant: false);
            __state = _server.InDistribution;
        }

        private static void GrantInstantPrefix(object[] __args, out bool __state)
        {
            __state = false;
            if (_server == null) return;
            _server.BeginDistribution(__args, instant: true);
            __state = _server.InDistribution;
        }

        private static void DistributionPostfix(bool __state)
        {
            if (__state) _server?.EndDistribution(successful: true);
        }

        private static Exception? DistributionFinalizer(Exception? __exception, bool __state)
        {
            if (__exception != null && __state && _server != null && _server.InDistribution)
                _server.EndDistribution(successful: false);
            return __exception;
        }

        private static void IsDownPostfix(object[] __args, ref bool __result)
        {
            if (!__result || _server == null) return;
            var master = __args.OfType<CharacterMaster>().FirstOrDefault();
            if (master != null && _server.ShouldTreatAsActive(master)) __result = false;
        }

        private static void ItemShareCommandSelectionPostfix(object[] __args, bool __state)
        {
            if (__state || _server == null) return;
            var picker = __args.OfType<PickupPickerController>().FirstOrDefault();
            if (picker != null) _server.OnItemShareCommandSelectionCompleted(picker);
        }

        private static bool GiveDirectPrefix(object[] __args)
        {
            if (_server == null) return true;
            var inventory = __args.OfType<Inventory>().FirstOrDefault();
            return inventory == null || !_server.ShouldSuppressImmediateGive(inventory);
        }

        private static void LocalPlayersHaveTakenPostfix(object[] __args, ref bool __result)
        {
            if (_presentation == null) return;
            var pickup = __args.OfType<GenericPickupController>().FirstOrDefault();
            if (pickup != null && _presentation.TryEvaluateLocalCollected(pickup, out var corrected)) __result = corrected;
        }

        private static void ApplyOrbVisibilityPostfix(object[] __args)
        {
            if (_presentation == null) return;
            var pickup = __args.OfType<GenericPickupController>().FirstOrDefault();
            if (pickup != null) _presentation.OnUpstreamVisibilityApplied(pickup);
        }

        private static void RefreshVisibilityPostfix() => _presentation?.RequestRefresh();

        private static void PickupGetInteractabilityPostfix(GenericPickupController __instance, Interactor __0, ref Interactability __result)
        {
            if (_presentation != null && __instance != null && __0 != null
                && _presentation.ShouldSuppressLocalPickupInteraction(__instance, __0))
            {
                // ItemShare uses the same zero-valued result for already-completed Command pickers.
                __result = (Interactability)0;
            }
        }

        private static void TransferOrbStatePostfix(object[] __args, bool __result)
        {
            if (!__result || _server == null || __args.Length < 2) return;
            try
            {
                var oldId = Convert.ToInt32(__args[0], System.Globalization.CultureInfo.InvariantCulture);
                var newId = Convert.ToInt32(__args[1], System.Globalization.CultureInfo.InvariantCulture);
                _server.OnPickupTransferred(oldId, newId);
                _presentation?.RequestRefresh();
            }
            catch { }
        }

        private static void NetworkDestroyObservationPrefix(object __instance) => _server?.OnNetworkDestroyObserved(__instance);

        private static void InstallBlockingModalLifecyclePatches(Harmony harmony)
        {
            var assembly = typeof(Run).Assembly;

            var pauseType = assembly.GetType(BlockingModalLifecyclePolicy.PauseScreenControllerTypeName, throwOnError: false);
            if (pauseType != null && typeof(Component).IsAssignableFrom(pauseType))
            {
                // PauseScreenController is instantiated for the ESC screen; Awake is the exact open transition path.
                var awake = DeclaredParameterless(pauseType, "Awake");
                var onEnable = DeclaredParameterless(pauseType, "OnEnable");
                if (awake != null) Patch(harmony, awake, postfix: nameof(BlockingModalLifecycleObservedPostfix));
                if (onEnable != null) Patch(harmony, onEnable, postfix: nameof(BlockingModalLifecycleObservedPostfix));
                if (awake == null && onEnable == null)
                    throw new MissingMethodException(pauseType.FullName, "PauseScreenController open lifecycle (Awake/OnEnable)");
            }

            var dialogType = assembly.GetType(BlockingModalLifecyclePolicy.SimpleDialogBoxTypeName, throwOnError: false);
            if (dialogType != null && typeof(Component).IsAssignableFrom(dialogType))
            {
                // SimpleDialogBox.Create is the exact factory transition used by RoR2/mod callers. Patch every matching
                // static overload so a newly-created blocking dialog is seeded without any scene-wide discovery.
                var createMethods = dialogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method => string.Equals(method.Name, "Create", StringComparison.Ordinal)
                                     && dialogType.IsAssignableFrom(method.ReturnType))
                    .ToArray();
                for (var i = 0; i < createMethods.Length; i++)
                    Patch(harmony, createMethods[i], postfix: nameof(BlockingModalFactoryPostfix));

                var onEnable = DeclaredParameterless(dialogType, "OnEnable");
                if (onEnable != null) Patch(harmony, onEnable, postfix: nameof(BlockingModalLifecycleObservedPostfix));
                if (createMethods.Length == 0 && onEnable == null)
                    throw new MissingMethodException(dialogType.FullName, "SimpleDialogBox open lifecycle (Create/OnEnable)");
            }

            // RoR2.UI.PickupPickerPanel is the visible Command picker panel; hook its exact lifecycle/content signals.
            // PickupPickerController.panelPrefab component used by Artifact of Command/item-choice panels. Awake is
            // the exact panel-open lifecycle exposed by the target HookGen surface; OnEnable is patched when declared.
            var pickerPanelType = assembly.GetType(BlockingModalLifecyclePolicy.PickupPickerPanelTypeName, throwOnError: false);
            if (pickerPanelType == null || !typeof(Component).IsAssignableFrom(pickerPanelType))
                throw new TypeLoadException("Required RoR2.UI.PickupPickerPanel target UI type is unavailable.");

            var pickerAwake = DeclaredParameterless(pickerPanelType, "Awake");
            var pickerOnEnable = DeclaredParameterless(pickerPanelType, "OnEnable");
            var pickerSetOptions = pickerPanelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => string.Equals(method.Name, "SetPickupOptions", StringComparison.Ordinal))
                .ToArray();
            if (pickerAwake != null) Patch(harmony, pickerAwake, postfix: nameof(BlockingModalLifecycleObservedPostfix));
            if (pickerOnEnable != null) Patch(harmony, pickerOnEnable, postfix: nameof(BlockingModalLifecycleObservedPostfix));
            for (var i = 0; i < pickerSetOptions.Length; i++)
                Patch(harmony, pickerSetOptions[i], postfix: nameof(BlockingModalLifecycleObservedPostfix));
            if (pickerAwake == null && pickerOnEnable == null && pickerSetOptions.Length == 0)
                throw new MissingMethodException(pickerPanelType.FullName, "PickupPickerPanel open lifecycle/content signal (Awake/OnEnable/SetPickupOptions)");
        }

        private static MethodInfo? DeclaredParameterless(Type type, string name)
            => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                binder: null, types: Type.EmptyTypes, modifiers: null);

        private static void BlockingModalLifecycleObservedPostfix(object __instance)
            => ObserveBlockingModalLifecycle(__instance);

        private static void BlockingModalFactoryPostfix(object __result)
            => ObserveBlockingModalLifecycle(__result);

        private static void ObserveBlockingModalLifecycle(object candidate)
        {
            if (candidate is Component component) _presentation?.OnBlockingModalLifecycleObserved(component);
        }

        private static bool ContainsInstanceId(FieldInfo? field, int instanceId)
        {
            if (field == null) throw new InvalidOperationException("ItemShare state field was not initialized.");
            var value = field.GetValue(null) ?? throw new InvalidOperationException("ItemShare state field is null: " + field.Name);
            if (value is IDictionary dictionary) return dictionary.Contains(instanceId);
            if (value is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                    if (entry is int exact && exact == instanceId) return true;
                return false;
            }
            throw new InvalidOperationException("Unsupported ItemShare state collection shape: " + field.Name);
        }

        private static void InvokeSuppliedOriginal(object candidate, params object?[] arguments)
        {
            if (candidate is not Delegate original)
                throw new InvalidOperationException("Required supplied original ItemShare delegate is unavailable.");
            original.DynamicInvoke(arguments);
        }

        private static FieldInfo RequiredField(Type type, string name)
            => type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new MissingFieldException(type.FullName, name);

        private static MethodInfo Required(Type type, string name, int parameterCount, Type returnType, bool isStatic)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            var matches = type.GetMethods(flags)
                .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal)
                                 && method.GetParameters().Length == parameterCount
                                 && method.ReturnType == returnType)
                .ToArray();
            if (matches.Length != 1)
                throw new MissingMethodException(type.FullName, name + "/" + parameterCount + " -> " + returnType.Name);
            return matches[0];
        }

        private static void Patch(Harmony harmony, MethodBase original, string? prefix = null, string? postfix = null, string? finalizer = null)
        {
            var patchType = typeof(RuntimePatches);
            HarmonyMethod? Prefix(string? name) => name == null ? null : new HarmonyMethod(patchType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(original, prefix: Prefix(prefix), postfix: Prefix(postfix), finalizer: Prefix(finalizer));
        }
    }
}
