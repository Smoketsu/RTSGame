<!-- rts/docs/proposals/P-001-art-pipeline.md: proposal for how the game's art is directed and produced by a solo developer; feeds the art and audio track in DEVELOPMENT_PLAN.md. -->
# P-001: Art direction and asset pipeline

**Status, 16 Nov 2026:** approved by the developer ("We can generate the art assets with tripo for now"). The three-model trial below was run the same day; results are in `docs/art/models/trial` and `trial2`, and the route that worked (a front, side and back sheet into Tripo3D's multi-view endpoint) is recorded in `docs/art/CONCEPT_BRIEF.md`. This text was written by a session that believed the project was at Phase 0; its phase references are out of date, see `docs/STATUS.md`.

**Decision needed:** approve generated 3D models as the main route for units and buildings, starting with a three-model trial in December 2026.

## Problem

Early access needs about 12 units, 2 HQ units and 16 to 20 buildings for Skai and water, each readable at RTS zoom, team-coloured and animated. Revision 1 of the plan gave this one line in Phase 2. The Asset Store has no bird-people or mollusk armies, and commissioning roughly 30 rigged models is outside a small budget.

## Proposal

1. **Concept art sets the direction first.** One brief ([CONCEPT_BRIEF.md](../art/CONCEPT_BRIEF.md)), rounds of generated images, the developer approves. Round 1 is under way.
2. **Trial generated 3D before committing.** Turn three approved concepts into models with the 3D generation service already connected to this project (Tripo3D): the Windrunner (a biped), the Shellback (a simple rounded shape) and the Grand Shell (large, slow). Import them into the Unity greybox and judge them at game zoom.
3. **Team colour by mask.** Each model carries a mask texture; one shader tints the masked area per player. Concept art already reserves a saturated team-colour area for this.
4. **Animation is the known weak spot.** Automatic rigging handles bipeds; wings and tentacles usually need hand work or procedural motion. The trial measures this. Fallbacks: shader-driven tentacle and wing motion, or commissioning rigs for the few units that need them.
5. **Buy what is generic.** Terrain textures, rocks, water shaders, effects, UI kits and sound packs come from the Asset Store shortlist.

## Trial pass or fail (December 2026)

| Check | Pass when |
| --- | --- |
| Readability | The three models are told apart at game zoom from silhouette alone |
| Style match | Each matches its approved concept and the brief's palette |
| Budget | Triangle count and texture size fit a limit set with the trial (starting point: 5,000 triangles per unit) |
| Animation | The Windrunner walks and attacks with an automatic rig; the cost of the other two is known |
| Cost | Credits and hours per model are measured, so 30 models can be priced |

If the trial fails on style or animation, the fallback is stylised proxy shapes through the vertical slice and a commissioned set for the early-access roster only.

## Costs and risks

- **Cost is unknown until the trial.** Image and 3D generation are billed per request; the trial exists to measure it.
- **Steam asks developers to disclose generated content** on the store page. The disclosure text is written with the store page (P-002).
- **Style drift** between rounds. Mitigation: one brief, a checklist per image, approved images become style references for later rounds.
- **The Skai Riders rule.** Every Skai image is checked against the brief's rule, and the sensitivity read in May 2027 covers the art.

## Alternatives considered

| Option | Why not first |
| --- | --- |
| Asset Store kitbash only | No matching species; the factions would look generic |
| Commission everything | Cost, and every design change is a paid revision |
| 2D sprites | The design doc fixes a 3D game with a three-quarter camera |
