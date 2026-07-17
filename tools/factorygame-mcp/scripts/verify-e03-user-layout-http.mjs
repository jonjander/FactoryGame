/**
 * Direct HTTP playtest (no MCP stdio). API: http://localhost:5176
 */
const BASE = process.env.FACTORYGAME_BASE_URL?.trim() || "http://localhost:5176";
const E03 = 3;
const DNA = "144964032628459529";

const plan = {
  machines: [
    {
      id: "seaportconnector1",
      type: "SeaportConnector",
      settings: {
        x: 0,
        y: 38.75,
        outElementId: E03,
        outMaterialDna: DNA,
      },
    },
    {
      id: "seaportconnector2",
      type: "SeaportConnector",
      settings: { x: 980.53, y: 115.73 },
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

async function api(method, path, body, token) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} ${res.status}: ${text.slice(0, 400)}`);
  return text ? JSON.parse(text) : null;
}

function e03Stacks(pool) {
  const groups = pool?.groups ?? pool?.Groups ?? [];
  return groups
    .flatMap((g) => g.variants ?? g.Variants ?? [])
    .filter((v) => (v.elementId ?? v.ElementId) === E03);
}

async function main() {
  const auth = await api("POST", "/v1/auth/guest", { deviceKey: `e03-http-${Date.now()}` });
  const token = auth.sessionToken;
  console.log("playerId", auth.playerId);

  const wallet0 = await api("GET", "/v1/me/wallet", null, token);
  console.log("wallet before purchases", wallet0?.cash);

  for (const [type, qty] of [
    ["SeaportConnector", 2],
    ["Cooler", 6],
    ["Crystallizer", 2],
  ]) {
    await api("POST", "/v1/me/machine-inventory/purchase", { machineType: type, quantity: qty }, token);
  }

  const wallet1 = await api("GET", "/v1/me/wallet", null, token);
  console.log("wallet after machines", wallet1?.cash);

  const depth = await api("GET", `/v1/market/elements/${E03}/depth?dna=${DNA}`, null, token);
  if (!depth?.bestAsk) throw new Error("No bestAsk for E03");
  const buyQty = 80;
  await api(
    "POST",
    "/v1/market/orders",
    {
      elementId: E03,
      dna: DNA,
      side: "buy",
      limitPrice: depth.bestAsk,
      quantity: buyQty,
      idempotencyKey: `buy-${Date.now()}`,
    },
    token
  );

  const pool1 = await api("GET", "/v1/me/pool/view?locale=en", null, token);
  const liquid = e03Stacks(pool1).find((v) => String(v.dna) === DNA);
  if (!liquid?.quantity) throw new Error("No E03 liquid in pool");
  console.log("liquid qty", liquid.quantity, "phase", liquid.phase);

  const board = await api("POST", "/v1/boards", { name: "E03 user HTTP verify" }, token);
  await api("PUT", `/v1/boards/${board.id}/plan`, { plan }, token);
  await api("POST", `/v1/boards/${board.id}/start`, null, token);

  let solidQty = 0;
  const maxPolls = Number(process.env.E03_MAX_POLLS ?? 900);
  for (let i = 0; i < maxPolls; i++) {
    await new Promise((r) => setTimeout(r, 1000));
    const info = await api("GET", `/v1/boards/${board.id}/info`, null, token);
    const outUps = info?.seaport?.outOfFactory?.[0]?.unitsPerSecond ?? 0;
    const pool = await api("GET", "/v1/me/pool/view?locale=en", null, token);
    const solid = e03Stacks(pool).filter(
      (v) => (v.phase === "Solid" || v.phase === 2) && String(v.dna) !== DNA
    );
    solidQty = solid.reduce((s, v) => s + Number(v.quantity ?? 0), 0);
    console.log(`poll ${i + 1}: tick=${info.simulationTick} outUps=${outUps} solidQty=${solidQty}`);
    if (solidQty > 0 && outUps > 0) break;
  }

  const poolF = await api("GET", "/v1/me/pool/view?locale=en", null, token);
  console.log("E03 stacks:", JSON.stringify(e03Stacks(poolF), null, 2));
  await api("POST", `/v1/boards/${board.id}/stop`, null, token);

  if (solidQty <= 0) {
    console.error("FAIL: no solid E03 in pool");
    process.exit(1);
  }
  console.log("OK solidQty=", solidQty);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
