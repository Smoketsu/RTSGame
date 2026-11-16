<!-- rts/docs/art/CONCEPT_BRIEF.md: art direction for concept art rounds; the concept-artist subagent checks every image against it. Source: Game Design Proposal (26 Oct 2026), sections Factions, Maps, and Art and audio. -->
# Concept art brief

Twelve rounds of concept art on 16 Nov 2026 settled the art direction. The table below is the approved set: later art, 3D models and the Unity presentation layer are checked against these files. The sections after it record how each decision was reached.

## Approved designs (16 Nov 2026)

| Subject | Approved file (under `concept/`) | Decided |
| --- | --- | --- |
| Rendering style: matte low-poly | `round8/water_lineup_style_c_lowpoly.jpg` | Round 8 |
| In-game look and HUD: army clash | `round12/ingame_engagement_choke.jpg` | Round 12 |
| In-game look and HUD: base raid | `round12/ingame_raid_expansion.jpg` | Round 12 |
| Whole-match view | `round11/battle_take_c_wide_map.jpg` | Round 11 |
| Skai ground units: pelican worker, secretary-bird scout, heron trooper, owl storm-caller | `round9/skai_ground_lineup.jpg` | Round 9 |
| Skai air units: Roc Rider, Thunderhead (top view) | `round9/skai_air_units.jpg` | Round 9 |
| Thunderhead front view with missiles (earlier style) | `round7/skai_thunderhead.jpg` | Round 7 |
| Water units: octopus worker, tracked giant clam, mantis shrimp, cuttlefish, sea snail | `round8/water_lineup_style_c_lowpoly.jpg` | Round 8 |
| Sea snail artillery: travelling and firing poses | `round12/water_snail_poses.jpg` | Round 12 |
| Earth units: sentinel, bruiser, titan | `round10/earth_lineup.jpg` | Round 10 |
| Map layout, 1v1: dry and grassy before any faction changes it; floods come from the water faction | `round20/map_full_1v1.jpg` | Round 20 |
| Water Tidecaller and Leviathan | `round20/water_tidecaller_leviathan.jpg` | Round 20 |
| Titan, earth HQ, Uprooted and Rooted (meditating, base risen around him) | `round20/earth_titan_hq.jpg` | Round 20 |
| Earth buildings (seven) | `round20/earth_buildings.jpg` | Round 20 |
| Great Aerie, Skai HQ: hovering from below, hovering from the game camera, and landed. The propellers are barely visible from the game camera; revisit once it is seen in motion | `round17/skai_great_aerie_hq.jpg` | Round 17 |
| Skai buildings (seven, conifer and canopy styles) | `round16/skai_buildings.jpg` | Round 16 |
| Water buildings (seven, coral reef) | `round16/water_buildings.jpg` | Round 16 |
| Grand Shell, water HQ, Uprooted and Rooted | `round13/water_grand_shell_hq.jpg` | Round 13 |

From concept to 3D (trialled 16 Nov 2026, see `models/trial2/README.md`): each unit is redrawn as a front, side and back sheet in a neutral standing pose, then generated with Tripo3D's multi-view endpoint at about 5,000 triangles. A single side-on drawing makes the generator invent the far side, which is where the first trial's faults came from. The scout and clam models from trial 2 are approved.

Design-doc names for the approved units: pelican worker is the Fledgling, secretary-bird scout the Windrunner, heron trooper the Talon Marksman (was Talon Archer), owl storm-caller the Stormcaller; octopus worker is the Tender, giant clam the Shellback, mantis shrimp the Pummeler (was Grasper), cuttlefish the Spoutling, sea snail the Brinecaster (was Ink Lobber; fires water-energy, not ink).

Not yet drawn: the fire faction. The rest of the earth roster is in review (round 21).

## Style

Direction set by the developer on 16 Nov 2026 after round 1: **science fiction, not low-tech fantasy.** Each species is an advanced civilisation whose technology grew out of its element.

- **Rendering style (decided 16 Nov 2026, round 8):** simple low-polygon 3D game models: chunky faceted geometry with visible flat polygons, plain hand-painted colours in broad areas, fully matte shading, no specular highlights, no reflections, no glow bloom, no fine surface detail. The reference image is `concept/round8/water_lineup_style_c_lowpoly.jpg`. Not photoreal, not glossy, not pixel art, not anime.
- Every unit and structure shows technology: armour plating, engines, emitters, energy weapons, glowing power lines. No bows, spears, baskets, cloth robes, wood, rope or stone masonry.
- Readability first: a unit must be identifiable from its outline alone at the fixed three-quarter top-down camera.
- Each faction is recognised by its own colours (table below), not by a red or blue team colour; red is reserved for the fire faction. How players on the same faction are told apart in a mirror match is still open.
- Soft daylight, neutral backdrop on line-up sheets, no text, no logos, no borders, no watermarks.

## Faction palettes and motifs

| Faction | Technology | Palette | Motifs | Silhouette |
| --- | --- | --- | --- | --- |
| Skai Riders | Lightweight aerospace: anti-gravity, hard-light feathers, thrusters, storm energy. Each unit type is a different bird species | Storm charcoal armour, white feather tips, electric yellow energy | Angular faceted plates, blade fins, feather-shaped vanes | Tall, sharp, pointed, wings and tails |
| Water faction | Grown bio-technology: pressure suits, armoured shells, hydro-cannons, dome shields. Each unit type is a different sea creature | Muted sea-green with dusky rose-coral bands, dark teal bodies, turquoise lights; no white | Spiral shells, domes, circles, treads and braces | Low, rounded, menacing |
| Earth faction | Stone constructs held together by energy: levitating columns, gravity fields | Pale cream stone with deep blue lapis plates, blue crystal prisms, amber energy | Hexagonal columns, honeycomb plates, floating prisms | Heavy, built from hexagons, uneven outlines |
| Fire faction (later) | Plasma and magma forges | Obsidian black, ember orange | Magma channels, vents, heat shimmer | Jagged, top-heavy |

## Developer notes on round 2 (16 Nov 2026)

- **Skai:** more angular, with more interesting silhouettes. Red is reserved for the fire faction, so the Skai need their own colour scheme; round 3 offers three.
- **Water:** keep the round edges and circles, but make the units more intimidating.
- **Earth:** build the bodies from hexagons; the round 2 shapes were blocky with dull silhouettes.
- **Map:** the idea is right, but real maps are much larger than the round 2 image showed.
- **Battle mock:** redo it once the unit designs above are approved.

## Developer notes on round 3 (16 Nov 2026)

- **Skai colour is decided:** scheme B, storm charcoal with white feather tips and electric yellow energy. The brushed gold and sky teal in the palette table are superseded.
- **Skai heads:** not one stock bird head. Each unit type is a different kind of bird.
- **Skai air units:** redo in scheme B and the angular style.
- **Water:** closer, but the white (pearl) shells look strange; show other colour combinations.
- **Earth:** much better, but plain grey is boring; show a few options using more varied stone.
- **Map:** the full 1v1 map is kept.

## Developer notes on round 4 (16 Nov 2026)

- **Skai ground units are chosen by species:** raven worker, secretary-bird scout (add energy blades on the feet), heron trooper (the rifle must be much longer), great horned owl storm-caller (show several staff and backpack designs).
- **Roc Rider:** remove the stray extra wing at the rear. **Thunderhead:** looks great, but the wing feathers must sweep backwards, not forwards.
- **Water colour is decided:** scheme C, sea-green shells with rose-coral banding and turquoise glow. Now vary the species per unit, as was done for the Skai.
- **Earth:** stone A (cream marble) is closest; show more colour variations of it. Malachite and mixed strata are dropped.

## Developer notes on round 5 (16 Nov 2026)

- **Earth colour is decided:** marble C, pale cream stone with deep blue lapis plates, sapphire prisms and amber energy.
- **Roc Rider:** the wing is attached wrongly. **Thunderhead:** feathers still read as being on the front of the wing; they belong on the back (trailing) edge. Add missiles under the Thunderhead's wings.
- **Raven worker:** show a few options for its tools.
- **Owl storm-caller:** the trident staff and its side-rotor backpack are chosen; make the owl more high-tech and augmented.
- **Water units are chosen by species:** octopus worker; giant clam defender with a much larger bubble shield that covers nearby units; mantis shrimp fighter; cuttlefish anti-air with more barrels or several guns mounted on its head; sea-snail artillery whose whole shell is the cannon.

## Developer notes on round 6 (16 Nov 2026)

- **Thunderhead:** the top view is right. In the front view the feathers must not show on the front (they sit behind the wing and may not be visible at all), and the missiles must point straight forward, not angle downwards.
- **Skai worker:** plasma cutter and visor, with the cargo grabber claws as its backpack. The developer is having second thoughts about the raven: try a penguin or another bird.
- **Water materials:** too shiny. Make every unit matte with a rusty patina texture.
- **Giant clam:** remove its small personal shield disc (the large bubble stays) and replace the legs with treads or something else.
- **Sea snail:** the shell stays upright and spirals like a normal snail shell; it swings forward to aim when firing. Show both poses.

## Developer notes on round 7 (16 Nov 2026)

- **Skai worker is the pelican**, with the plasma cutter, visor and cargo-claw backpack. Thunderhead is kept.
- **Water line-up:** good, but it still has a "way too shiny AI look". Round 8 tries rendering treatments that remove gloss and micro-detail.
- **Sea snail:** the two-pose idea is right, but the two drawings were inconsistent. Both poses must be the same design and clearly show the mechanism that moves the shell.

## Developer notes on round 8 (16 Nov 2026)

- **Rendering style is decided:** the low-poly treatment. Re-render the Skai line-up in it.
- **Sea snail:** the round 8 sheet (ball shell on an industrial steel rig) was a step in the wrong direction. Redo it in the decided style, starting from the snail in the low-poly line-up.

## Developer notes on round 9 (16 Nov 2026)

- **Skai ground and air units in low-poly are kept.**
- **Sea snail:** the braces were placed inconsistently between the two poses. They must be on the outside of the shell, not inside it.

## Developer notes on round 10 (16 Nov 2026)

- **Earth units in low-poly are kept.**
- **Sea snail:** the bracket is good. Make the cannon larger and more impressive, and put a targeting reticle on the eye stalks.
- **Battle mock:** does not look like the battles the developer imagines; no detail given on how. Round 11 offers three different takes to find the direction.

## Developer notes on round 11 (16 Nov 2026)

- **Sea snail, last changes:** the cannon fires angled upward for a lobbing shot; it fires water or energy, not ink; a magnifying lens with the reticle sits over one eye stalk instead of reticles drawn on the eyes.
- **In-game battle view:** rejected. Units standing in tidy rows look nothing like units engaging in an RTS. The project needs a good mock-up so that later development ends up looking right.
- **Whole-match view is kept.**

## What an in-game engagement must show (from the round 11 note)

- The tilted three-quarter camera of the design doc, at normal play zoom: a unit is a few percent of the screen height.
- Armies in contact, not parade rows: melee units mixed together at the front, ranged units in a loose arc behind them, flyers overhead, stragglers arriving.
- Evidence of a fight: projectiles in flight, impacts, wrecks, units at different health.
- The things a player reads: health bars, selection rings under the player's units, a HUD (minimap, selected-unit panel, command buttons, resource counters).
- Terrain that matters: a ramp or choke, cliffs, water with a visible edge, and part of a base.

## Buildings (round 13, first proposal)

The design doc names what a base needs (a drop-off per base, supply, production, tech, static defences capped at six per base, walls; water structures that release flood water) but not the buildings themselves. Round 13 draws one building for each job so the developer can react; the names below are placeholders until the faction documents are written.

| Job | Skai (placeholder name) | Water (placeholder name) |
| --- | --- | --- |
| Drop-off | Perch Depot | Pearl Depot |
| Supply | Roost | Shell Cluster |
| Ground production | Muster Deck | Brood Pool |
| Air production / flood source | Sky Dock | Tide Well |
| Tech | Storm Spire | Reef Workshop |
| Static defence | Arc Turret | Jet Turret |
| Wall | Wind Baffle | Coral Wall |

## Developer notes on round 13 (16 Nov 2026)

- **Grand Shell is kept.**
- **Great Aerie:** the large eagle head is out of place; show different leg options.
- **Skai buildings:** the wall's feathers should point up, not down; the legs that hold the buildings up should end in little talons.
- **Water buildings:** a little too clam-themed; lean into the reef look.

## Developer notes on round 14 (16 Nov 2026)

- **Great Aerie:** the taloned legs (left option) are chosen. The rim feathers must all go in the same direction around the building, not in different directions.
- **Skai buildings:** they do not evoke the angular shapes wanted for this faction. Try making them tree-like rather than bird-like; generate options.
- **Water buildings:** the tube-sponge supply cluster and the tube-coral tide well are too similar and need variation. (The note was cut off after "Also, make the".)

## Developer notes on round 15 (16 Nov 2026)

- **Skai buildings are tree-like and angular.** Conifer style for the wall and the buildings with landing pads; canopy style for the turret, the defence and the supply depot. The lightning style is dropped.
- **Great Aerie:** almost there. When hovering it should have propellers underneath; they will also be an animation point.
- **Water turret:** too floppy and unintimidating.

## Developer notes on round 16 (16 Nov 2026)

- **Skai buildings and water buildings are kept.**
- **Great Aerie:** generally correct, but the hovering drawing is warped: it shows too much of the top while also showing the underside.

## Earth buildings (round 18, first proposal)

Placeholder names, one building per job, as for Skai and water. The Shaping structure and the Gravity Spire are named in the design doc.

| Job | Earth (placeholder name) |
| --- | --- |
| Drop-off | Quarry Hub |
| Supply | Column Cluster |
| Production | Forge Ring |
| Recasting | Shaping structure (design doc) |
| Tech | Resonance Vault |
| Static defence that grounds flyers | Gravity Spire (design doc) |
| Wall | Raised stone wall |

## Developer notes on round 18 (16 Nov 2026)

- **Leviathan:** bigger claws, big enough to drag on the ground while it walks.
- **Earth roster:** start over. They look like structures, not beings. Only the worker is good. Earth units are living constructs with bodies, heads and limbs.
- **Titan HQ:** the two states must match. Rooted, the Titan sits in a meditative pose and the rest of the rooted base rises out of the ground around him.
- **Earth buildings:** a bit incomprehensible. The Quarry Hub, Shaping structure and Resonance Vault are fine; the other four need a complete redesign so each job is readable.
- **Map:** floods come from the water faction's presence. An empty map is ordinary dry land: grassy fields in the low ground, not flooded basins.

## Developer notes on round 19 (16 Nov 2026)

- **Leviathan:** the back is too clam-like; make it crabbier and armour-plated.
- **Earth roster:** much better. The siege beast's cannons must face the same way as its face. The lancer's pose is a bit odd.
- **Titan HQ:** much better. Bring back the big gun arm. The emitter on only one side should be more symmetrical.
- **Earth buildings:** liked. The blue parts on the wall should be subtle spikes.
- **Map:** looks good; starting locations are fine. It needs more ramps up and down between elevations in the middle area and the low zones.

## Developer notes on round 20 (16 Nov 2026)

- **Kept:** the Leviathan and Tidecaller, the Titan HQ, the earth buildings and the dry map.
- **Earth siege beast:** the weapons on its back should be actual cannons with barrels, in the earth colours, not crystals.

## Skai Riders rule (design decision, 26 Oct 2026)

The Skai are an invented species of feathered bird people. Borrow nothing from real peoples: no war bonnets or headdresses, no tipis or totem poles, no face paint, no real tribal patterns, no dreamcatchers. Their iconography is invented from feathers, wind and sky, and no real culture's ornament (including East Asian cloud scrolls, which appeared in round 1) is used. An image that breaks this rule fails.

## Checklist (every image)

1. Matches the shot description: the named units or scene are all present.
2. Faction technology, palette and motifs match the table; nothing low-tech; team colour is visible and saturated.
3. Silhouettes are distinct from each other and from the other faction.
4. Passes the Skai Riders rule.
5. No text, watermark, border or UI unless the shot asks for it.
6. Camera matches the shot (line-up sheet, or three-quarter top-down for scenes).

## Rounds

| Round | Question | Status |
| --- | --- | --- |
| 1 | Skai and water line-ups, both HQ units, a flooded map, one battle mock, earth mood | Done; superseded: the developer found it too low-tech |
| 2 | The same eight shots redone as science fiction | Done. Kept: Great Aerie, Grand Shell. The rest revised in round 3 |
| 3 | Revisions from the developer's notes on round 2: angular Skai in three colour schemes, more intimidating water units, hexagonal earth, a full-size 1v1 map | Done. Kept: the full map and Skai scheme B (charcoal and electric yellow). Schemes A and C dropped |
| 4 | Options from the developer's notes on round 3: varied bird species for the Skai, air units in scheme B, three water colour schemes without white, three earth stone palettes | Done. Chosen: water scheme C (reef); earth stone A (warm marble) as the direction; Skai species per unit (see notes) |
| 5 | From the developer's notes on round 4: the chosen Skai ground line-up, storm-caller staff and backpack options, corrected air units, three more earth colour variations, two sets of varied water species | Done. Chosen: earth marble C (cream with lapis blue); owl with the trident staff and side-rotor backpack; water species per unit (see notes) |
| 6 | From the developer's notes on round 5: Roc Rider and Thunderhead redrawn separately, raven worker tool options, a more augmented owl, the chosen water line-up with its fixes | Done. Kept: Roc Rider and the owl storm-caller. Thunderhead top view accepted |
| 7 | From the developer's notes on round 6: Thunderhead front view, the worker as a different bird with the chosen tools, matte water line-up with a tracked clam, sea-snail travel and firing poses | Done. Kept: Thunderhead. Skai worker is the pelican. All Skai unit designs are now chosen |
| 8 | From the developer's notes on round 7: the water line-up in three less glossy rendering treatments, and the sea snail's two poses with one consistent design and a visible shell mechanism | Done. Chosen: treatment C (low-poly) as the rendering style for every faction. Snail sheet rejected as a step in the wrong direction |
| 9 | From the developer's notes on round 8: the chosen Skai ground and air units in the low-poly style, and the sea snail's two poses in that style | Done. Kept: Skai ground units and Skai air units in low-poly. All Skai designs are final |
| 10 | The sea snail with its braces on the outside of the shell; the earth units in low-poly with the chosen lapis marble; the battle mock redone from the final Skai and water designs | Done. Kept: earth units in low-poly (cream and lapis). The snail's bracket is accepted |
| 11 | From the developer's notes on round 10: the sea snail with a larger cannon and targeting reticles on its eye stalks; three different takes on the battle, because the first did not match what the developer imagines | Done. Kept: the whole-match view (take C). Cinematic take dropped. In-game take rejected |
| 12 | From the developer's notes on round 11: the sea snail lobbing a water-energy shot upward with a scope lens over one eye stalk; two in-game engagement mock-ups that look like a real RTS fight, with a HUD, to guide development | Done. All three kept: both in-game mock-ups and the sea snail |
| 13 | Requested by the developer: the Skai and water HQ units in low-poly and the final colours, each in its Uprooted and Rooted state, and a first building sheet for each of the two factions | Done. Kept: Grand Shell HQ |
| 14 | From the developer's notes on round 13: the Great Aerie without the eagle head and with three leg options; Skai buildings with talon feet and an upward-feathered wall; water buildings that lean into a coral reef look | Done. Chosen: taloned legs for the Great Aerie. All three sheets revised in round 15 |
| 15 | From the developer's notes on round 14: the Great Aerie with taloned legs and all rim feathers sweeping one way; three tree-like, more angular options for Skai buildings; water buildings with the supply cluster and tide well made clearly different | Done. Skai buildings: conifer and canopy styles kept and mixed per building; lightning dropped |
| 16 | From the developer's notes on round 15: one combined Skai building sheet mixing the conifer and canopy styles; the Great Aerie hovering on propellers; a sturdier, more intimidating water turret | Done. Kept: Skai buildings and water buildings. The Great Aerie's design is right but its hovering drawing is warped |
| 17 | From the developer's note on round 16: the Great Aerie redrawn with correct perspective, hovering seen from below and from the game camera, and landed | Done. Kept: "Looks good, we can revisit once we see everything in motion" |
| 18 | The rest of the drawings, requested by the developer: the water Tidecaller and Leviathan, the rest of the earth roster, the Titan HQ in both states, earth buildings, and the 1v1 map in low-poly. Fire stays for later, as the design doc schedules it | Done. All five sheets revised in round 19; the earth worker, Quarry Hub, Shaping structure and Resonance Vault are liked |
| 19 | From the developer's notes on round 18: the Leviathan with huge dragging claws; the earth roster started over as beings; the Titan HQ meditating when rooted; four earth buildings redesigned; the map dry and grassy | Done. All five much closer; small fixes in round 20 |
| 20 | From the developer's notes on round 19: a crabbier armour-plated Leviathan back; siege beast cannons facing forward and a steadier lancer pose; the Titan's big gun arm restored and its emitters made symmetrical; subtle blue spikes on the earth wall; more ramps in the map's middle and low ground | Done. Kept: Leviathan and Tidecaller, Titan HQ, earth buildings, the dry map. Earth roster revised once more |
| 21 | From the developer's note on round 20: real barrelled cannons on the earth siege beast | Done. Revised: the cannons did not look realistically placed on the model |
| 22 | From the developer's note on round 21: the siege beast's guns mounted in one turret built into its back | Done; awaiting the developer's marks |
| 3b | Battle mock redone from the approved unit designs; buildings for Skai and water | After the developer picks a Skai colour scheme |
| 3 | Earth roster and the Titan HQ | Before Phase 4 |
| 4 | Fire faction | During early access |
