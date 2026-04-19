# Changelog

All notable workspace changes are documented here.

## 1.0.1-alpha.5 - 2026-04-19
- Set app version to 1.0.1-alpha.5.
- Moved pilot offline gain popup trigger from Hangar to Galaxy.
- Selecting a pilot now queues the offline gain report and shows it as a modal on the Galaxy page after navigation.
- Dismissing the Galaxy popup persists `offline-popup-seen` timestamp for that pilot.

## 1.0.1-alpha.4 - 2026-04-19
- Set app version to 1.0.1-alpha.4.
- Added best-effort LastActive update on app/tab close for the currently selected pilot.
- Client now registers `pagehide`/`beforeunload` hooks and sends `POST /api/game/player/{id}/touch-last-active` via `sendBeacon` (with `fetch keepalive` fallback).

## 1.0.1-alpha.3 - 2026-04-19
- Set app version to 1.0.1-alpha.3.
- Fixed Hangar Last Active behavior to keep DB timestamps instead of resetting all pilots to "just now" while loading account slots.
- Added a dedicated endpoint to persist LastActiveUtc on pilot switch (`POST api/game/player/{id}/touch-last-active`).
- PlayerSelection now always updates the previous pilot's LastActive timestamp when switching to another pilot.

## 1.0.1-alpha.2 - 2026-04-19
- Set app version to 1.0.1-alpha.2.
- Pilot Academy: removed the top-right header resource panel (Credits/Fuel box).
- Fixed class credit flow so Pilot and Gardener no longer generate Credits/s from class flow.
- Class credits flow is now Gambler-only, matching class rule intent.

## 1.0.1-alpha.1 - 2026-04-19
- Set app version to 1.0.1-alpha.1.
- Pilot Hangar card duty text updated: removed reserve pilot XP wording from support duty descriptions.
- Support contribution line now includes Credits/s.
- Support contribution display now hides any resource entries that are 0/s.

## 1.0.1-alpha.0 - 2026-04-19
- Set app version to 1.0.1-alpha.0.
- gorßes Update zu einen main pilot soweit fertig

## 1.0.0-alpha.41 - 2026-04-19
- Set app version to 1.0.0-alpha.41.
- Removed XP from offline gains entirely.
- Offline duty calculations no longer grant player XP or reserve XP.
- Offline popup now shows only resource gains (Credits/Fuel/Alloy/Biomass).

## 1.0.0-alpha.40 - 2026-04-19
- Set app version to 1.0.0-alpha.40.
- Gambler class adjusted to Credits-only gains (no Fuel/Alloy/Biomass class flow).
- Updated Academy and class rule texts to match Gambler Credits-only behavior.
- Offline gain popup flow changed: account-wide auto popup removed; offline popup now appears only when entering a pilot.
- Added DB-backed per-pilot offline popup seen timestamp (`OfflinePopupSeenUtc`) with endpoint to persist popup dismissal state.
- Offline duty math and hangar preview math aligned to per-second formulas (minutes x 60).
- PlayerSelection duty/support texts updated to match current resource behavior (including Gambler duty/support lines).

## 1.0.0-alpha.39 - 2026-04-19
- Set app version to 1.0.0-alpha.39.
- Offline duty calculation refactored to per-second resource gains (minutes * 60), matching class gain expectations.
- Flying/Gardening duty now scale with class skill and doctrine percent bonuses as per-second gains.
- Gambler duty now yields Credits and XP only (no Fuel/Alloy/Biomass from gambling duty).
- Offline account login popup no longer shows repeated combined summaries; it now lists per-pilot gain lines clearly.
- Pilot switch offline popup now reflects only the selected pilot's own offline earnings with the corrected duty math.

## 1.0.0-alpha.38 - 2026-04-19
- Set app version to 1.0.0-alpha.38.
- Startup flow: initial load from root/galaxy now redirects to Account Selection first.
- Pilot Hangar: offline progress popup now also appears when entering a pilot right after account login (no prior active pilot in session).
- Pilot switch popup remains per selected pilot and continues to show only that pilot's offline earnings.
- Pilot cards now show inactivity info: "Last active: ... ago".

## 1.0.0-alpha.37 - 2026-04-19
- Set app version to 1.0.0-alpha.37.
- Pilot Academy: changed gain display to individual boxes per resource gain (Credits, Alloy, Fuel, Biomass).
- Pilot Academy: moved training/doctrine explanation into a dedicated info note below the gain boxes.
- Gambler class update: now provides Credits per second, targeted at about +10 Credits/s at training level 1 (before global multipliers).
- Updated class role text to include Gambler credit flow.

## 1.0.0-alpha.36 - 2026-04-19
- Set app version to 1.0.0-alpha.36.
- Fixed top header reliability on first Galaxy load and after pilot switches.
- MainLayout now rehydrates missing player context on route changes when no active pilot is present.
- Added top-row fallback status chips so the header no longer appears empty while context is restoring.

## 1.0.0-alpha.35 - 2026-04-19
- Set app version to 1.0.0-alpha.35.
- Aligned class resource gain formulas with training levels across all classes (Pilot, Gardener, Gambler), including doctrine scaling.
- Synced Hangar support contribution preview with the same class gain formulas used in PlayerService.
- Removed Credits and XP lines from pilot cards in the Hangar detail block.
- Fixed Slot 4 creation check on server: creation now validates against Account LVL unlock requirement (not highest pilot LVL).

## 1.0.0-alpha.34 - 2026-04-19
- Set app version to 1.0.0-alpha.34.
- Fixed account slot milestone popup trigger so it now reacts to real unlocked-slot increases, not only level-up event timing.
- Added a dedicated account slot unlock event in PlayerService and wired MainLayout popup display to that event.
- Prevented false slot-unlock popups on first account hydration or account switch.

## 1.0.0-alpha.33 - 2026-04-19
- Set app version to 1.0.0-alpha.33.
- Added a new Danger Zone test button in Game1: `TEST: +50 XP`.
- The button grants 50 pilot XP instantly and saves player progress immediately.

## 1.0.0-alpha.32 - 2026-04-19
- Set app version to 1.0.0-alpha.32.
- Removed marker-style badges from pilot cards in the Hangar (for example Best Fuel / Best Credits / Best XP and focus-rank badge).
- Pilot cards now show only role/class/duty/support/resource details without overlay marker tags.

## 1.0.0-alpha.31 - 2026-04-19
- Set app version to 1.0.0-alpha.31.
- Removed Reactor Core click throttle so clicking is now effectively unlimited like Command Nexus.
- Kept click pulse feedback animation without blocking rapid consecutive clicks.

## 1.0.0-alpha.30 - 2026-04-19
- Set app version to 1.0.0-alpha.30.
- Added account milestone popup for newly unlocked pilot slots when account level reaches unlock thresholds.
- Slot unlock popup now triggers for account milestones at LVL 2, 5, and 7.
- Adjusted Slot 4 unlock requirement from Account LVL 6 to Account LVL 7.

## 1.0.0-alpha.29 - 2026-04-19
- Set app version to 1.0.0-alpha.29.
- Added 4th pilot slot support across account, slot unlock, and roster handling.
- Slot 1 remains Main Pilot (classless), Slots 2-4 are support pilot slots for class picks.
- Hangar cards now show Main/Support role labels and per-support contribution values sent to Main Pilot (+Alloy/s, +Fuel/s, +Biomass/s).
- Updated account and academy slot messaging from 3-slot to 4-slot model.

## 1.0.0-alpha.28 - 2026-04-19
- Set app version to 1.0.0-alpha.28.
- Added Main Pilot system: Slot 1 is now the classless main character.
- Classed support pilots in other slots now contribute their passive resource gains to the Main Pilot.
- Pilot Academy now reflects Main Pilot behavior and support-gain presentation.
- Server now blocks class selection for Slot 1 and normalizes legacy Slot 1 class data to classless.

## 1.0.0-alpha.27 - 2026-04-19
- Set app version to 1.0.0-alpha.27.
- Pilot Academy: replaced old class bonus display (mining/cargo/crit) with the new class resource gain model.
- Pilot class gains now show and use resource flow bonuses (Alloy/Fuel/Biomass) directly.
- Training now increases class resource gains by level.
- Doctrine now applies additional percentage scaling to the relevant class gains.

## 1.0.0-alpha.26 - 2026-04-19
- Set app version to 1.0.0-alpha.26.
- Pilot Hangar: fixed pilot card ordering to always follow slot order (Slot 1, Slot 2, Slot 3) instead of dynamic focus sorting.

## 1.0.0-alpha.25 - 2026-04-19
- Set app version to 1.0.0-alpha.25.
- Pilot Hangar: removed the Offline Duty selection popup when switching pilots.
- Pilot Hangar: offline duty is now assigned automatically from the locked class specialization (Pilot -> Flying Duty, Gardener -> Gardening Duty, Gambler -> Gambling Duty).

## 1.0.0-alpha.24 - 2026-04-19
- Set app version to 1.0.0-alpha.24.
- Game5 Defense Grid: removed the Repair base action button from the battlefield footer.
- Game5 Defense Grid: fixed projectile animation start position so shots render from tower positions instead of appearing in the top-left corner.

## 1.0.0-alpha.23 - 2026-04-19
- Set app version to 1.0.0-alpha.23.
- Main layout: added an explicit post-restore re-render on first load so the header/top row is visible immediately after initial bootstrap.

## 1.0.0-alpha.22 - 2026-04-19
- Set app version to 1.0.0-alpha.22.
- Galaxy map achievement hover switched from account-based XP/goal display to pilot-based progress.
- Achievement hover now shows Player XP progress and pilot-specific next unlock goals for the currently active pilot.

## 1.0.0-alpha.21 - 2026-04-19
- Set app version to 1.0.0-alpha.21.
- Pilot Academy: renamed training upgrades to class-specific tracks (Pilot Flight Training, Gardener Growth Training, Gambler Risk Training).
- Pilot Academy: renamed doctrines to class-matching identities and updated doctrine effect text for the current class design.

## 1.0.0-alpha.20 - 2026-04-19
- Set app version to 1.0.0-alpha.20.
- XP flow unified: XP gains now count for both pilot progress and account progress.
- Lootbox rewards now use one combined XP amount (single XP wording) instead of separate Pilot XP and Account XP values.
- Offline report popups now show a single XP metric instead of split Player XP and Pilot XP values.

## 1.0.0-alpha.19 - 2026-04-19
- Set app version to 1.0.0-alpha.19.
- Research page: removed all research upgrade cards and purchase actions for now.
- Research API: disabled research upgrade purchases temporarily and returns a clear disabled message.

## 1.0.0-alpha.18 - 2026-04-19
- Set app version to 1.0.0-alpha.18.
- Game5 Defense Grid: tower shots now spawn visible projectile symbols that fly toward targets.
- Game5 Defense Grid: damage and slow effects are now applied on projectile impact after the flight animation delay, not instantly on fire.

## 1.0.0-alpha.17 - 2026-04-19
- Set app version to 1.0.0-alpha.17.
- Galaxy map-info-hover: removed the "Current highest pilot level" text from the milestone progress line.

## 1.0.0-alpha.16 - 2026-04-19
- Set app version to 1.0.0-alpha.16.
- PlayerSelection slot unlock text: removed the "Current Account LVL X" suffix and kept only the unlock requirement line.
- Top navigation: removed the marked "Nebula Grid v..." version text from the header.

## 1.0.0-alpha.15 - 2026-04-19
- Set app version to 1.0.0-alpha.15.
- Offline rewards: fixed XP mapping so reserve/offline pilot XP is no longer counted as account XP.
- Offline reward popup: changed the second XP metric from Account XP to Pilot XP and wired it to offline pilot XP values.
- Offline summary text: renamed "Reserve Pilot XP" to "Pilot XP" for clearer wording.

## 1.0.0-alpha.14 - 2026-04-19
- Set app version to 1.0.0-alpha.14.
- Player cards: removed marked reserve-heavy info from the slot view.
- Removed reserve badges and reserve projection/preview/progress panel from each pilot card for a cleaner layout.
- Header version label now resolves from the top changelog entry (served as /CHANGELOG.md), so manual version text updates in the client layout are no longer needed.

## 1.0.0-alpha.13 - 2026-04-19
- Set app version to 1.0.0-alpha.13.
- Offline report popup: added Alloy and Biomass metrics to the top summary cards.
- Offline report popup: changed XP labels to Player XP and Account XP.
- Offline report text: added a real line break between the total summary and duty details.
- Offline progress tracking: added explicit offline Alloy/Biomass/Account XP fields for accurate popup totals.

## 1.0.0-alpha.12 - 2026-04-19
- Set app version to 1.0.0-alpha.12.
- README: added dedicated troubleshooting section for recurring `dotnet.js` startup errors when clean/build alone is not enough.

## 1.0.0-alpha.11 - 2026-04-19
- Set app version to 1.0.0-alpha.11.
- Galaxy: hide vertical page scrollbar while the full-screen galaxy map is active.

## 1.0.0-alpha.10 - 2026-04-19
- Set app version to 1.0.0-alpha.10.
- Fixed lootbox account XP mismatch by introducing a persistent AccountXpBank in AccountProfiles.
- Account progression no longer reuses live pilot XP remainder, preventing +18 account XP when the reward says +9.
- Lootbox account XP rewards are now added to AccountXpBank and reflected correctly in account progress.

## 1.0.0-alpha.9 - 2026-04-19
- Set app version to 1.0.0-alpha.9.
- Academy class rules: removed "steady credit flow" from the Pilot rule text.

## 1.0.0-alpha.8 - 2026-04-19
- Set app version to 1.0.0-alpha.8.
- Academy copy: updated rule text to "Each of your three pilots will end up covering one class."
- Class resource flows updated:
	- Pilot: +3 Alloy/s and +1 Fuel/s, no Biomass/s.
	- Gardener: Biomass/s only.
	- Gambler: low gain of all three resources (+1 Fuel/s, +1 Alloy/s, +1 Biomass/s).

## 1.0.0-alpha.7 - 2026-04-19
- Set app version to 1.0.0-alpha.7.
- Achievement popup: now triggers on Pilot level-up instead of Account level-up.
- Achievement popup: account level-up no longer opens the achievement popup.
- Global goals: switched milestone goals from account-level wording to pilot-level unlock targets.
(12 Uhr patch)
## 1.0.0-alpha.6 - 2026-04-19
- Set app version to 1.0.0-alpha.6.
- Client startup: removed fixed blazor.webassembly.js query version to avoid stale loader mismatches.
- Client startup: added one-time automatic retry with cache-busting when dotnet.js dynamic import fails.

## 1.0.0-alpha.5 - 2026-04-19
- Set app version to 1.0.0-alpha.5.
- Maintenance: cleared client/server bin+obj artifacts and rebuilt solution to resolve intermittent static web assets startup/compression errors.
- Verified server startup succeeds on http://localhost:5237 after restore and rebuild.

## 1.0.0-alpha.4 - 2026-04-19
- Set app version to 1.0.0-alpha.4.
- Academy: fixed LVL 3 unlock ring CSS so "LVL 3" and "Reached" no longer overlap.

## 1.0.0-alpha.3 - 2026-04-19
- Set app version to 1.0.0-alpha.3.
- Lootbox: changed XP text order to show Account XP before Pilot XP in reward messages.

## 1.0.0-alpha.2 - 2026-04-19
- Set app version to 1.0.0-alpha.2.
- Lootbox: removed the "Last roll: +X Pilot XP | +Y Account XP" line from the panel.

## 1.0.0-alpha.1 - 2026-04-19
- Set app version to 1.0.0-alpha.1.
- Game2: removed the "Fly to dock and unload" button from the HUD panel.
- Game2: added cargo dock highlight logic that depends on selected resource cargo size and free capacity.
- Game2: cargo dock now glows when unloading is needed for the currently selected resource type.
