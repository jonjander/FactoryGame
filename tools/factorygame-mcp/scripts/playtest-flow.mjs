/**
 * Full headless playtest via MCP stdio against FACTORYGAME_BASE_URL.
 * Run from package root: npm run playtest
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  assert,
  callOk,
  callOkOrNullOn404,
  callToolRaw,
  createMcpClient,
  defaultBaseUrl,
  guestAuth,
  parseToolResult,
  sleep,
} from "./mcp-client-helpers.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const plans = JSON.parse(
  readFileSync(join(__dirname, "..", "fixtures", "plans.json"), "utf8")
);

const POLL_ATTEMPTS = 15;
const POLL_MS = 500;

async function pollRunning(client, sessionToken, boardId) {
  for (let i = 0; i < POLL_ATTEMPTS; i++) {
    const json = await callOkOrNullOn404(client, "boards_keyframe_latest", {
      sessionToken,
      boardId,
    });
    if (json?.mode === "Running") return json;
    await sleep(POLL_MS);
  }
  throw new Error(
    `Board did not reach Running within ${POLL_ATTEMPTS * POLL_MS}ms — try diagnostics_recent_logs`
  );
}

async function tryOptionalMarketOrder(client, sessionToken) {
  try {
    const { json: summary } = await callOk(client, "market_summary", {
      sessionToken,
      locale: "en",
    });
    const row = summary?.[0];
    if (!row?.elementId || !row?.dna) {
      console.error("playtest: skip market_place_order (no market summary row)");
      return;
    }
    const { json: depth } = await callOk(client, "market_element_depth", {
      elementId: row.elementId,
      dna: String(row.dna),
    });
    if (!depth?.bestAsk) {
      console.error("playtest: skip market_place_order (no bestAsk on element 1)");
      return;
    }
    await callOk(client, "market_place_order", {
      sessionToken,
      elementId: row.elementId,
      dna: String(row.dna),
      side: "buy",
      limitPrice: depth.bestAsk,
      quantity: 1,
      idempotencyKey: `playtest-buy-${Date.now()}`,
    });
    console.error("playtest: market_place_order OK");
  } catch (e) {
    console.error(`playtest: skip market_place_order (${e.message})`);
  }
}

async function main() {
  const { client, transport } = await createMcpClient("factorygame-playtest");
  const sessionToken = await guestAuth(client, "playtest");

  console.error("playtest: content + player");
  await callOk(client, "content_list_elements", { locale: "en" });
  await callOk(client, "content_wiki", { locale: "en" });
  await callOk(client, "player_wallet", { sessionToken });
  await callOk(client, "player_pool_view", { sessionToken, locale: "en" });

  console.error("playtest: market");
  await callOk(client, "market_summary", { sessionToken, locale: "en" });
  await callOk(client, "market_element_depth", { elementId: 1 });

  console.error("playtest: factory board A (minimal loop)");
  const plan = plans.minimalLoop.plan;
  const { json: boardA } = await callOk(client, "boards_create", {
    sessionToken,
    name: "Playtest factory",
  });
  await callOk(client, "boards_save_plan", {
    sessionToken,
    boardId: boardA.id,
    plan,
  });
  await callOk(client, "boards_info_preview", {
    sessionToken,
    boardId: boardA.id,
    plan,
  });
  const { json: savedPlan } = await callOk(client, "boards_get_plan", {
    sessionToken,
    boardId: boardA.id,
  });
  assert(savedPlan?.connections?.length === 2, "expected 2 connections in saved plan");

  console.error("playtest: machine store + place from stock (board B)");
  await callOk(client, "content_machine_store", {});
  await callOk(client, "player_machine_purchase", {
    sessionToken,
    machineType: "Boiler",
  });
  const { json: inventory } = await callOk(client, "player_machine_inventory", {
    sessionToken,
  });
  assert(inventory?.length > 0, "expected purchased machine in inventory");
  const { json: boardB } = await callOk(client, "boards_create", {
    sessionToken,
    name: "Playtest stock",
  });
  await callOk(client, "boards_place_from_stock", {
    sessionToken,
    boardId: boardB.id,
    stockId: inventory[0].id,
    machineId: "boiler1",
  });
  const { json: stockPlan } = await callOk(client, "boards_get_plan", {
    sessionToken,
    boardId: boardB.id,
  });
  assert(
    stockPlan?.machines?.some((m) => m.type === "Boiler"),
    "board B missing placed boiler"
  );

  console.error("playtest: start + poll board A");
  await callOk(client, "boards_start", { sessionToken, boardId: boardA.id });
  const keyframe = await pollRunning(client, sessionToken, boardA.id);
  assert(keyframe.mode === "Running", "expected Running mode");

  let infoJson = null;
  for (let i = 0; i < 10; i++) {
    const { json } = await callOk(client, "boards_info", { sessionToken, boardId: boardA.id });
    infoJson = json;
    if (json?.throughput?.totalUnitsPerSecond > 0) break;
    await sleep(500);
  }
  assert(infoJson != null, "boards_info missing");
  const errors = (infoJson.issues ?? []).filter((i) => i.severity === "error");
  assert(errors.length === 0, `expected no error issues: ${JSON.stringify(errors)}`);
  assert(
    infoJson.throughput?.totalUnitsPerSecond > 0,
    `expected throughput > 0, got ${infoJson.throughput?.totalUnitsPerSecond}`
  );

  await callOk(client, "boards_stop", { sessionToken, boardId: boardA.id });

  await tryOptionalMarketOrder(client, sessionToken);
  try {
    await callOk(client, "market_orders_mine", { sessionToken });
  } catch (e) {
    console.error(`playtest: skip market_orders_mine (${e.message})`);
  }

  await transport.close();
  console.error(`playtest-flow: OK (${defaultBaseUrl})`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
