import { spawn, spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join, resolve } from "node:path";

const projectRoot = resolve(process.cwd());
const port = String(process.env.FIGMA_WS_PORT || "9225");
const mcpCommand = resolveMcpCommand();
const child = spawnMcp(mcpCommand);
let nextMessageId = 1;
let stdoutBuffer = "";
const inflight = new Map();

function resolveMcpCommand() {
  if (process.env.FIGUNITY_MCP_CMD) return process.env.FIGUNITY_MCP_CMD;
  if (process.platform === "win32" && process.env.APPDATA) {
    const cmd = join(process.env.APPDATA, "npm", "figma-console-mcp.cmd");
    if (existsSync(cmd)) return cmd;
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
    if (!line) continue;

    let message;
    try {
      message = JSON.parse(line);
    } catch {
      process.stderr.write(`[figunity:test-menus] ${line}\n`);
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

async function waitForDesktopLink() {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const status = parseToolText(await tool("figma_get_status", { probe: true }, 20000)) || {};
    if (status?.setup?.valid === true && status?.setup?.probeResult?.success === true) {
      return status;
    }
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 1500));
  }
  throw new Error("figma-console-mcp Desktop Bridge did not connect.");
}

const createMenusCode = `
await figma.loadAllPagesAsync();
await figma.loadFontAsync({ family: "Inter", style: "Regular" });
await figma.loadFontAsync({ family: "Inter", style: "Bold" });

const pageName = "FIGUNITY Test Menus";
let page = figma.root.children.find((item) => item.name === pageName);
if (!page) page = figma.createPage();
page.name = pageName;
await figma.setCurrentPageAsync(page);

for (const node of [...page.children]) {
  if ((node.name || "").startsWith("FIGUNITY Test ")) node.remove();
}

function solid(hex, opacity = 1) {
  const h = hex.replace("#", "");
  return {
    type: "SOLID",
    color: {
      r: parseInt(h.slice(0, 2), 16) / 255,
      g: parseInt(h.slice(2, 4), 16) / 255,
      b: parseInt(h.slice(4, 6), 16) / 255,
    },
    opacity,
  };
}

function frame(name, x, y, w, h) {
  const node = figma.createFrame();
  node.name = name;
  node.x = x;
  node.y = y;
  node.resize(w, h);
  node.fills = [solid("#12171F")];
  node.clipsContent = true;
  page.appendChild(node);
  return node;
}

function rect(parent, name, x, y, w, h, fill = "#232B36", radius = 0) {
  const node = figma.createRectangle();
  node.name = name;
  node.x = x;
  node.y = y;
  node.resize(w, h);
  node.fills = [solid(fill)];
  node.cornerRadius = radius;
  parent.appendChild(node);
  return node;
}

function text(parent, name, value, x, y, w, h, size = 20, fill = "#F4F1E8", bold = false) {
  const node = figma.createText();
  node.name = name;
  node.x = x;
  node.y = y;
  node.resize(w, h);
  node.fontName = { family: "Inter", style: bold ? "Bold" : "Regular" };
  node.characters = value;
  node.fontSize = size;
  node.fills = [solid(fill)];
  parent.appendChild(node);
  return node;
}

function button(parent, name, label, x, y, w, h, fill = "#C7753D") {
  const group = figma.createFrame();
  group.name = "Button - " + name;
  group.x = x;
  group.y = y;
  group.resize(w, h);
  group.fills = [solid(fill)];
  group.cornerRadius = 10;
  group.layoutMode = "HORIZONTAL";
  group.primaryAxisAlignItems = "CENTER";
  group.counterAxisAlignItems = "CENTER";
  parent.appendChild(group);
  text(group, "Label", label, 0, 0, w, h, 18, "#10141A", true);
  return group;
}

function slider(parent, name, x, y, w, value, active) {
  const group = figma.createFrame();
  group.name = (active ? "Slider - " : "Progress - ") + name;
  group.x = x;
  group.y = y;
  group.resize(w, 24);
  group.fills = [];
  parent.appendChild(group);
  rect(group, "Track", 0, 8, w, 8, "#313A47", 4);
  rect(group, "Fill", 0, 8, w * value, 8, active ? "#7BC4FF" : "#71B978", 4);
  rect(group, "Handle", Math.max(0, w * value - 8), 4, 16, 16, active ? "#DDF2FF" : "#71B978", 8);
  return group;
}

function toggle(parent, name, x, y, on) {
  const group = figma.createFrame();
  group.name = "Toggle - " + name;
  group.x = x;
  group.y = y;
  group.resize(220, 36);
  group.fills = [];
  parent.appendChild(group);
  rect(group, "Track", 0, 4, 56, 28, on ? "#538D63" : "#313A47", 14);
  rect(group, "Handle", on ? 30 : 2, 6, 24, 24, "#F4F1E8", 12);
  text(group, "Label", name, 72, 5, 140, 24, 18);
  return group;
}

function input(parent, name, x, y, w) {
  const group = figma.createFrame();
  group.name = "Input - " + name;
  group.x = x;
  group.y = y;
  group.resize(w, 48);
  group.fills = [solid("#1C232D")];
  group.cornerRadius = 8;
  parent.appendChild(group);
  text(group, "Placeholder", "Type player name", 14, 12, w - 28, 22, 16, "#8E99A8");
  return group;
}

function dropdown(parent, name, x, y, w) {
  const group = figma.createFrame();
  group.name = "Dropdown - " + name;
  group.x = x;
  group.y = y;
  group.resize(w, 48);
  group.fills = [solid("#1C232D")];
  group.cornerRadius = 8;
  parent.appendChild(group);
  text(group, "Value", "Normal", 14, 12, w - 60, 22, 16);
  text(group, "Icon", "v", w - 34, 10, 20, 22, 18);
  return group;
}

function card(parent, index, x, y) {
  const group = figma.createFrame();
  group.name = "Card - Mission " + String(index).padStart(2, "0");
  group.x = x;
  group.y = y;
  group.resize(300, 86);
  group.fills = [solid("#202832")];
  group.cornerRadius = 10;
  parent.appendChild(group);
  text(group, "Title", "Mission " + index, 18, 14, 180, 22, 18, "#F4F1E8", true);
  text(group, "Subtitle", "Repeated item prefab candidate", 18, 42, 220, 20, 13, "#A7B1BF");
  slider(group, "Risk", 204, 18, 72, 0.25 + index * 0.12, false);
  return group;
}

const main = frame("FIGUNITY Test Main Menu", 0, 0, 960, 540);
main.layoutMode = "VERTICAL";
main.paddingLeft = 32;
main.paddingRight = 32;
main.paddingTop = 28;
main.paddingBottom = 28;
main.itemSpacing = 18;
main.primaryAxisSizingMode = "FIXED";
main.counterAxisSizingMode = "FIXED";
text(main, "Title", "FIGUNITY TEST MENU", 32, 28, 520, 40, 30, "#F4F1E8", true);
rect(main, "Panel - Rounded Status", 32, 86, 420, 116, "#202832", 18);
text(main, "Status Label", "Rounded panel + TMP text", 56, 112, 330, 26, 18);
slider(main, "Music Volume", 56, 154, 330, 0.68, true);
button(main, "Start", "START", 32, 224, 180, 54);
button(main, "Options", "OPTIONS", 230, 224, 180, 54, "#E0B15B");
toggle(main, "Music", 32, 308, true);
input(main, "Player Name", 32, 362, 360);
dropdown(main, "Difficulty", 32, 424, 280);

const masks = frame("FIGUNITY Test Mask Menu", 1000, 0, 960, 540);
text(masks, "Title", "MASKS AND CLIPPING", 32, 28, 500, 38, 28, "#F4F1E8", true);
const maskGroup = figma.createFrame();
maskGroup.name = "Panel - Masked Avatar";
maskGroup.x = 48;
maskGroup.y = 98;
maskGroup.resize(260, 260);
maskGroup.fills = [];
masks.appendChild(maskGroup);
const mask = rect(maskGroup, "Mask - Avatar", 0, 0, 180, 180, "#FFFFFF", 90);
mask.isMask = true;
rect(maskGroup, "Visual - Oversized Color Block", -40, -20, 260, 220, "#8D5BE0", 0);
rect(maskGroup, "Visual - Accent", 20, 34, 240, 160, "#E05B7A", 0);
const clip = figma.createFrame();
clip.name = "Scroll View - Clipped Log";
clip.x = 360;
clip.y = 98;
clip.resize(380, 300);
clip.fills = [solid("#1C232D")];
clip.cornerRadius = 12;
clip.clipsContent = true;
masks.appendChild(clip);
for (let i = 0; i < 8; i++) {
  text(clip, "Log Row " + i, "Clipped row " + (i + 1), 18, 18 + i * 42, 260, 24, 16);
}

const list = frame("FIGUNITY Test Scroll Menu", 0, 620, 960, 620);
text(list, "Title", "SCROLL + REPEATED CARDS", 32, 28, 620, 38, 28, "#F4F1E8", true);
const scroll = figma.createFrame();
scroll.name = "Scroll View - Mission List";
scroll.x = 32;
scroll.y = 92;
scroll.resize(360, 420);
scroll.fills = [solid("#17202A")];
scroll.cornerRadius = 12;
scroll.clipsContent = true;
scroll.layoutMode = "VERTICAL";
scroll.paddingLeft = 18;
scroll.paddingRight = 18;
scroll.paddingTop = 18;
scroll.paddingBottom = 18;
scroll.itemSpacing = 12;
list.appendChild(scroll);
for (let i = 1; i <= 6; i++) card(scroll, i, 18, 18 + (i - 1) * 98);
const tabs = figma.createFrame();
tabs.name = "Segmented Tabs";
tabs.x = 450;
tabs.y = 92;
tabs.resize(380, 54);
tabs.fills = [solid("#1C232D")];
tabs.cornerRadius = 10;
tabs.layoutMode = "HORIZONTAL";
tabs.itemSpacing = 6;
tabs.paddingLeft = 8;
tabs.paddingRight = 8;
tabs.paddingTop = 8;
tabs.paddingBottom = 8;
list.appendChild(tabs);
button(tabs, "Tab Inventory", "Inventory", 0, 0, 116, 38, "#7BC4FF");
button(tabs, "Tab Map", "Map", 0, 0, 90, 38, "#2A3440");
button(tabs, "Tab Crew", "Crew", 0, 0, 100, 38, "#2A3440");

figma.viewport.scrollAndZoomIntoView([main, masks, list]);
return {
  page: page.name,
  frames: [main, masks, list].map((node) => ({ id: node.id, name: node.name, width: node.width, height: node.height }))
};
`;

try {
  await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "figunity-test-menu-generator", version: "0.1.0" },
  });
  notify("notifications/initialized", {});
  await rpc("tools/list", {});
  await waitForDesktopLink();
  const result = parseToolText(await tool("figma_execute", { code: createMenusCode, timeout: 240000 }, 300000));
  if (!result?.success) {
    throw new Error(`figma_execute failed: ${JSON.stringify(result)?.slice(0, 4000)}`);
  }
  console.log(JSON.stringify(result.result, null, 2));
} finally {
  stopMcp();
}
