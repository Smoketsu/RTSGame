<!-- rts/docs/specs/research_and_unlocks.md -->
<!-- Build spec for P-007 option (a), developer decisions of 21 Nov 2026: research items queued at the existing tech buildings (Storm Spire, Reef Workshop) through the existing train_unit command and production queue; each finished item sets a per-player flag that the existing requires list can name. Package R1 (simulation, data, loader, the queries Unity needs, and the smallest AI change that keeps every existing profile playing) is built now; package R2 (the AI using research, behind a profile switch, plus its harness accounting) is specified here and built later. Every number is a placeholder. Part of rts/docs/specs (index: README.md); built by a Sonnet builder, reviewed by the sim-reviewer after each package. -->
# Spec: research and unlocks (P-007 option a)

Written 21 Nov 2026 by the designer against `main`. Sources: `proposals/P-007-research-and-unlocks.md` (option a, sections 2 to 6) and the developer decisions of 21 Nov 2026 (below); `specs/walls_and_tech.md` package T (`requires`, `FirstMissingRequirement`, rule T2); `specs/ai_budget_queues.md` (tech queue, `RareSurplusActive`); `specs/combat_mechanics.md` section 5 (re-pin procedure); the header of `rts/src/Sim/Simulation.cs`; code read: `Economy/EconomySystem.cs` (`StepProduction`, `RecomputeSupply`), `Economy/EconomyRules.cs`, `Commands/EconomyCommands.cs` (`TrainUnitCommand`), `Units/Unit.cs` (production queue and `WriteHash`), `Units/UnitEconomyDefinition.cs`, `Teams/Player.cs`, `Core/ContentCatalog.cs`, `Data/DataLoader.cs`, `Data/UnitSchema.cs`, `rts/src/Ai/AiBrain.cs` (`TechPrerequisite`, `FirstUnmetRequirement`, `FindFreeProducer`), `rts/src/Ai/AiProfile.cs` (opening requires check), `rts/src/Ai/AiWorldView.cs`, `tools/BalanceHarness/SummaryObserver.cs`, `PlayerLedger.cs`, `GameSummary.cs`, `BalanceReport.cs`; unit files under `rts/data/units`.

## 0. Decisions in force and ground rules

Developer decisions (21 Nov 2026), final:
1. Research is in the game, option (a): items queued at the tech buildings; a finished item sets a per-player flag that `requires` can name.
2. At the existing `skai.storm_spire` and `water.reef_workshop`. No new building.
3. Research unlocks units only: Thunderhead, Leviathan, Ink Lobber (id `water.ink_lobber`, shown as "Brinecaster"). The Roc Rider stays Q-T1 (ungated).
4. The tree is the **same size for every faction**: same number of items, same total cost.
5. No repeatable upgrades. 6. Enemy research is not visible. 7. Tech building lost mid-research: the item in progress is lost; finished research is kept.
8. Build now, before further AI work, in two packages: R1 now, R2 later. R1 must leave every existing AI profile able to play.
9. Map rare amounts unchanged; unit and combat numbers frozen (P-005).

Ground rules:
- **Least new state and code.** Research reuses the `train_unit` command, the producer's production queue, the `requires` list and `FirstMissingRequirement`. New runtime state: one sorted set of research IDs per player. No new command, codec, step or tuning key.
- **Numbers.** No existing unit, combat, economy or map number changes. New numbers (research costs and times) are placeholders by the rule in R1.2; data comments say `Source: placeholder, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).`
- **Determinism.** Integers and `Fix64` only; every scan is over `sim.Units.All` (ascending ID) and is an existence test; the research set is a `SortedSet<string>` with `StringComparer.Ordinal`.
- **Test setups.** "Economy setup" = `TestWorld.StartedEconomy()` as in `walls_and_tech.md` section 0 (20 ticks per second, P0 test faction starts with 200 common and 0 rare). Rare for tests is granted with the internal `Player.AddResource(ResourceKind.Rare, n)` before the first step. New test definitions go in a test-local catalog (a copy of `TestWorld.Catalog()` plus additions), never into `TestWorld.Catalog()`.

---

## Package R1: simulation, data, queries, and the AI kept playing

### R1.1 Rules

1. **A research item** is a data definition (`ResearchDefinition`, R1.3) with an ID, a faction, a common and a rare cost and a research time. It is not a unit: it never spawns, uses no supply and has no `requires` of its own.
2. **Where.** A building lists the items it can research in a new unit key `researches` (R1.2), as it lists units in `trains`. Shipped: `skai.storm_spire` and `water.reef_workshop`. The tech buildings still train nothing (`walls_and_tech.md` T.1 rule 5 is amended: "no `trains`, no supply, no weapon; research only").
3. **Queueing uses `train_unit`.** `TrainUnitCommand` with `DefinitionId` naming a research ID queues it at the producer. Cost is paid when queued, as for a unit. The entry goes into the producer's existing `ProductionQueue` (a list of definition ID strings) and counts against `max_queue_length` like a unit. Reason for reusing the command: same fields (producer, ID), so no new command class, codec, script kind, AI intent or replay change; the content digest already refuses older replays.
4. **Progress.** The head entry counts down in step 7b exactly like a unit (`StepProduction`): only while the producer is complete. Its length in ticks is `max(1, Clock.SecondsToTicks(research_seconds))`. A research head never needs a spawn tile and never waits.
5. **Completion.** When a research head reaches 0: add the ID to the producer owner's research set, emit `ResearchCompletedEvent` (R1.4), remove the head and start the next entry's countdown with that entry's own length (unit or research, R1.5 helper `QueueEntryTicks`).
6. **Once per player.** An item can be researched once per player. A second queue order is rejected while the item is queued at any of the owner's producers (`already_queued`) or once it is done (`already_researched`). No repeatable research (decision 5).
7. **`requires` may name a research item** of the same faction. `FirstMissingRequirement` treats such an entry as met when the owner's research set contains it; building entries keep the T.1 rule. The check stays at order time only (T.1 rule 3).
8. **Building lost** (decision 7): the producer's queue goes with it (existing behaviour: the queue lives on the unit). The item in progress and any items queued behind it are lost without refund, as queued units are today (Q-R4). Finished research stays in the player's set for the rest of the match, whatever happens to the building. Because the queue is gone, the item is no longer `already_queued` and can be queued again elsewhere (paid again).
9. **Own only.** Research counts only for the player whose producer finished it. Allies' research never counts (as Q-T4). Units transferred by a hand-over policy credit the producer's owner at completion time.
10. **Direct spawns** (`spawn_unit`, `StandardMatchStart`, harness `--fights`) still ignore `requires` (T.1 rule 2). There is no script command that grants research; scripts queue it with `train_unit` like a player.
11. **Fog** (decision 6): research is never visible to an enemy. `AiWorldView` already copies production queues only for own units (`AiUnitView.Own`); enemy views have no queue. R2 adds the AI's own research set only. `ResearchCompletedEvent` is in the global event log like every event; Unity shows it only for the local player (R1.10). A completed enemy item is revealed only by seeing the unit it unlocks.

### R1.2 Data

#### Which units move behind research, and how the trees were equalised

Decision 3 names one Skai unit (Thunderhead) and two Water units (Leviathan, Ink Lobber). **Rule used: mirror by tier.** Each Water item gets one Skai counterpart that gates the Skai unit in the same role, cost and build-time band. The Ink Lobber (artillery with blast splash, 150 common / 40 rare, 30 s, mid producer) has an exact Skai twin in the Stormcaller (ground bombardment with blast splash, 150 / 40, 30 s, mid producer). So the smallest change that makes the trees equal is **one extra Skai item, `skai.stormcaller_plans`, gating the Stormcaller.** Both trees then have two items: one for the top unit and one for the mid artillery unit. **The choice of the Stormcaller is a placeholder for the developer to confirm (Q-R1);** it widens decision 3's list by one unit and delays the Downdraft, which the Stormcaller carries (Q-R2).

Costs. **Rule used:** the proposal's Water tree is the shared size (P-007 section 3a: Leviathan plans 25 / 175 / 45 s, Ink plans 25 / 125 / 30 s, total 50 common / 300 rare), because P-007 section 2 result 1 sizes "about 300 rare per faction" as the total that covers both factions to 600 s. Each Skai item copies the values of its Water tier twin. One total for both factions: **50 common, 300 rare, 75 s of research.** All placeholders. (P-007 sized Skai at 125 rare; equal trees raise Skai's top item from 125 to 175 rare. P-007 risk "starving Skai" is measured by the R2 bars.)

| Research ID | Display name | Faction | Researched at | Gates | `cost_common` | `cost_rare` | `research_seconds` (ticks at 20/s) |
| --- | --- | --- | --- | --- | ---: | ---: | ---: |
| `skai.stormcaller_plans` | Stormcaller Plans | skai | `skai.storm_spire` | `skai.stormcaller` | 25 | 125 | 30 (600) |
| `skai.thunderhead_plans` | Thunderhead Plans | skai | `skai.storm_spire` | `skai.thunderhead` | 25 | 175 | 45 (900) |
| `water.ink_lobber_plans` | Brinecaster Plans | water | `water.reef_workshop` | `water.ink_lobber` | 25 | 125 | 30 (600) |
| `water.leviathan_plans` | Leviathan Plans | water | `water.reef_workshop` | `water.leviathan` | 25 | 175 | 45 (900) |

Display names are placeholders (the proposal's working names "Thunderhead plans", "Leviathan plans", "Ink plans"; "Brinecaster" because that is the unit's display name). The developer renames at the end.

#### New folder `rts/data/research/<faction>/<name>.json`

One file per item, like `units/`. Example (`rts/data/research/skai/thunderhead_plans.json`; the other three follow with the table's values):
```
// rts/data/research/skai/thunderhead_plans.json: Thunderhead Plans, a Skai research item queued at the Storm Spire; finishing it lets its owner train Thunderheads. Part of rts/data/research; read by the data loader (rts/src/Sim/Data) into a ResearchDefinition. Every number is a placeholder.
{
  "$schema_version": 1,
  "$doc": "Thunderhead Plans research item for the skai faction (unlocks skai.thunderhead; researched at the Storm Spire); placeholder numbers.",
  // Stable research ID, equal to the faction folder, a dot and the file name; shares one namespace with unit IDs. Unit: text ID. Range: lower-case letters, digits, underscores and dots; not equal to any unit ID. Source: docs/decisions/ADR-003-entity-ids.md.
  "id": "skai.thunderhead_plans",
  // Name shown to players. Unit: text. Range: 1 to 40 characters. Source: placeholder, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).
  "display_name": "Thunderhead Plans",
  // Faction that can research this; must equal the folder name and match a file in rts/data/factions. Unit: faction ID. Range: an existing faction ID. Source: Factions.
  "faction_id": "skai",
  // Cost in the common resource, paid when queued, not refunded. Unit: resource units. Range: 0 to 100000. Source: placeholder, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).
  "cost_common": 25,
  // Cost in the rare resource, paid when queued, not refunded. Unit: resource units. Range: 0 to 100000. Source: placeholder, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).
  "cost_rare": 175,
  // Time the item takes at the head of its building's queue. Unit: seconds. Range: 0.05 to 3600. Source: placeholder, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).
  "research_seconds": 45
}
```
Allowed keys: `$schema_version`, `$doc`, `id`, `display_name`, `faction_id`, `cost_common`, `cost_rare`, `research_seconds`. All required.

#### New unit key `researches`

At the root of a unit file, read by `UnitSchema.ReadEconomy` like `trains` (add to the root `RejectUnknownKeys` list in `DataLoader.ReadUnit`); stored as `UnitEconomyDefinition.Researches` (`IReadOnlyList<string>`, optional constructor argument defaulting to empty):
```
// Research items this building can queue (each once per player, through train_unit); a finished item lets its owner meet a requires entry that names it. Unit: list of research IDs. Range: 0 to 8 distinct IDs of research files of this unit's faction; only on a building (footprint 1) that is not an HQ (empty when absent). Source: P-007 developer decisions 1 and 2 (21 Nov 2026); rts/docs/specs/research_and_unlocks.md R1.2.
"researches": ["skai.stormcaller_plans", "skai.thunderhead_plans"],
```
- `skai/storm_spire.json`: `"researches": ["skai.stormcaller_plans", "skai.thunderhead_plans"]`; `$doc` becomes "tech building: researches Stormcaller Plans and Thunderhead Plans; Thunderheads also need one standing".
- `water/reef_workshop.json`: `"researches": ["water.ink_lobber_plans", "water.leviathan_plans"]`; `$doc` likewise.
- List order is menu order (mid item first).

#### `requires` entries on the four units

| File | `requires` before | after |
| --- | --- | --- |
| `skai/thunderhead.json` | `["skai.storm_spire"]` | `["skai.storm_spire", "skai.thunderhead_plans"]` |
| `water/leviathan.json` | `["water.reef_workshop"]` | `["water.reef_workshop", "water.leviathan_plans"]` |
| `skai/stormcaller.json` | absent | `["skai.stormcaller_plans"]` |
| `water/ink_lobber.json` | absent | `["water.ink_lobber_plans"]` |

The building entries of Thunderhead and Leviathan stay (package T unchanged; Q-R3). The Stormcaller and Ink Lobber get only the research entry: smallest change, no new building gate the decisions do not ask for. Each of the four files gets the `requires` comment line with the range widened:
```
// Buildings the owner must have standing and complete, and research items the owner must have finished, to train or build this; checked only when the order is given. Unit: list of unit and research IDs. Range: 0 to 8 distinct IDs of buildings (footprint 1) or research items of this unit's faction, never this unit, no cycles (empty when absent). Source: P-005 developer decision 8 (18 Nov 2026) and P-007 developer decisions 1 and 3 (21 Nov 2026); gated units and the Stormcaller choice are placeholders, developer to confirm (rts/docs/specs/research_and_unlocks.md R1.2).
```
Update the `$doc` of the four units ("needs Thunderhead Plans" and so on). Faction files do not change (research belongs to a faction through its folder and `faction_id`).

#### Loader validation

Each failure is a `DataValidationException` naming the file and field.
- **Folder routing** (`DataLoader.Load`): new `case "research"` calling `ReadResearch(root, path, parts)`; the unknown-folder message lists `research/` too: `"is not under a known data folder (tuning/, units/, research/, factions/, maps/, cycles/, ai/ or scripts/)"`.
- **`ReadResearch`** (per file): `CheckDepth(path, parts, 3, "research/<faction>/<name>.json")`; `RejectUnknownKeys` with the keys above; `id` via `ReadId(root, parts[1] + "." + StemOf(parts), true)`; `display_name` non-empty; `faction_id` must equal the folder (same message as units: `"must be 'f' to match the folder the file is in (found 'g')"`); `cost_common`, `cost_rare` integers 0 to 100000; `research_seconds` `Fix64` 0.05 to 3600 (as `build_seconds`). A duplicate research ID: `"duplicates the research id 'x' defined elsewhere"` (field `id`).
- **`researches`** (per file, `UnitSchema`): not an array of strings; more than 8 entries (`"lists more than 8 entries"`); a duplicate (`"lists 'x' twice"`).
- **`CheckReferences`**, new checks after the `requires` loop:
  - a research file's `faction_id` with no faction file: `"names the faction 'f', which has no file in factions/"` (research file, `faction_id`);
  - a research ID equal to a unit ID: `"duplicates the unit id 'x'; research and unit IDs share one namespace"` (research file, `id`);
  - `researches[i]` unknown: `"names the research 'x', which has no file in research/"`; of another faction: `"names 'x' of faction 'f'; research must be of the same faction"`; a non-empty `researches` on a unit with footprint 0 or an `hq` object: `"is only allowed on a building (footprint 1) that is not an HQ"` (field `researches`).
- **`requires[i]`** (the existing loop): an entry that is a research ID skips the footprint check; the faction check uses the research's faction with the existing message; an entry in neither catalog keeps today's message (`"names the unit 'x', which has no file in units/"`, so T-9 stays green). New: a research entry that no building of the same faction lists in `researches`: `"names the research 'x', which no building of faction 'f' lists in researches"`.
- **`CheckRequirementCycles`**: research IDs are leaves (they have no `requires`); `VisitRequirement` must not index `content.Units` with a research ID (skip it). No new cycle message.
- `ContentCatalog.Validate()` also returns a problem for a `researches` entry with no research definition (`"Unit 'u' researches unknown research 'x'."`), for catalogs built in code.

### R1.3 Types

- **`ResearchDefinition`** (new, `rts/src/Sim/Units/ResearchDefinition.cs`, next to `UnitDefinition`; immutable): `Id` (string, non-empty), `DisplayName` (string, presentation only, not hashed), `FactionId` (string), `CostCommon` (int, resource units, 0 to 100000), `CostRare` (int, resource units, 0 to 100000), `ResearchSeconds` (`Fix64`, seconds, 0.05 to 3600). `WriteHash(StateHasher)` writes `Id, FactionId, CostCommon, CostRare, ResearchSeconds` in that order.
- **`ContentCatalog`**: `IReadOnlyDictionary<string, ResearchDefinition> Researches` (backed by a `SortedDictionary` with `StringComparer.Ordinal`), `AddResearch(ResearchDefinition)` (throws on a duplicate or when frozen, like `AddUnit`).
- **`UnitEconomyDefinition.Researches`**: `IReadOnlyList<string>`, file order.
- **`Player`** (new state, the only runtime state of this spec):
  - `private readonly SortedSet<string> _researched` (ordinal): research IDs this player has finished. Range: 0 to the number of research definitions of the player's faction.
  - `public IReadOnlyCollection<string> Researched` (ordinal order) and `public bool HasResearched(string researchId)`.
  - `internal void AddResearch(string researchId)`: called only by `EconomySystem.StepProduction`. Adding an ID that is already present cannot happen (rule 6) and is a no-op.

### R1.4 Command, rejections, event

**`train_unit` with a research ID.** `TrainUnitCommand.Apply` looks the ID up in `sim.Content.Units` first and then in `sim.Content.Researches` (IDs cannot collide, R1.2). The unit path is unchanged. The research path rejects, in this order:

1. `unknown_unit` (the producer), 2. `not_owner`, 3. `unknown_definition` (in neither catalog), 4. `not_owner` (no player record), 5. `under_construction`, 6. `cannot_train` (the ID is not in the producer's `Economy.Researches`), 7. `already_researched` (`owner.HasResearched(id)`), 8. `already_queued` (`EconomyRules.IsResearchQueued(sim, producer.Owner, id)`), 9. `queue_full`, 10. `insufficient_resources`.

`hq_not_rooted`, `missing_prerequisite` and `supply_cap` do not apply (a research holder is never an HQ, research has no `requires` and no supply). On acceptance: `owner.Spend(CostCommon, CostRare)`, `producer.EnqueueProduction(id, EconomyRules.QueueEntryTicks(sim, id))`. `RecomputeSupply` needs no change: its queue loop already skips IDs that are not in `Content.Units`. Two research orders for the same item on one tick: the first in (issuer, sequence) order is accepted and the second sees it queued (`already_queued`). Update the `Apply` summary with the research rejection list.

**Codec, replays, network.** No change: the `train_unit` codec already carries the definition ID as a string, and `CommandScript`'s `train_unit` entry already takes any definition string. No replay format or protocol bump; older replays are refused by the content digest (ADR-009).

**New event** in `Events/EconomyEvents.cs`:
```
ResearchCompletedEvent(int tick, EntityId producer, PlayerId owner, string researchId)
```
Emitted in step 7b on the completion tick. Several on one tick come in ascending producer ID order (the 7b loop). No "research started" event (accepted train orders have none either; Unity reads the queue).

### R1.5 Public queries (Unity and the AI)

In `EconomyRules` (pure, no allocation beyond what exists):
- `public static bool IsResearchQueued(Simulation sim, PlayerId owner, string researchId)`: true when a unit owned by `owner` has `researchId` anywhere in its `ProductionQueue`. One scan of `sim.Units.All`; an existence test.
- `public static int QueueEntryTicks(Simulation sim, string definitionId)`: `BuildTicks` for a unit ID, `max(1, Clock.SecondsToTicks(ResearchSeconds))` for a research ID, 0 for an unknown ID. Used by `TrainUnitCommand` and by `StepProduction` for the next entry (today `StepProduction` looks the next entry up in `Content.Units` only, which would give a research entry 0 ticks and finish it on the next tick).
- `FirstMissingRequirement` (existing): may now return a research ID. Callers that show it look the name up in `Content.Researches` when the ID is not a unit.
- `Player.HasResearched`, `Player.Researched` (R1.3).

Unity's progress bar reads `producer.ProductionTicksRemaining` and `QueueEntryTicks(sim, producer.ProductionQueue[0])`.

### R1.6 Step order and code placement

No new step. Step 2: `train_unit` may queue research. Step 7b: `StepProduction` does, in this order: tick the head; if it is still above 0, return; if the head ID is a research ID: `AddResearch`, emit `ResearchCompletedEvent`, `FinishProductionHead(QueueEntryTicks(next or 0))`, return; otherwise today's unit path, with the next entry's ticks from `QueueEntryTicks`. An unknown head ID keeps today's drop (`FinishProductionHead` with the next entry's `QueueEntryTicks`).

Header edits in `Simulation.cs`: step 2 "(train orders may also queue a research item listed in the producer's researches, once per player)"; step 7b "production queues (a finished research item sets its owner's research flag, ResearchCompletedEvent), ...". Update the `EconomySystem.cs` header "Production" paragraph the same way.

### R1.7 State hash and content digest

- **`Player.WriteHash`**: append after `SupplyCap`: `_researched.Count`, then each ID in ordinal order. Update its summary's field list. `HashCoverageTests` gains the new field.
- Queued research is already hashed: `Unit.WriteHash` writes the production queue strings and `ProductionTicksRemaining`.
- **Content digest** (`ContentCatalog.ComputeDigest`): after the factions loop and before `Combat.WriteHash`, write `_researches.Count`, then each definition's `WriteHash` in ordinal ID order. `UnitEconomyDefinition.WriteHash`: append `Researches.Count` and each ID in file order after `IsGate`.

### R1.8 Existing AI: how every profile keeps playing (the R1 answer to decision 8)

**Answer: in R1 the AI never researches, so it never fields the four gated units (Stormcaller, Thunderhead, Ink Lobber, Leviathan). It plays on with the rest of its roster, and it neither crashes nor issues a rejected command.** This is the smallest honest answer. It invents no AI behaviour before R2, and it gives the AI no free research the player does not get.

What the current code already does: `AiBrain.FirstUnmetRequirement` looks for an own unit with the required ID. No unit ever has a research ID, so `RequirementsMet` is false for the four units. `FindFreeProducer` then returns null, `Produce` moves to the next unit in deficit, and with budget queues the failure records no want. The counter-mix still gives the four units weight, but they are never candidates.

Three changes are needed, all in `rts/src/Ai`:
1. **`AiProfile` opening check** (`AiProfile.cs`, the composition loop that throws "requires 'x', which this opening's build_order never builds", and the step loop above it): skip `requires` entries that are keys of `content.Researches`. Without this, every profile whose openings list a Stormcaller, Ink Lobber, Thunderhead or Leviathan with weight above 0 fails to load (for example `aggressive_s5` openings `skai_air`, `water_leviathan_tech` and the standard ones).
2. **Rule T2** (`TechPrerequisite`): skip a composition unit when any of its `requires` entries is a key of `_view.Content.Researches`. Without this, T2 would index `Content.Units` with a research ID and throw, or would build a Storm Spire or Reef Workshop for a unit it can never train.
3. Nothing else. Build-order steps that name the Storm Spire (`skai_air`) or the Reef Workshop (`water_leviathan_tech`) still build them. In R1 that money is wasted (150 common and 50 rare); it is accepted as part of the measured cost below.

**Cost to measurement (state it in the R1 report and in every report run on R1).**
- Every AI game whose composition gives one of the four units weight changes, which is nearly all of them: the Water standard mixes list the Ink Lobber, the Skai mixes list the Stormcaller, and the counter-mix adds Thunderhead and Leviathan. `balance_first`, `ai_b`, `ai_p` and every other stored ladder or pool result is therefore **not comparable** with games played on R1. Paired comparisons between two profiles that both run on R1 remain valid as comparisons, but their absolute numbers move.
- Rare spending falls in R1 AI games: the only rare items left are the Roc Rider, Spoutling, Tidecaller and the two tech buildings. Expect `CHECK balance_unspent` to fail by more than today. That reflects the R1 AI, not the design.
- **Rule:** no new balance baseline and no P-006 ladder is run on R1 alone. The re-baseline (the two ladders, about 95 minutes, P-007 section 5) runs once R2 is merged and its candidate passes. Unit tests and the exit test still run on R1.

Tests that expect the AI to train a gated unit are updated to finish the research in their setup (R1.11, R-17), never by removing a `requires` entry. Known ones: `AiArmyMixTests` (the Thunderhead `Train` intent near line 786), `AiPrerequisiteTests` TA-2 and TA-3 (R-17), `MissingRosterTests` (training a Thunderhead and a Leviathan), and any `AiOpeningTests` or `AiBudgetTests` case that asserts a Stormcaller or Ink Lobber `Train`. Report every other test whose assertion moves, with its old and new value.

### R1.9 Pins

The content digest (four research files, `researches`, wider `requires`, the research section) and the player hash (count 0 for every player in existing games) move **every pin**. Re-pin as `combat_mechanics.md` section 5 describes, with comments that name this spec and R1.

| Pin | What may change beyond the hash |
| --- | --- |
| smoke and economy_smoke `STATE_HASH` (`Harness/HarnessUnitTests.cs`, `Economy/WorkerConstructionTests.cs`), and the other `STATE_HASH` pins (`GatherSlotTests`, `P003CombatTests`, `FloodingTests`, `CommandScriptTests`) | hash only (these scripts train none of the four units; if any other assertion moves, stop and report) |
| `DeterminismTests.GoldenFinalHash` (TestWorld) | hash only; the golden run's event log is identical before and after |
| `V2ReferenceHash` (`ai_vs_ai.json`: `aggressive` Skai against `defensive` Water) | **the outcome and end tick may move** (their compositions name the Stormcaller or Ink Lobber); write the old and new `OUTCOME` lines in the re-pin comment and the report |
| `BlindTraceHash` (`water_vs_water`, `abl_s2_blind`) | **the end tick may move** (Ink Lobber in the Water mix); old and new values in the comment |

### R1.10 Unity (PC session; listed, not specified)

- Research buttons on the Storm Spire and Reef Workshop, one per `Definition.Economy.Researches` entry in list order, named from `Content.Researches[id].DisplayName`, issuing `train_unit(building, id)`.
- Button states: done (`Player.HasResearched`), in progress anywhere (`EconomyRules.IsResearchQueued`), unaffordable; a rejection shows its reason (`already_researched`, `already_queued`, `queue_full`, `insufficient_resources`).
- Progress bar on the building from `ProductionTicksRemaining` and `QueueEntryTicks`.
- Gated unit buttons: disabled while `FirstMissingRequirement` returns an ID; the tooltip names the building or research display name (T.5, extended to look in `Content.Researches`).
- A notice on the local player's own `ResearchCompletedEvent` only; never for other players (decision 6).
- Any Unity code that resolves production-queue entries through `Content.Units` must also accept research IDs.

### R1.11 Acceptance tests (`rts/tests/Sim.Tests/Economy/ResearchTests.cs`)

The test-local catalog adds:
- `test.plans_a`: research, faction `test`, cost 25 common and 50 rare, `research_seconds` 2 (40 ticks);
- `test.plans_b`: research, cost 25 and 0, 1 s (20 ticks);
- `test.lab`: building, footprint 1, radius 0.5, cost 50, build 5 s, max health 200, `researches ["test.plans_a", "test.plans_b"]`;
- `test.locked`: mobile ground unit, speed 4, supply 1, cost 50, build 2 s (40 ticks), `requires ["test.plans_a"]`;
- `test.lab_barracks`: building, footprint 1, `trains ["test.locked", "test.runner"]`, provides 8 supply;
- `test.mixed`: building, footprint 1, `trains ["test.runner"]`, `researches ["test.plans_b"]`, provides 8 supply.

Setup: economy setup. The lab is spawned complete for P0 at (16, 16) and the barracks at (12, 16). P0 is granted 100 rare, so it holds 200 common and 100 rare. T is the tick of the first command.

| # | Test | Setup and expected values |
| --- | --- | --- |
| R-1 | **Queue and complete** | On T, `train_unit(lab, test.plans_a)` is accepted: common **175**, rare **50**, `SupplyUsed` unchanged, `IsResearchQueued(P0, plans_a)` true, `QueueEntryTicks` **40**. `HasResearched` is false after every tick up to T + 38. On **T + 39** (the command tick counts as the first of 40), `ResearchCompletedEvent(T + 39, lab, P0, "test.plans_a")`, `HasResearched` true, the lab's queue empty, `IsResearchQueued` false, and the unit count unchanged (nothing spawned). |
| R-2 | **Unlock at order time** | `train_unit(barracks, test.locked)` on T is rejected `missing_prerequisite`, and so is the same order on T + 39 (step 2 runs before 7b). On **T + 40** it is accepted: common 175 - 50 = **125**. `FirstMissingRequirement(sim, P0, test.locked)` is `"test.plans_a"` before T + 39 and null after it. |
| R-3 | **Rejections** | Each in a fresh setup: (a) `train_unit(barracks, test.plans_a)` gives `cannot_train`. (b) `train_unit(lab, "test.nothing")` gives `unknown_definition`. (c) Two `train_unit(lab, test.plans_a)` on T with sequences s and s + 1: the first is accepted, the second gives `already_queued`, and only one payment is made (rare **50**). (d) After R-1 completes, the same order gives `already_researched`. (e) A lab spawned under construction gives `under_construction`. (f) Issuer P1 on P0's lab gives `not_owner`. (g) With rare 0: `plans_a` gives `insufficient_resources` and `plans_b` is accepted. (h) `test.mixed` holding 5 queued `test.runner` (`max_queue_length` 5; grant common as needed): `train_unit(mixed, test.plans_b)` gives `queue_full`. |
| R-4 | **Rejection order** | Rare 0 and `plans_a` already researched: `already_researched` (not `insufficient_resources`). `plans_a` queued and the queue full: `already_queued` (not `queue_full`). |
| R-5 | **Serial queue lengths** | On T, `plans_b` then `plans_a` at the lab (sequences s, s + 1): `plans_b` completes on **T + 19** and `plans_a` on **T + 59**. Mixed queue: at `test.mixed`, on T `plans_b` then `test.runner`: `plans_b` completes on T + 19, and the runner appears on T + 19 + Rb, where Rb is the runner's `BuildTicks`. This guards `QueueEntryTicks` in `StepProduction`. |
| R-6 | **Building lost mid-research** | `plans_a` queued on T. `remove_unit` of the lab on T + 10. No `ResearchCompletedEvent` through T + 100; `HasResearched` false; common **175**, rare **50** (no refund); `IsResearchQueued` false. A new lab spawned complete on T + 11 accepts `plans_a` on T + 12 (not `already_queued`) and is paid again (rare **0**). Combat variant: the lab killed by an enemy shooter; same result from the death tick. |
| R-7 | **Finished research kept** | After R-1, `remove_unit` of the lab: `HasResearched` stays true, and `train_unit(barracks, test.locked)` is accepted. |
| R-8 | **Own only** | A complete `test.lab` owned by P1 finishes `plans_a` for P1: P1's `HasResearched` true, P0's false, and P0's `test.locked` order is rejected `missing_prerequisite`. |
| R-9 | **Two completions on one tick** | Two labs for P0, L1 < L2 by ID. `plans_a` at L2 on T and `plans_b` at L1 on T + 20: both complete on **T + 39**; the events come in the order L1 (`plans_b`), then L2 (`plans_a`). |
| R-10 | **Hash** | Two identical runs of R-1 give equal hashes at every hash tick. R-1 against the same run with `plans_b` instead of `plans_a`: differ at the first hash tick at or after T. `Player.WriteHash` writes the count, then the IDs in ordinal order: players that finished `plans_b` then `plans_a`, and `plans_a` then `plans_b`, write equal bytes. |
| R-11 | **Queries** | `QueueEntryTicks` is 40 for `plans_a`, `BuildTicks` for `test.runner`, and 0 for an unknown ID. For a definition with `requires ["test.lab", "test.plans_a"]`, `FirstMissingRequirement` returns `"test.lab"` with no lab, `"test.plans_a"` with a complete lab, and null after the research. |
| R-12 | **Direct spawn** | `spawn_unit` of `test.locked` for P0 without the research: the unit exists. |
| R-13 | **Script path** | A `CommandScript` `train_unit` entry naming `test.plans_a` at the lab gives the result of R-1. Harness: a shipped-data script line `train_unit` for `skai.thunderhead_plans` at a script-spawned Storm Spire completes after **900** ticks. |
| R-14 | **Loader** | Each of these is rejected with the file, the field and the R1.2 message: a research file at the wrong depth; an unknown key; an `id` not matching the path; a `faction_id` not matching the folder; `cost_rare` 100001; `research_seconds` 0; a research ID equal to a unit ID; `researches` naming an unknown ID, a research of another faction, a duplicate, or 9 entries; `researches` on a mobile unit and on an HQ; `requires` naming a research no building of the faction lists; `requires` naming a research of another faction. A unit whose `requires` mixes a building and a research item loads, and the cycle check does not throw on research IDs. |
| R-15 | **Shipped data** | The four research files load with the R1.2 values. `storm_spire` and `reef_workshop` have the R1.2 `researches`. The four units have the R1.2 `requires`, and no other shipped unit names a research item. **Equal trees:** for each faction, the count of research items (2), the sum of `cost_common` (50), the sum of `cost_rare` (300) and the sum of `research_seconds` (75) are equal across `skai` and `water`. Update T-10 ("no other shipped unit has `requires`") to the four units. |
| R-16 | **Digest and coverage** | Two catalogs that differ only in one research `cost_rare`, or only in one `researches` entry, give different `ComputeDigest()`. `DisplayName` does not change the digest. `HashCoverageTests` covers `Player._researched`. |
| R-17 | **AI kept playing** (`Ai/AiPrerequisiteTests.cs`) | (a) Every shipped profile under `rts/data/ai` parses. (b) `ExitTest_AiVsAi_TwoRunsAreIdenticalAndDecided` is green with zero rejections. Over the run, no `train_unit` names a research ID or one of the four gated units. (c) TA-2 changes: with a complete Shell Forge, mix weight 700 on the Leviathan and `use_tech_on_demand` on, the think emits **no** `BuildStructure` of `water.reef_workshop` (T2 skips research-gated units in R1). (d) TA-3 changes: with a complete Reef Workshop but without `water.leviathan_plans`, there is no Leviathan `Train`. With the research finished in setup (queued at the workshop and stepped to completion), the Leviathan `Train` is emitted. |

---

## Package R2: the AI uses research (specified now, built later)

Built only after R1 is merged. Same tiers: Sonnet `builder`, Opus `sim-reviewer` on the `rts/src/Ai` diff. No `rts/src/Sim` change and no number change. **No pin moves**: AI profiles are not in the content digest, and every existing profile gets the new switch set to false.

### R2.1 Switch and profiles

New key in **every** profile file under `rts/data/ai` (the parser requires every key, as for `use_budget_queues`):
```
// Research rules RQ-1 and RQ-2 on: the AI queues research its army mix needs at its tech buildings, and, while the rare surplus is active (budget queues only), any research left. Off: research-gated units are never trained (research_and_unlocks.md R1.8). Unit: boolean. Range: true or false. Source: rts/docs/specs/research_and_unlocks.md R2.1; placeholder.
"use_research": false,
```
- Existing files: `false`. With it false, every code path is R1's, bit for bit (test RA-1).
- Candidate `rts/data/ai/aggressive_s5_r.json`: a copy of `aggressive_s5.json` with `id` changed and `use_research` true. Its off-copy for paired runs is `aggressive_s5` itself.

### R2.2 Rules (only when `use_research` is true)

- **View.** `AiWorldView.OwnResearched`: a copy of the AI player's `Player.Researched` (ordinal), taken when the view is built. It holds own information only and nothing about enemies.
- **Requirements.** `FirstUnmetRequirement`: a research entry is met when it is in `OwnResearched`. With the switch off, keep R1 behaviour: research entries are never met.
- **Rule T2.** With the switch on, R1's skip is removed. When a unit's first unmet requirement is a research ID, T2 moves on to the next unit (RQ-1 owns research). Example: the Thunderhead's first entry is the Storm Spire, so T2 still builds the Spire when it is missing.
- **Rule RQ-1 (research for the mix).** It runs in `AiBrain.Decide` immediately after `Tech()`, whatever the value of `use_tech_on_demand`, at most one research purchase per think.
  1. It takes the composition units (`_composition`) with weight above 0, in descending weight and then ordinal ID order. The first one that has a producer (`AnyOwnTrains`) and whose first unmet requirement is a research ID R is chosen; if none, stop.
  2. If any own unit's `Queue` contains R, stop: it is already in progress.
  3. The researcher is the lowest-ID own complete structure whose `Economy.Researches` contains R and whose queue is empty. If one exists, buy R there: emit `AiIntent.Train(researcher, R)`, paid from the `tech` queue. With budget queues, use the same can-pay, pays and money-short rules as any `tech` item (`ai_budget_queues.md` B.1.2); without budget queues, use the same affordability and reservation check as `TryBuild(..., AiBudget.Tech)`, applied to the research cost.
  4. If no researcher exists in any state (complete, under construction or a pending order in `_planned`), build one with `TryBuild(S, null, false, AiBudget.Tech)`. S is the ordinally lowest unit ID of the AI's faction whose `researches` contains R. `TryBuild` already refuses when S's own requirements are not met.
  5. Otherwise (a researcher exists but is busy or unfinished), stop.
- **Rule RQ-2 (rare surplus; only with `use_budget_queues`).** When RQ-1 bought nothing this think and `RareSurplusActive` is true: take the first research ID, in ordinal order, of the AI's faction that is neither in `OwnResearched` nor in any own queue and that has an idle, complete own researcher. Buy it as in RQ-1 step 3. This is P-007 section 4 item 3: it puts surplus rare into the tree when no mix unit asks for it.

### R2.3 Harness accounting (R2.0; build first within R2)

- **`PlayerLedger`**: a new eighth class, `research`. `SpendResearch(ResearchDefinition)` adds the cost to the totals and to that class. In `SummaryObserver.AfterStep` and `GameTrace`, an accepted `TrainUnitCommand` whose ID is in `Content.Researches` is charged through `SpendResearch` (today both call sites look in `Content.Units` only and would drop research spending). A record without `research` in `spent` reads it as [0, 0] (`spent` is already a map).
- **`GameSummary.Completed`** also counts `ResearchCompletedEvent` by research ID for the owner's slot. There is no new field.
- **`BalanceReport.UnitUse`**: each research ID of a faction is a row of kind `research`, used when `Completed` holds it at least once. The `min_use_share` bar (0.100) then flags research that is never used, as P-007 section 4 asks.

### R2.4 Acceptance tests (`rts/tests/Sim.Tests/Ai/AiResearchTests.cs`)

| # | Test | Assertion |
| --- | --- | --- |
| RA-1 | Switch off is R1 | `V2ReferenceHash` and `BlindTraceHash` are unchanged by R2. For a fixed view, the think intents of `aggressive_s5` and of a copy with `use_research` false are equal. |
| RA-2 | Research for the mix | Water AI with `use_research` true, a complete Shell Forge, an idle complete Reef Workshop, mix weight 700 on the Leviathan and enough money: the think emits `Train(workshop, water.leviathan_plans)`. On the next think, with it queued, there is no second research `Train`. |
| RA-3 | Builds the researcher | Water AI, composition with the Ink Lobber (weight above 0), a complete Shell Forge and no Reef Workshop: the think emits `BuildStructure` of `water.reef_workshop`. With one pending or under construction, it emits none. |
| RA-4 | Trains once researched | With `water.leviathan_plans` in `OwnResearched`, the Leviathan `Train` is emitted at the Shell Forge. |
| RA-5 | Surplus | With budget queues on, `RareSurplusActive` true, no mix unit needing research and an idle researcher: the think emits the ordinally first unresearched item. With `RareSurplusActive` false, it emits none. |
| RA-6 | Budget accounting | A research purchase lowers `A[tech]` by its cost and raises `S[tech]` by the same amount. When money is short, it records the `tech` want. |
| RA-7 | Exit test with research | A copy of `ai_vs_ai.json` with both profiles switched to `use_research` true runs twice with identical hashes, is decided, and has zero rejections. |
| RA-8 | Harness | In a game where `skai.thunderhead_plans` completes, the slot's `Completed` holds it and the ledger's `research` class holds 25 / 175. `BalanceReport` lists a `research` row for each of the four items. |
| RA-9 | Fog | `AiWorldView` for P0 exposes no research of P1 and no queue of an enemy structure. |

### R2.5 Measurement (stated before any run; P-006 paired method, P-007 section 6)

Candidate `aggressive_s5_r` against `aggressive_s5` on R2, paired. Every bar is a placeholder:
1. `CHECK balance_unspent` passes at 300 and 600 s for both factions (median rare at most 150).
2. Each research item reaches the use share (0.100) in its faction's games.
3. The median time of the first Thunderhead and of the first Leviathan moves later by no more than the item's research time plus 30 s, compared with the stored pre-R1 baseline (`balance_first`); this is the one place a pre-R1 baseline is read.
4. Skai rare spent per game does not fall below 1000.
5. Water's 900 s bank is reported, not judged.
6. One traced game per matchup shows no `missing_prerequisite`, `already_queued` or `already_researched` rejection from the AI.

If the candidate passes, the developer decides whether to switch `use_research` on in the other profiles, and then the full re-baseline runs (R1.8).

---

## Determinism notes for the sim-reviewer

- The new state is `Player._researched`: an ordinal `SortedSet`, written only in step 7b inside the ascending-ID economy loop, and hashed as count then IDs in ordinal order.
- Queued research lives in the already-hashed `ProductionQueue` and `ProductionTicksRemaining`.
- `IsResearchQueued`, `FirstMissingRequirement` and the research path of `TrainUnitCommand` are existence tests over `sim.Units.All`, so order cannot change their results.
- Same-tick duplicates are resolved by the (issuer, sequence) order of step 2.
- Research time is converted through `Clock.SecondsToTicks` on `Fix64`. There is no RNG and no new collection type.
- Check that `StepProduction` gives a research entry its own length when it becomes the head (`QueueEntryTicks`), and that `RecomputeSupply` still skips research IDs.
- Definitions enter the content digest (research section after factions; `Researches` after `IsGate`).
- R2's AI state is the view copy `OwnResearched` (own player only). `AiMemory` is unchanged.

## Build order and tiers

1. **R1**: Sonnet `builder` (code, data, the three `rts/src/Ai` changes of R1.8, tests, re-pins, full suite green with no warnings), then the Opus `sim-reviewer` (sim and the `rts/src/Ai` diff), then the Fable final review for simulation code. The PC session can start R1.10 once R1 is on `main`.
2. **R2** (later, after the developer's "make the AI work" step begins): R2.0 harness, then R2.1 and R2.2, by the Sonnet `builder`, then the Opus `sim-reviewer` on the AI diff. Measurement R2.5 follows.

Documentation edits in R1:
- the `Simulation.cs` and `EconomySystem.cs` headers (R1.6);
- the `TrainUnitCommand.Apply` summary;
- `Player` and `ContentCatalog` summaries;
- `Economy/README.md`;
- `rts/data/README.md`: the `research/` folder and the `researches` key, and `requires` may name research;
- `walls_and_tech.md` T.1 rule 5: a dated note "21 Nov 2026: tech buildings research (research_and_unlocks.md)".

## Rejected extras (one line each)

- A separate `start_research` command: the same fields as `train_unit`, plus a class, a codec and a script kind for no gain.
- `ResearchStartedEvent`: accepted train orders have no event; Unity reads the queue.
- `requires` on research items (research chains): no decision needs them; data can add them later with a cycle check over the mixed graph.
- Research as a unit definition with a flag: it would put non-units into every `Content.Units` loop (AI, harness, counter-mix).
- Granting research by script or at match start (for example to keep AI baselines): it is a cheat the player does not get, and it is new code.
- Refunds, cancel and pause: none exist for units; add them for both or neither.
- Repeatable or levelled research: decision 5.
- Showing enemy research: decision 6.

## Questions for the developer

The builder uses each default until you reply.

- **Q-R1. Which extra Skai item makes the trees equal?** *Default (placeholder): `skai.stormcaller_plans` gating the Stormcaller, the Ink Lobber's exact twin (same role, cost and build time).* Alternatives: (a) the Thunderhead needs two Skai items in a row, which keeps decision 3's unit list but makes Skai's top unit cost the whole tree (300 rare, 75 s) against the Leviathan's 175 rare and 45 s; (b) one Water item unlocks both the Ink Lobber and the Leviathan, so each tree has one item, but Water's mid unit then costs top-unit research.
- **Q-R2. Gating the Stormcaller delays the Downdraft**, which P-007's default left ungated. *Default: accepted, as part of Q-R1.* If not, choose alternative (a) or (b) of Q-R1.
- **Q-R3. Do the Thunderhead and Leviathan still need the tech building standing** once researched (package T)? *Default: yes, unchanged; the Stormcaller and Ink Lobber need only their research.* The alternative drops the building entry so that research alone gates all four units (two data lines).
- **Q-R4. Items queued behind the one in progress** when the building is lost. *Default: lost without refund, like queued units.* Decision 7 names only the item in progress.
- **Q-R5. Costs and times.** *Default: two tiers, 25 / 125 / 30 s and 25 / 175 / 45 s, the same for both factions (50 common and 300 rare per tree).* P-007 sized Skai's need at 85 to 135 rare by 600 s, so equal trees may take rare that Skai now spends on Roc Riders (R2.5 bar 4).
- **Q-R6. Measurement pause.** *Default: no ladder or new baseline on R1; the re-baseline waits for R2 (R1.8).* The AI fields none of the four gated units until R2 is on.
