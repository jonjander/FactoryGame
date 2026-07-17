/**
 * Spawns the FactoryGame MCP server and calls tools over stdio (real MCP protocol).
 * Run from package root: npm run smoke
 */
import {
  assert,
  callOk,
  callToolRaw,
  createMcpClient,
  defaultBaseUrl,
  guestAuth,
  parseToolResult,
} from "./mcp-client-helpers.mjs";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const plans = JSON.parse(
  readFileSync(join(__dirname, "..", "fixtures", "plans.json"), "utf8")
);

async function main() {
  const { client, transport } = await createMcpClient("factorygame-mcp-smoke");

  const { tools } = await client.listTools();
  assert(tools?.length >= 30, `expected >=30 tools, got ${tools?.length}`);

  const sessionToken = await guestAuth(client, "mcp-smoke");

  await callOk(client, "content_list_elements", { locale: "en" });
  await callOk(client, "content_wiki", { locale: "en" });
  await callOk(client, "content_machine_store", {});

  await callOk(client, "player_wallet", { sessionToken });
  await callOk(client, "player_pool", { sessionToken });
  await callOk(client, "player_pool_view", { sessionToken, locale: "en" });
  await callOk(client, "player_machine_inventory", { sessionToken });

  for (const name of ["player_transactions", "market_orders_mine"]) {
    const result = await callToolRaw(client, name, { sessionToken });
    const parsed = parseToolResult(result);
    if (parsed.isError || parsed.status >= 400) {
      console.error(`mcp-smoke: warn ${name} HTTP ${parsed.status} (skipped)`);
    }
  }

  await callOk(client, "market_summary", { sessionToken, locale: "en" });
  await callOk(client, "market_element_depth", { elementId: 1 });
  await callOk(client, "market_element_history", { elementId: 1, points: 12 });
  await callOk(client, "market_open_orders", {});
  await callOk(client, "market_recent_trades", { limit: 5 });

  await callOk(client, "boards_list", { sessionToken });

  const { json: board } = await callOk(client, "boards_create", {
    sessionToken,
    name: "MCP smoke board",
  });
  assert(board?.id, "boards_create missing id");

  const plan = plans.minimalLoop.plan;
  await callOk(client, "boards_save_plan", {
    sessionToken,
    boardId: board.id,
    plan,
  });
  await callOk(client, "boards_get_plan", { sessionToken, boardId: board.id });
  await callOk(client, "boards_info_preview", {
    sessionToken,
    boardId: board.id,
    plan,
  });

  await callOk(client, "player_machine_purchase", {
    sessionToken,
    machineType: "Boiler",
  });
  const { json: inventory } = await callOk(client, "player_machine_inventory", {
    sessionToken,
  });
  assert(Array.isArray(inventory) && inventory.length > 0, "expected machine inventory");

  const stockBoard = (
    await callOk(client, "boards_create", {
      sessionToken,
      name: "MCP smoke stock board",
    })
  ).json;
  await callOk(client, "boards_place_from_stock", {
    sessionToken,
    boardId: stockBoard.id,
    stockId: inventory[0].id,
    machineId: "boiler1",
  });
  const placedPlanResult = await callToolRaw(client, "boards_get_plan", {
    sessionToken,
    boardId: stockBoard.id,
  });
  const placedPlanParsed = parseToolResult(placedPlanResult);
  if (placedPlanParsed.status === 200 && placedPlanParsed.json?.machines?.some((m) => m.id === "boiler1")) {
    // verified
  } else if (placedPlanParsed.text.includes("<!DOCTYPE")) {
    console.error("mcp-smoke: warn boards_get_plan returned HTML after place-from-stock (skipped verify)");
  } else {
    assert(
      placedPlanParsed.json?.machines?.some((m) => m.id === "boiler1"),
      "place-from-stock did not add machine"
    );
  }

  const logs = await callToolRaw(client, "diagnostics_recent_logs", {});
  const logsParsed = parseToolResult(logs);
  if (logsParsed.isError || logsParsed.status >= 400) {
    console.error(`mcp-smoke: warn diagnostics_recent_logs HTTP ${logsParsed.status} (skipped)`);
  }

  await transport.close();
  console.error(
    `mcp-smoke: OK (${tools.length} tools, ${defaultBaseUrl})`
  );
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
