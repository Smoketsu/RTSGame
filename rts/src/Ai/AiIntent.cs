// rts/src/Ai/AiIntent.cs
// What the brain decided, before it becomes a command, and the writer that turns intents into commands (spec section 6:
// AiBrain -> intents -> AiCommandWriter). Part of Rts.Ai. The writer stamps each command with the sink's schedule tick, the AI's
// player as issuer and the next sequence number from AiMemory, in intent order, so (issuer, sequence) is unique for the match.
using System;
using System.Collections.Generic;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Ai
{
    /// <summary>Kinds of intent, one per command type the AI emits.</summary>
    public enum AiIntentKind : byte
    {
        /// <summary>GatherCommand(unit, field).</summary>
        Gather = 0,

        /// <summary>TrainUnitCommand(unit = producer, definition).</summary>
        Train = 1,

        /// <summary>BuildStructureCommand(unit = worker, definition, tile x, tile y).</summary>
        Build = 2,

        /// <summary>AttackMoveCommand(unit, point).</summary>
        AttackMove = 3,

        /// <summary>MoveUnitCommand(unit, point).</summary>
        Move = 4,

        /// <summary>RootHqCommand(unit).</summary>
        RootHq = 5,

        /// <summary>AttackUnitCommand(unit, target): only ever aimed at an enemy the team sees on this think.</summary>
        AttackUnit = 6,
    }

    /// <summary>One decided action. Immutable.</summary>
    public sealed class AiIntent
    {
        /// <summary>Which command this becomes.</summary>
        public AiIntentKind Kind { get; }

        /// <summary>The ordered unit: worker, producer, army unit or HQ.</summary>
        public EntityId Unit { get; }

        /// <summary>Field index for Gather, else -1. Range: -1 to field count - 1.</summary>
        public int Field { get; }

        /// <summary>Definition ID for Train and Build, else empty.</summary>
        public string DefinitionId { get; }

        /// <summary>Site tile column for Build. Unit: tiles.</summary>
        public int TileX { get; }

        /// <summary>Site tile row for Build. Unit: tiles.</summary>
        public int TileY { get; }

        /// <summary>Destination for AttackMove and Move. Unit: tiles.</summary>
        public FixVec2 Point { get; }

        /// <summary>Enemy unit for AttackUnit (visible to the team when decided), else None.</summary>
        public EntityId Target { get; }

        /// <summary>Stores the intent; use the static helpers.</summary>
        private AiIntent(AiIntentKind kind, EntityId unit, int field, string definitionId, int tileX, int tileY, FixVec2 point)
            : this(kind, unit, field, definitionId, tileX, tileY, point, EntityId.None)
        {
        }

        /// <summary>Stores the intent with a target; use the static helpers.</summary>
        private AiIntent(AiIntentKind kind, EntityId unit, int field, string definitionId, int tileX, int tileY, FixVec2 point, EntityId target)
        {
            Target = target;
            Kind = kind;
            Unit = unit;
            Field = field;
            DefinitionId = definitionId;
            TileX = tileX;
            TileY = tileY;
            Point = point;
        }

        /// <summary>Send a worker to a field.</summary>
        public static AiIntent Gather(EntityId worker, int field) => new AiIntent(AiIntentKind.Gather, worker, field, string.Empty, 0, 0, FixVec2.Zero);

        /// <summary>Queue a unit at a producer.</summary>
        public static AiIntent Train(EntityId producer, string definitionId) => new AiIntent(AiIntentKind.Train, producer, -1, definitionId, 0, 0, FixVec2.Zero);

        /// <summary>Place a structure on a tile.</summary>
        public static AiIntent Build(EntityId worker, string definitionId, int x, int y) => new AiIntent(AiIntentKind.Build, worker, -1, definitionId, x, y, FixVec2.Zero);

        /// <summary>Attack-move a unit to a point.</summary>
        public static AiIntent AttackMove(EntityId unit, FixVec2 point) => new AiIntent(AiIntentKind.AttackMove, unit, -1, string.Empty, 0, 0, point);

        /// <summary>Plain move (no stopping to fight) to a point.</summary>
        public static AiIntent Move(EntityId unit, FixVec2 point) => new AiIntent(AiIntentKind.Move, unit, -1, string.Empty, 0, 0, point);

        /// <summary>Order a unit to attack one visible enemy unit.</summary>
        public static AiIntent AttackUnit(EntityId unit, EntityId target) => new AiIntent(AiIntentKind.AttackUnit, unit, -1, string.Empty, 0, 0, FixVec2.Zero, target);

        /// <summary>Root an Uprooted HQ.</summary>
        public static AiIntent RootHq(EntityId hq) => new AiIntent(AiIntentKind.RootHq, hq, -1, string.Empty, 0, 0, FixVec2.Zero);

        /// <summary>Builds the command for this intent.</summary>
        /// <param name="tick">Tick to schedule. Unit: ticks.</param>
        /// <param name="issuer">The AI's player.</param>
        /// <param name="sequence">The issuer's next sequence number.</param>
        public Command ToCommand(int tick, PlayerId issuer, uint sequence)
        {
            switch (Kind)
            {
                case AiIntentKind.Gather:
                    return new GatherCommand(tick, issuer, sequence, Unit, Field);
                case AiIntentKind.Train:
                    return new TrainUnitCommand(tick, issuer, sequence, Unit, DefinitionId);
                case AiIntentKind.Build:
                    return new BuildStructureCommand(tick, issuer, sequence, Unit, DefinitionId, TileX, TileY);
                case AiIntentKind.AttackMove:
                    return new AttackMoveCommand(tick, issuer, sequence, Unit, Point);
                case AiIntentKind.Move:
                    return new MoveUnitCommand(tick, issuer, sequence, Unit, Point);
                case AiIntentKind.RootHq:
                    return new RootHqCommand(tick, issuer, sequence, Unit);
                case AiIntentKind.AttackUnit:
                    return new AttackUnitCommand(tick, issuer, sequence, Unit, Target);
                default:
                    throw new InvalidOperationException("Unknown intent kind " + Kind + ".");
            }
        }
    }

    /// <summary>Turns intents into commands for a sink.</summary>
    public static class AiCommandWriter
    {
        /// <summary>
        /// Submits one command per intent, in order, to <paramref name="sink"/>, scheduled at the sink's tick with issuer
        /// <paramref name="player"/> and sequence numbers taken from <paramref name="memory"/>. Returns the commands submitted.
        /// </summary>
        /// <param name="intents">Intents in decision order (already capped by the brain at max_commands_per_think).</param>
        /// <param name="player">The AI's player.</param>
        /// <param name="sink">Where the commands go.</param>
        /// <param name="memory">Holds the sequence counter.</param>
        public static List<Command> Write(IReadOnlyList<AiIntent> intents, PlayerId player, ICommandSink sink, AiMemory memory)
        {
            // Commands built, in submission order.
            List<Command> written = new List<Command>(intents.Count);
            foreach (AiIntent intent in intents)
            {
                // The command for this intent, with the next sequence number.
                Command command = intent.ToCommand(sink.ScheduleTick, player, memory.NextSequence);
                memory.NextSequence = unchecked(memory.NextSequence + 1);
                sink.Submit(command);
                written.Add(command);
            }
            return written;
        }
    }
}
