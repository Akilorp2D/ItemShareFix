# ItemShareFix architecture

## Purpose

ItemShareFix is a bounded compatibility/quality-of-life layer around ItemShare. ItemShare remains the upstream sharing system; ItemShareFix owns only the participant-state, entitlement, local-presentation, and marker paths it explicitly repairs.

## Solution layout

```text
ItemShareFix.sln
├─ src/ItemShareFix.Core
├─ src/ItemShareFix.Plugin
└─ tests/ItemShareFix.Core.Tests
```

### `ItemShareFix.Core`

`netstandard2.1` game-independent policy/state logic where practical:

- participant identity/state and claim lifecycle;
- deferred entitlement and duplicate-grant barriers;
- authoritative disconnect confirmation;
- exact Remote Operation decision policy;
- temporary-sharing authority boundary;
- local pickup/Command presentation policy;
- marker classification, world clustering, dense-area summary, LOD, layout, and off-screen aggregation.

### `ItemShareFix.Plugin`

`netstandard2.1` BepInEx/Risk of Rain 2 integration:

- `ItemShareFixPlugin` — plugin lifecycle and startup;
- `CompatibilityGuard` — exact supported upstream runtime validation;
- `UpstreamBridge` — ItemShare/PickupShareApi adapter;
- `RuntimePatches` — Harmony integration points;
- `ParticipantRuntime` — authoritative runtime state to `ParticipantState` mapping;
- `ServerCoordinator` — claim/deferred/disconnect lifecycle;
- `ClientPresentation` — participant-local pickup/marker eligibility;
- `NativeHudMarkerRenderer` — Detailed/Compact/off-screen rendering;
- `PluginConfig` — canonical BepInEx configuration;
- `OptionalRiskOfOptionsIntegration` — soft reflection-based options UI.

## Participant identity and state

Stable user identity is separated from a participation **generation**. A reconnect/new master generation cannot silently resurrect a terminal claim from an earlier generation.

```text
Alive
SupportDrone
FullyDead
Disconnected
```

- `Alive`: normal active participant.
- `SupportDrone`: active participant through the exact Remote Operation signal.
- `FullyDead`: no normal active body; pending entitlement may be deferred.
- `Disconnected`: authoritative absence confirmed; distinct from death and transient object destruction.

## Claim lifecycle

```text
Pending
Collected
Deferred
GrantedDeferred
CancelledDisconnected
```

`ClaimLedger` keeps current generation records and stable-user historical barriers. Collected/granted terminal history prevents duplicate entitlement; confirmed disconnect cancellation cannot overwrite stronger terminal history.

## Disconnect authority boundary

A generic Unity/UNet object-destroy callback is lifecycle evidence, not proof that a player left the session. Disconnect cleanup requires authoritative absence plus the configured confirmation/grace policy.

## Temporary-sharing boundary

Temporary detection uses the exact game lifetime signal. If temporary sharing is disabled, the interaction leaves ItemShareFix/ItemShare distribution at the early vanilla-bypass boundary. Once upstream ItemShare state has been established for an interaction, ItemShare remains authoritative for that state.

Presentation lifetime remains orthogonal to rarity/category. Disabling temporary sharing also makes exact temporary candidates ineligible for ItemShareFix marker membership without hiding/destroying the underlying vanilla pickup.

## Local pickup visibility

Local-collected suppression is participant-local presentation behavior. It does not globally destroy the shared network object and does not remove presentation needed by another local/remote participant.

## Marker pipeline

```text
eligible local pending candidates
        ↓
world-space semantic membership
        ↓
physical clusters / dense-area summaries
        ↓
adaptive presentation
        ├─ in-FOV Detailed / Compact
        └─ off-screen directional aggregation
```

World membership is based on stable logical identity and world-space geometry. Camera projection is presentation state only; moving the camera must not redefine which pickups belong to the same semantic cluster.

Compact outer geometry is category-based. Permanent/temporary subsets can render as separate diamond/clock glyph groups inside one category slot without physical item count multiplying category glyphs.

Off-screen lifetime cues are shown only when the represented lifetime truth is exact; mixed/unknown/multi-node sectors remain neutral rather than claiming temporary-only truth.

## Risk Of Options

BepInEx configuration is canonical. Risk Of Options is a soft dependency loaded through reflection. Registration is best-effort: unsupported option API shapes fail softly and do not prevent the mod from using BepInEx config.

## Compatibility failure policy

The supported ItemShare/PickupShareApi versions and critical API/assembly shapes are intentionally exact. When a required contract cannot be proven, ItemShareFix fails closed for the affected path rather than using item-name/prefab/body heuristics or broad fallback grants.
