const defaultAzureBaseUrl =
  "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net";

/** Local API http profile — use via FACTORYGAME_BASE_URL or factorygame-local MCP. */
export const localDevBaseUrl = "http://localhost:5176";

export function getBaseUrl(): string {
  const raw = process.env.FACTORYGAME_BASE_URL?.trim() || defaultAzureBaseUrl;
  return raw.replace(/\/+$/, "");
}
