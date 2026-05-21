/**
 * Full playtest against local API (default http://localhost:5176).
 * Requires: dotnet run --project src/FactoryGame.Api --launch-profile http
 */
import { waitForLocalApi } from "./wait-for-local-api.mjs";

const localBaseUrl =
  process.env.FACTORYGAME_BASE_URL?.trim() || "http://localhost:5176";
process.env.FACTORYGAME_BASE_URL = localBaseUrl;

console.error(`playtest-local: waiting for ${localBaseUrl} ...`);
await waitForLocalApi(localBaseUrl);
await import("./playtest-flow.mjs");
