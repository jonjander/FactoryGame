import { getBaseUrl } from "./config.js";

export type PlayerAuthArgs = {
  sessionToken?: string | undefined;
  apiKey?: string | undefined;
};

export type FetchOptions = {
  locale?: string | undefined;
};

function resolvePlayerHeaders(args: PlayerAuthArgs): Record<string, string> {
  const apiKey = args.apiKey?.trim() || process.env.FACTORYGAME_API_KEY?.trim();
  const session =
    args.sessionToken?.trim() || process.env.FACTORYGAME_SESSION_TOKEN?.trim();
  if (apiKey) return { "X-Api-Key": apiKey };
  if (session) return { Authorization: `Bearer ${session}` };
  return {};
}

function applyLocale(headers: Record<string, string>, options?: FetchOptions): void {
  const locale = options?.locale?.trim();
  if (locale) headers["Accept-Language"] = locale;
}

export async function fetchPublic(
  method: string,
  pathAndQuery: string,
  body?: unknown,
  options?: FetchOptions
): Promise<{ ok: boolean; status: number; bodyText: string; json: unknown }> {
  const url = `${getBaseUrl()}${pathAndQuery}`;
  const headers: Record<string, string> = { Accept: "application/json" };
  applyLocale(headers, options);
  if (body !== undefined) headers["Content-Type"] = "application/json";
  const res = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const bodyText = await res.text();
  let json: unknown;
  try {
    json = bodyText ? JSON.parse(bodyText) : null;
  } catch {
    json = null;
  }
  return { ok: res.ok, status: res.status, bodyText, json };
}

export async function fetchPlayer(
  method: string,
  pathAndQuery: string,
  auth: PlayerAuthArgs,
  body?: unknown,
  options?: FetchOptions
): Promise<{ ok: boolean; status: number; bodyText: string; json: unknown }> {
  const playerHeaders = resolvePlayerHeaders(auth);
  if (Object.keys(playerHeaders).length === 0) {
    return {
      ok: false,
      status: 0,
      bodyText:
        "Missing auth: set FACTORYGAME_SESSION_TOKEN or FACTORYGAME_API_KEY, or pass sessionToken / apiKey on the tool.",
      json: null,
    };
  }
  const url = `${getBaseUrl()}${pathAndQuery}`;
  const headers: Record<string, string> = { ...playerHeaders, Accept: "application/json" };
  applyLocale(headers, options);
  if (body !== undefined) headers["Content-Type"] = "application/json";
  const res = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const bodyText = await res.text();
  let json: unknown;
  try {
    json = bodyText ? JSON.parse(bodyText) : null;
  } catch {
    json = null;
  }
  return { ok: res.ok, status: res.status, bodyText, json };
}

export async function fetchAdmin(
  method: string,
  pathAndQuery: string,
  body?: unknown
): Promise<{ ok: boolean; status: number; bodyText: string; json: unknown }> {
  const token = process.env.FACTORYGAME_ADMIN_TOKEN?.trim();
  if (!token) {
    return {
      ok: false,
      status: 0,
      bodyText:
        "Missing FACTORYGAME_ADMIN_TOKEN in environment (never pass admin tokens as tool arguments).",
      json: null,
    };
  }
  const url = `${getBaseUrl()}${pathAndQuery}`;
  const headers: Record<string, string> = {
    "X-Admin-Token": token,
  };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  const res = await fetch(url, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const bodyText = await res.text();
  let json: unknown;
  try {
    json = bodyText ? JSON.parse(bodyText) : null;
  } catch {
    json = null;
  }
  return { ok: res.ok, status: res.status, bodyText, json };
}

export async function fetchDiagnostics(): Promise<{
  ok: boolean;
  status: number;
  bodyText: string;
}> {
  const url = `${getBaseUrl()}/diagnostics/recent-logs`;
  const res = await fetch(url, { method: "GET" });
  const bodyText = await res.text();
  return { ok: res.ok, status: res.status, bodyText };
}

export function formatToolResult(r: {
  ok: boolean;
  status: number;
  bodyText: string;
  json: unknown;
}): { content: Array<{ type: "text"; text: string }>; isError?: boolean } {
  const payload =
    r.json !== null && r.json !== undefined
      ? JSON.stringify(r.json, null, 2)
      : r.bodyText;
  const text = `HTTP ${r.status}\n${payload}`;
  if (!r.ok || r.status === 0) {
    return { content: [{ type: "text", text }], isError: true };
  }
  return { content: [{ type: "text", text }] };
}

export function formatDiagnostics(r: {
  ok: boolean;
  status: number;
  bodyText: string;
}): { content: Array<{ type: "text"; text: string }>; isError?: boolean } {
  const text = `HTTP ${r.status}\n${r.bodyText}`;
  if (!r.ok) return { content: [{ type: "text", text }], isError: true };
  return { content: [{ type: "text", text }] };
}
