// rts/src/Sim/AssemblyInfo.cs
// Assembly-level attributes for Rts.Sim. Part of Rts.Sim (root). Kept in source rather than the .csproj so the
// same declaration applies inside Unity, in the .NET SDK build and in the bootstrap compiler used without MSBuild.
using System.Runtime.CompilerServices;

// The NUnit test assembly may call internal members (Clock.Advance, CommandQueue.Enqueue, EntityIdAllocator.Allocate
// and the like) to test them in isolation; no other assembly can, so hosts cannot mutate state outside a step.
[assembly: InternalsVisibleTo("Rts.Sim.Tests")]
