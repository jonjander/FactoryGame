/**
 * Spawns the FactoryGame MCP server and calls tools over stdio (real MCP protocol).
 * Run from package root: npm run smoke
 */
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { existsSync } from "node:fs";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const entry = join(root, "dist", "index.js");

if (!existsSync(entry)) {
  console.error("Missing dist/index.js — run: npm run build");
  process.exit(1);
}

const baseUrl =
  process.env.FACTORYGAME_BASE_URL?.trim() ||
  "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net";

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

async function main() {
  const transport = new StdioClientTransport({
    command: "node",
    args: [entry],
    cwd: root,
    env: {
      ...process.env,
      FACTORYGAME_BASE_URL: baseUrl,
    },
    stderr: "inherit",
  });

  const client = new Client({ name: "factorygame-mcp-smoke", version: "1.0.0" });
  await client.connect(transport);

  const { tools } = await client.listTools();
  assert(tools?.length >= 10, `expected tools, got ${tools?.length}`);

  const auth = await client.callTool({
    name: "guest_auth",
    arguments: { deviceKey: `mcp-smoke-${Date.now()}` },
  });
  assert(!auth.isError, `guest_auth: ${JSON.stringify(auth)}`);
  const authText = auth.content?.[0]?.text ?? "";
  assert(authText.includes("200"), `guest_auth bad: ${authText}`);
  const sessionToken = JSON.parse(authText.split("\n").slice(1).join("\n")).sessionToken;
  assert(sessionToken, "no sessionToken");

  const els = await client.callTool({
    name: "content_list_elements",
    arguments: {},
  });
  assert(!els.isError, `content_list_elements: ${JSON.stringify(els)}`);

  const wallet = await client.callTool({
    name: "player_wallet",
    arguments: { sessionToken },
  });
  assert(!wallet.isError, `player_wallet: ${JSON.stringify(wallet)}`);

  const boards = await client.callTool({
    name: "boards_list",
    arguments: { sessionToken },
  });
  assert(!boards.isError, `boards_list: ${JSON.stringify(boards)}`);

  const logs = await client.callTool({
    name: "diagnostics_recent_logs",
    arguments: {},
  });
  assert(!logs.isError, `diagnostics_recent_logs: ${JSON.stringify(logs)}`);

  await transport.close();
  console.error("mcp-smoke: OK (guest_auth, content_list_elements, player_wallet, boards_list, diagnostics_recent_logs)");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
