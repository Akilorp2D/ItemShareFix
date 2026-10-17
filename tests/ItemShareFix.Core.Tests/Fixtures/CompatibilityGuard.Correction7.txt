using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx.Logging;

namespace ItemShareFix
{
    internal sealed class CompatibilityResult
    {
        public bool Supported { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Assembly? ItemShareAssembly { get; set; }
        public Assembly? PickupShareApiAssembly { get; set; }
        public MethodInfo? DisconnectMethod { get; set; }
    }

    internal static class CompatibilityGuard
    {
        internal const string ExpectedItemShareSha256 = "48C25FE558CB095B2AC73836BE0563EBFDCD1C481AF9263D9D3740618F160F38";
        internal const string ExpectedPickupShareApiSha256 = "5EF4AC9457DB29BDA76C5DB110914EA14720ADE65DC6D5E48359B3DF8DED7F1D";

        public static CompatibilityResult Probe(ManualLogSource log)
        {
            try
            {
                var itemShare = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => string.Equals(x.GetName().Name, "ItemShare", StringComparison.Ordinal));
                if (itemShare == null) return Fail("ItemShare assembly is not loaded.");
                if (itemShare.GetName().Version?.ToString() != "1.7.1.0") return Fail("Unsupported ItemShare assembly version: " + itemShare.GetName().Version);
                var itemHash = HashAssembly(itemShare);
                if (!string.Equals(itemHash, ExpectedItemShareSha256, StringComparison.OrdinalIgnoreCase)) return Fail("ItemShare.dll SHA-256 mismatch: " + itemHash);

                var pickupApi = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => string.Equals(x.GetName().Name, "PickupShareApi", StringComparison.Ordinal));
                if (pickupApi == null) return Fail("PickupShareApi assembly is not loaded.");
                if (pickupApi.GetName().Version?.ToString() != "1.0.0.0") return Fail("Unsupported PickupShareApi assembly version: " + pickupApi.GetName().Version);
                var apiHash = HashAssembly(pickupApi);
                if (!string.Equals(apiHash, ExpectedPickupShareApiSha256, StringComparison.OrdinalIgnoreCase)) return Fail("PickupShareApi.dll SHA-256 mismatch: " + apiHash);

                var apiType = pickupApi.GetType("PickupShare.PickupShareApi", false);
                var apiVersion = apiType?.GetProperty("ApiVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (apiVersion == null)
                {
                    apiVersion = apiType?.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                }
                if (Convert.ToInt32(apiVersion, System.Globalization.CultureInfo.InvariantCulture) != 1) return Fail("PickupShareApi contract is not ApiVersion 1.");

                var itemType = itemShare.GetType("ItemShare.ItemSharePlugin", false);
                var mirrorType = itemShare.GetType("ItemShare.ClientPickMirror", false);
                var providerType = itemShare.GetType("ItemShare.ItemShareStateProvider", false);
                var classifierType = pickupApi.GetType("PickupShare.PickupClassifier", false);
                if (itemType == null || mirrorType == null || providerType == null || classifierType == null) return Fail("Required exact ItemShare 1.7.1 / PickupShareApi 1.0.0 types are missing.");

                foreach (var field in new[] { "Claims", "Distributed", "Choices", "_mode", "_hideCollectedOrbs", "_shareToDead", "_shareCommandPicks" })
                {
                    if (itemType.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) == null) return Fail("Required ItemShare field missing: " + field);
                }
                var requiredShapes = new (string Name, int ParameterCount, Type ReturnType)[]
                {
                    ("OnAttemptGrant", 3, typeof(void)),
                    ("GrantIndividual", 5, typeof(void)),
                    ("GrantInstant", 5, typeof(void)),
                    ("GiveDirect", 3, typeof(bool)),
                    ("IsDown", 1, typeof(bool)),
                    ("OnPickupSelected", 3, typeof(void)),
                    ("IsShareable", 1, typeof(bool)),
                    ("BroadcastOrbState", 2, typeof(void)),
                    ("ApplyOrbVisibility", 2, typeof(void)),
                    ("LocalPlayersHaveTaken", 1, typeof(bool)),
                    ("RefreshOrbVisibility", 0, typeof(void)),
                };
                foreach (var shape in requiredShapes)
                {
                    var matches = itemType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                        .Where(x => string.Equals(x.Name, shape.Name, StringComparison.Ordinal) && x.GetParameters().Length == shape.ParameterCount && x.ReturnType == shape.ReturnType)
                        .ToArray();
                    if (matches.Length != 1) return Fail("Required ItemShare method shape mismatch: " + shape.Name + "/" + shape.ParameterCount + " -> " + shape.ReturnType.Name);
                }
                var onAttemptGrant = itemType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Single(x => string.Equals(x.Name, "OnAttemptGrant", StringComparison.Ordinal) && x.GetParameters().Length == 3 && x.ReturnType == typeof(void));
                if (!HasExactOriginalDelegateHookShape(onAttemptGrant, typeof(RoR2.GenericPickupController), typeof(RoR2.CharacterBody)))
                    return Fail("ItemShare OnAttemptGrant exact orig/self/body delegate shape mismatch.");
                var onPickupSelected = itemType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    .Single(x => string.Equals(x.Name, "OnPickupSelected", StringComparison.Ordinal) && x.GetParameters().Length == 3 && x.ReturnType == typeof(void));
                if (!HasExactOriginalDelegateHookShape(onPickupSelected, typeof(RoR2.PickupPickerController), typeof(int)))
                    return Fail("ItemShare OnPickupSelected exact orig/self/choiceIndex delegate shape mismatch.");
                var transfer = providerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SingleOrDefault(x => string.Equals(x.Name, "TransferOrbState", StringComparison.Ordinal) && x.GetParameters().Length == 2 && x.ReturnType == typeof(bool));
                if (transfer == null) return Fail("ItemShare provider TransferOrbState(int,int)->bool private shape missing.");
                foreach (var mirrorField in new[] { "Orbs", "Cubes" })
                {
                    if (mirrorType.GetField(mirrorField, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null) return Fail("ItemShare ClientPickMirror." + mirrorField + " private shape missing.");
                }

                var isCommandCube = classifierType.GetMethod(
                    "IsCommandCube",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(RoR2.PickupPickerController) },
                    modifiers: null);
                if (isCommandCube == null || isCommandCube.ReturnType != typeof(bool)) return Fail("PickupShareApi PickupClassifier.IsCommandCube(PickupPickerController)->bool shape missing.");
                var hasPickerStateMethod = apiType?.GetMethod(
                    "HasPickerState",
                    BindingFlags.Static | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(int) },
                    modifiers: null);
                if (hasPickerStateMethod == null || hasPickerStateMethod.ReturnType != typeof(bool)) return Fail("PickupShareApi HasPickerState(int)->bool public shape missing.");

                var pickerOptions = typeof(RoR2.PickupPickerController).GetField("options", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pickerOptions == null) return Fail("RoR2 PickupPickerController.options exact choice-data field missing.");
                var pickerOptionType = typeof(RoR2.PickupPickerController).GetNestedType("Option", BindingFlags.Public | BindingFlags.NonPublic);
                var optionPickup = pickerOptionType?.GetField("pickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (optionPickup == null || optionPickup.FieldType != typeof(RoR2.UniquePickup))
                    return Fail("RoR2 PickupPickerController.Option.pickup : UniquePickup exact field missing.");
                var tempProperty = typeof(RoR2.UniquePickup).GetProperty("isTempItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tempProperty == null || tempProperty.PropertyType != typeof(bool) || tempProperty.GetIndexParameters().Length != 0)
                    return Fail("RoR2 UniquePickup.isTempItem : bool exact property missing.");

                var hasProvider = apiType?.GetProperty("HasProvider", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var providerName = apiType?.GetProperty("ProviderName", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)?.ToString();
                if (hasProvider is not bool providerPresent || !providerPresent) return Fail("PickupShareApi has no registered provider; ItemShare must remain the sole provider.");
                if (string.IsNullOrEmpty(providerName) || !providerName.StartsWith("ItemShare", StringComparison.Ordinal)) return Fail("PickupShareApi provider is not ItemShare: " + (providerName ?? "<null>"));

                var ror2Assembly = typeof(RoR2.Run).Assembly;
                if (!HasStableNetworkUserIdShape(ror2Assembly, out var networkUserIdReason))
                    return Fail("Stable NetworkUserId identity shape unsupported: " + networkUserIdReason);
                var localUserManager = ror2Assembly.GetType("RoR2.LocalUserManager", false);
                if (localUserManager == null) return Fail("RoR2.LocalUserManager type missing.");
                var localUsers = (MemberInfo?)localUserManager.GetProperty("readOnlyLocalUsersList", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                 ?? localUserManager.GetField("readOnlyLocalUsersList", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (localUsers == null) return Fail("RoR2.LocalUserManager.readOnlyLocalUsersList shape missing; personal visibility cannot safely resolve local identity.");
                var disconnect = FindDisconnectMethod(ror2Assembly);
                if (disconnect == null) return Fail("No supported network-destroy observation callback shape was found (NetworkUser/PlayerCharacterMasterController OnNetworkDestroy). DisconnectCleanup must fail closed.");

                log.LogInfo("[ItemShareFix] compatibility guard PASS: exact ItemShare 1.7.1 + PickupShareApi 1.0.0 / API 1 + Command picker shapes; disconnect candidate hook=" + disconnect.DeclaringType?.FullName + "." + disconnect.Name);
                return new CompatibilityResult
                {
                    Supported = true,
                    Reason = "Exact baseline and private shapes verified.",
                    ItemShareAssembly = itemShare,
                    PickupShareApiAssembly = pickupApi,
                    DisconnectMethod = disconnect,
                };
            }
            catch (Exception ex)
            {
                return Fail("Compatibility probe exception: " + ex);
            }
        }

        private static bool HasExactOriginalDelegateHookShape(MethodInfo hookMethod, Type selfType, Type payloadType)
        {
            var parameters = hookMethod.GetParameters();
            if (parameters.Length != 3 || parameters[1].ParameterType != selfType || parameters[2].ParameterType != payloadType) return false;
            var delegateType = parameters[0].ParameterType;
            if (!typeof(Delegate).IsAssignableFrom(delegateType)) return false;
            var invoke = delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
            if (invoke == null || invoke.ReturnType != typeof(void)) return false;
            var invokeParameters = invoke.GetParameters();
            return invokeParameters.Length == 2
                && invokeParameters[0].ParameterType == selfType
                && invokeParameters[1].ParameterType == payloadType;
        }

        private static bool HasStableNetworkUserIdShape(Assembly ror2Assembly, out string reason)
        {
            reason = string.Empty;
            var networkUserType = ror2Assembly.GetType("RoR2.NetworkUser", false);
            var networkUserIdType = ror2Assembly.GetType("RoR2.NetworkUserId", false);
            var platformIdType = ror2Assembly.GetType("RoR2.PlatformID", false);
            if (networkUserType == null || networkUserIdType == null || platformIdType == null)
            {
                reason = "NetworkUser/NetworkUserId/PlatformID type missing";
                return false;
            }

            var idProperty = networkUserType.GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var idField = networkUserType.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var idMemberType = idProperty != null ? idProperty.PropertyType : idField?.FieldType;
            if (idMemberType != networkUserIdType)
            {
                reason = "NetworkUser.id is missing or not RoR2.NetworkUserId";
                return false;
            }

            var platformMembers = networkUserIdType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => (x is FieldInfo field && field.FieldType == platformIdType)
                         || (x is PropertyInfo property && property.GetIndexParameters().Length == 0 && property.PropertyType == platformIdType))
                .ToArray();
            if (platformMembers.Length == 0)
            {
                reason = "NetworkUserId has no PlatformID member";
                return false;
            }

            var slotMembers = networkUserIdType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => (x is FieldInfo field && field.FieldType == typeof(byte))
                         || (x is PropertyInfo property && property.GetIndexParameters().Length == 0 && property.PropertyType == typeof(byte)))
                .ToArray();
            if (slotMembers.Length == 0)
            {
                reason = "NetworkUserId has no byte player-controller slot member";
                return false;
            }

            var platformValueProperty = platformIdType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var platformValueField = platformIdType.GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (platformValueProperty == null && platformValueField == null)
            {
                reason = "PlatformID.value missing";
                return false;
            }

            return true;
        }

        private static MethodInfo? FindDisconnectMethod(Assembly ror2Assembly)
        {
            foreach (var typeName in new[] { "RoR2.NetworkUser", "RoR2.PlayerCharacterMasterController" })
            {
                var type = ror2Assembly.GetType(typeName, false);
                var method = type?.GetMethod("OnNetworkDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.GetParameters().Length == 0) return method;
            }
            return null;
        }

        private static string HashAssembly(Assembly assembly)
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location) || !File.Exists(location)) throw new InvalidOperationException("Assembly has no hashable on-disk location: " + assembly.FullName);
            using (var stream = File.OpenRead(location))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static CompatibilityResult Fail(string reason) => new CompatibilityResult { Supported = false, Reason = reason };
    }
}
