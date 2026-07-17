import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import * as z from "zod/v4";
import {
  fetchAdmin,
  fetchDiagnostics,
  fetchPlayer,
  fetchPublic,
  formatDiagnostics,
  formatToolResult,
} from "./http.js";

const optionalSession = {
  sessionToken: z
    .string()
    .optional()
    .describe("Bearer session from guest_auth; else FACTORYGAME_SESSION_TOKEN"),
  apiKey: z
    .string()
    .optional()
    .describe("X-Api-Key; else FACTORYGAME_API_KEY. Takes precedence over session."),
};

const optionalLocale = {
  locale: z
    .string()
    .optional()
    .describe("Accept-Language header (e.g. sv). Also used as ?locale= on content routes."),
};

const planSchema = z.object({
  machines: z.array(
    z.object({
      id: z.string(),
      type: z.string(),
      settings: z.unknown().optional(),
    })
  ),
  connections: z.array(
    z.object({
      fromId: z.string(),
      toId: z.string(),
      fromPort: z.string(),
      toPort: z.string(),
    })
  ),
});

function registerTools(mcp: McpServer): void {
  mcp.registerTool(
    "guest_auth",
    {
      description:
        "POST /v1/auth/guest — obtain playerId and sessionToken (guest session).",
      inputSchema: {
        deviceKey: z
          .string()
          .describe("Stable device identifier string (any non-empty string)."),
      },
    },
    async ({ deviceKey }) => {
      const r = await fetchPublic("POST", "/v1/auth/guest", { deviceKey });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "content_list_elements",
    {
      description: "GET /v1/content/elements — element catalog (public).",
      inputSchema: { ...optionalLocale },
    },
    async ({ locale }) => {
      const q = locale ? `?locale=${encodeURIComponent(locale)}` : "";
      const r = await fetchPublic("GET", `/v1/content/elements${q}`, undefined, {
        locale,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "content_wiki",
    {
      description: "GET /v1/content/wiki — wiki snapshot (public).",
      inputSchema: { ...optionalLocale },
    },
    async ({ locale }) => {
      const q = locale ? `?locale=${encodeURIComponent(locale)}` : "";
      const r = await fetchPublic("GET", `/v1/content/wiki${q}`, undefined, {
        locale,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "content_machine_store",
    {
      description:
        "GET /v1/content/machine-store — purchasable machines and port catalog (public).",
      inputSchema: {},
    },
    async () => {
      const r = await fetchPublic("GET", "/v1/content/machine-store");
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_open_orders",
    {
      description: "GET /v1/market/orders/open — public order book view.",
      inputSchema: {
        elementId: z.number().int().optional(),
      },
    },
    async ({ elementId }) => {
      const q =
        elementId !== undefined
          ? `?elementId=${encodeURIComponent(String(elementId))}`
          : "";
      const r = await fetchPublic("GET", `/v1/market/orders/open${q}`);
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_recent_trades",
    {
      description: "GET /v1/market/trades — recent trades (public).",
      inputSchema: {
        elementId: z.number().int().optional(),
        limit: z.number().int().optional().describe("Max trades, default 50."),
        includeSynthetic: z
          .boolean()
          .optional()
          .describe("Include synthetic liquidity trades."),
      },
    },
    async ({ elementId, limit, includeSynthetic }) => {
      const params = new URLSearchParams();
      if (elementId !== undefined) params.set("elementId", String(elementId));
      if (limit !== undefined) params.set("limit", String(limit));
      if (includeSynthetic !== undefined)
        params.set("includeSynthetic", String(includeSynthetic));
      const q = params.size ? `?${params.toString()}` : "";
      const r = await fetchPublic("GET", `/v1/market/trades${q}`);
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_summary",
    {
      description:
        "GET /v1/market/summary — per-element DNA variant rows (elementId + dna string + phase). Authenticated.",
      inputSchema: { ...optionalSession, ...optionalLocale },
    },
    async ({ sessionToken, apiKey, locale }) => {
      const r = await fetchPlayer("GET", "/v1/market/summary", {
        sessionToken,
        apiKey,
      }, undefined, { locale: locale ?? "en" });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_element_depth",
    {
      description:
        "GET /v1/market/elements/{elementId}/depth — order book depth for one element variant (public). Pass dna as decimal string when not using catalog default.",
      inputSchema: {
        elementId: z.number().int(),
        dna: z
          .string()
          .optional()
          .describe("Material DNA variant as decimal string (matches pool/market summary dna)."),
      },
    },
    async ({ elementId, dna }) => {
      const q =
        dna !== undefined
          ? `?dna=${encodeURIComponent(dna)}`
          : "";
      const r = await fetchPublic(
        "GET",
        `/v1/market/elements/${encodeURIComponent(String(elementId))}/depth${q}`
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_element_history",
    {
      description:
        "GET /v1/market/elements/{elementId}/history — price candles (public).",
      inputSchema: {
        elementId: z.number().int(),
        points: z.number().int().optional().describe("Candle count, default 48."),
      },
    },
    async ({ elementId, points }) => {
      const q =
        points !== undefined
          ? `?points=${encodeURIComponent(String(points))}`
          : "";
      const r = await fetchPublic(
        "GET",
        `/v1/market/elements/${encodeURIComponent(String(elementId))}/history${q}`
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_orders_mine",
    {
      description: "GET /v1/market/orders/mine — open orders for current player.",
      inputSchema: {
        ...optionalSession,
        elementId: z.number().int().optional(),
        dna: z
          .string()
          .optional()
          .describe("Filter by material DNA variant (decimal string)."),
      },
    },
    async ({ sessionToken, apiKey, elementId, dna }) => {
      const params = new URLSearchParams();
      if (elementId !== undefined)
        params.set("elementId", String(elementId));
      if (dna !== undefined)
        params.set("dna", dna);
      const q = params.size > 0 ? `?${params.toString()}` : "";
      const r = await fetchPlayer("GET", `/v1/market/orders/mine${q}`, {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_place_order",
    {
      description:
        "POST /v1/market/orders — place a limit order for a specific material DNA variant (authenticated).",
      inputSchema: {
        ...optionalSession,
        elementId: z.number().int(),
        dna: z
          .string()
          .describe("Material DNA variant as decimal string (from market_summary or pool groups)."),
        side: z.string().describe("Buy or Sell per API contract."),
        limitPrice: z.number(),
        quantity: z.number().int(),
        idempotencyKey: z.string().optional(),
      },
    },
    async (args) => {
      const { sessionToken, apiKey, ...body } = args;
      const r = await fetchPlayer(
        "POST",
        "/v1/market/orders",
        { sessionToken, apiKey },
        body
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_wallet",
    {
      description: "GET /v1/me/wallet — cash and pool summary.",
      inputSchema: { ...optionalSession },
    },
    async ({ sessionToken, apiKey }) => {
      const r = await fetchPlayer("GET", "/v1/me/wallet", {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_pool",
    {
      description: "GET /v1/me/pool — inventory pool stacks.",
      inputSchema: { ...optionalSession },
    },
    async ({ sessionToken, apiKey }) => {
      const r = await fetchPlayer("GET", "/v1/me/pool", {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_transactions",
    {
      description: "GET /v1/me/transactions — recent economy transactions.",
      inputSchema: { ...optionalSession },
    },
    async ({ sessionToken, apiKey }) => {
      const r = await fetchPlayer("GET", "/v1/me/transactions", {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_pool_view",
    {
      description:
        "GET /v1/me/pool/view — pool overview with stacks, groups[] (per elementId + dna variant), phase labels, and estimated values. DNA fields are JSON strings.",
      inputSchema: { ...optionalSession, ...optionalLocale },
    },
    async ({ sessionToken, apiKey, locale }) => {
      const r = await fetchPlayer("GET", "/v1/me/pool/view", {
        sessionToken,
        apiKey,
      }, undefined, { locale: locale ?? "en" });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_machine_inventory",
    {
      description: "GET /v1/me/machine-inventory — owned machines in stock.",
      inputSchema: { ...optionalSession },
    },
    async ({ sessionToken, apiKey }) => {
      const r = await fetchPlayer("GET", "/v1/me/machine-inventory", {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "player_machine_purchase",
    {
      description:
        "POST /v1/me/machine-inventory/purchase — buy a machine into stock.",
      inputSchema: {
        ...optionalSession,
        machineType: z
          .string()
          .describe('Machine type, e.g. "Boiler" or "SeaportConnector".'),
      },
    },
    async ({ sessionToken, apiKey, machineType }) => {
      const r = await fetchPlayer(
        "POST",
        "/v1/me/machine-inventory/purchase",
        { sessionToken, apiKey },
        { machineType }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_create",
    {
      description: "POST /v1/boards — create a new board.",
      inputSchema: {
        ...optionalSession,
        name: z.string().optional().describe("Board name; default Factory."),
      },
    },
    async ({ sessionToken, apiKey, name }) => {
      const r = await fetchPlayer(
        "POST",
        "/v1/boards",
        { sessionToken, apiKey },
        name !== undefined ? { name } : {}
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_list",
    {
      description: "GET /v1/boards — list boards for the current player.",
      inputSchema: { ...optionalSession },
    },
    async ({ sessionToken, apiKey }) => {
      const r = await fetchPlayer("GET", "/v1/boards", {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_save_plan",
    {
      description:
        "PUT /v1/boards/{boardId}/plan — save machines and connections (see BoardPlanDto). Seaport OUT: set settings.outElementId and settings.outMaterialDna as decimal string for pool variant.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
        plan: planSchema,
      },
    },
    async ({ sessionToken, apiKey, boardId, plan }) => {
      const r = await fetchPlayer(
        "PUT",
        `/v1/boards/${boardId}/plan`,
        { sessionToken, apiKey },
        { plan }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_get_plan",
    {
      description: "GET /v1/boards/{boardId}/plan — read saved board plan.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
      },
    },
    async ({ sessionToken, apiKey, boardId }) => {
      const r = await fetchPlayer("GET", `/v1/boards/${boardId}/plan`, {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_place_from_stock",
    {
      description:
        "POST /v1/boards/{boardId}/place-from-stock — place a machine from inventory onto the board.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
        stockId: z.string().uuid(),
        machineId: z.string().describe("Instance id on the board, e.g. boiler1."),
      },
    },
    async ({ sessionToken, apiKey, boardId, stockId, machineId }) => {
      const r = await fetchPlayer(
        "POST",
        `/v1/boards/${boardId}/place-from-stock`,
        { sessionToken, apiKey },
        { stockId, machineId }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_info_preview",
    {
      description:
        "POST /v1/boards/{boardId}/info/preview — preview factory report for a plan without saving.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
        plan: planSchema,
      },
    },
    async ({ sessionToken, apiKey, boardId, plan }) => {
      const r = await fetchPlayer(
        "POST",
        `/v1/boards/${boardId}/info/preview`,
        { sessionToken, apiKey },
        { plan }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_start",
    {
      description: "POST /v1/boards/{boardId}/start — start simulation.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
      },
    },
    async ({ sessionToken, apiKey, boardId }) => {
      const r = await fetchPlayer(
        "POST",
        `/v1/boards/${boardId}/start`,
        { sessionToken, apiKey }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_stop",
    {
      description: "POST /v1/boards/{boardId}/stop — stop simulation.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
      },
    },
    async ({ sessionToken, apiKey, boardId }) => {
      const r = await fetchPlayer(
        "POST",
        `/v1/boards/${boardId}/stop`,
        { sessionToken, apiKey }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_snapshot",
    {
      description: "GET /v1/boards/{boardId}/snapshot — board state snapshot.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
        afterTick: z.number().int().optional(),
      },
    },
    async ({ sessionToken, apiKey, boardId, afterTick }) => {
      const q =
        afterTick !== undefined
          ? `?afterTick=${encodeURIComponent(String(afterTick))}`
          : "";
      const r = await fetchPlayer(
        "GET",
        `/v1/boards/${boardId}/snapshot${q}`,
        { sessionToken, apiKey }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_info",
    {
      description:
        "GET /v1/boards/{boardId}/info — factory report (seaport flows, throughput, issues).",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
      },
    },
    async ({ sessionToken, apiKey, boardId }) => {
      const r = await fetchPlayer("GET", `/v1/boards/${boardId}/info`, {
        sessionToken,
        apiKey,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_keyframe_latest",
    {
      description:
        "GET /v1/boards/{boardId}/keyframes/latest — latest simulation keyframe.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
      },
    },
    async ({ sessionToken, apiKey, boardId }) => {
      const r = await fetchPlayer(
        "GET",
        `/v1/boards/${boardId}/keyframes/latest`,
        { sessionToken, apiKey }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "boards_keyframes",
    {
      description:
        "GET /v1/boards/{boardId}/keyframes?afterTick= — poll keyframes after a tick.",
      inputSchema: {
        ...optionalSession,
        boardId: z.string().uuid(),
        afterTick: z.number().int().optional(),
      },
    },
    async ({ sessionToken, apiKey, boardId, afterTick }) => {
      const q =
        afterTick !== undefined
          ? `?afterTick=${encodeURIComponent(String(afterTick))}`
          : "";
      const r = await fetchPlayer(
        "GET",
        `/v1/boards/${boardId}/keyframes${q}`,
        { sessionToken, apiKey }
      );
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "admin_list_players",
    {
      description:
        "GET /v1/admin/players — list recent players (requires FACTORYGAME_ADMIN_TOKEN).",
      inputSchema: {},
    },
    async () => {
      const r = await fetchAdmin("GET", "/v1/admin/players");
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "admin_create_api_key",
    {
      description:
        "POST /v1/admin/api-keys — create API key for a player (requires FACTORYGAME_ADMIN_TOKEN).",
      inputSchema: {
        playerId: z.string().uuid(),
        name: z.string(),
        scopes: z
          .string()
          .describe(
            "Comma-separated scopes, e.g. market,boards,content,player"
          ),
      },
    },
    async ({ playerId, name, scopes }) => {
      const r = await fetchAdmin("POST", "/v1/admin/api-keys", {
        playerId,
        name,
        scopes,
      });
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "diagnostics_recent_logs",
    {
      description:
        "GET /diagnostics/recent-logs — server log buffer (when enabled on host).",
      inputSchema: {},
    },
    async () => {
      const r = await fetchDiagnostics();
      return formatDiagnostics(r);
    }
  );
}

async function main(): Promise<void> {
  const mcp = new McpServer({
    name: "factorygame-mcp",
    version: "1.0.0",
  });
  registerTools(mcp);
  const transport = new StdioServerTransport();
  await mcp.connect(transport);
}

main().catch((err: unknown) => {
  console.error(err);
  process.exit(1);
});
