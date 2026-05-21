/**
 * Iteration 2: two guests trade on market + player B runs liquidSeparator factory.
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = (process.env.FACTORYGAME_BASE_URL || "http://localhost:5176").replace(/\/+$/, "");
const __dirname = dirname(fileURLToPath(import.meta.url));
const plans = JSON.parse(readFileSync(join(__dirname, "..", "fixtures", "plans.json"), "utf8"));
const ELEMENT_LIQUID = 7; // same as SimpleGameFlowTests

async function api(path, { method = "GET", token, body } = {}) {
  const headers = { Accept: "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body) headers["Content-Type"] = "application/json";
  const res = await fetch(`${base}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    /* ignore */
  }
  return { status: res.status, json, text: text.slice(0, 400) };
}

const ok = (status) => status >= 200 && status < 300;

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function guest(label) {
  const r = await api("/v1/auth/guest", {
    method: "POST",
    body: { deviceKey: `${label}-${Date.now()}` },
  });
  if (r.status !== 200) throw new Error(`${label} auth ${r.status}: ${r.text}`);
  return { token: r.json.sessionToken, playerId: r.json.playerId };
}

const findings = [];

function note(msg) {
  findings.push(msg);
  console.error(`[finding] ${msg}`);
}

console.error("=== Iteration 2: two players + liquid separator ===");

const playerA = await guest("playerA");
const playerB = await guest("playerB");
console.error("playerA", playerA.playerId);
console.error("playerB", playerB.playerId);

await api("/v1/market/summary", { token: playerA.token });
await api("/v1/market/summary", { token: playerB.token });

const depth = await api(`/v1/market/elements/${ELEMENT_LIQUID}/depth`);
if (!depth.json?.bestAsk) note(`element ${ELEMENT_LIQUID}: no bestAsk — liquidity gap`);
else {
  const buyA = await api("/v1/market/orders", {
    method: "POST",
    token: playerA.token,
    body: {
      elementId: ELEMENT_LIQUID,
      side: "buy",
      limitPrice: depth.json.bestAsk + 2,
      quantity: 20,
      idempotencyKey: `iter2-a-buy-${Date.now()}`,
    },
  });
  console.error("playerA buy", buyA.status, buyA.json?.status, "filled", buyA.json?.quantityFilled);
  if (!ok(buyA.status)) note(`playerA buy failed: ${buyA.status} ${buyA.text}`);

  const walletA0 = await api("/v1/me/wallet", { token: playerA.token });
  const bestAsk = depth.json.bestAsk;
  const playerSellPrice = Math.max(0.01, bestAsk - 0.01);
  const sellA = await api("/v1/market/orders", {
    method: "POST",
    token: playerA.token,
    body: {
      elementId: ELEMENT_LIQUID,
      side: "sell",
      limitPrice: playerSellPrice,
      quantity: 5,
      idempotencyKey: `iter2-a-sell-${Date.now()}`,
    },
  });
  console.error("playerA sell (resting)", sellA.status, sellA.json?.status);

  const buyB = await api("/v1/market/orders", {
    method: "POST",
    token: playerB.token,
    body: {
      elementId: ELEMENT_LIQUID,
      side: "buy",
      limitPrice: playerSellPrice,
      quantity: 5,
      idempotencyKey: `iter2-b-buy-${Date.now()}`,
    },
  });
  console.error("playerB buy (vs A sell)", buyB.status, buyB.json?.status, "filled", buyB.json?.quantityFilled);

  const walletA1 = await api("/v1/me/wallet", { token: playerA.token });
  const walletB1 = await api("/v1/me/wallet", { token: playerB.token });
  const cashDeltaA = (walletA1.json?.cash ?? 0) - (walletA0.json?.cash ?? 0);
  console.error("playerA cash delta after trade", cashDeltaA);
  console.error("playerB cash", walletB1.json?.cash);

  if (buyB.json?.quantityFilled > 0) console.error("P2P trade: OK");
  else note("playerB did not fill against playerA sell — matchning eller pris");
}

// Player B: buy element for factory, machines, liquid separator plan
const depthB = await api(`/v1/market/elements/${ELEMENT_LIQUID}/depth`);
if (depthB.json?.bestAsk) {
  await api("/v1/market/orders", {
    method: "POST",
    token: playerB.token,
    body: {
      elementId: ELEMENT_LIQUID,
      side: "buy",
      limitPrice: depthB.json.bestAsk + 2,
      quantity: 30,
      idempotencyKey: `iter2-b-factory-buy-${Date.now()}`,
    },
  });
}

for (const type of ["SeaportConnector", "LiquidSeparator"]) {
  const p = await api("/v1/me/machine-inventory/purchase", {
    method: "POST",
    token: playerB.token,
    body: { machineType: type },
  });
  if (!ok(p.status)) note(`purchase ${type}: ${p.status} ${p.text}`);
}

const board = await api("/v1/boards", {
  method: "POST",
  token: playerB.token,
  body: { name: "Iter2 separator" },
});
const boardId = board.json?.id;
const plan = {
  machines: [
    { id: "sea1", type: "SeaportConnector", settings: { outElementId: ELEMENT_LIQUID } },
    { id: "sep1", type: "LiquidSeparator", settings: { cutFreeze: 2048 } },
  ],
  connections: plans.liquidSeparatorFlow.plan.connections,
};
const savePlan = await api(`/v1/boards/${boardId}/plan`, {
  method: "PUT",
  token: playerB.token,
  body: { plan },
});
if (!ok(savePlan.status)) note(`save plan: ${savePlan.status} ${savePlan.text}`);
const preview = await api(`/v1/boards/${boardId}/info/preview`, {
  method: "POST",
  token: playerB.token,
  body: { plan },
});
console.error("preview cycle", preview.json?.planHasCycle, "connections", preview.json?.planConnectionCount);

const start = await api(`/v1/boards/${boardId}/start`, { method: "POST", token: playerB.token });
if (!ok(start.status)) note(`board start: ${start.status} ${start.text}`);
for (let i = 0; i < 10; i++) {
  await sleep(500);
  const kf = await api(`/v1/boards/${boardId}/keyframes/latest`, { token: playerB.token });
  if (kf.json?.mode === "Running" && i % 5 === 4) {
    console.error(`poll ${i}: tick=${kf.json?.tick} Running`);
  }
}
const info = await api(`/v1/boards/${boardId}/info`, { token: playerB.token });
console.error("board info", info.json?.mode, "seaport out", info.json?.seaport?.outOfFactory?.length);
await api(`/v1/boards/${boardId}/stop`, { method: "POST", token: playerB.token });

const poolB = await api("/v1/me/pool/view", { token: playerB.token, body: null });
const stacks = poolB.json?.stacks ?? poolB.json?.Stacks ?? [];
console.error("playerB pool stacks", stacks.length);

const mine = await api("/v1/market/orders/mine", { token: playerA.token });
console.error("playerA open orders", mine.status, mine.json?.length ?? 0);

console.error("=== Findings:", findings.length ? findings.join("; ") : "none ===");
if (findings.length) process.exit(1);
console.error("dev-lead-iter2: OK");
