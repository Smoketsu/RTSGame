<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-ui-composition@1.0.9 -->

# Choose The Existing UI Framework

Read this before applying the Canvas component guidance to an unfamiliar or mixed-framework project.

- Classify the target screen, not the whole repository: uGUI uses `Canvas`, `RectTransform` and `Graphic`; UI Toolkit uses `UIDocument`, UXML, USS and `VisualElement`; IMGUI uses `OnGUI` / `EditorGUI` / `GUILayout`. Runtime and Editor tooling can use different systems in the same project.
- Follow the target's existing framework and bindings. Do not convert a working screen, install a package, or wrap UI Toolkit in a new Canvas to make uGUI instructions fit. If a new screen's framework is genuinely undecided, explain the project-compatible choices and clarify a consequential choice.
- The component table, prefab-first authoring, layout audits and raycast advice in the main skill primarily describe uGUI. Do not claim that a uGUI audit covers UI Toolkit or IMGUI. Inspect the tool's supported scope; use scoped public Editor APIs through MCP for actual gaps.

## UI Toolkit

Reuse project UXML/USS, reusable visual-tree templates, PanelSettings, styles and data/event conventions. Author stable structure in those assets rather than rebuilding it procedurally; add runtime behavior only when the task requires it. UXML/USS/C# are source files and can use normal repository tools; Unity-owned PanelSettings `.asset` edits still require Unity APIs.

Check the installed Unity/package version before assuming support for runtime data binding, controls, style properties or UI Builder features. USS is not browser CSS: verify supported properties/selectors rather than importing web layout rules unchanged. UI Toolkit text uses the version's TextCore/TextSettings system, not automatically TMP font assets. Inspect flex layout, resolved styles, clipping, focus, picking and panel scaling in the live panel. CanvasScaler, Image.Type.Sliced, RectMask2D and GraphicRaycaster settings are not UI Toolkit fixes. Test the requested input and rendered states in the appropriate panel, not just UXML syntax.

## IMGUI and Editor UI

Preserve the existing EditorWindow/Inspector framework; IMGUI redraws its layout during GUI events and is not an authored mobile page prefab. Keep event handling and layout balanced, avoid asset writes on every repaint, and verify changes in the actual window. Use Undo/serialized properties for intentional edits. Do not introduce a runtime IMGUI interface merely to avoid building a requested uGUI page.

## Sources and adaptation

Original Funplay guidance informed by the official plugin 0.1.8-beta, with framework routing adapted to MCP and the existing project's conventions:

- [UI routing](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/ui)
- [UI Toolkit](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/ui-uitk)
- [IMGUI](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/ui-imgui)
