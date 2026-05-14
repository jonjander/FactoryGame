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
      inputSchema: {
        locale: z.string().optional().describe("Locale code, default en."),
      },
    },
    async ({ locale }) => {
      const q = locale ? `?locale=${encodeURIComponent(locale)}` : "";
      const r = await fetchPublic("GET", `/v1/content/elements${q}`);
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "content_wiki",
    {
      description: "GET /v1/content/wiki — wiki snapshot (public).",
      inputSchema: {
        locale: z.string().optional().describe("Locale code, default en."),
      },
    },
    async ({ locale }) => {
      const q = locale ? `?locale=${encodeURIComponent(locale)}` : "";
      const r = await fetchPublic("GET", `/v1/content/wiki${q}`);
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
      },
    },
    async ({ elementId }) => {
      const q =
        elementId !== undefined
          ? `?elementId=${encodeURIComponent(String(elementId))}`
          : "";
      const r = await fetchPublic("GET", `/v1/market/trades${q}`);
      return formatToolResult(r);
    }
  );

  mcp.registerTool(
    "market_place_order",
    {
      description: "POST /v1/market/orders — place a limit order (authenticated).",
      inputSchema: {
        ...optionalSession,
        elementId: z.number().int(),
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
        "PUT /v1/boards/{boardId}/plan — save machines and connections (see BoardPlanDto).",
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
