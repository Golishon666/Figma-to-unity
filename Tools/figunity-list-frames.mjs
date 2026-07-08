import { spawn, spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join, resolve } from "node:path";

const projectRoot = resolve(process.cwd());
const args = parseArgs(process.argv.slice(2));
const port = String(args.port || process.env.FIGMA_WS_PORT || "9225");
const expectedFileName = args.file || process.env.FIGUNITY_FILE_NAME || "";
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
      process.stderr.write(`[figunity:list-frames] ${line}\n`);
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

async function waitForDesktopLink() {
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

      return { status, fileName };
    }

    await new Promise((resolvePromise) => setTimeout(resolvePromise, 1500));
  }

  throw new Error("figma-console-mcp Desktop Bridge did not connect. Start Figma Desktop and run its bridge plugin.");
}

const listFramesCode = `
await figma.loadAllPagesAsync();

const exportableTypes = new Set(["FRAME", "COMPONENT", "COMPONENT_SET", "INSTANCE", "SECTION"]);
const panels = [];

function safeSlug(value) {
  return String(value || "panel")
    .replace(/[^a-z0-9]+/gi, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 54)
    .toLowerCase() || "panel";
}

function shouldInclude(node, depth) {
  if (!node || node.visible === false || !node.absoluteBoundingBox || !exportableTypes.has(node.type)) return false;
  if (depth <= 1) return true;
  return node.parent && node.parent.type === "SECTION";
}

function visit(node, pageName, path, depth) {
  if (shouldInclude(node, depth)) {
    const box = node.absoluteBoundingBox;
    panels.push({
      nodeId: node.id,
      name: node.name || node.type,
      pageName,
      path,
      type: node.type,
      x: box.x,
      y: box.y,
      width: box.width,
      height: box.height,
      key: safeSlug((node.name || node.type) + "-" + node.id),
      slug: safeSlug(node.name || node.type)
    });
  }

  if (!("children" in node)) return;
  for (const child of node.children) {
    const childPath = path ? path + "/" + (child.name || child.type) : (child.name || child.type);
    visit(child, pageName, childPath, depth + 1);
  }
}

for (const page of figma.root.children) {
  for (const child of page.children) {
    visit(child, page.name || "Page", child.name || child.type, 1);
  }
}

panels.sort((left, right) =>
  left.pageName.localeCompare(right.pageName) ||
  left.y - right.y ||
  left.x - right.x ||
  left.name.localeCompare(right.name));

return {
  currentPageName: figma.currentPage ? figma.currentPage.name : "",
  panels
};
`;

try {
  await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "figunity-frame-list", version: "0.1.0" },
  });
  notify("notifications/initialized", {});
  await rpc("tools/list", {});

  const { status, fileName } = await waitForDesktopLink();
  const result = parseToolText(await tool("figma_execute", {
    code: listFramesCode,
    timeout: 120000,
  }, 180000));

  if (!result?.success) {
    throw new Error(`figma_execute failed: ${JSON.stringify(result)?.slice(0, 4000)}`);
  }

  console.log(JSON.stringify({
    currentFileName: fileName,
    currentFileKey:
      status.currentFileKey ||
      status.transport?.websocket?.connectedFile?.fileKey ||
      "",
    currentPageName: result.result?.currentPageName || "",
    panels: result.result?.panels || []
  }, null, 2));
} finally {
  stopMcp();
}
