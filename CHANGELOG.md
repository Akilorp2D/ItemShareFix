# Changelog

## Version 1.2.0

- Updated compatibility with Risk of Rain 2 and the Hallowed Concepts DLC.
- Reworked the item marker system, improving performance and the display of large numbers of markers.
- Added item sharing support for Hallowed, Hex, and N'Tormat Pages. Sharing is disabled by default for these new item types and can be enabled individually through the mod configuration or Risk of Options.
- Fixed compatibility with Belmont and HUNK: special items required for character progression are no longer shared with other players.

## 1.1.1

### Hallowed Concepts compatibility

- Updated Remote Operation / Support Drone detection for the Risk of Rain 2 1.5.0 API.
- Added independent OFF-by-default sharing controls for Hallowed items, Hex items and active N'Tormat Pages, including Risk Of Options integration.
- Sharing controls now apply during a run. Already-started shared objects retain eligible pending shares; completed shares are never granted retroactively.
- Added explicit DLC Command categories, localized labels for all 11 supported languages and native category text colors.

### Multiplayer and compatibility fixes

- Fixed excluded Belmont/HUNK progression choices receiving ItemShareFix markers or entering Command sharing (Correction14).
- Made distribution scopes owned by their exact invocation, including nested calls and exception cleanup.
- Added server-authoritative presentation snapshots for eligible options and existing collectors, including late client observation. The metadata protocol never grants items or creates claims.
- Hardened eligibility-cache lifetime, transfer validation and stale snapshot rejection.
- Corrected Remote Operation diagnostic parsing and optional diagnostic log-level filtering.

### Performance and maintenance

- Bounded saturated HUD placement searches, removed per-probe HUD allocations and cached formatted text before TMP width measurements.
- Added live controls to hide markers in DLC4 gong trials and automatically declutter crowded screens, with a configurable 4–16 group limit and focused detail expansion.
- Preserved cold Command prewarming, typed upstream shareability calls and buffered marker layout.
- Reused presentation buffers and cached recurring reflection metadata to reduce allocation churn.
- Removed unused private HUD discovery and lifetime-summary branches.
- Expanded regression checks for DLC compatibility, progression exclusions, marker performance and crowded-screen grouping.

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
