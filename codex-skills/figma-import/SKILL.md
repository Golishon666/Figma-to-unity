---
name: figma-import
description: "Elementwise Figma-to-Unity UI import workflow. Use when Codex needs to export or rebuild a Figma UI screen, frame, HUD, dashboard, menu, panel, or modal in Unity as real UI objects: text as TextMeshPro text, progress bars as Unity Sliders, buttons/tabs/cards as editable hierarchy, and raster Figma art only for actual visual/image layers. Trigger on requests like 'export Figma to Unity', 'import this Figma UI', 'not a screenshot', 'text must be text', 'progress bars should be sliders', or 'make a normal Unity hierarchy'."
---

# Figma Import

## Core Rules

Import the UI as editable Unity hierarchy, not as a flattened screenshot. A full-frame Figma export may be used only as a reference, verification target, or true raster background when the Figma design intentionally contains a single raster image.

Keep text as `TextMeshProUGUI`. Preserve the exact string, font choice, size, alignment, color, wrapping, and line spacing as closely as Unity allows. Create or reuse a TMP font asset for the Figma font instead of rasterizing labels.

Convert progress bars, capacity bars, and meter fills into `UnityEngine.UI.Slider`. Use the Figma fill width to set `value`, assign `fillRect`, and hide/remove the handle when the design is a passive bar. Leave the `Slider` component real even if the bar visually looks static.

Use raster assets only for actual icons, illustrations, textured panels, photos, decorative borders, and other visual layers that are not text or standard controls. Export them at sufficient resolution, import without damaging compression, and avoid low-resolution screenshots.

Build a semantic hierarchy. Do not leave hundreds of flat or Figma-generated names. Use role-based groups and names such as `Header`, `ResourceBar`, `ResourceCell_Food`, `LeftColumn`, `Panel_CampStatus`, `StatusRows`, `StatusRow_Morale`, `Slider_Morale`, `MainContent`, `Panel_CampOverview`, `RightColumn`, `ActionCards`, `Button_ViewUpgrade`, `BottomNavigation`, and `NavTab_Camp`.

Preserve visual stacking order. When regrouping objects, keep sibling order equivalent to Figma back-to-front order and verify the result with a Unity screenshot.

## FIGUNITY Importer

Use the standalone FIGUNITY package for Figma-to-Unity imports before hand-rolling a one-off hierarchy. FIGUNITY lives in the GitHub repo `https://github.com/Golishon666/Figma-to-unity` and should be connected through Unity Package Manager as `com.golishon666.figunity`.

FIGUNITY uses the external Figma Console MCP server as the transport:
- GitHub: `https://github.com/southleft/figma-console-mcp`
- npm: `https://www.npmjs.com/package/figma-console-mcp`
- docs: `https://docs.figma-console-mcp.southleft.com`

Expected transport:
- command: `figma-console-mcp.cmd` on Windows or `figma-console-mcp` elsewhere
- port: `FIGMA_WS_PORT=9225` unless the project explicitly changes it
- Figma file check: set the expected file name in `Project Settings > FIGUNITY` when the workflow should guard against the wrong file
- required tools: `figma_get_status` and `figma_execute`

FIGUNITY package paths:
- Unity menu: `Tools/FIGUNITY/Importer Panel`
- Unity menu: `Tools/FIGUNITY/Export From Figma via figma-console-mcp`
- Unity menu: `Tools/FIGUNITY/Rebuild Prefabs From Payload`
- Unity menu: `Tools/FIGUNITY/Export And Rebuild`
- Test menu: `Tools/FIGUNITY/Tests/Create Test Menus In Figma`
- Test menu: `Tools/FIGUNITY/Tests/Create Export Rebuild And Build Test Scene`
- frame config: `Assets/FIGUNITY/figunity.frames.json`
- payload/assets: `Assets/FIGUNITY/Imports/`
- generated prefabs: `Assets/FIGUNITY/Prefabs/`
- diagnostics: `Assets/FIGUNITY/Imports/diagnostics.md`
- test scene: `Assets/FIGUNITY/TestScene/FigunityTestMenus.unity`
- sample config in the package: `Samples~/Frame Config/figunity.frames.json`

FIGUNITY handles the core conversion rules: `TEXT` to `TextMeshProUGUI`, simple solid or stroked panels to editable FIGUNITY graphics, complex/vector/image/background nodes to exported PNG `RawImage`, `Button - ...` frames to `Button`, Figma meter groups with `Track`/`Fill`/`Handle` to passive or active `UnityEngine.UI.Slider`, `clipsContent` and mask nodes to Unity masks, Figma constraints to Unity anchors, Figma auto-layout hints to layout groups, and common toggle/input/dropdown/scroll/tab controls to UGUI/TMP components.

Use `figunity:*` tags in a Figma node name or description when automatic detection is ambiguous. Supported overrides include `figunity:raw`, `figunity:raster`, `figunity:image`, `figunity:container`, `figunity:background`, `figunity:visual`, `figunity:text`, `figunity:ignore`, `figunity:button`, `figunity:toggle`, `figunity:input`, `figunity:dropdown`, `figunity:scroll`, `figunity:tab`, `figunity:slider`, `figunity:passive-slider`, `figunity:mask`, `figunity:no-mask`, `figunity:no-control`, `figunity:no-slider`, and `figunity:no-meter`. The importer strips these tags from Unity object names, preserves them in `FigunityImportedNode`, and writes decision reasons into diagnostics.

Use `Tools/FIGUNITY/Importer Panel` when the user needs a visual import workflow. The panel reads exportable Figma panels through `figma-console-mcp`, highlights selected rows, supports `Import Selected` and `Import All`, writes a temporary frame config under the import folder, then runs the same export and prefab rebuild pipeline. Its `Settings` tab edits the same `FigunitySettings.asset` values as `Project Settings > FIGUNITY`.

When extending a project-specific Figma import:
1. Prefer `Tools/FIGUNITY/Importer Panel` for selecting and importing multiple Figma panels visually; use `Assets/FIGUNITY/figunity.frames.json` for repeatable scripted imports.
2. Use FIGUNITY to export payload and generate preview prefabs.
3. Add project-specific runtime bindings in the Unity project, not inside FIGUNITY.
4. Preserve dirty-tree changes outside the requested importer/generated prefab scope.
5. Verify with Unity MCP: refresh/compile, run the import/rebuild menu, check console errors, run focused EditMode tests, and capture a screenshot under `Assets/Screenshots/` when visual output changes.

## Workflow

1. Inspect the Figma frame or exported node data before building. Identify frames, text nodes, image/vector layers, masks, repeated cells, bars, buttons, tabs, and panels.
2. Export individual raster layers or grouped visual assets only where Unity should use an image. Keep transparent PNGs for icons/cards/panels that need alpha.
3. Create or update a Unity prefab and preview scene. Use a `Canvas`, `CanvasScaler`, `GraphicRaycaster`, a camera if needed, and an `EventSystem` when sliders/buttons exist.
4. Rebuild the layout element by element with `RectTransform` anchors, positions, sizes, masks, images, TMP text, sliders, and buttons following the Figma coordinates.
5. Normalize hierarchy and naming after import. Group by UI meaning, not by export implementation detail. Strip names like `[background]`, `[visual]`, duplicate `Text_Text*`, and raw Figma frame noise.
6. Verify in Unity. Check console errors/warnings, count core component types, and capture a Game View screenshot. Compare visually against the Figma reference before reporting done.

## Unity MCP Expectations

When Unity MCP is available, use the Unity MCP skill/workflow. Read `mcpforunity://custom-tools` and `mcpforunity://editor/state` before mutating the project, then use editor tools for prefab/scene changes, screenshots, and console checks.

After any script, prefab, asset, or scene mutation, check the Unity console. If scripts are created or changed, wait for compilation to finish before relying on new types.

For final verification, report the main prefab/scene paths, component counts such as sliders/TMP texts/images, console status, and the screenshot path.

## Quality Bar

Do not claim completion if the UI is just a screenshot, text is rasterized, bars are images instead of sliders, or the hierarchy is still flat/noisy.

If exact Figma data is unavailable, state what was inferred and still preserve the editable Unity structure. Prefer a slightly imperfect editable reconstruction over a visually convenient flattened image.
