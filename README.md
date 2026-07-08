# FIGUNITY

FIGUNITY is a Unity Editor package for downloading Figma frames through an MCP transport and rebuilding them as editable Unity UGUI prefabs.

It is intentionally small:

- it calls the external Figma MCP server;
- it exports a JSON payload and PNG assets into `Assets/FIGUNITY/Imports`;
- it converts the payload into Unity prefabs in `Assets/FIGUNITY/Prefabs`;
- it keeps text as `TextMeshProUGUI`;
- it converts obvious progress/capacity/slider groups into passive `UnityEngine.UI.Slider` components;
- it uses `Image` for simple solid panels and `RawImage` for exported raster/vector visuals.
- it creates masks, layout groups, scroll views, toggles, inputs, dropdowns, tabs, active sliders, diagnostics, and repeated-item prefab candidates when the Figma structure exposes those hints.

## Figma MCP Server

FIGUNITY uses Southleft's Figma Console MCP server as the Figma transport:

- GitHub: [southleft/figma-console-mcp](https://github.com/southleft/figma-console-mcp)
- npm: [figma-console-mcp](https://www.npmjs.com/package/figma-console-mcp)
- docs: [docs.figma-console-mcp.southleft.com](https://docs.figma-console-mcp.southleft.com)

FIGUNITY does not vendor that MCP server. The Unity menu starts the locally installed `figma-console-mcp.cmd`/`figma-console-mcp` process and uses its `figma_get_status` and `figma_execute` tools over stdio JSON-RPC.

## Install

1. Install Node.js 18 or newer.
2. Install the MCP package:

   ```powershell
   npm install -g figma-console-mcp
   ```

3. Start the MCP server once so it writes the Figma Desktop Bridge plugin files:

   ```powershell
   $env:FIGMA_WS_PORT = "9225"
   figma-console-mcp
   ```

4. In Figma Desktop, import the bridge manifest from the stable path shown by the MCP server. On Windows it is usually:

   ```text
   C:\Users\<you>\.figma-console-mcp\plugin\manifest.json
   ```

5. Add this package to Unity Package Manager:

   ```text
   https://github.com/Golishon666/Figma-to-unity.git
   ```

## Unity Workflow

Use the Unity menu:

- `Tools/FIGUNITY/Export From Figma via figma-console-mcp`
- `Tools/FIGUNITY/Rebuild Prefabs From Payload`
- `Tools/FIGUNITY/Export And Rebuild`
- `Tools/FIGUNITY/Tests/Create Test Menus In Figma`
- `Tools/FIGUNITY/Tests/Create Export Rebuild And Build Test Scene`

Default paths:

- frame config: `Assets/FIGUNITY/figunity.frames.json`
- payload: `Assets/FIGUNITY/Imports/payload.json`
- screenshots: `Assets/FIGUNITY/Imports/Screenshots`
- element PNGs: `Assets/FIGUNITY/Imports/ElementAssets`
- generated prefabs: `Assets/FIGUNITY/Prefabs`
- repeated item prefabs: `Assets/FIGUNITY/Prefabs/Repeated`
- diagnostics: `Assets/FIGUNITY/Imports/diagnostics.md`
- test scene: `Assets/FIGUNITY/TestScene/FigunityTestMenus.unity`

If `Assets/FIGUNITY/figunity.frames.json` exists, FIGUNITY exports those frames by name or node id. If the config is missing, it exports the current Figma selection and writes it as `selected-node`.

## Frame Config

Create `Assets/FIGUNITY/figunity.frames.json`:

```json
{
  "expectedFileName": "My UI File",
  "frames": [
    {
      "key": "survivors",
      "slug": "06-survivors-elementwise",
      "name": "06 Survivors Elementwise"
    }
  ]
}
```

Supported frame fields:

- `key`: stable lookup id used in `payload.json`.
- `slug`: filesystem-safe folder/prefab name.
- `name`: exact Figma frame name.
- `nodeId`: direct Figma node id; this wins over `name`.
- `optional`: if `true`, missing frames are skipped.
- `selection`: if `true`, export the first currently selected Figma node.

The package includes a neutral sample under `Samples~/Frame Config/figunity.frames.json`.
It also includes `Samples~/Frame Config/figunity-test-menus.frames.json` for the generated test menus.

## Test Menus

Open a Figma file in Figma Desktop, run the Figma Console MCP Desktop Bridge plugin, then use:

```text
Tools/FIGUNITY/Tests/Create Export Rebuild And Build Test Scene
```

That menu creates three Figma frames named:

- `FIGUNITY Test Main Menu`
- `FIGUNITY Test Mask Menu`
- `FIGUNITY Test Scroll Menu`

Then it installs the test frame config, exports through `figma-console-mcp`, rebuilds prefabs, writes diagnostics, extracts repeated-card prefabs, and creates the Unity scene configured in `Project Settings > FIGUNITY`.

## Legal And Attribution Notes

The package does not include code from commercial Unity/Figma converter plugins. It is a standalone Unity importer plus a Node worker that calls `figma-console-mcp`, which is MIT licensed by Southleft. Keep attribution and the external MCP link in documentation if this package is redistributed.

## Current Scope

FIGUNITY is a generic importer, not a project-specific production binder. It generates editable preview prefabs. Project-specific runtime binding, replacing existing screen prefabs, or wiring presenter fields should live in the consuming Unity project.

More details are in `Documentation~/WORKFLOW.md`.
