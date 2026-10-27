// rts/src/Net/Codecs/BuiltInCodecs.cs
// Composition root for the command codecs shipped with the protocol: the core set plus the codecs of the built-in
// faction commands (currently the Water cast_wave). Part of Rts.Net.Codecs (ADR-008). This is the only file in the
// assembly that names a faction, mirroring rts/src/Sim/Factions/BuiltInMechanics.cs; a new faction command adds one
// line here. Hosts call CreateRegistry and pass the result to LockstepPeer.
namespace Rts.Net.Codecs
{
    /// <summary>Builds the registry every shipped host uses.</summary>
    public static class BuiltInCodecs
    {
        /// <summary>Adds the core codecs and the built-in faction codecs to <paramref name="registry"/>.</summary>
        /// <param name="registry">The registry to fill; must not already hold any of these kinds.</param>
        public static void RegisterAll(CommandCodecRegistry registry)
        {
            CoreCommandCodecs.RegisterAll(registry);
            registry.Register(new CastWaveCodec());
        }

        /// <summary>A new registry with everything built in registered.</summary>
        public static CommandCodecRegistry CreateRegistry()
        {
            // Registry that receives every built-in codec.
            CommandCodecRegistry registry = new CommandCodecRegistry();
            RegisterAll(registry);
            return registry;
        }
    }
}
