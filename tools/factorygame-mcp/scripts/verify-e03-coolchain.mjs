/**
 * Verifies user E03 cool-chain layout: preview + run, checks solid output / throughput.
 * Requires local API: http://localhost:5176
 */
import {
  assert,
  callOk,
  createMcpClient,
  guestAuth,
  sleep,
} from "./mcp-client-helpers.mjs";

const E03_ELEMENT_ID = 3;
const E03_POOL_DNA = "144964032628459529";

const plan = {
  machines: [
    {
      id: "seaportconnector1",
      type: "SeaportConnector",
      settings: {
        x: 40,
        y: 40,
        outElementId: E03_ELEMENT_ID,
        outMaterialDna: E03_POOL_DNA,
      },
    },
    {
      id: "seaportconnector2",
      type: "SeaportConnector",
      settings: { x: 352, y: 137 },
    },
    {
      id: "cooler1",
      type: "Cooler",
      settings: { x: 274, y: 8, coolDelta: 16 },
    },
    {
      id: "crystallizer1",
      type: "Crystallizer",
      settings: { x: 456, y: 39, chillDelta: 32, cutFreeze: 2048 },
    },
    {
      id: "cooler2",
      type: "Cooler",
      settings: { x: 40, y: 140, coolDelta: 16 },
    },
  ],
  connections: [
    { fromId: "seaportconnector1", fromPort: "out", toId: "cooler1", toPort: "in" },
    { fromId: "crystallizer1", fromPort: "out", toId: "seaportconnector2", toPort: "in" },
    { fromId: "cooler1", fromPort: "out", toId: "cooler2", toPort: "in" },
    { fromId: "cooler2", fromPort: "out", toId: "crystallizer1", toPort: "in" },
  ],
};

const POLL_ATTEMPTS = 40;
const POLL_MS = 500;

async function main() {
  const { client, transport } = await createMcpClient("verify-e03");
  const sessionToken = await guestAuth(client, `e03-verify-${Date.now()}`);

  for (const t of ["SeaportConnector", "Cooler", "Crystallizer"]) {
    await callOk(client, "player_machine_purchase", {
      sessionToken,
      machineType: t,
      quantity: t === "SeaportConnector" ? 2 : t === "Cooler" ? 2 : 1,
    });
  }

  const { json: depth } = await callOk(client, "market_element_depth", {
    sessionToken,
    elementId: E03_ELEMENT_ID,
    dna: E03_POOL_DNA,
  });
  assert(depth?.bestAsk, "No bestAsk for E03 pool DNA — enable MarketLiquidity in dev");

  await callOk(client, "market_place_order", {
    sessionToken,
    elementId: E03_ELEMENT_ID,
    dna: E03_POOL_DNA,
    side: "buy",
    limitPrice: depth.bestAsk,
    quantity: 50,
    idempotencyKey: `e03-buy-${Date.now()}`,
  });

  const { json: board } = await callOk(client, "boards_create", {
    sessionToken,
    name: "E03 cool chain verify",
  });

  const { json: preview } = await callOk(client, "boards_info_preview", {
    sessionToken,
    boardId: board.id,
    plan,
  });

  console.log("preview status:", preview?.statusHint ?? preview?.status);
  console.log("preview issues:", (preview?.issues ?? []).map((i) => i.message ?? i).join(" | "));

  await callOk(client, "boards_save_plan", {
    sessionToken,
    boardId: board.id,
    plan,
  });

  await callOk(client, "boards_start", { sessionToken, boardId: board.id });

  let lastInfo = null;
  for (let i = 0; i < POLL_ATTEMPTS; i++) {
    const { json: kf } = await callOk(client, "boards_keyframe_latest", {
      sessionToken,
      boardId: board.id,
    });
    if (kf?.mode !== "Running") {
      await sleep(POLL_MS);
      continue;
    }
    const { json: info } = await callOk(client, "boards_info", {
      sessionToken,
      boardId: board.id,
    });
    lastInfo = info;
    const tp = info?.throughputUnitsPerSecond ?? info?.ThroughputUnitsPerSecond;
    const stored = info?.seaportStoredQuantity ?? info?.SeaportStoredQuantity;
    console.log(`poll ${i}: throughput=${tp} stored=${stored} hint=${info?.statusHint}`);
    if (stored > 0 || (tp != null && tp > 0)) break;
    await sleep(POLL_MS);
  }

  assert(lastInfo, "No boards_info after start");
  console.log("final:", JSON.stringify(lastInfo, null, 2).slice(0, 1200));

  await callOk(client, "boards_stop", { sessionToken, boardId: board.id });
  await transport.close();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
