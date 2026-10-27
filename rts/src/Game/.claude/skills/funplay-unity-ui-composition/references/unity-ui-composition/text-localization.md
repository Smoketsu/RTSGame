<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-ui-composition@1.0.9 -->

# TMP Fonts, Effects And Localization

Read the relevant section for font creation/repair, missing glyphs, text performance or requested localization. Ordinary UI work is not authorization to localize the project or replace legacy Text.

## Font assets and visual effects

- Follow the existing Text/TMP convention and font/material presets. Check TMP settings/resources and actual loaded APIs before font creation; if required resources are missing, locate the installed package's supported import asset and verify completion. Do not assume a menu command that opened a modal dialog completed an import, or import Examples & Extras as a routine prerequisite.
- Reproduce reference-visible TMP effects with component settings or a scoped material preset. Reuse the font atlas rather than duplicating font assets for styles. Check shader compatibility, SDF padding, fallback font weight/baselines and effect clipping at the target scale; do not mutate a shared material for a local change.
- Choose static/dynamic atlases, fallbacks or locale-specific font swaps from glyph coverage, the project's localization system, target devices and measured memory. Neither a universal CJK fallback ban nor mandatory font swapping is appropriate. Use licensed project fonts; system availability does not imply redistribution rights or reliable coverage on every device.
- For dynamic fonts inspect atlas growth, multi-atlas behavior and supported clear-on-build options. Evaluate autosizing and Canvas rebuild cost for frequently changing text before altering them. Do not prescribe fixed atlas sizes, font metrics, one Canvas per counter or OS-font modes across all Unity/TMP versions.
- When creating a TMP font asset through public APIs, persist any newly created material and atlas sub-assets as required by that version, without re-adding already persistent assets. Save and inspect `LoadAllAssetsAtPath`, material/texture links and a fresh load after reimport. An in-memory non-null material does not prove it was saved. Verify representative glyphs and effects after reopening the prefab.

## Localization coverage and bindings

1. Confirm the requested languages, existing localization solution and loaded package version. Do not replace project services or install Localization/Addressables automatically. Read the package-readiness guidance in the workflow skill if setup is required.
2. Inventory both authored legacy `Text` and `TMP_Text` across the requested scenes/prefabs, including inactive content. Separately inspect code-created strings, interpolated assignments, `SetText`, constants and project formatters; a component scan cannot find them all. Report scanned scope and unconverted sites with reasons.
3. Map table data by locale identifier, not enumeration/array order. Preserve stable keys, GUIDs, variables, pluralization and project-specific bindings. Never populate missing translations with the base language merely to make a completeness check pass; report intentional exceptions and unresolved gaps.
4. For Unity Localization, use public `LocalizeStringEvent` and persistent UnityEvent APIs when authored bindings are needed. Preserve unrelated listeners, avoid duplicates and read back target, method and call state. Use `EditorAndRuntime` only when authoring preview is intended; a runtime-only binding can be correct for runtime. Refresh the binding and verify the actual label changes after a save/reload. Do not edit private persistent-call fields or invoke internal localization plug-ins.
5. Check required key/locale pairs for missing or empty values and count what was examined. A zero-entry scan is inconclusive, not complete. For localized assets verify references and the existing Addressables build flow when applicable; avoid moving assets or rebuilding all content outside the requested scope.
6. Preview via the existing localization system, and test runtime switching when requested. Verify missing glyphs, fallback/style continuity, line breaks, clipping, long strings, RTL/shaping where relevant and reference fidelity in each requested locale. Preserve layout ownership rather than adding ContentSizeFitter to already driven children. Restore the original preview locale and editor state after verification.

## Sources and adaptation

Original Funplay guidance informed by the pinned official plugin 0.1.8-beta; its recommendations are filtered for project conventions, public APIs and Unity 2022.3 compatibility:

- [TMP optimization](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/optimize-text-mesh-pro)
- [Localization](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/localization)
- [TMP font properties](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/FontAssetsProperties.html) (check the installed version)
