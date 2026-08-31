# ItemShareFix

**ItemShareFix** is a compatibility and quality-of-life extension for **ItemShare** in **Risk of Rain 2**. It does not replace ItemShare; it adds bounded participant-state, pickup-presentation, marker, death/disconnect, and temporary-item behavior around the supported ItemShare runtime.

## Features

### Personal pickup visibility

When your local participant has completed their share for a pickup, ItemShareFix suppresses that completed local presentation while preserving it for participants who still need it. The upstream ItemShare visibility preference remains respected.

### Participant-local markers

ItemShareFix adds local-only markers for shared ordinary pickups and Artifact of Command choices that are still pending for your participant.

Marker presentation includes:

- **Detailed** mode with item/category information, count cues, rarity cues, and optional distance;
- **Compact** mode with bounded category glyph groups, counts, and optional distance;
- stable world-space semantic grouping for dense pickup areas;
- off-screen directional aggregation to reduce duplicate arrow clutter;
- separate permanent/temporary lifetime glyphs without turning lifetime into a new rarity/category;
- configurable scale, opacity, count display, category ordering, off-screen presentation, and per-category colors.

ItemShareFix-owned marker text follows the game's interface language for the 11 supported Risk of Rain 2 languages: English, French, Italian, German, Spanish, Japanese, Korean, Portuguese (Brazil), Russian, Simplified Chinese, and Turkish. Actual item names remain game-native through Risk of Rain 2 localization. Unknown/unsupported language values fall back to English. Compact mode remains glyph/count based and does not add category-name text or a visible lifetime word.

### Support Drone / Remote Operation handling

A player in the game's Remote Operation / Support Drone state remains an active sharing participant when the exact supported runtime signal is available. ItemShareFix does not infer this state from names, prefabs, or body heuristics.

### Fully-dead deferred entitlement

When enabled, a fully dead participant's pending entitlement can be deferred until a safe restored-player point instead of forcing the current-stage pickup to remain blocked indefinitely.

### Disconnect cleanup

Confirmed disconnects are handled separately from death or transient network-object destruction. ItemShareFix can cancel its own pending/deferred state for a participant who has actually left, while protecting collected/deferred-granted history from duplicate entitlement.

### Temporary item sharing

`ShareTemporaryItems` controls whether exact temporary pickups participate in ItemShare distribution:

- **ON** — temporary pickups use ItemShare sharing and ItemShareFix marker presentation;
- **OFF** — temporary pickups stay on the vanilla first-come-first-served path and are excluded from ItemShareFix markers.

Fresh `1.0.0` configurations default this setting to **OFF**. Existing saved BepInEx values are preserved and are not reset by the release. The setting can change live. Permanent sharing remains independent.

## Requirements

Hard dependencies:

- BepInExPack `5.4.2121`
- PickupShareApi `1.0.0`
- ItemShare `1.7.1`

Optional:

- Risk Of Options — best-effort reflection-based in-game UI for ItemShareFix configuration. It is **not** a hard package dependency, and BepInEx configuration remains authoritative if a Risk Of Options API shape is unavailable or only partially compatible.

ItemShareFix intentionally validates the exact supported ItemShare/PickupShareApi runtime contract. Do not assume arbitrary upstream versions are compatible.

For multiplayer use, all participating players should use the compatible mod set required by ItemShare/ItemShareFix behavior.

## Installation

### Mod manager

Install ItemShareFix with a Thunderstore-compatible mod manager. The package declares its hard dependencies.

### Manual

Install the exact required dependencies first. Then place both runtime assemblies under a BepInEx plugin folder, for example:

```text
BepInEx/plugins/ItemShareFix/ItemShareFix.dll
BepInEx/plugins/ItemShareFix/ItemShareFix.Core.dll
```

## Configuration

BepInEx `ConfigEntry` values are canonical. Major groups include:

- **General** — master enable, temporary sharing, personal pickup visibility, personal markers, fully-dead deferred entitlement, disconnect cleanup;
- **Markers** — Detailed/Compact mode, distance, scale, opacity, category cues, row limits, category ordering, Compact counts, off-screen indicators;
- **Marker Colors** — Common, Uncommon, Legendary, Boss, Lunar, Void, Equipment, Command, Neutral, and off-screen colors;
- **Diagnostics** — bounded compatibility/state logging and refresh intervals.

See [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) for the public configuration reference.

## Compatibility and failure policy

ItemShareFix is a bounded patch/extension layer around ItemShare. When an exact required runtime/API shape cannot be established, the affected repair path fails closed rather than guessing through unsafe heuristics.

Legacy `ISF_*` diagnostic identifiers may appear in logs. They are stable diagnostic keys retained for regression/field tooling; they are not release-version identifiers.

## Development

See:

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
- [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- [`docs/README_RU.md`](docs/README_RU.md)

## Reporting issues

Include:

- ItemShareFix version;
- ItemShare and PickupShareApi versions;
- BepInEx/mod-manager profile information;
- host/client role;
- relevant `LogOutput.log` excerpts;
- clear reproduction steps;
- screenshots for marker/presentation issues when useful.

## License

MIT. See [`LICENSE`](LICENSE).

ItemShareFix is an independent project that depends on and extends ItemShare/PickupShareApi behavior. It is not an official release of those upstream projects.
