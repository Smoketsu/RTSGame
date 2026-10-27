<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-ui-composition@1.0.9 -->

# Sprite Importer Safety

Read this when changing nine-slice borders, pivots, slicing or shared Sprite imports. Setting an Image to Sliced alone is not a border repair.

## Identify the source and scope

- Inspect the Image's effective Sprite, its source asset and importer, Single/Multiple mode, packing/atlas membership, import scale and other consumers. An atlas texture is not the source to edit. Verify the exact sub-sprite identity, not just a duplicate display name.
- Derive border insets from the source artwork's non-stretchable corners and edges. Sprite Editor rectangles and borders use source-pixel coordinates; imported texture dimensions may be reduced by platform/max-size settings, and packed atlas coordinates may be rotated. Do not copy dimensions from a screenshot or the runtime packed texture into importer data without resolving this distinction.
- Preserve names, pivots, existing slice rectangles and identities unless the requested change needs them. Explain shared-asset effects before a change that would alter unrelated consumers; prefer an explicitly scoped variant when appropriate.

## Use the public importer API for the installed version

- For a verified Single Sprite TextureImporter, `spriteBorder` / supported importer properties may suffice. For Multiple sprites, layered source importers or slicing, inspect availability of the Sprite Editor Data Provider API in the installed 2D Sprite/importer version. Do not assume all importers support TextureImporter or silently convert their import mode.
- Where supported, initialize `SpriteDataProviderFactories` and the selected `ISpriteEditorDataProvider`, read its existing SpriteRects, and update only the intended entries. Preserve each surviving `spriteID`. Keep `ISpriteNameFileIdDataProvider` name/ID mappings consistent for supported versions when adding, removing or renaming slices; a border-only change must not generate new IDs or replace every mapping.
- Check the importer's permission for the actual edit, not merely that a provider exists. Where the installed API exposes `ISpriteFrameEditCapability`, require the matching border/pivot/rectangle/name or create/delete capability. A denied or required-but-missing capability stops the edit; do not override importer locks. Older versions need their documented supported edit path, not an assumed newer interface.
- Apply provider changes, reimport through its AssetImporter, then reacquire the imported Sprites. If the provider/API is absent, report the exact capability gap and stop that edit; do not patch `.meta`, use internal reflection, or rebuild slices as a fallback.

## Verify the saved result

Read the imported border, source identity and prefab/scene references again after reimport. Verify no missing or switched sprites, and resize the actual Sliced Image in the intended UI to check corners, edges and center. Restore any temporary validation size. Nonzero borders alone do not establish correct artwork, and an import success message does not prove reference preservation.

## Sources and adaptation

- [Official plugin 0.1.8-beta Sprite Editor guidance](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/sprite-editor)
- [Unity Sprite Editor Data Provider API](https://docs.unity3d.com/Packages/com.unity.2d.sprite@1.0/manual/DataProvider.html) (use the manual matching the installed package)
