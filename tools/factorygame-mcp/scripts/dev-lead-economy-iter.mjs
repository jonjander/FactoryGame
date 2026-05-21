/**
 * Dev-lead iteration: minimal factory + wallet delta + optional sell.
 * Usage: FACTORYGAME_BASE_URL=http://localhost:5176 node scripts/dev-lead-economy-iter.mjs
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = (process.env.FACTORYGAME_BASE_URL || "http://localhost:5176").replace(/\/+$/, "");
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

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const auth = await api("/v1/auth/guest", {
  method: "POST",
  body: { deviceKey: `dev-lead-econ-${Date.now()}` },
});
if (auth.status !== 200) throw new Error(`auth ${auth.status}: ${auth.text}`);
const token = auth.json.sessionToken;
console.error("playerId", auth.json.playerId);

const wallet0 = await api("/v1/me/wallet", { token });
console.error("wallet before", wallet0.json);

const board = await api("/v1/boards", {
  method: "POST",
  token,
  body: { name: "DevLead econ" },
});
const boardId = board.json.id;
const plan = plans.minimalLoop.plan;
await api(`/v1/boards/${boardId}/plan`, { method: "PUT", token, body: plan });
await api(`/v1/boards/${boardId}/start`, { method: "POST", token });

for (let i = 0; i < 12; i++) {
  await sleep(500);
  const kf = await api(`/v1/boards/${boardId}/keyframes/latest`, { token });
  if (kf.json?.mode === "Running") console.error(`tick poll ${i}: Running`);
}

await api(`/v1/boards/${boardId}/stop`, { method: "POST", token });
const wallet1 = await api("/v1/me/wallet", { token });
console.error("wallet after", wallet1.json);

const tx = await api("/v1/me/transactions?limit=10", { token });
console.error("transactions", tx.json?.length ?? tx.status, tx.json?.slice?.(0, 3));

const depth = await api("/v1/market/elements/1/depth");
if (depth.json?.bestBid) {
  const sell = await api("/v1/market/orders", {
    method: "POST",
    token,
    body: {
      elementId: 1,
      side: "sell",
      limitPrice: depth.json.bestBid,
      quantity: 1,
      idempotencyKey: `dev-lead-sell-${Date.now()}`,
    },
  });
  console.error("sell order", sell.status, sell.json?.status ?? sell.text);
}

const mine = await api("/v1/market/orders/mine", { token });
console.error("my open orders", mine.status, mine.json?.length ?? 0);

const cashDelta =
  (wallet1.json?.cash ?? 0) - (wallet0.json?.cash ?? 0);
console.error(`cash delta: ${cashDelta}`);
console.error("dev-lead-economy-iter: OK");
