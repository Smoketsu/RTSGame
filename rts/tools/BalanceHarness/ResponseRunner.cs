// rts/tools/BalanceHarness/ResponseRunner.cs
// Response mode of the balance harness (spec: rts/docs/specs/skirmish_ai_v3.md A.4): plays each scripted-opponent scenario of ai_eval.json (a command script in
// rts/data/scripts/ai_eval whose slot 1 is a responder profile) with the AI under test in slot 0, records what happened through a read-only observer into a ResponseSeries
// and hands the series to ResponseChecks. The observer never submits a command and never steps, so the final state hash of an observed run equals the unobserved run's.
// CLI: --response --profile <id> [--only <script file name>]; prints one RESPONSE line per scenario and RESPONSE_SUMMARY passed=<n> of=<m>; exit 0 all passed, 1 error, 2 any failed.
// Part of Rts.Tools.BalanceHarness. The AI under test replaces the "ai" value of slot 0 in the script; every other slot keeps its script profile.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Ai;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;
using Rts.Sim.Data;
using Rts.Sim.Economy;
using Rts.Sim.Events;
using Rts.Sim.Factions;
using Rts.Sim.Teams;
using Rts.Sim.Units;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>The outcome of one scenario run.</summary>
    public sealed class ResponseResult
    {
        /// <summary>The scenario entry that was run.</summary>
        public ResponseTest Test { get; }

        /// <summary>Numbered parts of the check; empty for a run made without the observer.</summary>
        public IReadOnlyList<CheckPart> Parts { get; }

        /// <summary>What the observer recorded; null for a run made without the observer.</summary>
        public ResponseSeries? Series { get; }

        /// <summary>State hash after the last tick. Range: any ulong.</summary>
        public ulong FinalHash { get; }

        /// <summary>Digest of the loaded content (Simulation.ContentDigest); 0 when not recorded. Range: any ulong.</summary>
        public ulong ContentDigest { get; }

        /// <summary>Profile ID the controller of slot 0 used.</summary>
        public string Slot0Profile { get; }

        /// <summary>Profile ID the controller of slot 1 used.</summary>
        public string Slot1Profile { get; }

        /// <summary>True when every part passed (false for an unobserved run).</summary>
        public bool Passed
        {
            get
            {
                if (Parts.Count == 0)
                {
                    return false;
                }
                foreach (CheckPart part in Parts)
                {
                    if (!part.Passed)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>Bundles the values.</summary>
        public ResponseResult(ResponseTest test, IReadOnlyList<CheckPart> parts, ResponseSeries? series, ulong finalHash, string slot0Profile, string slot1Profile, ulong contentDigest = 0UL)
        {
            ContentDigest = contentDigest;
            Test = test;
            Parts = parts;
            Series = series;
            FinalHash = finalHash;
            Slot0Profile = slot0Profile;
            Slot1Profile = slot1Profile;
        }

        /// <summary>The report line: "RESPONSE &lt;script&gt; &lt;check&gt; PASS|FAIL" then, per part, "p&lt;n&gt;=PASS|FAIL" followed by its measured values.</summary>
        public string Line()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("RESPONSE ").Append(Test.Script).Append(' ').Append(Test.Check).Append(Passed ? " PASS" : " FAIL");
            foreach (CheckPart part in Parts)
            {
                sb.Append(" p").Append(part.Number.ToString(CultureInfo.InvariantCulture)).Append(part.Passed ? "=PASS " : "=FAIL ").Append(part.Metric);
            }
            return sb.ToString();
        }
    }

    /// <summary>Runs response scenarios.</summary>
    public static class ResponseRunner
    {
        /// <summary>Folder of the scenario scripts inside the data folder. Range: fixed path.</summary>
        public const string ScriptFolder = "scripts/ai_eval";

        /// <summary>Slot of the AI under test. Range: 0.</summary>
        private const int UnderTestSlot = 0;

        /// <summary>CLI entry: runs every scenario (or --only) with the profile and prints the report lines and the summary.</summary>
        /// <param name="options">Parsed options (--response, --profile, --only).</param>
        /// <param name="data">The loaded data set.</param>
        /// <param name="output">stdout: RESPONSE lines and RESPONSE_SUMMARY.</param>
        /// <param name="error">stderr: unused except for errors thrown to the caller.</param>
        public static int RunCli(BatchOptions options, DataSet data, TextWriter output, TextWriter error)
        {
            AiEvalSettings settings = AiEvalSettings.Load(options.DataDirectory);
            // Fails early (exit 1) when the profile is missing.
            AiProfile.FromDataSet(data, options.Profile);
            int run = 0;
            int passed = 0;
            foreach (ResponseTest test in settings.ResponseTests)
            {
                if (options.Only.Length > 0 && test.Script != options.Only)
                {
                    continue;
                }
                ResponseResult result = Run(data, options.DataDirectory, test, options.Profile, true);
                output.Write(result.Line() + "\n");
                output.Flush();
                run++;
                passed += result.Passed ? 1 : 0;
            }
            if (run == 0)
            {
                throw new HarnessException("no response test matches --only '" + options.Only + "'");
            }
            output.Write("RESPONSE_SUMMARY passed=" + passed.ToString(CultureInfo.InvariantCulture) + " of=" + run.ToString(CultureInfo.InvariantCulture) + "\n");
            output.Flush();
            return passed == run ? 0 : 2;
        }

        /// <summary>Plays one scenario.</summary>
        /// <param name="data">The loaded data set.</param>
        /// <param name="dataDirectory">The data folder holding scripts/ai_eval.</param>
        /// <param name="test">The scenario entry.</param>
        /// <param name="profile">Profile ID of the AI under test (slot 0).</param>
        /// <param name="observe">True to record a series and evaluate the check; false for a plain run (the observer-free reference for the hash comparison).</param>
        /// <param name="matchSeed">Match seed replacing the script's "seed", or null for the script's own (the single-game behaviour). Range: any ulong, or null.</param>
        public static ResponseResult Run(DataSet data, string dataDirectory, ResponseTest test, string profile, bool observe, ulong? matchSeed = null)
        {
            // Script file path and parsed script.
            string path = Path.Combine(dataDirectory, ScriptFolder, test.Script);
            if (!File.Exists(path))
            {
                throw new HarnessException("response script not found: " + path);
            }
            CommandScript script = CommandScript.Parse(File.ReadAllText(path), ScriptFolder + "/" + test.Script, BuiltInMechanics.ScriptParsers);
            MapDefinition map = data.RequireMap(script.MapId);
            Simulation sim = new Simulation(new SimulationConfig(data.CreateMatchSettings(matchSeed ?? script.Seed), data.Content, script.Players, BuiltInMechanics.CreateRegistry(),
                data.Tuning, map, data.Cycles.Values));
            sim.Start();
            foreach (Command command in script.Commands)
            {
                sim.Submit(command);
            }
            // Controllers in ascending slot order: the script's profiles, with slot 0 replaced by the profile under test.
            SortedDictionary<int, string> slots = new SortedDictionary<int, string>();
            foreach (KeyValuePair<PlayerId, string> pair in script.AiProfiles)
            {
                slots[pair.Key.Value] = pair.Value;
            }
            slots[UnderTestSlot] = profile;
            List<IPlayerController> controllers = new List<IPlayerController>();
            foreach (KeyValuePair<int, string> pair in slots)
            {
                controllers.Add(new SkirmishAiController(new PlayerId((byte)pair.Key), AiProfile.FromDataSet(data, pair.Value)));
            }
            AiHost.StartAll(sim, controllers);
            // Faction of the AI under test and its profile object.
            sim.Players.TryGet(new PlayerId(UnderTestSlot), out Player? self);
            ResponseObserver? observer = observe ? new ResponseObserver(sim, script) : null;
            for (int i = 0; i < script.Ticks; i++)
            {
                AiHost.ThinkAll(sim, controllers);
                sim.Step();
                observer?.AfterStep(sim);
                if (MatchOutcome.Evaluate(sim.Players).IsDecided)
                {
                    break;
                }
            }
            ulong hash = sim.ComputeStateHash();
            string slot1 = slots.ContainsKey(1) ? slots[1] : string.Empty;
            if (observer == null)
            {
                return new ResponseResult(test, new List<CheckPart>(), null, hash, profile, slot1, sim.ContentDigest);
            }
            ResponseSeries series = observer.Finish(sim);
            foreach (IPlayerController controller in controllers)
            {
                if (controller is SkirmishAiController ai && ai.Player.Value == UnderTestSlot)
                {
                    // First tick the AI under test's enemy model reported air supply (skirmish_ai_v3.md A.4 / B.3); -1 while it has no model.
                    series.ModelAirKnownTick = ai.Memory.ModelAirKnownTick;
                }
            }
            List<CheckPart> parts = ResponseChecks.Evaluate(test, series, data, AiProfile.FromDataSet(data, profile), self!.FactionId);
            return new ResponseResult(test, parts, series, hash, profile, slot1, sim.ContentDigest);
        }
    }

    /// <summary>
    /// Read-only recorder of one scenario run. AfterStep is called after every Simulation.Step; it reads events and state and writes only its own series.
    /// </summary>
    internal sealed class ResponseObserver
    {
        /// <summary>The series being filled.</summary>
        private readonly ResponseSeries _series = new ResponseSeries();

        /// <summary>Team of the AI under test.</summary>
        private readonly TeamId _team;

        /// <summary>Definition and owner of every unit spawned so far by entity ID (units are never forgotten, so deaths can be classified). Bounded by the entities of one match.</summary>
        private readonly SortedDictionary<EntityId, KeyValuePair<UnitDefinition, PlayerId>> _spawned = new SortedDictionary<EntityId, KeyValuePair<UnitDefinition, PlayerId>>();

        /// <summary>Entity IDs of enemy army units already counted in the seen-armour totals.</summary>
        private readonly SortedSet<EntityId> _seen = new SortedSet<EntityId>();

        /// <summary>Entity ID of the AI's HQ, or None before the first step.</summary>
        private EntityId _hq = EntityId.None;

        /// <summary>Tick rate. Unit: ticks per second. Range: 1 to 1000.</summary>
        private readonly int _tps;

        /// <summary>Creates the observer for the simulation and records the script's wave spawn point.</summary>
        public ResponseObserver(Simulation sim, CommandScript script)
        {
            _team = sim.Players.TeamOf(new PlayerId(0));
            _tps = sim.Clock.TicksPerSecond;
            _series.TicksPerSecond = _tps;
            foreach (Command command in script.Commands)
            {
                if (command is SpawnUnitCommand spawn && spawn.Tick > 0 && spawn.Owner.Value != 0)
                {
                    _series.WaveSpawnPoint = new TilePoint(HarnessStats.ToDouble(spawn.Position.X), HarnessStats.ToDouble(spawn.Position.Y));
                    break;
                }
            }
        }

        /// <summary>Records one stepped tick: spawns, deaths, what the AI's team sees, and a sample at every game second.</summary>
        public void AfterStep(Simulation sim)
        {
            // Tick that was just stepped. Unit: ticks.
            int tick = sim.Clock.Tick - 1;
            foreach (SimEvent simEvent in sim.Events.Current)
            {
                if (simEvent is UnitSpawnedEvent spawned && sim.Units.TryGet(spawned.Unit, out Unit? unit))
                {
                    _spawned[spawned.Unit] = new KeyValuePair<UnitDefinition, PlayerId>(unit.Definition, spawned.Owner);
                    if (spawned.Owner.Value == 0)
                    {
                        if (_hq == EntityId.None && unit.Definition.Hq != null)
                        {
                            _hq = unit.Id;
                        }
                        if (IsArmy(unit.Definition))
                        {
                            _series.Trained.Add(new TimedAmount(spawned.Tick, unit.Definition.Id, unit.Definition.Supply));
                        }
                    }
                }
                else if (simEvent is UnitKilledEvent killed && _spawned.TryGetValue(killed.Victim, out KeyValuePair<UnitDefinition, PlayerId> victim))
                {
                    RecordDeath(killed.Tick, victim.Key, killed.VictimOwner);
                }
                else if (simEvent is UnitRemovedEvent removed && removed.Unit == _hq && _series.HqDeathTick < 0)
                {
                    _series.HqDeathTick = removed.Tick;
                }
            }
            // Enemy units the AI's team can see now (the observer may read full state; this only records what the team could see).
            foreach (Unit unit in sim.Units.All)
            {
                if (!unit.Owner.IsValid || sim.Players.TeamOf(unit.Owner) == _team || !IsArmy(unit.Definition) || !sim.Vision.IsUnitVisibleTo(_team, unit))
                {
                    continue;
                }
                if (_series.FirstEnemyArmyVisibleTick < 0)
                {
                    _series.FirstEnemyArmyVisibleTick = tick;
                }
                if (unit.EffectiveLayer == MovementLayer.Air && _series.FirstEnemyAirVisibleTick < 0)
                {
                    _series.FirstEnemyAirVisibleTick = tick;
                }
                if (_seen.Add(unit.Id))
                {
                    _series.SeenEnemyDefinitions.Add(unit.Definition.Id);
                    _series.SeenArmourSupply.TryGetValue(unit.Definition.ArmourType, out int before);
                    _series.SeenArmourSupply[unit.Definition.ArmourType] = before + unit.Definition.Supply;
                }
            }
            if (sim.Clock.Tick % _tps == 0)
            {
                _series.Samples.Add(Sample(sim, sim.Clock.Tick));
            }
        }

        /// <summary>Closes the series: the last tick, the outcome and a final sample.</summary>
        public ResponseSeries Finish(Simulation sim)
        {
            _series.EndTick = Math.Max(0, sim.Clock.Tick - 1);
            MatchOutcome outcome = MatchOutcome.Evaluate(sim.Players);
            _series.AiWon = outcome.IsDecided && !outcome.IsDraw && outcome.Winner == _team;
            if (_series.Samples.Count == 0 || _series.Samples[_series.Samples.Count - 1].Tick != sim.Clock.Tick)
            {
                _series.Samples.Add(Sample(sim, sim.Clock.Tick));
            }
            return _series;
        }

        /// <summary>Classifies a death into the series.</summary>
        private void RecordDeath(int tick, UnitDefinition definition, PlayerId owner)
        {
            // True when the dead unit belonged to the AI under test.
            bool own = owner.IsValid && owner.Value == 0;
            if (IsArmy(definition))
            {
                (own ? _series.OwnArmyLost : _series.EnemyArmyKilled).Add(new TimedAmount(tick, definition.Id, definition.Supply));
            }
            else if (own && definition.Economy.CanGather)
            {
                _series.WorkerLossTicks.Add(tick);
            }
            else if (!own && (definition.Economy.FootprintTiles > 0 || definition.Hq != null) && _series.FirstEnemyStructureKillTick < 0)
            {
                _series.FirstEnemyStructureKillTick = tick;
            }
        }

        /// <summary>True for an army unit: mobile (footprint 0), not a worker, not an HQ (the rule shared with the fight mode).</summary>
        private static bool IsArmy(UnitDefinition definition)
        {
            return FightSettings.IsArmy(definition);
        }

        /// <summary>Samples the AI's air-capable units, static defences and drop-offs.</summary>
        private ResponseSample Sample(Simulation sim, int tick)
        {
            int aa = 0;
            List<TilePoint> defences = new List<TilePoint>();
            List<TilePoint> dropOffs = new List<TilePoint>();
            foreach (Unit unit in sim.Units.All)
            {
                if (unit.Owner.Value != 0)
                {
                    continue;
                }
                // Position of the unit. Unit: tiles.
                TilePoint at = new TilePoint(HarnessStats.ToDouble(unit.Position.X), HarnessStats.ToDouble(unit.Position.Y));
                if (unit.Definition.Economy.IsStaticDefence)
                {
                    defences.Add(at);
                }
                if (!unit.IsUnderConstruction && EconomyRules.IsDropOff(unit))
                {
                    dropOffs.Add(at);
                }
                if (!unit.IsUnderConstruction && !unit.Definition.Economy.CanGather && unit.Definition.Hq == null && unit.Definition.Weapon != null && unit.Definition.Weapon.CanTargetAir)
                {
                    aa++;
                }
            }
            return new ResponseSample(tick, aa, defences, dropOffs);
        }
    }
}
