# Changelog

## 1.1.0

### Risk Of Options integration

- Added one combined `ItemShareFix` Risk Of Options page containing the 27 intended ItemShareFix controls plus the 15 required original ItemShare 1.7.1 controls.
- Upstream controls bind to the real `com.majai.itemshare` BepInEx `ConfigEntry` instances; no ItemShareFix shadow copies are created.
- Added the approved ItemShareFix icon and a non-empty page description using an embedded runtime resource.
- Added a single combined ItemShareFix Risk Of Options page with five tabs: Sharing, Item Tiers, Markers, Off-screen Indicators, and Marker Colors.
- Added presentation-only PickupMode predicates: marker and Individual-only controls are disabled in Instant mode, while ShareEquipment is disabled in Individual mode. Stored values are never reset when a control becomes inactive.
- Fixed `MarkerDetailRows` registration by using Risk Of Options `IntSliderOption` for its integer `ConfigEntry<int>`.
- Risk Of Options remains optional and is still accessed only through reflection when present.

### Public metadata

- Updated release identity and manifest to `1.1.0`.
- Replaced the public English/Russian README text with the approved concise release documentation.


## 1.0.0

Initial public release.

### Sharing and participant state

- Added personal pickup visibility for locally completed shares while preserving presentation for participants who still need the pickup.
- Added exact Remote Operation / Support Drone-aware participant handling.
- Added deferred entitlement handling for fully dead participants.
- Added authoritative disconnect cleanup separated from death/transient network-object destruction.
- Added configurable temporary-item sharing with a vanilla first-come-first-served bypass when disabled.
- Fresh configurations now default `ShareTemporaryItems` to OFF; existing saved values are preserved.

### Personal marker system

- Added participant-local markers for pending ordinary shared pickups and Artifact of Command choices.
- Added Detailed and Compact presentation modes.
- Added stable world-space semantic grouping and dense-area summaries.
- Added off-screen directional aggregation.
- Added permanent/temporary lifetime glyph presentation.
- Added configurable marker scale, opacity, distance/count presentation, category ordering, and per-category colors.
- Added optional best-effort Risk Of Options integration over canonical BepInEx configuration.
- Added ItemShareFix-owned marker text localization for all 11 supported Risk of Rain 2 interface languages, while keeping actual item names game-native and falling back to English for unknown language values.

### Reliability

- Added explicit participant/claim lifecycle state with duplicate-grant barriers.
- Added modal/HUD suppression and presentation cleanup behavior.
- Added exact upstream compatibility guards for the supported ItemShare/PickupShareApi runtime shapes.
- Added automated regression coverage for core policy and presentation contracts.
