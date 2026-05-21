/**
 * Iteration 3: liquid separator factory end-to-end (single player).
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = (process.env.FACTORYGAME_BASE_URL || "http://localhost:5176").replace(/\/+$/, "");
const TIMEOUT_MS = 30_000;
const ELEMENT = 7;
const __dirname = dirname(fileURLToPath(import.meta.url));
const plans = JSON.parse(readFileSync(join(__dirname, "..", "fixtures", "plans.json"), "utf8"));

async function api(path, { method = "GET", token, body } = {}) {
  const headers = { Accept: "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body) headers["Content-Type"] = "application/json";
  const res = await fetch(`${base}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    /* ignore */
  }
  return { status: res.status, json, text: text.slice(0, 300) };
}

const ok = (s) => s >= 200 && s < 300;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const auth = await api("/v1/auth/guest", {
  method: "POST",
  body: { deviceKey: `iter3-${Date.now()}` },
});
if (!ok(auth.status)) throw new Error(`auth ${auth.status}`);
const token = auth.json.sessionToken;
console.error("player", auth.json.playerId);

await api("/v1/market/summary", { token });
const depth = await api(`/v1/market/elements/${ELEMENT}/depth`);
console.error("depth bestAsk", depth.json?.bestAsk);
if (depth.json?.bestAsk) {
  const buy = await api("/v1/market/orders", {
    method: "POST",
    token,
    body: {
      elementId: ELEMENT,
      side: "buy",
      limitPrice: depth.json.bestAsk + 2,
      quantity: 30,
      idempotencyKey: `iter3-buy-${Date.now()}`,
    },
  });
  console.error("buy pool", buy.status, buy.json?.status, buy.json?.quantityFilled);
}

const board = await api("/v1/boards", { method: "POST", token, body: { name: "Iter3 sep" } });
const boardId = board.json.id;
const plan = {
  machines: [
    { id: "sea1", type: "SeaportConnector", settings: { outElementId: ELEMENT } },
    { id: "sep1", type: "LiquidSeparator", settings: { cutFreeze: 2048 } },
  ],
  connections: plans.liquidSeparatorFlow.plan.connections,
};

const save = await api(`/v1/boards/${boardId}/plan`, { method: "PUT", token, body: { plan } });
console.error("save plan", save.status);
const prev = await api(`/v1/boards/${boardId}/info/preview`, { method: "POST", token, body: { plan } });
console.error("preview", prev.json?.planHasCycle, "conns", prev.json?.planConnectionCount);

const start = await api(`/v1/boards/${boardId}/start`, { method: "POST", token });
console.error("start", start.status, start.text);

let running = false;
for (let i = 0; i < 8; i++) {
  await sleep(1000);
  const kf = await api(`/v1/boards/${boardId}/keyframes/latest`, { token });
  if (kf.json?.mode === "Running") {
    running = true;
    console.error(`poll ${i}: Running tick=${kf.json.tick ?? kf.json.Tick}`);
    break;
  }
}

const info = await api(`/v1/boards/${boardId}/info`, { token });
console.error("info mode", info.json?.mode, "active", info.json?.activeMachines);
await api(`/v1/boards/${boardId}/stop`, { method: "POST", token });

if (!ok(save.status)) process.exit(1);
if (!ok(start.status)) process.exit(1);
if (!running) {
  console.error("FAIL: board never reached Running");
  process.exit(1);
}
console.error("dev-lead-iter3-separator: OK");
