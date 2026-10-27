// rts/src/Ai/AssemblyInfo.cs
// Assembly-level attributes for Rts.Ai. Part of Rts.Ai (root). Kept in source rather than the .csproj so the same
// declaration applies inside Unity, in the .NET SDK build and in the bootstrap compiler used without MSBuild.
using System.Runtime.CompilerServices;

// The NUnit test assembly may call internal members of the AI (the brain's rule helpers) to test them in isolation.
[assembly: InternalsVisibleTo("Rts.Sim.Tests")]
