# Configuration

BepInEx configuration is authoritative. Risk Of Options, when available with a compatible option API shape, is an optional UI over these entries.

## General

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch for ItemShareFix. |
| `ShareTemporaryItems` | `false` (fresh config) | Share exact temporary pickups through ItemShare; when false they use vanilla first-come-first-served behavior and do not enter ItemShareFix marker presentation. Existing saved values are preserved and are not reset. |
| `PersonalPickupVisibilityRepairEnabled` | `true` | Participant-local ordinary pickup visibility repair; ItemShare's own `HideCollectedOrbs` preference remains respected. |
| `PersonalMarkersEnabled` | `true` | Local-only markers for pending ordinary pickups and Artifact of Command choices. |
| `DeadPlayerDeferredItemsEnabled` | `true` | Fully dead participants defer pending entitlement to a safe restored-player point instead of receiving ItemShare's immediate dead-player grant. |
| `DisconnectCleanupEnabled` | `true` | Cancel ItemShareFix pending/deferred state after authoritative disconnect confirmation. |


### Marker text language

ItemShareFix-owned marker text follows the current Risk of Rain 2 interface language for English, French, Italian, German, Spanish, Japanese, Korean, Portuguese (Brazil), Russian, Simplified Chinese, and Turkish. Actual item names continue to come from the game localization (`Language.GetString(nameToken)`). Unknown/unsupported language values fall back to English. Compact presentation remains category-text-free, and temporary lifetime is represented by glyphs rather than a visible lifetime word.

## Markers

| Key | Default | Meaning |
| --- | --- | --- |
| `MarkerPresentationMode` | `Detailed` | `Detailed` or `Compact`. |
| `ShowMarkerDistance` | `true` | Show distance on in-FOV marker cards. |
| `MarkerScale` | policy default | Presentation scale; does not redefine world membership. |
| `MarkerOpacity` | policy default | Marker opacity. |
| `MarkerBackgroundOpacity` | `0` | Marker background opacity. |
| `ShowMarkerCategoryDiamond` | `true` | Detailed-mode category/rarity glyph. |
| `MarkerDetailRows` | policy default | Maximum visible distinct ordinary item rows before overflow. |
| `MarkerCategorySortOrder` | `HighToLow` | Category display order; `LowToHigh` is exact reverse. |
| `MarkerCompactShowCount` | `true` | Show represented subset/category counts in Compact mode. |
| `EnableOffscreenIndicators` | `true` | Enable broad-direction off-screen indicators. |
| `ShowOffscreenDistance` | `true` | Show nearest represented distance on off-screen indicators. |
| `ShowOffscreenTotalCount` | `false` | Show a total represented count per off-screen sector. |
| `OffscreenIndicatorScale` | policy default | Off-screen indicator scale. |
| `OffscreenIndicatorOpacity` | policy default | Off-screen indicator opacity. |
| `OffscreenEdgePadding` | policy default | Minimum screen-edge padding. |

`ShowMarkerTierComposition` and `MarkerCompactMixedStyle` remain bound as legacy config-compatibility entries; current grouped presentation does not use them to change semantics.

## Marker Colors

Configurable palette entries:

- `Common`
- `Uncommon`
- `Legendary`
- `Boss`
- `Lunar`
- `Void`
- `Equipment`
- `Command`
- `Neutral`
- `OffscreenIndicator`

Temporary lifetime is not a separate rarity/category color. Temporary glyphs inherit the relevant item/category color.

## Diagnostics

| Key | Default | Meaning |
| --- | --- | --- |
| `DiagnosticLogging` | `true` | Enable bounded compatibility/state diagnostics. |
| `DiagnosticLogLevel` | `Info` | `Error`, `Warning`, `Info`, or `Debug`. |
| `PresentationSweepSeconds` | `0.20` | Bounded presentation refresh interval over tracked pickups. |
| `ParticipantSweepSeconds` | `0.25` | Server participant-state refresh interval. |
| `RemoteOperationGraceSeconds` | `2.0` | Reserved compatibility entry; exact Remote Operation classification does not use heuristic grace. |

Legacy `ISF_*` tokens in logs are stable diagnostic identifiers retained for regression tooling; they are not package/release version names.
