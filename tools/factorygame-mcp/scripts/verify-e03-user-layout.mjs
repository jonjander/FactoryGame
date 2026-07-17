/**
 * Playtest: user E03 layout (6 coolers + 2 crystallizers). Requires API on :5176.
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
        x: 0,
        y: 38.75200653076172,
        outElementId: E03_ELEMENT_ID,
        outMaterialDna: E03_POOL_DNA,
      },
    },
    {
      id: "seaportconnector2",
      type: "SeaportConnector",
      settings: { x: 980.527587890625, y: 115.72689819335938 },
    },
    { id: "cooler1", type: "Cooler", settings: { x: 249.27, y: 44.26, coolDelta: 16 } },
    {
      id: "crystallizer1",
      type: "Crystallizer",
      settings: { x: 170.71, y: 140.54, chillDelta: 8, cutFreeze: 2048 },
    },
    { id: "cooler2", type: "Cooler", settings: { x: 455.33, y: 42.07, coolDelta: 16 } },
    { id: "cooler3", type: "Cooler", settings: { x: 616.95, y: 106.46, coolDelta: 16 } },
    { id: "cooler4", type: "Cooler", settings: { x: 766.71, y: 47.05, coolDelta: 16 } },
    { id: "cooler5", type: "Cooler", settings: { x: 666.59, y: 246.91, coolDelta: 16 } },
    { id: "cooler6", type: "Cooler", settings: { x: 885.92, y: 262.87, coolDelta: 16 } },
    {
      id: "crystallizer2",
      type: "Crystallizer",
      settings: { x: 190, y: 240, cutFreeze: 3072 },
    },
  ],
  connections: [
    { fromId: "seaportconnector1", fromPort: "out", toId: "cooler1", toPort: "in" },
    { fromId: "cooler1", fromPort: "out", toId: "cooler2", toPort: "in" },
    { fromId: "cooler2", fromPort: "out", toId: "cooler3", toPort: "in" },
    { fromId: "cooler3", fromPort: "out", toId: "cooler4", toPort: "in" },
    { fromId: "cooler4", fromPort: "out", toId: "cooler5", toPort: "in" },
    { fromId: "cooler5", fromPort: "out", toId: "cooler6", toPort: "in" },
    { fromId: "cooler6", fromPort: "out", toId: "crystallizer1", toPort: "in" },
    { fromId: "crystallizer1", fromPort: "out", toId: "crystallizer2", toPort: "in" },
    { fromId: "crystallizer2", fromPort: "out", toId: "seaportconnector2", toPort: "in" },
  ],
};

const POLL = 60;
const POLL_MS = 1000;

async function main() {
  const { client, transport } = await createMcpClient("verify-e03-user");
  const sessionToken = await guestAuth(client, `e03-user-${Date.now()}`);

  const { json: poolBefore } = await callOk(client, "player_pool_view", {
    sessionToken,
    locale: "en",
  });
  const e03Stacks =
    poolBefore?.groups?.flatMap((g) => g.variants ?? [])?.filter((v) => v.elementId === E03_ELEMENT_ID) ??
    [];
  console.log("pool E03 before:", JSON.stringify(e03Stacks, null, 2));

  for (const t of [
    "SeaportConnector",
    "Cooler",
    "Crystallizer",
  ]) {
    const qty = t === "SeaportConnector" ? 2 : t === "Cooler" ? 6 : 2;
    await callOk(client, "player_machine_purchase", {
      sessionToken,
      machineType: t,
      quantity: qty,
    });
  }

  const { json: depth } = await callOk(client, "market_element_depth", {
    sessionToken,
    elementId: E03_ELEMENT_ID,
    dna: E03_POOL_DNA,
  });
  assert(depth?.bestAsk, "No sell side for E03 liquid DNA");

  await callOk(client, "market_place_order", {
    sessionToken,
    elementId: E03_ELEMENT_ID,
    dna: E03_POOL_DNA,
    side: "buy",
    limitPrice: depth.bestAsk,
    quantity: 200,
    idempotencyKey: `e03-buy-${Date.now()}`,
  });

  const { json: poolAfterBuy } = await callOk(client, "player_pool_view", {
    sessionToken,
    locale: "en",
  });
  const liquid = poolAfterBuy?.groups
    ?.flatMap((g) => g.variants ?? [])
    ?.find((v) => v.elementId === E03_ELEMENT_ID && String(v.dna) === E03_POOL_DNA);
  assert(liquid?.quantity > 0, "E03 liquid not in pool after buy");
  console.log(`E03 liquid qty after buy: ${liquid.quantity}, phase: ${liquid.phase ?? "?"}`);

  const { json: board } = await callOk(client, "boards_create", {
    sessionToken,
    name: "E03 user layout verify",
  });

  const { json: preview } = await callOk(client, "boards_info_preview", {
    sessionToken,
    boardId: board.id,
    plan,
  });
  console.log(
    "preview issues:",
    (preview?.issues ?? []).map((i) => `${i.severity}:${i.code} ${i.message}`).join(" | ")
  );

  await callOk(client, "boards_save_plan", {
    sessionToken,
    boardId: board.id,
    plan,
  });

  await callOk(client, "boards_start", { sessionToken, boardId: board.id });

  let solidQty = 0;
  let outUps = 0;
  for (let i = 0; i < POLL; i++) {
    await sleep(POLL_MS);
    const { json: info } = await callOk(client, "boards_info", {
      sessionToken,
      boardId: board.id,
    });
    outUps =
      info?.seaport?.outOfFactory?.[0]?.unitsPerSecond ??
      info?.Seaport?.OutOfFactory?.[0]?.UnitsPerSecond ??
      0;
    const { json: pool } = await callOk(client, "player_pool_view", {
      sessionToken,
      locale: "en",
    });
    const stacks =
      pool?.groups?.flatMap((g) => g.variants ?? [])?.filter((v) => v.elementId === E03_ELEMENT_ID) ??
      [];
    const solid = stacks.filter(
      (v) =>
        (v.phase === "Solid" || v.phase === 2) &&
        String(v.dna) !== E03_POOL_DNA
    );
    solidQty = solid.reduce((s, v) => s + (v.quantity ?? 0), 0);
    console.log(
      `poll ${i + 1}: tick=${info?.simulationTick} outUps=${outUps} solidStacks=${solid.length} solidQty=${solidQty}`
    );
    if (solidQty > 0 && outUps > 0) break;
  }

  const { json: poolFinal } = await callOk(client, "player_pool_view", {
    sessionToken,
    locale: "en",
  });
  console.log(
    "pool E03 final:",
    JSON.stringify(
      poolFinal?.groups?.flatMap((g) => g.variants ?? [])?.filter((v) => v.elementId === E03_ELEMENT_ID),
      null,
      2
    )
  );

  await callOk(client, "boards_stop", { sessionToken, boardId: board.id });
  await transport.close();

  if (solidQty <= 0) {
    console.error("FAIL: no solid E03 deposited to pool");
    process.exit(1);
  }
  console.log("OK: solid E03 in pool, qty=", solidQty);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
