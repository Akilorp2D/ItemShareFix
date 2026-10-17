using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ItemShareFix.Core;
using RoR2;

namespace ItemShareFix
{
    internal sealed class ParticipantSnapshot
    {
        public ParticipantKey Key { get; set; }
        public CharacterMaster? Master { get; set; }
        public PlayerCharacterMasterController? Controller { get; set; }
        public ParticipantState State { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    internal sealed class ParticipantClassifier
    {
        public void Forget(ParticipantKey key) => _ = key;

        public void Reset()
        {
        }

        public bool TrySnapshot(PlayerCharacterMasterController controller, out ParticipantSnapshot snapshot, out string unsupportedEvidence)
        {
            snapshot = null!;
            unsupportedEvidence = string.Empty;
            if (controller == null || controller.master == null)
            {
                unsupportedEvidence = "stable identity unsupported: controller/master unavailable";
                return false;
            }

            var master = controller.master;
            if (master.netId.Value == 0u)
            {
                unsupportedEvidence = "stable identity unsupported: master netId is zero";
                return false;
            }

            if (!ParticipantIdentityResolver.TryResolve(controller, master, out var key, out var identityEvidence))
            {
                unsupportedEvidence = identityEvidence;
                return false;
            }

            if (!TryClassify(controller, master, out var state, out var stateEvidence))
            {
                unsupportedEvidence = stateEvidence;
                return false;
            }
            snapshot = new ParticipantSnapshot
            {
                Key = key,
                Master = master,
                Controller = controller,
                State = state,
                Evidence = identityEvidence + "; " + stateEvidence,
            };
            return true;
        }

        private static bool TryClassify(PlayerCharacterMasterController controller, CharacterMaster master, out ParticipantState state, out string evidence)
        {
            bool deadAndOut;
            try { deadAndOut = master.IsDeadAndOutOfLivesServer(); }
            catch (Exception ex)
            {
                state = default;
                evidence = "participant state unsupported: IsDeadAndOutOfLivesServer failed: " + ex.GetType().Name + "; no FULLY_DEAD inference; upstream grants remain authoritative";
                return false;
            }

            if (!deadAndOut)
            {
                state = ParticipantState.Alive;
                evidence = "IsDeadAndOutOfLivesServer=false";
                return true;
            }

            var body = master.GetBody();
            if (RemoteOperationProbe.HasExactControlledDroneSignal(controller, master, body, out var remoteEvidence))
            {
                state = ParticipantState.SupportDrone;
                evidence = remoteEvidence;
                return true;
            }

            state = ParticipantState.FullyDead;
            evidence = remoteEvidence + "; dead/out-of-lives classified FULLY_DEAD fail-closed";
            return true;
        }
    }

    internal static class ParticipantIdentityResolver
    {
        private static readonly object MemberCacheGate = new object();
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo?>> MemberInfoCache =
            new Dictionary<Type, Dictionary<string, MemberInfo?>>();
        private static readonly Dictionary<Type, MemberInfo[]> ReadableMemberCache =
            new Dictionary<Type, MemberInfo[]>();
        private static readonly string[] PlayerSlotPreferredNames = { "playerControllerId", "subId" };

        public static bool TryResolve(PlayerCharacterMasterController controller, CharacterMaster master, out ParticipantKey key, out string evidence)
        {
            key = default;
            evidence = string.Empty;

            NetworkUser? networkUser = null;
            foreach (var candidate in NetworkUser.readOnlyInstancesList)
            {
                if (candidate == null || !ReferenceEquals(candidate.master, master)) continue;
                networkUser = candidate;
                break;
            }
            if (networkUser == null)
            {
                evidence = "stable identity unsupported: no NetworkUser matched authoritative CharacterMaster";
                return false;
            }

            var boxedNetworkUserId = GetMember(networkUser, "id");
            if (boxedNetworkUserId == null)
            {
                evidence = "stable identity unsupported: NetworkUser.id unavailable";
                return false;
            }

            if (!TryFormatStableNetworkUserId(boxedNetworkUserId, out var stableIdentity, out var identityEvidence))
            {
                evidence = "stable identity unsupported: " + identityEvidence;
                return false;
            }

            var masterNetId = master.netId.Value;
            if (masterNetId == 0u)
            {
                evidence = "stable identity unsupported: authoritative master netId is zero";
                return false;
            }

            key = new ParticipantKey(
                new StableUserKey(stableIdentity),
                "masterNetId=" + masterNetId.ToString(CultureInfo.InvariantCulture));
            evidence = identityEvidence + "; generation=masterNetId:" + masterNetId.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryFormatStableNetworkUserId(object boxedNetworkUserId, out string stableIdentity, out string evidence)
        {
            stableIdentity = string.Empty;
            evidence = string.Empty;
            var idType = boxedNetworkUserId.GetType();
            if (!string.Equals(idType.FullName, "RoR2.NetworkUserId", StringComparison.Ordinal))
            {
                evidence = "NetworkUser.id runtime type is " + (idType.FullName ?? idType.Name) + ", expected RoR2.NetworkUserId";
                return false;
            }

            if (!TryReadTypedMember(boxedNetworkUserId, "platformId", "RoR2.PlatformID", out var platformId, out var platformMember))
            {
                evidence = "RoR2.NetworkUserId has no uniquely proven RoR2.PlatformID identity member";
                return false;
            }

            if (!TryReadPlayerSlot(boxedNetworkUserId, out var playerSlot, out var slotMember))
            {
                evidence = "RoR2.NetworkUserId has no uniquely proven byte player-controller slot";
                return false;
            }

            var rawPlatformValue = GetMember(platformId!, "value");
            if (!TryFormatPlatformValue(rawPlatformValue, out var platformValue, out var platformValueType))
            {
                evidence = "RoR2.PlatformID.value is absent/zero or is not a proven UInt64 platform identifier";
                return false;
            }

            stableIdentity = "NetworkUserId:platformType=" + platformValueType + ":platform=" + platformValue + ":slot=" + playerSlot.ToString(CultureInfo.InvariantCulture);
            evidence = "stable identity proven from NetworkUser.id." + platformMember + ".value(" + platformValueType + ") + NetworkUser.id." + slotMember;
            return true;
        }

        private static bool TryReadTypedMember(object owner, string preferredName, string exactTypeFullName, out object? value, out string memberName)
        {
            value = null;
            memberName = string.Empty;
            var preferred = GetMemberInfo(owner.GetType(), preferredName);
            if (preferred != null && string.Equals(GetMemberType(preferred)?.FullName, exactTypeFullName, StringComparison.Ordinal))
            {
                value = GetMemberValue(owner, preferred);
                if (value != null)
                {
                    memberName = preferred.Name;
                    return true;
                }
            }

            var candidates = GetReadableMembers(owner.GetType());
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!string.Equals(GetMemberType(candidate)?.FullName, exactTypeFullName, StringComparison.Ordinal)) continue;
                var candidateValue = GetMemberValue(owner, candidate);
                if (candidateValue == null) continue;
                if (value != null) return false;
                value = candidateValue;
                memberName = candidate.Name;
            }
            return value != null;
        }

        private static bool TryReadPlayerSlot(object owner, out byte slot, out string memberName)
        {
            slot = 0;
            memberName = string.Empty;
            for (var i = 0; i < PlayerSlotPreferredNames.Length; i++)
            {
                var preferred = GetMemberInfo(owner.GetType(), PlayerSlotPreferredNames[i]);
                if (preferred == null || GetMemberType(preferred) != typeof(byte)) continue;
                var value = GetMemberValue(owner, preferred);
                if (value is byte exact)
                {
                    slot = exact;
                    memberName = preferred.Name;
                    return true;
                }
            }

            MemberInfo? matchedMember = null;
            byte matchedValue = 0;
            var matchedCount = 0;
            var candidates = GetReadableMembers(owner.GetType());
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (GetMemberType(candidate) != typeof(byte)) continue;
                var candidateValue = GetMemberValue(owner, candidate);
                if (candidateValue is not byte exact) continue;
                matchedCount++;
                if (matchedCount != 1) continue;
                matchedMember = candidate;
                matchedValue = exact;
            }
            if (matchedCount != 1 || matchedMember == null) return false;
            slot = matchedValue;
            memberName = matchedMember.Name;
            return true;
        }

        private static bool TryFormatPlatformValue(object? value, out string text, out string typeName)
        {
            text = string.Empty;
            typeName = string.Empty;
            switch (value)
            {
                case ulong unsigned when unsigned != 0UL:
                    text = unsigned.ToString(CultureInfo.InvariantCulture);
                    typeName = "UInt64";
                    return true;
                default:
                    return false;
            }
        }

        private static MemberInfo? GetMemberInfo(Type type, string name)
        {
            lock (MemberCacheGate)
            {
                if (!MemberInfoCache.TryGetValue(type, out var byName))
                {
                    byName = new Dictionary<string, MemberInfo?>(StringComparer.Ordinal);
                    MemberInfoCache[type] = byName;
                }
                if (byName.TryGetValue(name, out var cached)) return cached;

                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MemberInfo? resolved = property != null && property.GetIndexParameters().Length == 0
                    ? property
                    : type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                byName[name] = resolved;
                return resolved;
            }
        }

        private static MemberInfo[] GetReadableMembers(Type type)
        {
            lock (MemberCacheGate)
            {
                if (ReadableMemberCache.TryGetValue(type, out var cached)) return cached;

                var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var readable = new List<MemberInfo>(members.Length);
                for (var i = 0; i < members.Length; i++)
                {
                    if (IsReadableMember(members[i])) readable.Add(members[i]);
                }
                cached = readable.ToArray();
                ReadableMemberCache[type] = cached;
                return cached;
            }
        }

        private static Type? GetMemberType(MemberInfo member)
        {
            if (member is PropertyInfo property) return property.PropertyType;
            if (member is FieldInfo field) return field.FieldType;
            return null;
        }

        private static bool IsReadableMember(MemberInfo member)
            => member is FieldInfo || (member is PropertyInfo property && property.GetIndexParameters().Length == 0);

        internal static object? GetMember(object instance, string name)
        {
            var member = GetMemberInfo(instance.GetType(), name);
            return member == null ? null : GetMemberValue(instance, member);
        }

        internal static object? GetMemberValue(object instance, MemberInfo member)
        {
            try
            {
                if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) return property.GetValue(instance);
                if (member is FieldInfo field) return field.GetValue(instance);
            }
            catch
            {
            }
            return null;
        }
    }

    internal static class RemoteOperationProbe
    {
        public const string ExactApiContract = "RoR2.CharacterMaster.currentBodyHandlingConfig.DetermineCurrentPreset() == RemoteOperation";

        private sealed class RuntimeShapeStatus
        {
            public bool Compatible { get; set; }
            public string Evidence { get; set; } = string.Empty;
        }

        private static readonly Lazy<RuntimeShapeStatus> RuntimeShape = new Lazy<RuntimeShapeStatus>(InspectRuntimeShape);

        public static bool TryVerifyRuntimeShape(out string evidence)
        {
            var shape = RuntimeShape.Value;
            evidence = shape.Evidence;
            return shape.Compatible;
        }

        public static bool HasExactControlledDroneSignal(PlayerCharacterMasterController controller, CharacterMaster master, CharacterBody? body, out string evidence)
        {
            _ = body; // Signature compatibility only. Never infer participant state from body/name/prefab heuristics.

            if (controller == null || master == null)
            {
                evidence = ExactApiContract + "; fail-closed: controller/master unavailable";
                return false;
            }

            if (!ReferenceEquals(controller.master, master))
            {
                evidence = ExactApiContract + "; fail-closed: authoritative CharacterMaster ownership mismatch";
                return false;
            }

            if (!TryVerifyRuntimeShape(out var shapeEvidence))
            {
                evidence = shapeEvidence + "; fail-closed: exact runtime shape unavailable";
                return false;
            }

            try
            {
                var config = master.currentBodyHandlingConfig;
                var exactSignal = master.hasBodyHandlingConfig && config != null
                    && config.DetermineCurrentPreset() == BodyHandlingPresetIndex.RemoteOperation;
                var accepted = RemoteOperationSignalPolicy.ShouldClassifySupportDrone(
                    authoritativeMasterMatches: true,
                    runtimeShapeCompatible: true,
                    invocationSucceeded: true,
                    exactSignalValue: exactSignal);
                evidence = ExactApiContract
                    + "; authoritativeMasterOwnership=ReferenceEquals(controller.master, master)"
                    + "; exactRemoteOperationPreset=" + (exactSignal ? "true" : "false");
                return accepted;
            }
            catch (Exception ex)
            {
                evidence = ExactApiContract + "; fail-closed: invocation threw " + ex.GetType().Name;
                return RemoteOperationSignalPolicy.ShouldClassifySupportDrone(
                    authoritativeMasterMatches: true,
                    runtimeShapeCompatible: true,
                    invocationSucceeded: false,
                    exactSignalValue: false);
            }
        }

        private static RuntimeShapeStatus InspectRuntimeShape()
        {
            try
            {
                var configProperty = typeof(CharacterMaster).GetProperty("currentBodyHandlingConfig", BindingFlags.Instance | BindingFlags.Public);
                var hasConfigProperty = typeof(CharacterMaster).GetProperty("hasBodyHandlingConfig", BindingFlags.Instance | BindingFlags.Public);
                var method = typeof(BodyHandlingConfig).GetMethod(
                    "DetermineCurrentPreset",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                var compatible = configProperty?.PropertyType == typeof(BodyHandlingConfig)
                    && configProperty.GetGetMethod() != null
                    && hasConfigProperty?.PropertyType == typeof(bool)
                    && hasConfigProperty.GetGetMethod() != null
                    && method != null
                    && !method.IsStatic
                    && !method.ContainsGenericParameters
                    && method.ReturnType == typeof(BodyHandlingPresetIndex)
                    && method.GetParameters().Length == 0;
                return new RuntimeShapeStatus
                {
                    Compatible = compatible,
                    Evidence = compatible
                        ? ExactApiContract + "; runtimeShape=PASS(public config getters, zero parameters, exact preset return)"
                        : ExactApiContract + "; runtimeShape=FAIL(incompatible or missing member)",
                };
            }
            catch (Exception ex)
            {
                return new RuntimeShapeStatus
                {
                    Compatible = false,
                    Evidence = ExactApiContract + "; runtimeShape=FAIL(" + ex.GetType().Name + ")",
                };
            }
        }
    }

    internal static class LocalParticipantResolver
    {
        private static readonly Type? LocalUserManagerType = typeof(Run).Assembly.GetType("RoR2.LocalUserManager", false);
        private static readonly MemberInfo? LocalUsersMember = ResolveLocalUsersMember();

        public static IReadOnlyList<CharacterMaster> GetLocalMasters()
        {
            var result = new List<CharacterMaster>();
            FillLocalMasters(result);
            return result;
        }

        public static void FillLocalMasters(List<CharacterMaster> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (LocalUsersMember == null) return;

            object? users = ReadStaticMember(LocalUsersMember);
            if (users is not IEnumerable enumerable) return;

            foreach (var localUser in enumerable)
            {
                if (localUser == null) continue;
                var master = ParticipantIdentityResolver.GetMember(localUser, "cachedMaster") as CharacterMaster
                             ?? ParticipantIdentityResolver.GetMember(localUser, "master") as CharacterMaster;
                if (master != null && !result.Contains(master)) result.Add(master);
            }
        }

        public static ParticipantState ClassifyLocal(CharacterMaster master)
        {
            if (master == null)
                return LocalParticipantPresentationPolicy.Classify(masterAvailable: false, exactRemoteOperationSignal: false, bodyPresent: false);

            CharacterBody? body = null;
            try { body = master.GetBody(); } catch { }
            PlayerCharacterMasterController? controller = null;
            foreach (var candidate in PlayerCharacterMasterController.instances)
            {
                if (candidate == null || !ReferenceEquals(candidate.master, master)) continue;
                controller = candidate;
                break;
            }
            var exactRemoteOperation = controller != null
                && RemoteOperationProbe.HasExactControlledDroneSignal(controller, master, body, out _);
            return LocalParticipantPresentationPolicy.Classify(
                masterAvailable: true,
                exactRemoteOperationSignal: exactRemoteOperation,
                bodyPresent: body != null);
        }

        private static MemberInfo? ResolveLocalUsersMember()
        {
            if (LocalUserManagerType == null) return null;
            return (MemberInfo?)LocalUserManagerType.GetProperty(
                       "readOnlyLocalUsersList",
                       BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                   ?? LocalUserManagerType.GetField(
                       "readOnlyLocalUsersList",
                       BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static object? ReadStaticMember(MemberInfo member)
        {
            try
            {
                if (member is PropertyInfo property && property.GetIndexParameters().Length == 0) return property.GetValue(null);
                if (member is FieldInfo field) return field.GetValue(null);
            }
            catch
            {
            }
            return null;
        }
    }
}
