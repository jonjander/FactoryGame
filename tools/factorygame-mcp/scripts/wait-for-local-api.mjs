/** Wait until GET /health returns Healthy (local API startup). */
export async function waitForLocalApi(
  baseUrl,
  { attempts = 30, intervalMs = 1000 } = {}
) {
  const base = baseUrl.replace(/\/+$/, "");
  let lastError = "unknown";

  for (let i = 0; i < attempts; i++) {
    try {
      const res = await fetch(`${base}/health`);
      const text = await res.text();
      if (res.ok && text.includes("Healthy")) return;
      lastError = `HTTP ${res.status}: ${text.slice(0, 80)}`;
    } catch (err) {
      lastError = err instanceof Error ? err.message : String(err);
    }
    await new Promise((r) => setTimeout(r, intervalMs));
  }

  throw new Error(
    `Local API not ready at ${base} (${lastError}). Start with:\n` +
      "  dotnet run --project src/FactoryGame.Api --launch-profile http"
  );
}
