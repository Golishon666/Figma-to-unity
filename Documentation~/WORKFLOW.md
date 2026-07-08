# FIGUNITY Workflow

## Pipeline

1. Unity starts `Tools/figunity-export.mjs`.
2. The worker starts the installed `figma-console-mcp` process.
3. The worker waits for `figma_get_status` with `probe: true`.
4. The worker calls `figma_execute` once per requested frame.
5. Figma returns a structured tree, raster exports, and a screenshot.
6. The worker writes:
   - `Assets/FIGUNITY/Imports/payload.json`
   - `Assets/FIGUNITY/Imports/summary.json`
   - `Assets/FIGUNITY/Imports/Screenshots/*.png`
   - `Assets/FIGUNITY/Imports/ElementAssets/<frame>/*.png`
7. Unity refreshes assets and rebuilds preview prefabs into `Assets/FIGUNITY/Prefabs`.
8. Unity writes diagnostics, repeated-item prefab candidates, and an optional test scene.

## External MCP Dependency

FIGUNITY depends on the external MCP server:

- GitHub: https://github.com/southleft/figma-console-mcp
- npm: https://www.npmjs.com/package/figma-console-mcp
- docs: https://docs.figma-console-mcp.southleft.com

The package does not copy or modify that server. It invokes the installed command and uses the MCP tool API.

## Payload Contract

`payload.json` has a stable shape:

```json
{
  "source": "figunity via figma-console-mcp",
  "expectedFileName": "My UI File",
  "currentFileName": "My UI File",
  "port": "9225",
  "frames": []
}
```

Each frame includes:

- Figma identifiers: `key`, `slug`, `frameId`, `frameName`
- dimensions: `width`, `height`, `rootBounds`
- counters: `nodeCount`, `textCount`, `visualCount`
- hierarchy: `tree`
- exported visual assets: `exports`
- reference screenshot: `screenshotPath`

Each node includes bounds, fills, strokes, text data, render mode, children, and an optional Unity asset path for rasterized visuals.

## Unity Conversion

FIGUNITY converts:

- `renderMode: "text"` to `TextMeshProUGUI`
- simple solid nodes to `Image`
- simple rounded solid nodes to `FigunityRoundedRectGraphic`
- exported visual/background nodes with `assetPath` to `RawImage`
- nodes whose names contain `button` to `Button`
- groups with `Track` and `Fill` children to read-only `Slider`
- active slider names to interactable `Slider`
- `clipsContent` to `RectMask2D`
- Figma mask nodes to `Mask`
- Figma auto-layout metadata to `HorizontalLayoutGroup` or `VerticalLayoutGroup`
- scroll hints to `ScrollRect`
- toggle, input, dropdown, and tab hints to their closest UGUI/TMP controls

The generated prefabs are preview/editing artifacts. Production binding should stay in the consuming game project.

## Project Notes

Copy `Samples~/Frame Config/figunity.frames.json` into:

```text
Assets/FIGUNITY/figunity.frames.json
```

Then set `Project Settings > FIGUNITY`:

- `FIGMA_WS_PORT`: `9225`
- `Expected File Name`: your exact Figma file name

Run:

```text
Tools/FIGUNITY/Export And Rebuild
```

Keep project-specific production prefab replacement and runtime binding code in the consuming Unity project.

## Test Menu Workflow

Run:

```text
Tools/FIGUNITY/Tests/Create Export Rebuild And Build Test Scene
```

This creates neutral Figma test menus through `figma-console-mcp`, exports them, rebuilds prefabs, extracts repeated card prefabs, writes diagnostics, and creates:

```text
Assets/FIGUNITY/TestScene/FigunityTestMenus.unity
```
