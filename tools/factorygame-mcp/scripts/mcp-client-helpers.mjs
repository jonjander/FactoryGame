/**
 * Shared helpers for MCP smoke/playtest scripts.
 */
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { existsSync } from "node:fs";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

export const defaultBaseUrl =
  process.env.FACTORYGAME_BASE_URL?.trim() ||
  "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net";

export function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

export function parseToolJson(result) {
  assert(!result.isError, `tool error: ${JSON.stringify(result)}`);
  const text = result.content?.[0]?.text ?? "";
  const statusLine = text.split("\n")[0] ?? "";
  const statusMatch = statusLine.match(/^HTTP (\d+)/);
  assert(statusMatch, `missing HTTP status: ${text.slice(0, 200)}`);
  const status = Number(statusMatch[1]);
  assert(status >= 200 && status < 300, `HTTP ${status}: ${text.slice(0, 400)}`);
  const bodyText = text.split("\n").slice(1).join("\n").trim();
  if (!bodyText) return { status, json: null };
  try {
    return { status, json: JSON.parse(bodyText) };
  } catch {
    throw new Error(`HTTP ${status} non-JSON body: ${bodyText.slice(0, 200)}`);
  }
}

export async function createMcpClient(name) {
  const __dirname = dirname(fileURLToPath(import.meta.url));
  const root = join(__dirname, "..");
  const entry = join(root, "dist", "index.js");
  assert(existsSync(entry), "Missing dist/index.js — run: npm run build");

  const transport = new StdioClientTransport({
    command: "node",
    args: [entry],
    cwd: root,
    env: {
      ...process.env,
      FACTORYGAME_BASE_URL: defaultBaseUrl,
    },
    stderr: "inherit",
  });

  const client = new Client({ name, version: "1.0.0" });
  await client.connect(transport);
  return { client, transport };
}

export async function guestAuth(client, label) {
  const auth = await client.callTool({
    name: "guest_auth",
    arguments: { deviceKey: `${label}-${Date.now()}` },
  });
  const { json } = parseToolJson(auth);
  assert(json?.sessionToken, "guest_auth missing sessionToken");
  return json.sessionToken;
}

export function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export async function callOk(client, name, args, retries = 2) {
  let lastError;
  for (let attempt = 0; attempt < retries; attempt++) {
    try {
      const result = await client.callTool({ name, arguments: args });
      return parseToolJson(result);
    } catch (err) {
      lastError = err;
      if (attempt + 1 < retries) await sleep(400);
    }
  }
  throw lastError;
}

export async function callToolRaw(client, name, args) {
  return client.callTool({ name, arguments: args });
}

export function parseToolResult(result) {
  const text = result.content?.[0]?.text ?? "";
  const statusLine = text.split("\n")[0] ?? "";
  const statusMatch = statusLine.match(/^HTTP (\d+)/);
  const status = statusMatch ? Number(statusMatch[1]) : 0;
  const bodyText = text.split("\n").slice(1).join("\n").trim();
  let json = null;
  if (bodyText) {
    try {
      json = JSON.parse(bodyText);
    } catch {
      json = null;
    }
  }
  return { isError: Boolean(result.isError), status, json, text };
}

/** Calls tool; returns null on 404 (e.g. keyframe not ready yet). Throws on other errors. */
export async function callOkOrNullOn404(client, name, args) {
  const result = await callToolRaw(client, name, args);
  const parsed = parseToolResult(result);
  if (parsed.status === 404) return null;
  assert(!parsed.isError && parsed.status >= 200 && parsed.status < 300,
    `${name}: ${parsed.text.slice(0, 400)}`);
  return parsed.json;
}
