<!-- rts/docs/decisions/ADR-002-data-format.md: why game data lives in JSON with comments, parsed by our own reader inside the simulation assembly. -->
# ADR-002: Data files are JSON with comments, read by our own parser

Status: Accepted, 26 October 2026.

## Context

Rule 3 of the architecture: units, buildings, factions, maps, cycles and every tuning number live in data files. The documentation standard also demands that every tuning value carries a comment with its meaning, unit and design-doc source. The simulation assembly targets `netstandard2.1` with no engine references, so it cannot use Unity's `JsonUtility`, and `System.Text.Json` is not part of the `netstandard2.1` base library (it is a NuGet package Unity does not ship). The simulation also bans floating point, so numbers in data must reach `Fix64` without a `double` round trip.

Options considered:

1. **JSON with `//` comments (JSONC) and our own small reader.**
2. **Plain JSON with Newtonsoft.Json** (Unity ships `com.unity.nuget.newtonsoft-json`): mature, but it parses numbers as `double`/`decimal`, has no comments without a lenient mode, and adds a dependency to the engine-free assembly.
3. **YAML**: comments for free, but no dependency-free parser and a much larger grammar to keep deterministic.
4. **ScriptableObjects**: Unity-only, which breaks the headless rule.
5. **CSV / spreadsheets**: fine for flat unit tables, poor for nested data such as maps and abilities.

## Decision

- All data under `rts/data/` is JSON extended with `//` line comments and `/* */` block comments. Nothing else is extended: no trailing commas, no unquoted keys, no exponent notation in numbers.
- The reader lives in `rts/src/Sim/Data/` and is part of the simulation assembly, so it is engine-free and shared by Unity, the tests and the headless tools. It never touches the file system: hosts read the file text and pass it in with a source name used in error messages.
- Numbers are parsed from their decimal text straight into `Fix64` or `int`/`long`. `float` and `double` never appear.
- Every tuning value has a comment line immediately above it in the shape `// <what it controls>. Unit: <unit>. Range: <range>. Source: <design-doc section>.`
- Every data file has a `$schema_version` integer so later phases can migrate old files, and a `$doc` sentence saying what the file is for.
- Validation errors always name the file (source name) and the field path, for example `data/units/skai/windrunner.json: field 'movement.speed_tiles_per_second' must be a number greater than 0`.
- Durations are written in seconds in data and converted to ticks at load time (ADR-004). Fractions are written as fractions (`0.04`), never as percentages.

## Consequences

- Data files are readable and commentable in any text editor and diff cleanly in Git.
- The parser is ours to maintain; it is small (objects, arrays, strings, numbers, booleans, null, comments) and has unit tests.
- Unity's inspector cannot edit these files directly. That is acceptable: balance edits are text edits, and a Unity-side editor can be added later without changing the format.
- Replays and saves (Phase 2) get their own binary format and their own ADR; this record covers authored content only.
