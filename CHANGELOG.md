# Changelog

## 0.1.0

- Initial standalone FIGUNITY Unity Package Manager package.
- Added Figma constraint mapping to Unity anchors with diagnostics and imported-node metadata.
- Added editable stroke support for solid rectangle, rounded-rectangle, and ellipse primitives.
- Added `figunity:*` manual overrides, importer decision reasons, and recognition highlights in diagnostics.
- Added a visual Importer Panel with Figma panel discovery, highlighted selection, Import Selected, Import All, and an embedded Settings tab.
- Added a Unity Editor import flow that calls the external `figma-console-mcp` server.
- Added in-place selected element updates from the latest payload, including an Inspector `Update` button on imported nodes.
- Fixed rasterized glyph/icon text nodes being rebuilt as TMP text instead of their exported `RawImage` asset.
- Added configurable Figma frame export to `Assets/FIGUNITY/Imports`.
- Added generic UGUI prefab generation with TextMeshPro text, Image/RawImage visuals, Buttons, and read-only Slider meters.
- Added a neutral sample frame configuration.
- Added importer metadata, mask hints, auto-layout hints, control hints, repeated-node hints, and raster scale configuration.
- Added rounded-rectangle graphics for simple scalable panels.
- Added Toggle, TMP_InputField, TMP_Dropdown, ScrollRect, active Slider, ToggleGroup/tab, and passive meter generation.
- Added diagnostics report output, repeated-item prefab extraction, visual-diff helper, test Figma menu generator, and test scene builder.
