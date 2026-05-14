const defaultBaseUrl =
  "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net";

export function getBaseUrl(): string {
  const raw = process.env.FACTORYGAME_BASE_URL?.trim() || defaultBaseUrl;
  return raw.replace(/\/+$/, "");
}
