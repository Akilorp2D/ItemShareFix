# ItemShareFix

Version **1.2.0**.

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
- Supports the Risk of Rain 2 **1.5.0 Hallowed Concepts update** with separate **Hallowed**, **Hex**, and **N'Tormat Pages** sharing switches. All three default to **OFF**.
- Applies sharing switches during the current run. Untouched pickups follow current settings; already-started shared objects retain eligible pending shares, without granting collected items again.
- Keeps supported Belmont/HUNK progression pickups personal, including Artifact of Command choices.
- Localizes new Command category labels for all 11 supported interface languages and colors category text using the corresponding pickup color.
- Adds separate sharing support for **temporary items**, with an option to enable or disable it.
- Adds **Detailed** and **Compact** marker modes.
- Adds off-screen pickup indicators.
- Allows marker colors, size, and appearance to be customized.
- Adds full **Risk Of Options** support, including access to both original ItemShare settings and ItemShareFix settings from one menu.
- Hides markers in DLC4 gong trials by default, restoring them after leaving; sharing stays active.
- Automatically groups crowded on-screen markers into at most 8 cards by default. Aim at a member or approach a group to reveal up to four item rows. Both features and the 4–16 card limit can be changed during a run.

## Requirements

**Required:**
- BepInExPack
- PickupShareApi
- ItemShare

**Optional:**
- Risk Of Options — for convenient in-game configuration of ItemShare and ItemShareFix.

## Compatibility and multiplayer

This version targets Risk of Rain 2 **1.5.0** and the supported **ItemShare 1.7.1 / PickupShareApi 1.0.0** combination. Risk Of Options is optional; the combined page contains 33 ItemShareFix controls and 15 original ItemShare controls.

Install **both** `ItemShareFix.dll` and `ItemShareFix.Core.dll` on the host and clients, using the same version. Keep sharing settings consistent between players; configuration is not automatically synchronized. Hex sharing is opt-in because these pickups have harmful effects.

ItemShare remains responsible for grants, Individual/Instant behavior and its original sharing controls. ItemShareFix does not replace its sharing engine.

## Manual installation

Copy the package's `ItemShareFix` folder into `BepInEx/plugins/` in your game/mod-manager profile, so the two DLLs are under `BepInEx/plugins/ItemShareFix/`. Remove any older duplicate ItemShareFix installation before starting the game.

Русское описание: [README_RU.md](https://github.com/Akilorp2D/ItemShareFix/blob/main/docs/README_RU.md).

## License

ItemShareFix is distributed under the **MIT License**.
