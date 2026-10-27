// rts/tools/BalanceHarness/RecordingController.cs
// Decorator that lets the harness record the commands a skirmish AI controller submits. Part of Rts.Tools.BalanceHarness.
// AiHost.ThinkAll hands every controller a sink that submits straight to the simulation; this wrapper passes the inner controller
// a sink that also reports each command to the ReplayRecorder, so AI commands end up in the replay beside the script's own.
// The inner controller sees no difference (same ScheduleTick, same Submit), so a recorded run plays exactly like an unrecorded one.
using Rts.Ai;
using Rts.Replay;
using Rts.Sim;
using Rts.Sim.Commands;
using Rts.Sim.Core;

namespace Rts.Tools.BalanceHarness
{
    /// <summary>Wraps a controller so everything it submits is also recorded.</summary>
    public sealed class RecordingController : IPlayerController
    {
        /// <summary>The controller that does the thinking.</summary>
        private readonly IPlayerController _inner;

        /// <summary>The recorder that receives each submitted command.</summary>
        private readonly ReplayRecorder _recorder;

        /// <summary>Wraps <paramref name="inner"/>.</summary>
        /// <param name="inner">The controller to wrap. Range: not null.</param>
        /// <param name="recorder">The recorder attached to the same simulation. Range: not null.</param>
        public RecordingController(IPlayerController inner, ReplayRecorder recorder)
        {
            _inner = inner;
            _recorder = recorder;
        }

        /// <summary>The player slot the wrapped controller plays.</summary>
        public PlayerId Player => _inner.Player;

        /// <summary>Forwards to the wrapped controller.</summary>
        /// <param name="sim">The started simulation (read only).</param>
        public void OnMatchStart(Simulation sim)
        {
            _inner.OnMatchStart(sim);
        }

        /// <summary>Lets the wrapped controller think, with a sink that records what it submits.</summary>
        /// <param name="sim">The simulation between steps (read only).</param>
        /// <param name="sink">The host's sink, which still receives every command.</param>
        public void Think(Simulation sim, ICommandSink sink)
        {
            _inner.Think(sim, new RecordingSink(sink, _recorder));
        }

        /// <summary>A sink that forwards to another sink and then records the command.</summary>
        private sealed class RecordingSink : ICommandSink
        {
            /// <summary>The host's sink that submits to the simulation.</summary>
            private readonly ICommandSink _forward;

            /// <summary>The recorder to tell about each command.</summary>
            private readonly ReplayRecorder _recorder;

            /// <summary>Creates the sink.</summary>
            /// <param name="forward">Sink that actually submits. Range: not null.</param>
            /// <param name="recorder">Recorder to notify. Range: not null.</param>
            public RecordingSink(ICommandSink forward, ReplayRecorder recorder)
            {
                _forward = forward;
                _recorder = recorder;
            }

            /// <summary>Tick the controller must schedule for, as reported by the host's sink. Unit: ticks.</summary>
            public int ScheduleTick => _forward.ScheduleTick;

            /// <summary>Submits the command through the host's sink, then records it.</summary>
            /// <param name="command">The command; not null.</param>
            public void Submit(Command command)
            {
                _forward.Submit(command);
                _recorder.Record(command);
            }
        }
    }
}
