<!-- rts/docs/ASSET_STORE.md: Asset Store shortlist for presentation-only packages; Phase 0 deliverable, the developer picks in the Unity editor. -->
# Unity Asset Store shortlist (presentation only)

**These packages are presentation only: they must never move units, resolve combat or touch simulation state.** The simulation is an engine-free deterministic C# library; Unity only renders and takes input and turns it into commands.

Research notes (checked 2026-10-26): listings were confirmed through web search results that quote the assetstore.unity.com pages. The store pages themselves could not be fetched from this environment, so prices, versions and dates are snapshots and must be re-checked in the editor before buying. Anything marked "unverified" could not be confirmed. "Unity 6 confirmed" means the listing shows a 6000.x original or compatible version; "2022.3+" means the listing shows 2022.3 or newer but no Unity 6 entry.

**Update (Phase 1):** a hand-written RTS camera (`RtsCameraController`) and selection (`SelectionController`, box select, control groups, orders) now exist in `rts/src/Game/Assets/Scripts` as the free fallback, using the legacy Input Manager, so every package below is optional polish rather than a requirement.

## 1. RTS camera rig (fixed three-quarter top-down)

Free fallback: write a ~100 line camera script yourself (pan on a ground plane, zoom by distance, map bounds clamp, optional edge scroll) using the new Input System. With a fixed angle this is small, and Cinemachine can follow a pivot object if you want smoothing.

| Package | Publisher | Price | Why it fits | Caveat |
|---|---|---|---|---|
| [RTS Camera (DevBricks)](https://assetstore.unity.com/packages/slug/246597) | DevBricks (per search snippet; unverified on the page) | under $30 ($9.99) | Unity 6 confirmed (original version 6000.0.31, v1.3.0, Mar 2025). Uses the new Input System; movement borders and switchable features. | Only 3 ratings, so little public feedback. Built-in and URP listed; source and physics use unverified. |
| [Easy Camera: RTS & Top-Down](https://assetstore.unity.com/packages/slug/312045) | unverified | free | Edge scrolling, right-drag pan, wheel zoom, middle-mouse rotate; built on the Input System; v1.0.0. | Unity version support unverified, and v1.0.0 means a young package. Rotation may need disabling for a fixed angle. |
| [Modular Strategy Top Down Camera Controller](https://assetstore.unity.com/packages/slug/309175) | unverified | price unverified | Separate components per feature (pan, zoom, terrain height follow); Built-in, URP and HDRP listed; mentions new Input System. | Unity version unverified. Includes object selection, which you must not use; keep only the camera components. |

## 2. Box/drag selection and unit selection

Free fallback: a uGUI Image for the drag rectangle plus a script that projects unit screen positions into that rectangle (about 60 lines). It outputs a list of unit ids to your command layer. The GameDevAcademy RTS box select tutorial uses the legacy `Input` class, so port it to the new Input System.

| Package | Publisher | Price | Why it fits | Caveat |
|---|---|---|---|---|
| [Unit Selection System](https://assetstore.unity.com/packages/tools/input-management/unit-selection-system-295674) | unverified | free | Free; listing shows 2022.3.45f1 compatible with Built-in, URP and HDRP (v1.0, Sep 2024). Good for trying out selection feel. | Needs Quick Outline (another free store package). Unity 6 and Input System support unverified. |
| [RTS Selection System](https://assetstore.unity.com/packages/slug/304299) | unverified | price unverified | StarCraft II / Warcraft III style selection with a customisable drag box and an `ISelectable` interface, which maps well to selecting by unit id. Last release Feb 10, 2025. | Unity version and Input System support unverified. |
| [RTS Unit Selection v2.0](https://assetstore.unity.com/packages/tools/input-management/rts-unit-selection-v2-0-284090) | unverified | under $30 ($15) | Single and multi select, with 2D screen rectangle and 3D world cube prefabs. v2.1.1, Apr 25, 2025. Works with Built-in, URP and HDRP. | Original Unity version is 2021.3.45, so Unity 6 is unverified. Input System support unverified. |

Whichever you pick, wire it so selection produces a list of unit ids and nothing else. Selection that relies on colliders and raycasts needs proxy colliders on the view objects, because the simulation owns no physics.

## 3. UI kit for the RTS HUD

Free fallback: Unity UI Toolkit (built in; UXML and USS for the resource bar, selection panel, command card and minimap frame), or plain uGUI with TextMeshPro. Both are free and Unity 6 native. The HUD only reads simulation snapshots and emits commands.

| Package | Publisher | Price | Why it fits | Caveat |
|---|---|---|---|---|
| [Modern UI Pack](https://assetstore.unity.com/packages/tools/gui/modern-ui-pack-201717) | Michsky (per search snippet) | under $100 ($39.99, seen discounted to $19.99) | Unity 6 confirmed (6000.1 and 6000.2 listed compatible; v5.5.28f, Nov 17, 2025). Buttons, panels, progress bars, tooltips and dialogs to assemble a clean HUD. | Generic UI, not RTS specific: no minimap or command card. One reviewer notes weak gamepad selected states. |
| [Military Combat HUD (Synty INTERFACE)](https://assetstore.unity.com/packages/2d/gui/military-combat-hud-synty-interface-gui-302903) | Synty Studios (per search snippet) | price unverified | uGUI HUD pieces and icons in one art style; listing shows 2022.3.56f1 compatible with Built-in and URP. | Unity 6 unverified. Aimed at shooter-style HUDs, so you still build the RTS layout (minimap, command card) yourself. |

I found no Asset Store pack that is a true RTS HUD with minimap and command card and confirmed Unity 6 support. The all-in-one RTS starter kits (for example RTS Starter Kit) bring their own simulation and unit movement, which conflicts with the rule at the top, so they are excluded.

## How to evaluate

- Unity 6 compatibility: the compatibility table should list a 6000.x version, not only the original version; otherwise import into a blank Unity 6 project and check the console.
- Input system: it must work with the new Input System package (or be easily patched); a package that reads the legacy `Input` class will throw in a new-Input-only project.
- Source code included: prefer full C# source over DLLs so you can strip anything that touches gameplay.
- Licence per seat: the standard Asset Store EULA is per seat; check it if another person joins the project, and check redistribution terms for any art you ship.
- Last update date: prefer updates within the last 12 months and a changelog that mentions Unity 6.
- Physics and sim coupling: avoid packages that insist on Unity physics, Rigidbody movement or NavMesh to function; they must run purely as a view layer on top of the deterministic simulation.
