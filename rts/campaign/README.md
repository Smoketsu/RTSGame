<!-- rts/campaign/README.md: campaign data (missions and the scripted inputs that play them headless). The scripting layer is rts/src/Campaign; the headless runner is rts/tools/MissionRunner; the format is specified in rts/docs/specs/campaign_scripting.md. -->
# Campaign data

Campaign missions are data, and they produce commands like any player (architecture rules 2, 3 and 8).

- `missions/<id>.json`: a mission (map, seed, players, setup commands, objectives, triggers). Read by `Rts.Campaign.MissionLoader`, played by `Rts.Campaign.MissionRunner`. The file name without `.json` must equal the mission `id`.
- `inputs/<id>.json`: the scripted player that plays a mission headless (camera and selection signals and human commands, each due some seconds after its objective becomes active). Read by `Rts.Campaign.MissionInputs`.
- Game data (maps, units, factions, tuning, AI profiles) stays in `rts/data`; a mission only names IDs from it.

Play a mission headless:

```
dotnet run --project rts/tools/MissionRunner -- --data rts/data --campaign rts/campaign --mission tutorial_01 [--inputs <file>] [--ticks N]
```

Missions so far: `tutorial_01` (First Flight: camera, selection, movement, gathering, building). Phase 3 adds prologues and Phase 5 the paid chapters.
