import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, rmSync, writeFileSync, readFileSync } from "node:fs";
import { isAbsolute, join, relative, resolve } from "node:path";

const projectRoot = resolve(process.cwd());
const args = parseArgs(process.argv.slice(2));
const port = String(args.port || process.env.FIGMA_WS_PORT || "9225");
const outputTarget = args.out || process.env.FIGUNITY_OUT_DIR || "Assets/FIGUNITY/Imports";
const frameConfigTarget = args.frames || process.env.FIGUNITY_FRAMES || "Assets/FIGUNITY/figunity.frames.json";
const explicitExpectedFile = args.file || process.env.FIGUNITY_FILE_NAME || "";
const rasterScale = Math.max(1, Math.min(Number(args.rasterScale || process.env.FIGUNITY_RASTER_SCALE || 2), 4));
const { absolute: outDir, unity: unityOutDir } = resolveProjectAssetPath(outputTarget);
const screenshotsDir = join(outDir, "Screenshots");
const frameConfigPath = resolveMaybeProjectPath(frameConfigTarget);

const config = readFrameConfig(frameConfigPath);
const expectedFileName = explicitExpectedFile || config.expectedFileName || "";
const frameRequests = config.frames.length > 0
  ? config.frames
  : [{ key: "selection", slug: "selected-node", selection: true }];

mkdirSync(outDir, { recursive: true });
rmSync(screenshotsDir, { recursive: true, force: true });
mkdirSync(screenshotsDir, { recursive: true });

const mcpCommand = resolveMcpCommand();
const child = spawnMcp(mcpCommand);
let nextMessageId = 1;
let stdoutBuffer = "";
const inflight = new Map();

function parseArgs(values) {
  const parsed = {};
  for (let i = 0; i < values.length; i += 1) {
    const item = values[i];
    if (!item.startsWith("--")) continue;
    parsed[item.slice(2)] = values[i + 1];
    i += 1;
  }

  return parsed;
}

function resolveMaybeProjectPath(value) {
  if (isAbsolute(value)) {
    return value;
  }

  return join(projectRoot, value);
}

function resolveProjectAssetPath(value) {
  const absolute = resolveMaybeProjectPath(value);
  const unity = relative(projectRoot, absolute).replace(/\\/g, "/");
  if (!unity.startsWith("Assets/")) {
    throw new Error(`FIGUNITY output must be under Assets/: ${value}`);
  }

  return { absolute, unity };
}

function readFrameConfig(configPath) {
  if (!existsSync(configPath)) {
    return { expectedFileName: "", frames: [] };
  }

  const parsed = JSON.parse(readFileSync(configPath, "utf8"));
  return {
    expectedFileName: parsed.expectedFileName || "",
    frames: Array.isArray(parsed.frames) ? parsed.frames : [],
  };
}

function resolveMcpCommand() {
  if (process.env.FIGUNITY_MCP_CMD) {
    return process.env.FIGUNITY_MCP_CMD;
  }

  if (process.platform === "win32" && process.env.APPDATA) {
    const cmd = join(process.env.APPDATA, "npm", "figma-console-mcp.cmd");
    if (existsSync(cmd)) {
      return cmd;
    }
  }

  return "figma-console-mcp";
}

function spawnMcp(command) {
  const env = { ...process.env, FIGMA_WS_PORT: port, NO_COLOR: "1" };
  if (process.platform === "win32" && command.toLowerCase().endsWith(".cmd")) {
    return spawn("cmd.exe", ["/d", "/s", "/c", command], {
      env,
      cwd: projectRoot,
      stdio: ["pipe", "pipe", "pipe"],
      windowsHide: true,
    });
  }

  return spawn(command, [], {
    env,
    cwd: projectRoot,
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true,
  });
}

function rpc(method, params = {}, timeoutMs = 45000) {
  const id = nextMessageId++;
  child.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", id, method, params })}\n`);
  return new Promise((resolvePromise, rejectPromise) => {
    const timer = setTimeout(() => {
      if (!inflight.has(id)) return;
      inflight.delete(id);
      rejectPromise(new Error(`Timed out waiting for MCP method ${method}`));
    }, timeoutMs);

    inflight.set(id, { resolve: resolvePromise, reject: rejectPromise, timer });
  });
}

function notify(method, params = {}) {
  child.stdin.write(`${JSON.stringify({ jsonrpc: "2.0", method, params })}\n`);
}

function tool(name, toolArgs = {}, timeoutMs = 60000) {
  return rpc("tools/call", { name, arguments: toolArgs }, timeoutMs);
}

function parseToolText(result) {
  const text = result?.content?.find((item) => item.type === "text")?.text;
  if (!text) return null;

  try {
    return JSON.parse(text);
  } catch {
    return { rawText: text };
  }
}

child.stdout.on("data", (chunk) => {
  stdoutBuffer += chunk.toString("utf8");
  let newlineIndex;
  while ((newlineIndex = stdoutBuffer.indexOf("\n")) >= 0) {
    const line = stdoutBuffer.slice(0, newlineIndex).trim();
    stdoutBuffer = stdoutBuffer.slice(newlineIndex + 1);
    if (!line) {
      continue;
    }

    let message;
    try {
      message = JSON.parse(line);
    } catch {
      process.stderr.write(`[figunity:mcp] ${line}\n`);
      continue;
    }

    if (message.id && inflight.has(message.id)) {
      const pending = inflight.get(message.id);
      inflight.delete(message.id);
      clearTimeout(pending.timer);
      message.error ? pending.reject(new Error(JSON.stringify(message.error))) : pending.resolve(message.result);
    }
  }
});

child.stderr.on("data", (chunk) => process.stderr.write(chunk));

child.on("exit", (code) => {
  for (const [id, pending] of inflight) {
    inflight.delete(id);
    clearTimeout(pending.timer);
    pending.reject(new Error(`figma-console-mcp exited before request ${id} completed. Exit code: ${code}`));
  }
});

function stopMcp() {
  try {
    child.stdin.end();
  } catch {}

  if (process.platform === "win32" && child.pid) {
    spawnSync("taskkill.exe", ["/pid", String(child.pid), "/t", "/f"], { stdio: "ignore" });
    return;
  }

  try {
    child.kill("SIGTERM");
  } catch {}
}

async function waitForMcpDesktopLink() {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const status = parseToolText(await tool("figma_get_status", { probe: true }, 20000)) || {};
    const ready = status?.setup?.valid === true && status?.setup?.probeResult?.success === true;
    if (ready) {
      const fileName =
        status.currentFileName ||
        status.transport?.websocket?.connectedFile?.fileName ||
        status.transport?.websocket?.connectedFiles?.find((file) => file.isActive)?.fileName ||
        "";

      if (expectedFileName && fileName !== expectedFileName) {
        throw new Error(`Connected Figma file is "${fileName}", expected "${expectedFileName}".`);
      }

      return status;
    }

    await new Promise((resolvePromise) => setTimeout(resolvePromise, 1500));
  }

  throw new Error("figma-console-mcp Desktop Bridge did not connect. Start Figma Desktop and run its bridge plugin.");
}

function extractionProgram(request) {
  return `
await figma.loadAllPagesAsync();

const request = ${JSON.stringify(request)};
const rasterScale = ${JSON.stringify(rasterScale)};
let root = null;

if (request.selection) {
  root = figma.currentPage.selection && figma.currentPage.selection.length > 0 ? figma.currentPage.selection[0] : null;
}

if (!root && request.nodeId) {
  root = await figma.getNodeByIdAsync(request.nodeId);
}

if (!root && request.name) {
  for (const page of figma.root.children) {
    const found = page.findOne((node) =>
      node.type === "FRAME" &&
      ((node.name || "") === request.name || (node.name || "").endsWith("/ " + request.name))
    );
    if (found) {
      root = found;
      break;
    }
  }
}

if (!root) {
  if (request.optional) return { missing: true, key: request.key, slug: request.slug, requestedName: request.name, requestedNodeId: request.nodeId };
  throw new Error(request.selection ? "No Figma node is selected." : "Frame not found: " + (request.nodeId || request.name));
}

if (!root.absoluteBoundingBox) throw new Error("Node has no absoluteBoundingBox: " + root.name);

const rootBounds = root.absoluteBoundingBox;
const rasterJobs = [];
let nodeCount = 0;
let textCount = 0;
let visualCount = 0;

function copyPaint(paint) {
  if (!paint || paint.visible === false) return null;
  const out = { type: paint.type, opacity: paint.opacity == null ? 1 : paint.opacity };
  if (paint.color) out.color = paint.color;
  if (paint.scaleMode) out.scaleMode = paint.scaleMode;
  if (paint.imageHash) out.hasImage = true;
  return out;
}

function visiblePaints(paints) {
  return Array.isArray(paints) ? paints.map(copyPaint).filter(Boolean) : [];
}

function hasVisibleEffects(node) {
  return Array.isArray(node.effects) && node.effects.some((effect) => effect.visible !== false);
}

function hasRenderablePaint(node) {
  return visiblePaints(node.fills).length > 0 || visiblePaints(node.strokes).length > 0 || hasVisibleEffects(node);
}

function visibleChildren(node) {
  return "children" in node ? node.children.filter((child) => child.visible !== false && child.absoluteBoundingBox) : [];
}

function ownsFigmaMask(children) {
  return children.some((child) => child.isMask);
}

function textShouldRasterize(node) {
  if (node.type !== "TEXT") return false;
  const value = (node.characters || "").trim();
  if (!value) return false;
  if ((node.name || "").toLowerCase().includes("icon") && value.length <= 6) return true;
  if (value.length <= 4 && /[^\\x20-\\x7E]/.test(value)) return true;
  return false;
}

function compactName(value) {
  return String(value || "").replace(/[^a-z0-9]+/gi, "").toLowerCase();
}

function overrideTokens(node) {
  const source = String((node && node.name) || "") + " " + String((node && node.description) || "");
  const tokens = new Set();
  const pattern = /figunity:([a-z0-9_-]+)/gi;
  let match;
  while ((match = pattern.exec(source)) !== null) {
    tokens.add(match[1].toLowerCase().replace(/_/g, "-"));
  }

  return Array.from(tokens);
}

function hasOverride(overrides, ...names) {
  return names.some((name) => overrides.includes(name));
}

function inferControlHint(node, overrides) {
  if (hasOverride(overrides, "no-control", "none")) return "";
  if (hasOverride(overrides, "button")) return "button";
  if (hasOverride(overrides, "toggle")) return "toggle";
  if (hasOverride(overrides, "input", "textfield", "text-input")) return "input";
  if (hasOverride(overrides, "dropdown", "select")) return "dropdown";
  if (hasOverride(overrides, "scroll", "scrollview", "scroll-rect")) return "scroll";
  if (hasOverride(overrides, "tab")) return "tab";
  if (hasOverride(overrides, "slider")) return "slider";
  if (hasOverride(overrides, "passive-slider", "progress", "meter")) return "passive-slider";
  if (node.type === "TEXT") return "";

  const name = compactName(node.name);
  if (name.includes("scrollview") || name.includes("scrollrect") || name.startsWith("scroll")) return "scroll";
  if (name.startsWith("toggle") || name.includes("checkbox") || name.includes("switch")) return "toggle";
  if (name.startsWith("input") || name.includes("textfield") || name.includes("textinput")) return "input";
  if (name.startsWith("dropdown") || name.includes("select")) return "dropdown";
  if (name.startsWith("tab") || name.includes("segmented")) return "tab";
  if (name.includes("slider")) return name.includes("passive") || name.includes("readonly") ? "passive-slider" : "slider";
  if (name.includes("progress") || name.includes("capacitybar") || name.includes("meter")) return "passive-slider";
  if (name.startsWith("button") || name.includes("button")) return "button";
  return "";
}

function repeatKey(node) {
  const key = compactName(node.name)
    .replace(/\\d+$/g, "")
    .replace(/(copy|instance|variant)$/g, "");
  if (!key || key === "node" || key === "frame" || key === "group") return "";
  return key;
}

function layoutPayload(node) {
  return {
    layoutMode: node.layoutMode || "NONE",
    primaryAxisSizingMode: node.primaryAxisSizingMode || "",
    counterAxisSizingMode: node.counterAxisSizingMode || "",
    primaryAxisAlignItems: node.primaryAxisAlignItems || "",
    counterAxisAlignItems: node.counterAxisAlignItems || "",
    layoutWrap: node.layoutWrap || "",
    itemSpacing: typeof node.itemSpacing === "number" ? node.itemSpacing : 0,
    counterAxisSpacing: typeof node.counterAxisSpacing === "number" ? node.counterAxisSpacing : 0,
    paddingLeft: typeof node.paddingLeft === "number" ? node.paddingLeft : 0,
    paddingRight: typeof node.paddingRight === "number" ? node.paddingRight : 0,
    paddingTop: typeof node.paddingTop === "number" ? node.paddingTop : 0,
    paddingBottom: typeof node.paddingBottom === "number" ? node.paddingBottom : 0
  };
}

function constraintsPayload(node) {
  return node.constraints
    ? { horizontal: node.constraints.horizontal || "", vertical: node.constraints.vertical || "" }
    : null;
}

async function componentPayload(node) {
  if (node.type !== "INSTANCE") {
    return { isInstance: false, componentKey: "", componentName: "" };
  }

  let component = null;
  try {
    component = typeof node.getMainComponentAsync === "function"
      ? await node.getMainComponentAsync()
      : node.mainComponent;
  } catch {
    component = null;
  }

  return {
    isInstance: true,
    componentKey: component ? (component.key || component.id || "") : "",
    componentName: component ? (component.name || "") : ""
  };
}

function localBounds(node) {
  const box = node.absoluteBoundingBox || { x: rootBounds.x, y: rootBounds.y, width: 0, height: 0 };
  return { x: box.x - rootBounds.x, y: box.y - rootBounds.y, width: box.width, height: box.height };
}

function textPayload(node) {
  let fontName = null;
  if (node.fontName && typeof node.fontName === "object") {
    fontName = { family: node.fontName.family || "", style: node.fontName.style || "" };
  }

  return {
    characters: node.characters || "",
    fontName,
    fontSize: typeof node.fontSize === "number" ? node.fontSize : 16,
    lineHeight: node.lineHeight || null,
    letterSpacing: node.letterSpacing || null,
    textAlignHorizontal: node.textAlignHorizontal || "LEFT",
    textAlignVertical: node.textAlignVertical || "TOP",
    textAutoResize: node.textAutoResize || "NONE"
  };
}

function renderDecision(node, children, overrides) {
  const bounds = localBounds(node);
  if (bounds.width <= 0 || bounds.height <= 0) return { mode: "skip", reason: "zero-size bounds" };
  if (hasOverride(overrides, "ignore", "skip")) return { mode: "skip", reason: "figunity override: skip" };
  if (hasOverride(overrides, "raw", "raster", "image")) return { mode: "composite", reason: "figunity override: raw PNG" };
  if (hasOverride(overrides, "container")) return { mode: "container", reason: "figunity override: container" };
  if (hasOverride(overrides, "background")) return { mode: "background", reason: "figunity override: background" };
  if (hasOverride(overrides, "visual")) return { mode: "visual", reason: "figunity override: visual" };
  if (hasOverride(overrides, "text") && node.type === "TEXT") return { mode: "text", reason: "figunity override: text" };
  if (node.type === "TEXT") {
    return textShouldRasterize(node)
      ? { mode: "visual", reason: "short/icon text rasterized" }
      : { mode: "text", reason: "Figma TEXT node" };
  }

  if (children.length > 0 && ownsFigmaMask(children)) return { mode: "composite", reason: "Figma mask group" };
  if (children.length === 0) {
    return hasRenderablePaint(node)
      ? { mode: "visual", reason: "leaf visual paint" }
      : { mode: "container", reason: "leaf container" };
  }

  return hasRenderablePaint(node)
    ? { mode: "background", reason: "painted container background" }
    : { mode: "container", reason: "container" };
}

async function traverse(node, parentId, depth, siblingIndex, path) {
  nodeCount++;
  const children = visibleChildren(node);
  const overrides = overrideTokens(node);
  const decision = renderDecision(node, children, overrides);
  const mode = decision.mode;
  const controlHint = inferControlHint(node, overrides);
  const maskDisabled = hasOverride(overrides, "no-mask");
  if (mode === "text") textCount++;
  if (mode === "visual" || mode === "background" || mode === "composite") visualCount++;

  const data = {
    id: node.id,
    parentId,
    name: node.name || node.type,
    type: node.type,
    depth,
    siblingIndex,
    path,
    renderMode: mode,
    controlHint,
    overrideHint: overrides.join(","),
    decisionReason: decision.reason + (controlHint ? "; control=" + controlHint : ""),
    clipsContent: !!node.clipsContent && !maskDisabled,
    isMask: (!!node.isMask || hasOverride(overrides, "mask")) && !maskDisabled,
    maskType: node.maskType || "",
    opacity: node.opacity == null ? 1 : node.opacity,
    blendMode: node.blendMode || "PASS_THROUGH",
    bounds: localBounds(node),
    constraints: constraintsPayload(node),
    autoLayout: layoutPayload(node),
    fills: visiblePaints(node.fills),
    strokes: visiblePaints(node.strokes),
    strokeWeight: typeof node.strokeWeight === "number" ? node.strokeWeight : 0,
    cornerRadius: typeof node.cornerRadius === "number" ? node.cornerRadius : 0,
    ...(await componentPayload(node)),
    repeatKey: repeatKey(node),
    text: node.type === "TEXT" ? textPayload(node) : null,
    children: []
  };

  if (mode === "visual" || mode === "background" || mode === "composite") {
    rasterJobs.push({ id: node.id, name: node.name || node.type, mode, bounds: data.bounds, path, type: node.type });
  }

  data.children = await Promise.all(children.map((child, index) =>
    traverse(child, node.id, depth + 1, index, path + "/" + (child.name || child.type) + "[" + index + "]")));

  return data;
}

async function rasterize(job, index) {
  const node = await figma.getNodeByIdAsync(job.id);
  if (!node || typeof node.exportAsync !== "function") return { ...job, error: "not exportable" };

  let target = node;
  let cleanup = null;
  if (job.mode === "background" && "children" in node) {
    const rect = figma.createRectangle();
    const host = root.parent && root.parent.type === "PAGE" ? root.parent : figma.currentPage;
    const box = node.absoluteBoundingBox || job.bounds || { x: node.x || 0, y: node.y || 0, width: node.width || 1, height: node.height || 1 };
    host.appendChild(rect);
    rect.name = (node.name || "Background") + " Background Export";
    rect.x = box.x || 0;
    rect.y = box.y || 0;
    rect.resize(Math.max(1, box.width || 1), Math.max(1, box.height || 1));
    rect.visible = true;
    rect.opacity = typeof node.opacity === "number" ? node.opacity : 1;
    rect.fills = clonePaints(node.fills);
    rect.strokes = clonePaints(node.strokes);
    rect.strokeWeight = typeof node.strokeWeight === "number" ? node.strokeWeight : 0;
    rect.effects = cloneSerializable(node.effects || []);
    copyCornerRadii(node, rect);
    target = rect;
    cleanup = rect;
  }

  let bytes;
  try {
    bytes = await target.exportAsync({ format: "PNG", constraint: { type: "SCALE", value: rasterScale } });
  } finally {
    if (cleanup) cleanup.remove();
  }

  return { ...job, index, scale: rasterScale, byteLength: bytes.length, base64: figma.base64Encode(bytes) };
}

function cloneSerializable(value) {
  return JSON.parse(JSON.stringify(value));
}

function clonePaints(value) {
  return Array.isArray(value) ? cloneSerializable(value) : [];
}

function copyCornerRadii(source, target) {
  if (typeof source.cornerRadius === "number" && source.cornerRadius !== figma.mixed) {
    target.cornerRadius = source.cornerRadius;
  }

  const corners = ["topLeftRadius", "topRightRadius", "bottomRightRadius", "bottomLeftRadius"];
  for (const key of corners) {
    if (typeof source[key] === "number" && source[key] !== figma.mixed) {
      target[key] = source[key];
    }
  }
}

const tree = await traverse(root, null, 0, 0, root.name || root.type);
const exports = [];
for (let i = 0; i < rasterJobs.length; i++) exports.push(await rasterize(rasterJobs[i], i));

const screenshotBytes = await root.exportAsync({ format: "PNG", constraint: { type: "SCALE", value: 1 } });

return {
  key: request.key || "frame",
  slug: request.slug || "frame",
  frameId: root.id,
  frameName: root.name,
  width: root.width,
  height: root.height,
  rootBounds,
  nodeCount,
  textCount,
  visualCount,
  tree,
  exports,
  screenshotBase64: figma.base64Encode(screenshotBytes)
};
`;
}

function safeDiskToken(value) {
  return String(value || "node")
    .replace(/[^a-z0-9]+/gi, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 54)
    .toLowerCase() || "node";
}

function assignAssetPaths(node, exportMap) {
  if (!node) return;
  if (exportMap.has(node.id)) node.assetPath = exportMap.get(node.id);
  for (const child of node.children || []) assignAssetPaths(child, exportMap);
}

function pngDimensions(bytes) {
  if (bytes.length < 24 ||
      bytes[0] !== 0x89 ||
      bytes[1] !== 0x50 ||
      bytes[2] !== 0x4e ||
      bytes[3] !== 0x47) {
    return { width: 0, height: 0 };
  }

  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
}

function isInvalidTinyRaster(item, bytes) {
  const dimensions = pngDimensions(bytes);
  item.pixelWidth = dimensions.width;
  item.pixelHeight = dimensions.height;
  return dimensions.width <= 1 &&
    dimensions.height <= 1 &&
    ((item.bounds?.width || 0) > 2 || (item.bounds?.height || 0) > 2);
}

try {
  await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "figunity-exporter", version: "0.1.0" },
  });
  notify("notifications/initialized", {});
  await rpc("tools/list", {});
  const status = await waitForMcpDesktopLink();

  const exportedFrames = [];
  const summary = [];
  const assetPathByHash = new Map();

  for (const request of frameRequests) {
    const key = request.key || request.slug || request.name || "frame";
    const slug = request.slug || safeDiskToken(key);
    const assetDir = join(outDir, "ElementAssets", slug);
    rmSync(assetDir, { recursive: true, force: true });
    mkdirSync(assetDir, { recursive: true });

    const result = parseToolText(await tool("figma_execute", {
      code: extractionProgram({ ...request, key, slug }),
      timeout: 240000,
    }, 300000));

    if (!result?.success) {
      throw new Error(`figma_execute failed for ${slug}: ${JSON.stringify(result)?.slice(0, 4000)}`);
    }

    if (result.result?.missing) {
      if (request.optional) {
        summary.push({ key, slug, missing: true });
        continue;
      }

      throw new Error(`Required Figma frame is missing: ${request.name || request.nodeId || slug}`);
    }

    const payload = result.result;
    const exportMap = new Map();
    let saved = 0;
    let reused = 0;
    let skipped = 0;

    for (const item of payload.exports || []) {
      if (!item.base64 || item.error) continue;
      const bytes = Buffer.from(item.base64, "base64");
      if (isInvalidTinyRaster(item, bytes)) {
        item.error = `discarded invalid ${item.pixelWidth}x${item.pixelHeight} raster`;
        skipped++;
        delete item.base64;
        continue;
      }

      const hash = createHash("sha256").update(bytes).digest("hex");
      const existingUnityPath = assetPathByHash.get(hash);
      if (existingUnityPath) {
        exportMap.set(item.id, existingUnityPath);
        item.assetPath = existingUnityPath;
        item.dedupedFrom = existingUnityPath;
        reused++;
        delete item.base64;
        continue;
      }

      const fileName = `${String(item.index).padStart(4, "0")}-${safeDiskToken(item.name)}-${item.id.replace(/[^a-z0-9]+/gi, "-")}.png`;
      const unityPath = `${unityOutDir}/ElementAssets/${slug}/${fileName}`;
      writeFileSync(join(assetDir, fileName), bytes);
      assetPathByHash.set(hash, unityPath);
      exportMap.set(item.id, unityPath);
      item.assetPath = unityPath;
      saved++;
      delete item.base64;
    }

    assignAssetPaths(payload.tree, exportMap);

    if (payload.screenshotBase64) {
      const screenshotName = `${slug}.png`;
      const screenshotPath = join(screenshotsDir, screenshotName);
      writeFileSync(screenshotPath, Buffer.from(payload.screenshotBase64, "base64"));
      payload.screenshotPath = `${unityOutDir}/Screenshots/${screenshotName}`;
      delete payload.screenshotBase64;
    }

    exportedFrames.push(payload);
    summary.push({
      key,
      slug,
      frameName: payload.frameName,
      frameId: payload.frameId,
      frameSize: `${payload.width}x${payload.height}`,
      nodeCount: payload.nodeCount,
      textCount: payload.textCount,
      visualCount: payload.visualCount,
      exportedPngs: saved,
      dedupedPngs: reused,
      skippedPngs: skipped,
      screenshotPath: payload.screenshotPath,
    });
  }

  const document = {
    source: "figunity via figma-console-mcp",
    expectedFileName,
    currentFileName: "",
    currentFileKey:
      status.currentFileKey ||
      status.transport?.websocket?.connectedFile?.fileKey ||
      "",
    port,
    exportedAt: new Date().toISOString(),
    frames: exportedFrames,
  };

  const payloadPath = join(outDir, "payload.json");
  writeFileSync(payloadPath, JSON.stringify(document, null, 2));

  const summaryPath = join(outDir, "summary.json");
  writeFileSync(summaryPath, JSON.stringify(summary, null, 2));

  console.log(JSON.stringify({ payloadPath, summaryPath, summary }, null, 2));
} finally {
  stopMcp();
}
