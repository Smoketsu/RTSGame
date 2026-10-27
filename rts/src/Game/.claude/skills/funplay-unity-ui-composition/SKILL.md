---
name: funplay-unity-ui-composition
description: Build and revise Unity UI, primarily uGUI mobile page prefabs, with framework-aware routing, Sprite import safety, text effects, localization and visual verification.
---
<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-ui-composition@1.0.9 -->

# Unity UI Composition

Use this built-in skill when creating, assembling, adapting, reviewing, or fixing Unity UI, especially Canvas-based mobile screen or popup prefabs. Identify the target framework before applying component guidance.

## MCP-First Unity UI Operations

- Do not use computer use (desktop mouse/keyboard automation) to operate Unity unless necessary. When assembling, modifying, inspecting or validating UI, prefer Unity MCP whenever it can complete the step, including hierarchy/component/prefab reads and edits, compilation/Play state, clicks/scrolling, screenshots and recordings.
- Check the connected project's tools/list and, when available, `get_tool_capabilities`. A tool missing from exposure, compilation/domain reload or a temporary disconnection is not evidence of a missing capability: check exposure/readiness and recover status first. Respect custom allowlists; do not widen exposure or use another interaction method to bypass restrictions.
- Prefer specialized MCP tools; for project-specific gaps they do not cover, use a permitted, guarded `execute_code` call through Unity Editor APIs when it can perform the step reliably. Computer use is a fallback only for a confirmed MCP capability gap, or an explicit user request: explain the uncovered step before using it, limit it to that step, and return to MCP readback/validation when available. If recovery fails, report the connection blocker rather than silently switching methods or repeating uncertain mutations.
- A confirmed blocking native Unity modal that MCP cannot dismiss is a narrow computer-use exception. Explain the modal and handle only that blocker, then return to MCP and recheck project identity, Editor state and the interrupted operation. Do not infer permission to save, discard, overwrite, install, authorize or accept security changes merely to unblock Unity; ask when the consequential choice is not already authorized. Never replay an uncertain mutation.
- This routing applies to operating Unity, not ordinary source-file editing or viewing supplied design references and already-captured images/videos with appropriate file or media tools.

## Framework And Task Routing

Identify the target screen's framework before editing. The Canvas component guidance below applies to uGUI, not automatically to UI Toolkit or IMGUI. Preserve the existing framework, text convention and bindings; do not convert or rebuild a screen to fit this skill.

Read only the references needed for the current task:

- For unfamiliar or mixed UI frameworks and Editor windows: [UI framework selection](references/unity-ui-composition/ui-frameworks.md).
- Before changing Sprite borders, pivots or slicing: [Sprite importer safety](references/unity-ui-composition/sprite-importers.md).
- For font assets, text effects, missing glyphs or requested localization: [TMP and localization](references/unity-ui-composition/text-localization.md).

These are original Funplay adaptations informed by the pinned Unity official plugin 0.1.8-beta. They supplement, not replace, the MCP-first, prefab-preservation and design-fidelity rules here.


## Operating Loop

1. Inspect before editing.
   - Confirm the active scene, Canvas render mode, Canvas Scaler settings, EventSystem and input module, target orientations, design resolution, safe-area policy, and relevant prefab asset paths.
   - Inspect the existing hierarchy, anchors, pivots, offsets, layout controllers, sibling order, Canvas sorting, serialized references, animation targets, and Prefab overrides.
   - Inspect representative screens and prefabs to determine whether `UnityEngine.UI.Text` or `TextMeshProUGUI` is the project's prevailing text component, and inspect existing visual-effect components and material presets before introducing a new UI effect.
   - Treat screenshots and design coordinates as visual intent, not as permission to replace a working hierarchy.
   - When design images are supplied, follow Design References And Clarification below to map screens and states, resolve material uncertainty, and validate the actual result against each requested reference.
2. Classify each region.
   - Identify full-bleed art and the design's existing edge spacing; reuse any existing safe-area policy without adding a new adaptation layer by default.
   - Mark placement as fixed to an edge or corner, stretched between regions, content-sized, repeated-layout content, scrollable content, modal, or world-space UI.
   - Decide which component owns each axis. One axis must not be driven concurrently by a Layout Group, ContentSizeFitter, AspectRatioFitter, animation, and manual code.
3. Make the smallest coherent change.
   - Preserve the prefab root, existing children, components, names, serialized references, animation bindings, and Prefab overrides unless a specific replacement is required.
   - Modify only the necessary RectTransforms, components, fields, and children. Do not recreate an entire UI or GameObject prefab unless the user explicitly requests a rebuild.
   - Author reusable user-facing screens, panels, and controls as prefabs with their hierarchy and component references wired in the Editor. Prefer a complete, saved page prefab that can be inspected and reused without running a UI-construction script; use nested prefabs for repeated controls as appropriate. Runtime code should instantiate or pool the authored prefabs, bind data and listeners, and handle state or animation. Do not move a stable UI hierarchy into procedural runtime construction merely for implementation convenience.
   - Use Unity MCP or Unity Editor APIs for `.prefab`, `.unity`, and `.asset` changes; never patch Unity YAML as text.
4. Read back and validate.
   - Read exact hierarchy, anchors, offsets, sizes, sprites (including borders for Sliced Images), text settings, raycast state, sorting, and references back from Unity.
   - Test layout, input, safe area, localization, animation interruption, close and reopen state, and runtime data changes.
   - Capture screenshots at representative aspect ratios for static layout; use a short `record_game_view` clip when correctness depends on an animation or interaction sequence. Use a real device build for performance and platform behavior before claiming device validation.

## Design References And Clarification

- For one or more design images, map each reference to its intended screen, popup, state, or responsive variant before editing. Do not merge incompatible variants or assume that upload order establishes version priority. Inspect every requested reference at a readable scale and look for matching project sprites, fonts, materials, and existing controls before assuming a detail is unavailable.
- Keep a compact per-screen checklist of visible details that matter: composition, alignment, spacing, sizes, text and line breaks, typography and effects, colors, icons, borders and corners, layering, and shown control states. Distinguish what the image actually shows from inferred behavior. Do not omit decorative details, reuse an approximate asset, or simplify a control merely because the rough layout already looks similar.
- When revising existing UI, preserve prefab structure, serialized references, and working behavior, not known visual mismatches. A clearly requested design change calls for scoped visual edits; the old UI looking different is not itself a reason to ask. If matching the design would conflict with an explicit preservation requirement or require changing existing behavior, explain the conflict and ask before that affected change.
- If reference and asset inspection still leave a material ambiguity, proactively ask a focused question before committing to that interpretation: identify the image, screen and region, state what cannot be determined, and offer plausible alternatives and their impact when useful. Examples include unreadable copy, conflicting versions, unclear page or state mapping, or missing exact artwork; request a clearer crop or source asset when that would resolve it. Do not silently invent text, omit an element, or substitute an approximation. Continue independent, clearly specified work while awaiting an answer; use project conventions for low-risk reversible details and disclose assumptions that affect the visible result rather than asking about every pixel.
- Validate every requested screen and state with actual Unity captures at the reference aspect ratio and comparable content, then check responsive variants separately. Inspect the captures, compare each region against its reference (side-by-side or with aligned overlays when useful), correct the differences, and recapture. Compilation success, component readback, or one correct page does not establish visual fidelity for the other pages. Do not stretch or crop the comparison to hide a mismatch.
- Before reporting completion, distinguish verified matches, user-approved deviations, unresolved differences, and unverified screens or states. Include any missing assets, unanswered visual decisions, or capture limitations. Do not claim full fidelity while known unapproved mismatches remain; ask the user to resolve remaining design choices instead of declaring an approximation complete.

## Component Selection

| Component | Use it for | Configure deliberately | Avoid |
| --- | --- | --- | --- |
| `Canvas` | Root render and sorting space | Use Screen Space - Overlay for ordinary HUD and menus, Screen Space - Camera when camera composition or scene interleaving is required, and World Space only for UI that belongs in the 3D world | Adding independent canvases only to organize folders; leaving Event Camera unset in Camera or World Space modes |
| `CanvasScaler` | Converting a design resolution to screen-space scale | For mobile screen UI, normally use Scale With Screen Size and a documented portrait or landscape reference resolution; choose Match from actual width and height constraints | Assuming Match `0.5` solves every aspect ratio or relying on Constant Physical Size without validating device DPI |
| `RectTransform` | Parent-relative position and size | Set anchors first, then pivot and offsets; keep local scale at one; use stretch anchors for flexible regions | Using anchoredPosition from one screenshot as universal placement or using Transform scale as a layout tool |
| `HorizontalLayoutGroup` / `VerticalLayoutGroup` | Dynamic rows, columns, button rails, and variable-length lists | Set padding, spacing, child size control, expansion, and child `LayoutElement` intent | Applying a Layout Group to hand-composed full-screen art or manually positioning driven children |
| `GridLayoutGroup` | Uniform inventory, level, or card cells | Choose cell size, spacing, start axis, and a row or column constraint | Expecting child preferred sizes to change grid cells; GridLayoutGroup assigns fixed cells |
| `LayoutElement` | Declaring minimum, preferred, flexible, or ignored layout behavior | Use it to override an Image, text, or nested group's layout contribution and to make selected siblings flexible | Adding it without selecting the properties that should override layout input |
| `ContentSizeFitter` | Making the current RectTransform follow its content on one or two axes | Prefer a single required axis; set the pivot to control growth direction; allow deferred layout unless immediate measurement is truly required | Putting it on every child controlled by a parent Layout Group or writing the same driven size manually |
| `AspectRatioFitter` | Preserving aspect for an isolated preview, card art, or media surface | Use Fit In Parent for letterboxing or Envelope Parent for cover behavior | Treating it as general safe-area or screen-aspect adaptation, or combining it with another controller on the same axis |
| `Image` | Sprite UI, icons, frames, progress fills, and nine-sliced controls | Use Simple for fixed art, Sliced for resizable panels and buttons only after verifying suitable Sprite borders, Tiled for repeatable patterns, and Filled for progress or radial values | Sliced with all-zero borders, stretching bordered art as Simple, leaving decorative graphics as Raycast Target, or using a unique material without need |
| `RawImage` | Arbitrary Texture, RenderTexture, camera, video, downloaded, or generated texture content | Preserve the source aspect and manage texture lifetime explicitly | Using RawImage for ordinary Sprite UI that should atlas and batch with other Images |
| `UnityEngine.UI.Text` | Text in an established legacy uGUI project or screen family | Use it only after inspection shows it is the prevailing project convention; match the existing Font, material, alignment, line spacing, overflow, and localization behavior | Introducing it into a new project, mixing it casually into a TMP-based screen, or converting existing labels without checking layout and serialized references |
| `TextMeshProUGUI` | Text in an established TMP project and the default for a new project with no existing text convention | Match the project's font assets and material presets; set wrapping, alignment, overflow, fallback fonts, and localization limits; constrain Auto Size to a narrow range; when the design explicitly shows a text effect, use TMP's own component and shader-material controls | Replacing an established `Text` component merely to modernize, continuous Auto Size on rapidly changing text, or shipping without required CJK and symbol glyphs |
| TMP font material / material preset | An outline, underlay or shadow, glow, face dilation, softness, or other text treatment explicitly visible in the design for a `TextMeshProUGUI` project | For a simple outline set the TMP component's `outlineColor` and `outlineWidth`; for underlay, glow, or other shader effects reuse an approved project preset or create a dedicated preset or material instance and verify it with fallback fonts | Adding a uGUI `Outline` or `Shadow` to TMP text by habit, changing a shared `fontSharedMaterial` and unintentionally restyling other labels, or enabling effects absent from the design |
| `Outline` / `Shadow` / similar `BaseMeshEffect` | A specifically designed effect on legacy `UnityEngine.UI.Text` or another Graphic, when required by the design or established project style | Reuse the project's approved style and keep effect distance, color, alpha, and stacking minimal; verify legibility and cost on target hardware; prefer TMP-native effects when the text component is `TextMeshProUGUI` | Adding generic polish by default, stacking effects, applying them broadly, or using them to compensate for weak contrast or incorrect layout; these effects duplicate UI geometry and increase overdraw |
| `TMP_InputField` | Editable TMP text | Author and validate a prefab with `textComponent`, `textViewport`, `placeholder` when used, target Graphic, navigation, and input settings already serialized; instantiate the prefab and bind data or listeners at runtime | Rebuilding a stable input hierarchy in code; if dynamic construction is genuinely required, never add it to an active GameObject and bind `textComponent` afterward because affected TMP versions create the caret renderer in `OnEnable` only when that reference is already present |
| `ScrollRect` | Drag or wheel scrolling through content larger than a viewport | Use `ScrollRect -> Viewport + RectMask2D -> Content`, reference both Viewport and Content, enable only required axes, and choose Clamped or Elastic intentionally | Unrestricted movement without recovery, deeply nested competing scroll axes, or instantiating thousands of live rows without virtualization |
| `RectMask2D` | Rectangular clipping in 2D Canvas UI | Prefer it for scroll viewports and rectangular reveal areas | Using stencil `Mask` for a simple rectangle |
| `Mask` | Clipping to a non-rectangular Graphic shape | Use only when the shape matters and account for stencil and material cost | Deeply nested masks or using it where RectMask2D is sufficient |
| `CanvasGroup` | Fading and enabling or disabling a whole panel | Change alpha, interactable, and blocksRaycasts together according to visible state; decide whether parent groups apply | Setting alpha to zero while leaving an invisible panel interactive or raycast-blocking |
| `Button` and other `Selectable` controls | Click, toggle, slider, dropdown, and navigation behavior | Put the main Raycast Target on the interactive root, set Target Graphic and navigation, and add and remove runtime listeners symmetrically | Multiple child Raycast Targets for one control, duplicate listeners, or visual-only disabled states |
| `EventSystem` and `GraphicRaycaster` | Routing pointer, touch, submit, cancel, and navigation events | Keep one EventSystem and one active matching input module; use `InputSystemUIInputModule` with the Input System; enable raycast only where required | A second EventSystem in additive scenes or physics blocking checks when they are unnecessary |

## Sliced Images And Sprite Borders

- Before setting `Image.type = Image.Type.Sliced`, inspect the Sprite actually displayed, including `overrideSprite` when present, and read its `Sprite.border`. An all-zero border does not produce nine-slicing; selecting Sliced alone is insufficient. Do not leave `This Image doesn't have a border.` unresolved and claim that the frame is correctly sliced.
- Choose border insets from the source artwork in pixels, preserving its rounded corners, outlines, and other non-stretchable edge details while leaving a usable stretchable center. `Vector4` order is **left, bottom, right, top** (`x, y, z, w`). Do not invent arbitrary nonzero values just to suppress the warning. Zero on some sides can be intentional for one-axis stretching; not every design needs four positive insets.
- Configure Border in Sprite Editor and Apply, or use the matching Sprite import API: for Single mode, set `TextureImporter.spriteBorder` and call `SaveAndReimport`; for Multiple mode, edit only the intended sub-sprite's border metadata, preserving its rect, name, IDs, and other slices. For atlased sprites, edit the source Sprite metadata, not the packed atlas texture or atlas padding.
- Check other consumers before changing a shared Sprite's border. Reuse a suitable existing bordered Sprite when possible; if the art is unsuitable or cannot safely be changed, report the limitation and choose an appropriate Image type or asset within the task scope. Do not replace the whole prefab or silently alter unrelated UI to resolve a border warning.
- After Apply or reimport, reacquire the displayed Sprite and read back `Sprite.border` and `Image.type`; an importer assignment alone is not verification. Resize at the intended minimum and representative target sizes and aspect ratios, then inspect the result: corners retain their shape, edge thickness is consistent, and the center has no seams or clipping. `pixelsPerUnitMultiplier` changes border sizing, not the slicing insets, and cannot replace missing borders.

## Canvas And Layering

- Use a small number of semantic layers such as Background, Screen, HUD, Overlay, Modal, Loading, and Debug. Make each layer a full-stretch RectTransform and define sibling or sorting order once.
- Preserve the design's full-bleed backgrounds and edge spacing. Reuse an existing safe-area container when the project already uses one; do not automatically insert a SafeAreaRoot or additional margins into a page prefab.
- When Modal or Loading UI is visible, block gameplay input explicitly; a visible scrim alone does not prove input is blocked.
- Keep one stable scrim per popup layer when a popup stack owns it. Restore the previous popup and its input state when the top popup closes.
- Distinguish Hide from Close. Hide can retain a cached instance; Close must release instantiated assets, handles, listeners, and transient state.
- Split static and frequently changing UI only when profiling shows rebuild cost. Nested canvases isolate rebuilds but prevent batching across canvas boundaries, so do not create one Canvas per widget.

## Canvas Scaler And RectTransform Rules

- Choose the Canvas Scaler reference resolution in this order: explicit user requirements or design specifications first; otherwise preserve an existing project's established baseline, including when adding a new page. Do not silently rescale the project's Canvas to match a newly supplied image.
- For a new portrait project with no specified reference resolution, inspect the supplied full-page design image and use its actual pixel width and height as the design coordinates. Only when no design image is supplied, default to `1080 x 2340`. This fallback is not a landscape default and does not override an existing project's baseline.
- If design images have conflicting page sizes, show only a cropped region, or do not establish the full-page dimensions, ask which page size or source artboard to use; do not infer the Canvas size from a thumbnail or silently apply the no-image fallback. Resolve conflicting explicit requirements before changing the affected layout.
- Record the chosen resolution and its source. Reference resolution defines design coordinates, not a fixed device or Game View resolution; continue validating other aspect ratios and safe areas.
- Start with Scale With Screen Size and Match `0.5` when width and height are equally important, then verify. Move Match toward width when horizontal design width must remain stable, or toward height when vertical design height must remain stable.
- Use anchors to express attachment: top bars to top stretch, bottom actions to bottom or bottom stretch, edge buttons to their corner, and center gameplay viewports to stretch between reserved regions.
- Set anchors before recording offsets. With separated anchors, `sizeDelta` is the delta relative to the anchor rectangle, not the final absolute size.
- Resize UI through RectTransform width, height, anchors, and offsets; leave localScale at one. Animate a child named Visual or Container when the root is layout-driven.
- When maintaining required dynamic layout code, respond to `OnRectTransformDimensionsChange` or an equivalent resolution and orientation signal for calculated page widths or aspect branches. Prefer authored anchors and offsets when they suffice; do not add a layout script by default or poll and rewrite every RectTransform every frame.
- Prefer `LayoutRebuilder.MarkLayoutForRebuild` for deferred updates. Use `Canvas.ForceUpdateCanvases` or `LayoutRebuilder.ForceRebuildLayoutImmediate` only when code must measure the final layout in the same operation, never as a routine per-frame fix.

## Safe Area

- Do not proactively write safe-area adaptation scripts, add a SafeAreaRoot, or apply extra edge insets merely because this is mobile UI. Assemble the page prefab with the design's intended spacing first; do not double-inset or shrink a design that already reserves sufficient space.
- Preserve and reuse the project's existing adaptation where applicable. This default is not permission to remove an existing component, change its behavior, or change platform settings.
- Check representative cutouts and system bars without adding adaptation code: top and bottom in portrait, and both sides in landscape. Design spacing may already be sufficient, but matching one design image does not establish support for every device. If an actual overlap remains, report the affected device, region and control, then confirm the desired fix before introducing new adaptation unless the user already requested it.
- Only when implementing explicitly requested or confirmed safe-area adaptation, read `Screen.safeArea`, convert both minimum and maximum corners to normalized anchors, guard zero screen dimensions, and update on relevant size or orientation changes. Inspect `PlayerSettings.Android.renderOutsideSafeArea` and the existing hierarchy to avoid applying the same inset twice; keep full-bleed art outside the adapted content.
- For that confirmed adaptation, measure nested edge content after layout using `RectTransformUtility.CalculateRelativeRectTransformBounds(parent, visualRoot)` when complete visual bounds are needed. Do not attach runtime clamping scripts to otherwise-correct authored pages by default.

## Portrait Mobile Pattern

- Organize the screen as Top, Center, and Bottom regions. Anchor persistent status and currency UI to Top; navigation, primary actions, and skill buttons to Bottom; stretch the game or page viewport through Center.
- Reserve top and bottom space with offsets on the stretched center viewport instead of giving the viewport a fixed height.
- Let additional height on tall phones expand the center region. Do not multiply every vertical coordinate by the screen aspect ratio.
- For horizontally paged home screens, compute each page from the current viewport width and recompute content width and selected-page position when dimensions change.
- Keep centered popup content within a safe maximum height. Use scrolling for localized or data-driven content that can exceed that height.

## Landscape Mobile Pattern

- Organize the screen as Left, Center, Right, plus stable corners. Put high-frequency gameplay content in Center and distribute controls so neither side becomes a single crowded column.
- Use 16:9 as a common gameplay baseline, but branch deliberately for ultrawide, 16:10, and 4:3 tablet layouts. Anchors handle attachment; a small aspect-aware layout policy handles genuine composition changes.
- Keep camera framing separate from Canvas scaling. A narrow landscape viewport may require a larger orthographic size or alternate camera composition to preserve world-space gameplay even when the Canvas itself is correct.
- Use background cover scaling or phone and tablet background variants when one crop cannot preserve the art direction across 16:9 and 4:3.
- Reposition only the controls whose composition genuinely changes at tablet aspect ratios. Do not fork the entire screen prefab when a few region offsets or constraints are sufficient.

## Auto Layout And Dynamic Content

- Remember the allocation order: minimum size, then preferred size, then flexible size. Use `LayoutElement` to state which sibling can consume extra space.
- A Layout Group drives its children. Do not manually edit a driven child position or size and expect it to persist after the next rebuild.
- A ContentSizeFitter drives its own RectTransform and expands around its pivot. Use a top pivot for content that must grow downward and a left pivot for content that must grow rightward.
- Do not put ContentSizeFitter on children whose RectTransforms are already controlled by the parent Layout Group. Disable Child Force Expand and use child layout input instead.
- GridLayoutGroup ignores child minimum, preferred, and flexible dimensions and assigns the configured fixed cell size. Use a different layout or custom controller for variable-sized grid cells.
- Keep layout nesting shallow. Repeated layout invalidation walks the hierarchy; batch model changes, update content, then request one rebuild.

## Images, Text, Scrolling, And Input

- For scalable button and panel frames, follow Sliced Images And Sprite Borders above before using Image Type Sliced. Keep ornamental children non-raycastable.
- Use Sprite Atlas for compatible UI sprites, platform-specific texture overrides, sensible maximum sizes, and no mipmaps for ordinary screen-space UI unless a measured use case needs them.
- Treat large full-screen images separately from small control atlases. Verify memory, compression artifacts, overdraw, and crop behavior on target hardware.
- Before adding a label, inspect representative UI prefabs and scenes rather than inferring the text system from package availability. Preserve the component type on existing labels and use the text component that is most common in the relevant project or screen family. If the project is new and has no established convention, default to `TextMeshProUGUI`.
- Do not opportunistically migrate `UnityEngine.UI.Text` to `TextMeshProUGUI`, or the reverse, while composing unrelated UI. Such a migration can change preferred sizes, wrapping, materials, fallback behavior, animation bindings, and serialized component references and requires separate validation.
- In TMP projects, use font fallback chains for CJK, symbols, and localized glyphs. Keep common glyphs in the primary asset and verify fallback material appearance and draw-call impact.
- Prefer wrapping, truncation, or a known layout expansion policy over broad Auto Size ranges. TMP Auto Size performs repeated layout passes and is unsuitable for frequently changing counters or timers.
- Do not add `Outline`, `Shadow`, or another `BaseMeshEffect` merely because a control looks unfinished. Require an explicit design need or a verified existing project style, prefer the existing shared prefab or TMP material preset when applicable, avoid stacked effects, and verify the extra geometry and overdraw.
- When the reference image or design explicitly shows an outline or another font effect and the project uses `TextMeshProUGUI`, reproduce the visible treatment through the TMP component rather than omitting it or attaching a uGUI mesh effect by habit. Use `outlineColor` and `outlineWidth` for a simple outline; use an existing approved TMP material preset, or a dedicated preset or material instance, for underlay or shadow, glow, face dilation, softness, and other shader effects.
- Treat TMP material scope as part of the edit. Do not modify a shared `fontSharedMaterial` when the effect is local to one label or prefab because every user of that material may change. Reuse a matching project preset when one exists; otherwise create a deliberately scoped preset or instance, preserve the font atlas and fallback chain, and verify the result against the reference at target resolution.
- Prefer a prefab for `TMP_InputField` and other stable controls so hierarchy, references, navigation, styling, localization, and focus behavior are inspectable before Play Mode. Runtime code should instantiate the prefab and supply data and listeners, not recreate its child objects and component wiring.
- Only when procedural construction is explicitly required, treat the first enable of a runtime-created `TMP_InputField` as an initialization boundary. Create and wire its text hierarchy while the root is inactive (or the component is disabled), assign at least `textComponent` and `textViewport` plus `placeholder` when used, then enable it. In affected TMP versions, `OnEnable` creates the cached `Caret` renderer only when `textComponent` is already bound; assigning the property after that first enable does not retroactively create it.
- If a dynamically constructed `TMP_InputField` accepts text but shows no insertion caret, inspect whether a `Caret` / `TMP_SelectionCaret` object was created and whether `textComponent` was assigned before first enable. After wiring the missing references, disable and re-enable the field to run initialization again; then verify focus, blinking caret, selection highlight, placeholder state, and editing in Play Mode.
- For a vertical ScrollRect, top-anchor the Content and set its pivot to the top so growth is predictable. Preserve the normalized position intentionally when refreshing content.
- Use RectMask2D for rectangular viewports. Use Mask only when the clipping shape must follow a Graphic.
- Give touch controls a project-defined minimum hit area even when the visible art is smaller. Use one transparent or visible root Graphic or raycast padding rather than making every child Image a target.
- Add listeners once and remove them in the matching lifecycle. Disable interaction while entrance or exit animation makes a control visually unavailable.

## Animation And Prefab Safety

- Animate a popup Container or Visual child while leaving the full-screen scrim stable. This avoids scaling the raycast blocker and avoids fighting layout-driven roots.
- Before replaying an animation, kill or cancel the prior sequence and restore a deterministic base position, scale, alpha, interactable, and blocksRaycasts state.
- Use unscaled time for menu, pause, modal, and loading animations that must continue while gameplay time is zero.
- Preserve existing prefab objects by default. Replacing an asset at the same path can preserve the asset GUID while still changing child or component file IDs, breaking animation bindings, serialized references, Prefab Variants, and Scene overrides.
- Prefer serialized references or stable binding components. Use `Transform.Find` only for a verified stable hierarchy and fail clearly if it is missing; never silently create an alternate hierarchy.
- Use semantic names such as TopBar, ContentViewport, BottomActions, Visual, and Label. Replace ambiguous default names only when doing so will not break bindings, and update references atomically.

## Performance And Validation

- Profile before restructuring. Common uGUI bottlenecks are overdraw, Canvas batch rebuilds, repeated layout rebuilds, raycast candidates, text mesh generation, and excess materials or textures.
- Separate mostly static UI from high-frequency counters, timers, scrolling content, or animations when profiling justifies the extra Canvas. Co-locate elements that change together.
- Disable Raycast Target on decorative Images and TMP text. An active Graphic Raycaster tests eligible Graphics and raycast filters along their hierarchy.
- Avoid hiding large inactive screens only with alpha zero; they can still render or receive input depending on CanvasGroup state. Use the project's hide or pooling policy and measure reopen cost.
- Validate portrait at 16:9, 19.5:9 or 20:9, a cutout phone, and a portrait tablet. Validate landscape at 16:9, ultrawide, 16:10, 4:3, and both cutout sides.
- In every profile, verify full-bleed art, safe interactive content, text overflow and fallback glyphs, scroll bounds, modal input blocking, touch hit areas, selection navigation, animation interruption, and close and reopen state.
- Use `capture_game_view` for static composition, text fit, and before/after comparisons. Use a short `record_game_view` clip for behavior a still image cannot establish, such as popup transitions, scroll inertia, caret blinking, interrupted animations, or repeated close and reopen actions; record only the sequence relevant to the change.
- For a clip, finish compilation, enter Play Mode and wait for MCP recovery, keep the Game tab visible at a fixed resolution, then start recording before performing the interaction. Save `data.recording_id`, pass it to `action=status` or `action=stop`, and wait for `data.ready=true` before reviewing the local MP4. The Unity MCP Workflow skill describes supported Editors and failure handling. If recording or video viewing is unavailable, report that limitation; screenshots and hierarchy readback alone do not prove timing or transition correctness.
- Review intermediate frames as well as the final state: look for clipping or layout jumps, stuck raycast blocking, input leaking through a modal, and interruption or reopen state. Pair the clip with component-state readback and actual input checks; a visual result alone cannot prove event routing. Recording is silent and adds overhead, so it cannot validate audio or replace Profiler and real-device performance checks.
- Use Device Simulator for layout, safe-area, orientation, and basic single-touch checks. It does not simulate target CPU, GPU, memory, rendering backend, native plugins, or multitouch; use representative device builds for performance and final interaction validation.

## Official Unity References

- [Canvas render modes and nesting](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/class-Canvas.html)
- [CanvasScaler API and Match behavior](https://docs.unity.cn/Packages/com.unity.ugui%402.0/api/UnityEngine.UI.CanvasScaler.html)
- [RectTransform](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/class-RectTransform.html) and [multi-resolution UI](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/HOWTO-UIMultiResolution.html)
- [Auto Layout](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/UIAutoLayout.html), [LayoutElement](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-LayoutElement.html), and [ContentSizeFitter](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-ContentSizeFitter.html)
- [ScrollRect](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-ScrollRect.html), [RectMask2D](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-RectMask2D.html), and [Mask](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-Mask.html)
- [Image](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-Image.html), [CanvasGroup](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/class-CanvasGroup.html), and [Selectable navigation](https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/script-SelectableNavigation.html)
- [Image.Type.Sliced and its border prerequisite](https://docs.unity3d.com/2018.4/Documentation/ScriptReference/UI.Image.Type.Sliced.html), [Sprite.border component order](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Sprite-border.html), and [TextureImporter.spriteBorder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureImporter-spriteBorder.html)
- [TextMeshPro UI text and Auto Size](https://docs.unity.cn/Packages/com.unity.textmeshpro%403.2/manual/TMPObjectUIText.html) and [fallback fonts](https://docs.unity.cn/Packages/com.unity.textmeshpro%404.0/manual/FontAssetsFallback.html)
- [Screen.safeArea](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-safeArea.html), [relative RectTransform bounds](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RectTransformUtility.CalculateRelativeRectTransformBounds.html), and [Device Simulator](https://docs.unity3d.com/6000.0/Documentation/Manual/device-simulator-introduction.html)
- [Sprite Atlas](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/atlas/create-sprite-atlas.html), [platform texture overrides](https://docs.unity3d.com/6000.0/Documentation/Manual/class-TextureImporter-type-specific.html), and [official uGUI optimization guide](https://learn.unity.com/course/introduction-to-ui-in-unity/tutorial/optimizing-unity-ui)


## Structured UI Workflows

- Short MCP tasks briefly wait for completion (wait_seconds defaults to 2; zero returns immediately). For longer tasks use `get_task(data.task.task_id, wait_seconds=20, after_revision=<last revision>)`; it waits for completion or a meaningful state change. Honor poll_after_ms on unchanged responses instead of making the model poll every second. wait_complete is not proof of success: inspect native status, errors and ready/complete/restoration fields. A read_timeout carries only the snapshot_at observation. Cancellation of the HTTP wait does not cancel the task. On lost preparation/preview responses recover through kind + the original request_key; do not replay mutations. Recording and Test Runner starts return immediately, as do preparation/preview starts without a recovery key.
- Inspect before modifying: use `find_game_objects` with component/property filters and projections, `inspect_ui_sprites` for Image/effective Sprite/importer/border/local-ID associations, and `find_project_types` for exact type and assembly names. Check ambiguity, partial errors, scan completeness and pagination; an incomplete scan is not proof of absence. Component setters distinguish live in-memory readback from saved/reimported prefab values.
- Run `audit_ui` on relevant live roots or saved prefabs/scenes; small scans can finish in one call, otherwise read status and finding pages through `get_task`. It checks missing borders, missing/required references, transparent raycast blockers, text/clipping and layout conflicts without fixing or saving assets. Review measured evidence and contextual warnings; suppress intentional exceptions only with an explicit project reason. Do not invent border values or infer design fidelity from a clean audit.
- Before creating new UI, read `get_ui_defaults`. `create_project_ui` can reuse templates and retain their prefab connection, label bindings, font/material and authored geometry. Explicit overrides take precedence; existing template component types are not converted. `configure_ui_defaults` changes project-scoped authoring preferences, so use it only when that shared convention is intended. Tied/incomplete convention scans or missing TMP resources require a deliberate choice/action, never a silent legacy fallback. This is Edit Mode authoring: save the intended scene/prefab explicitly and preserve existing UI when revising it.
- When preview management is exposed (Full by default), use `start_ui_preview_session` with verified prefab_paths and/or a project scene_template, optionally enter_play_mode and target width/height. It needs saved clean original scenes and no open Prefab Stage; do not save/discard unrelated user work merely to satisfy this precondition. Retain session_id and data.task.task_id; use `get_task` until ready. Business data and initialization remain project-specific; entering the scene may run lifecycle code.
- End the matching session with `end_ui_preview_session`, then inspect scenes_restored, view_restored, selection_restored, assets_cleaned and warnings. Do not claim full restoration from a success envelope. Changed scene setup, dirty preview or modified temporary scene requires inspection; discard_preview_changes applies only to the owned preview scene and must reflect an intended discard. Network/save-game effects and source asset edits are not rolled back. Preserve user-created files and changed window choices; report recovery still needed.
- Use screenshot `geometry`, not an unrelated `Screen` size: render size and returned image size can differ. Pass coordinate_space=image_pixels, origin=top_left and a fresh capture_id to click/drag/scroll or `raycast_at_point` when measuring a screenshot. `get_object_screen_bounds` and `get_visual_coordinates` share the mapping. Expired IDs or changed mode/view/scene/camera viewport/render dimensions require a fresh capture, not clamping or guessing. Geometry validity does not prove animated content stayed unchanged.


## Metadata

- Original skill id: `unity-ui-composition`
- Skill version: `1.0.9`
- Platform: `claude`
- Source repository: `https://github.com/FunplayAI/funplay-unity-mcp`
