using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using ItemShareFix.Core;
using RoR2;
using UnityEngine.Networking;

namespace ItemShareFix
{
    internal sealed class UpstreamBridge
    {
        private readonly ManualLogSource _log;
        private readonly FieldInfo _claimsField;
        private readonly FieldInfo _choicesField;
        private readonly FieldInfo _modeField;
        private readonly FieldInfo _hideCollectedField;
        private readonly FieldInfo _shareCommandPicksField;
        private readonly FieldInfo _clientOrbsField;
        private readonly FieldInfo _clientCubesField;
        private readonly MethodInfo _clientContainsMethod;
        private readonly MethodInfo _isCommandCubeMethod;
        private readonly MethodInfo _hasPickerStateMethod;
        private readonly FieldInfo _pickerOptionsField;
        private readonly MethodInfo _isShareableMethod;
        private readonly MethodInfo _giveDirectMethod;
        private readonly MethodInfo _broadcastOrbStateMethod;
        private readonly MethodInfo _applyOrbVisibilityMethod;

        public UpstreamBridge(Assembly itemShareAssembly, Assembly pickupShareApiAssembly, ManualLogSource log)
        {
            _log = log;
            var itemSharePluginType = itemShareAssembly.GetType("ItemShare.ItemSharePlugin", true)!;
            _claimsField = RequiredField(itemSharePluginType, "Claims");
            _choicesField = RequiredField(itemSharePluginType, "Choices");
            _modeField = RequiredField(itemSharePluginType, "_mode");
            _hideCollectedField = RequiredField(itemSharePluginType, "_hideCollectedOrbs");
            _shareCommandPicksField = RequiredField(itemSharePluginType, "_shareCommandPicks");
            _isShareableMethod = RequiredMethod(itemSharePluginType, "IsShareable", 1);
            _giveDirectMethod = RequiredMethod(itemSharePluginType, "GiveDirect", 3);
            _broadcastOrbStateMethod = RequiredMethod(itemSharePluginType, "BroadcastOrbState", 2);
            _applyOrbVisibilityMethod = RequiredMethod(itemSharePluginType, "ApplyOrbVisibility", 2);

            var mirrorType = itemShareAssembly.GetType("ItemShare.ClientPickMirror", true)!;
            _clientOrbsField = RequiredField(mirrorType, "Orbs");
            _clientCubesField = RequiredField(mirrorType, "Cubes");
            var record = _clientOrbsField.GetValue(null) ?? throw new InvalidOperationException("ItemShare ClientPickMirror.Orbs is null.");
            _clientContainsMethod = record.GetType().GetMethod(
                "Contains",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(uint), typeof(uint) },
                modifiers: null)
                ?? throw new MissingMethodException(record.GetType().FullName, "Contains(uint,uint)");
            if (_clientContainsMethod.ReturnType != typeof(bool)) throw new InvalidOperationException("Unexpected ItemShare PickRecord.Contains return type.");
            var cubeRecord = _clientCubesField.GetValue(null) ?? throw new InvalidOperationException("ItemShare ClientPickMirror.Cubes is null.");
            if (cubeRecord.GetType() != record.GetType()) throw new InvalidOperationException("ItemShare cube/orb mirror record types differ unexpectedly.");

            var classifierType = pickupShareApiAssembly.GetType("PickupShare.PickupClassifier", true)!;
            _isCommandCubeMethod = RequiredStaticMethod(classifierType, "IsCommandCube", typeof(bool), typeof(PickupPickerController));
            var apiType = pickupShareApiAssembly.GetType("PickupShare.PickupShareApi", true)!;
            _hasPickerStateMethod = RequiredStaticMethod(apiType, "HasPickerState", typeof(bool), typeof(int));
            _pickerOptionsField = RequiredField(typeof(PickupPickerController), "options");
        }

        public bool IsIndividualMode => string.Equals(ReadConfigValue(_modeField)?.ToString(), "Individual", StringComparison.OrdinalIgnoreCase);
        public bool HideCollectedOrbsEnabled => ReadConfigBool(_hideCollectedField);
        public bool ShareCommandPicksEnabled => ReadConfigBool(_shareCommandPicksField);


        public bool IsCommandCube(PickupPickerController picker)
        {
            if (picker == null) return false;
            try { return (bool)_isCommandCubeMethod.Invoke(null, new object[] { picker }); }
            catch (Exception ex)
            {
                _log.LogWarning("[ItemShareFix] PickupClassifier.IsCommandCube failed closed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        public bool HasPickerProviderState(PickupPickerController picker)
        {
            if (picker == null) return false;
            try { return (bool)_hasPickerStateMethod.Invoke(null, new object[] { picker.GetInstanceID() }); }
            catch (Exception ex)
            {
                _log.LogDebug("[ItemShareFix] PickupShareApi.HasPickerState diagnostic query failed: " + ex.GetType().Name);
                return false;
            }
        }

        public bool TryHasCommandPicked(PickupPickerController picker, CharacterMaster master, out bool picked)
        {
            picked = false;
            if (picker == null || master == null || !IsIndividualMode || !ShareCommandPicksEnabled || !IsCommandCube(picker)) return false;

            if (NetworkServer.active)
            {
                var choices = _choicesField.GetValue(null) as IDictionary;
                if (choices == null) return false;
                var instanceId = picker.GetInstanceID();
                if (!choices.Contains(instanceId))
                {
                    picked = false; // untouched Command cube: every current participant is still pending.
                    return true;
                }

                var chosen = choices[instanceId];
                if (chosen == null) return false;
                picked = ServerClaimSetContains(chosen, master);
                return true;
            }

            var pickerNetId = picker.netId.Value;
            var masterNetId = master.netId.Value;
            if (pickerNetId == 0u || masterNetId == 0u) return false;
            picked = ClientMirrorContains(_clientCubesField, pickerNetId, masterNetId);
            return true;
        }


        public bool TryGetCommandChoicePickupIndexes(
            PickupPickerController picker,
            out PickupIndex[] pickupIndexes,
            out string optionSource,
            out bool exactSource,
            out bool sourceDisagreement)
        {
            pickupIndexes = Array.Empty<PickupIndex>();
            optionSource = CommandOptionSourcePolicy.UnresolvedSourceToken;
            exactSource = false;
            sourceDisagreement = false;
            if (picker == null || !IsCommandCube(picker)) return false;

            try
            {
                if (_pickerOptionsField.GetValue(picker) is not IEnumerable options) return false;
                var result = new System.Collections.Generic.List<PickupIndex>();
                var sawNested = false;
                var sawFallback = false;
                foreach (var boxedOption in options)
                {
                    if (boxedOption == null) continue;
                    if (!TryReadBoolMember(boxedOption, "available", out var available) || !available) continue;
                    if (!TryReadPickupIndex(boxedOption, out var pickupIndex, out var source, out var disagreement) || pickupIndex == PickupIndex.none) continue;

                    sourceDisagreement |= disagreement;
                    if (source == CommandOptionPickupSource.NestedPickup) sawNested = true;
                    if (source == CommandOptionPickupSource.DirectCompatibilityFallback) sawFallback = true;
                    if (!result.Any(x => x == pickupIndex)) result.Add(pickupIndex);
                }

                if (result.Count == 0) return false;
                pickupIndexes = result.ToArray();
                exactSource = sawNested && !sawFallback;
                optionSource = sawFallback
                    ? (sawNested ? "nested-pickup+direct-fallback" : CommandOptionSourcePolicy.FallbackSourceToken)
                    : CommandOptionSourcePolicy.AuthoritativeSourceToken;
                return true;
            }
            catch (Exception ex)
            {
                _log.LogDebug("[ItemShareFix] Command picker option metadata read failed: " + ex.GetType().Name);
                return false;
            }
        }

        public bool TryGetCommandChoiceLifetime(
            PickupPickerController picker,
            out MarkerLifetimeKind lifetime,
            out int exactNestedAvailableOptionCount,
            out int unresolvedAvailableOptionCount)
        {
            lifetime = MarkerLifetimeKind.Unknown;
            exactNestedAvailableOptionCount = 0;
            unresolvedAvailableOptionCount = 0;
            if (picker == null || !IsCommandCube(picker)) return false;

            try
            {
                if (_pickerOptionsField.GetValue(picker) is not IEnumerable options) return false;
                var sawAvailable = false;
                var sawTemporary = false;
                var sawPermanent = false;
                foreach (var boxedOption in options)
                {
                    if (boxedOption == null) continue;
                    if (!TryReadBoolMember(boxedOption, "available", out var available) || !available) continue;
                    sawAvailable = true;

                    if (!TryReadMember(boxedOption, "pickup", out var pickup)
                        || pickup is not UniquePickup exactNestedPickup)
                    {
                        unresolvedAvailableOptionCount++;
                        continue;
                    }

                    exactNestedAvailableOptionCount++;
                    if (exactNestedPickup.isTempItem) sawTemporary = true;
                    else sawPermanent = true;
                }

                if (!sawAvailable) return false;
                if (unresolvedAvailableOptionCount > 0)
                {
                    lifetime = MarkerLifetimeKind.Unknown;
                    return true;
                }

                lifetime = MarkerLifetimePolicy.FromExactOptionKinds(sawTemporary, sawPermanent);
                return exactNestedAvailableOptionCount > 0;
            }
            catch (Exception ex)
            {
                _log.LogDebug("[ItemShareFix] Command picker exact lifetime metadata read failed: " + ex.GetType().Name);
                lifetime = MarkerLifetimeKind.Unknown;
                return false;
            }
        }

        private static bool TryReadPickupIndex(
            object boxedOption,
            out PickupIndex pickupIndex,
            out CommandOptionPickupSource source,
            out bool disagreement)
        {
            pickupIndex = PickupIndex.none;
            source = CommandOptionPickupSource.None;
            disagreement = false;

            var nestedAvailable = false;
            var nestedIndex = PickupIndex.none;
            if (TryReadMember(boxedOption, "pickup", out var pickup)
                && pickup != null
                && TryReadMember(pickup, "pickupIndex", out var nested)
                && nested is PickupIndex exactNestedIndex)
            {
                nestedAvailable = true;
                nestedIndex = exactNestedIndex;
            }

            var directAvailable = false;
            var directIndex = PickupIndex.none;
            if (TryReadMember(boxedOption, "pickupIndex", out var direct) && direct is PickupIndex exactDirectIndex)
            {
                directAvailable = true;
                directIndex = exactDirectIndex;
            }

            var decision = CommandOptionSourcePolicy.Resolve(
                nestedAvailable,
                nestedIndex,
                directAvailable,
                directIndex);
            if (!decision.HasValue) return false;

            pickupIndex = decision.Value;
            source = decision.Source;
            disagreement = decision.Disagreement;
            return true;
        }

        private static bool TryReadBoolMember(object instance, string name, out bool value)
        {
            value = false;
            if (!TryReadMember(instance, name, out var boxed) || boxed is not bool flag) return false;
            value = flag;
            return true;
        }

        private static bool TryReadMember(object instance, string name, out object? value)
        {
            value = null;
            var type = instance.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                value = field.GetValue(instance);
                return true;
            }

            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.GetIndexParameters().Length != 0) return false;
            value = property.GetValue(instance);
            return true;
        }

        public bool IsShareable(GenericPickupController pickup)
        {
            if (pickup == null) return false;
            var def = PickupCatalog.GetPickupDef(pickup.pickup.pickupIndex);
            return def != null && IsShareable(def);
        }

        public bool IsShareable(PickupDef def)
            => TryIsShareable(def, out var shareable) && shareable;

        public bool TryIsShareable(PickupDef def, out bool shareable)
        {
            shareable = false;
            if (def == null) return false;
            try
            {
                var result = _isShareableMethod.Invoke(null, new object[] { def });
                if (result is not bool exact) return false;
                shareable = exact;
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError("[ItemShareFix] ItemShare IsShareable(PickupDef) reflection failed: " + ex);
                return false;
            }
        }

        public bool HasCollected(GenericPickupController pickup, CharacterMaster master)
        {
            if (pickup == null || master == null) return false;
            if (NetworkServer.active) return ServerClaimsContains(pickup.GetInstanceID(), master);

            var dropNetId = pickup.netId.Value;
            var masterNetId = master.netId.Value;
            if (dropNetId == 0u || masterNetId == 0u) return false;
            return ClientMirrorContains(dropNetId, masterNetId);
        }

        public void NormalizeUpstreamVisualSubtree(GenericPickupController pickup)
        {
            if (pickup == null || pickup.pickupDisplay == null) return;
            try { _applyOrbVisibilityMethod.Invoke(null, new object[] { pickup, true }); }
            catch (Exception ex) { _log.LogDebug("[ItemShareFix] upstream visual normalization failed: " + ex.GetType().Name); }
        }

        public bool GiveDeferred(Inventory inventory, PickupDef pickupDef, object boxedUniquePickup)
        {
            var result = _giveDirectMethod.Invoke(null, new[] { (object)inventory, pickupDef, boxedUniquePickup });
            return result is bool granted && granted;
        }

        public bool TryMirrorHistoricalBarrier(int pickupInstanceId, CharacterMaster master, out string evidence)
        {
            evidence = string.Empty;
            if (!NetworkServer.active || master == null || master.netId.Value == 0u)
            {
                evidence = "server/master unavailable";
                return false;
            }

            var claims = _claimsField.GetValue(null) as IDictionary;
            if (claims == null || !claims.Contains(pickupInstanceId))
            {
                evidence = "ItemShare Claims has no authoritative entry for historical pickup " + pickupInstanceId;
                return false;
            }

            var claimSet = claims[pickupInstanceId];
            if (claimSet == null)
            {
                evidence = "ItemShare claim set is null for historical pickup " + pickupInstanceId;
                return false;
            }

            var alreadyPresent = ServerClaimSetContains(claimSet, master);
            var addMethod = claimSet.GetType().GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(CharacterMaster) },
                modifiers: null);
            if (addMethod == null)
            {
                evidence = "ItemShare claim set has no Add(CharacterMaster)";
                return false;
            }

            try
            {
                if (!alreadyPresent)
                {
                    var result = addMethod.Invoke(claimSet, new object[] { master });
                    if (result is bool changed && !changed && !ServerClaimSetContains(claimSet, master))
                    {
                        evidence = "ItemShare claim set rejected reconnect generation";
                        return false;
                    }
                }

                var pickup = FindPickupByInstanceId(pickupInstanceId);
                if (pickup == null || pickup.netId.Value == 0u)
                {
                    evidence = "historical barrier is present server-side; client rebroadcast is pending because pickup/netId is unavailable";
                    return false;
                }

                _broadcastOrbStateMethod.Invoke(null, new[] { (object)pickup, claimSet });
                evidence = alreadyPresent
                    ? "historical barrier already present in ItemShare Claims and was rebroadcast"
                    : "historical barrier mirrored into ItemShare Claims and rebroadcast";
                return true;
            }
            catch (Exception ex)
            {
                evidence = "historical barrier mirror failed: " + ex.GetType().Name + ": " + ex.Message;
                _log.LogWarning("[ItemShareFix] " + evidence);
                return false;
            }
        }

        public bool TryBroadcastTransferredOrbState(int newInstanceId)
        {
            if (!NetworkServer.active) return false;
            var pickup = FindPickupByInstanceId(newInstanceId);
            if (pickup == null || pickup.netId.Value == 0u) return false;

            var claims = _claimsField.GetValue(null) as IDictionary;
            if (claims == null) return false;
            if (!claims.Contains(newInstanceId)) return true; // Provider had no claim set to mirror; nothing to rebroadcast.
            var claimSet = claims[newInstanceId];
            if (claimSet == null) return false;

            try
            {
                _broadcastOrbStateMethod.Invoke(null, new[] { (object)pickup, claimSet });
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning("[ItemShareFix] transferred-orb state rebroadcast failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        public static GenericPickupController? FindPickupByInstanceId(int instanceId)
        {
            foreach (var pickup in InstanceTracker.GetInstancesList<GenericPickupController>())
            {
                if (pickup != null && pickup.GetInstanceID() == instanceId) return pickup;
            }
            return null;
        }

        private bool ServerClaimsContains(int pickupInstanceId, CharacterMaster master)
        {
            var claims = _claimsField.GetValue(null) as IDictionary;
            if (claims == null || !claims.Contains(pickupInstanceId)) return false;
            var claimSet = claims[pickupInstanceId];
            return claimSet != null && ServerClaimSetContains(claimSet, master);
        }

        private static bool ServerClaimSetContains(object claimSet, CharacterMaster master)
        {
            if (claimSet is not IEnumerable enumerable) return false;
            foreach (var value in enumerable)
            {
                if (ReferenceEquals(value, master)) return true;
            }
            return false;
        }

        private bool ClientMirrorContains(uint pickupNetId, uint masterNetId)
            => ClientMirrorContains(_clientOrbsField, pickupNetId, masterNetId);

        private bool ClientMirrorContains(FieldInfo mirrorField, uint pickupNetId, uint masterNetId)
        {
            try
            {
                var record = mirrorField.GetValue(null);
                if (record == null) return false;
                return (bool)_clientContainsMethod.Invoke(record, new object[] { pickupNetId, masterNetId });
            }
            catch (Exception ex)
            {
                _log.LogWarning("[ItemShareFix] client mirror query failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static FieldInfo RequiredField(Type type, string name)
            => type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new MissingFieldException(type.FullName, name);

        private static MethodInfo RequiredMethod(Type type, string name, int parameterCount)
        {
            var matches = type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => string.Equals(x.Name, name, StringComparison.Ordinal) && x.GetParameters().Length == parameterCount)
                .ToArray();
            if (matches.Length != 1) throw new MissingMethodException(type.FullName, name + " with " + parameterCount + " parameters");
            return matches[0];
        }

        private static MethodInfo RequiredStaticMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null || method.ReturnType != returnType) throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static object? ReadConfigValue(FieldInfo field)
        {
            var entry = field.GetValue(null);
            if (entry == null) return null;
            return entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
        }

        private static bool ReadConfigBool(FieldInfo field)
        {
            var value = ReadConfigValue(field);
            return value is bool flag && flag;
        }
    }
}
