# ItemShareFix

**ItemShareFix is a patch and QoL extension for the original ItemShare mod.**

It fixes several ItemShare issues, adds personal pickup markers, improves sharing behavior in several multiplayer situations, and adds support for temporary items.

## What ItemShareFix fixes and adds

- Adds personal markers for pickups you still need to collect. **Markers are only used in ItemShare `Individual` mode; in `Instant` mode they are automatically disabled.**
- Fixes the original **`HideCollectedOrbs`** feature so it correctly hides pickups you have already collected while other players can still collect their share.
- After you collect your share, that pickup's marker disappears only for you; players who still need it continue to see their marker.
- Improves ItemShare behavior when a player dies.
- Adds correct support for the **Support Drone** state.
- Improves handling of fully dead and disconnected players so they do not block pickup sharing.
- Improves **Artifact of Command** pickup presentation.
- Adds separate sharing support for **temporary items**, with an option to enable or disable it.
- Adds **Detailed** and **Compact** marker modes.
- Adds off-screen pickup indicators.
- Allows marker colors, size, and appearance to be customized.
- Adds full **Risk Of Options** support, including access to both original ItemShare settings and ItemShareFix settings from one menu.

## Requirements

**Required:**
- BepInExPack
- PickupShareApi
- ItemShare

**Optional:**
- Risk Of Options — for convenient in-game configuration of ItemShare and ItemShareFix.

## AI Assistance

This project was created with the assistance of **ChatGPT by OpenAI**. ChatGPT was used during development for code generation and refactoring, problem analysis, documentation, and review of changes.

Released versions of the mod were built, automatically tested, and manually validated before publication.

## Links

- **Thunderstore:** https://thunderstore.io/c/riskofrain2/p/Akilorp2D/ItemShareFix/

## License

ItemShareFix is distributed under the **MIT License**.
