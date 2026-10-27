// rts/src/Net/AssemblyInfo.cs
// Assembly-level attributes for Rts.Net. Part of Rts.Net (root). Kept in source rather than the .csproj so the same
// declaration applies inside Unity, in the .NET SDK build and in the bootstrap compiler used without MSBuild.
using System.Runtime.CompilerServices;

// The NUnit test assembly may call internal members of the protocol (bundle bookkeeping, the loopback hub's queues) to test them in isolation.
[assembly: InternalsVisibleTo("Rts.Net.Tests")]
