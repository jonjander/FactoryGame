# PWA service worker — pitfalls (FactoryGame)

Reference for agents and developers working on `src/FactoryGame.Web/wwwroot/service-worker.published.js`.  
**Source file:** edit `service-worker.published.js`; Release publish copies it to `wwwroot/service-worker.js`.  
**Dev:** `service-worker.js` is a no-op stub (always network) — SW bugs only appear in **Release / Azure**.

---

## Incident: 0.3.25–0.3.27 — “An unhandled error has occurred” with SW active

**Fixed in:** 0.3.28  
**Symptoms:**

- App loaded with service worker **blocked** (DevTools or fresh profile): OK.
- With SW **allowed** (normal PWA / return visit): yellow Blazor bar *“An unhandled error has occurred”*.
- Browser console / Playwright: `TypeError: Failed to fetch` for `/css/app.css`, `/v1/*`, etc. — while `/js/viewport-layout.js` could still succeed (network-first path).

**Not the root cause (but related hardening in 0.3.27–0.3.28):**

- Stale cached `viewport-layout.js` (0.3.25 cache-first for all `.js`) — fixed by network-first for `/js/` and `/css/`.
- `ResponsiveApp` now catches viewport init failure and continues boot (fallback layout).

---

## Root cause: double `respondWith`

The fetch listener was wired like this (broken):

```javascript
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

async function onFetch(event) {
    // ...
    if (shouldUseNetworkFirst(...)) {
        event.respondWith((async () => { return await fetch(...); })());  // ❌ second respondWith
        return;
    }
    // ...
    event.respondWith(cachedResponse || fetch(event.request));  // ❌ second respondWith
}
```

Each fetch event may call **`event.respondWith()` exactly once**. The outer listener already passed the **Promise returned by `onFetch`**. Calling `respondWith` again inside `onFetch`:

1. Is invalid / ignored depending on timing.
2. Makes the outer promise resolve to **`undefined`** instead of a `Response`.
3. Surfaces as **`Failed to fetch`** for cache-first routes (CSS, uncached assets, and anything not on the network-first list).

The network-first branch could appear to work sometimes because the inner `respondWith` ran **before the first `await`**, but the pattern was still wrong and unreliable.

---

## Correct pattern

**One** `respondWith` on the listener; **`onFetch` returns a `Response` (or Promise&lt;Response&gt;):**

```javascript
self.addEventListener('fetch', event => onFetchEvent(event));

function onFetchEvent(event) {
    const url = new URL(event.request.url);
    if (url.origin !== self.location.origin)
        return;

    if (shouldBypassServiceWorker(url, event.request))
        return;  // no respondWith — browser handles normally

    event.respondWith(onFetch(event));
}

async function onFetch(event) {
    if (shouldUseNetworkFirst(...)) {
        try {
            return await fetch(event.request);
        } catch {
            // cache fallback…
        }
    }
    const cached = await caches.match(...);
    return cached || fetch(event.request);
}
```

---

## FactoryGame-specific rules (do not regress)

| Rule | Why |
|------|-----|
| **Never intercept `/v1/`** (bypass SW) | API is dynamic; same-origin co-hosting must not go through offline cache logic. |
| **Bypass** `/diagnostics/`, `/swagger`, `/health` | Operational / live endpoints. |
| **Network-first** for `/_framework/`, `/js/`, `/css/`, `/index.html`, navigations | Avoid stale WASM/JS/CSS after deploy (see 0.3.25–0.3.27). |
| **Do not call `respondWith` inside `onFetch`** | Only return `Response` from the async handler. |
| **Test Release with SW enabled** | `dotnet publish` + local run, or Playwright against Azure with `serviceWorkers: 'allow'`. |

---

## How to verify after SW changes

1. **Publish locally:**  
   `dotnet publish src/FactoryGame.Api/FactoryGame.Api.csproj -c Release -o _publish_test`  
   Run and open with a normal browser tab (SW registers on load).

2. **Playwright smoke** (SW allow vs block):

   - `#blazor-error-ui` computed `display` should stay **`none`** with SW allowed.
   - In-page `fetch('/css/app.css')`, `fetch('/v1/app/version')` should return **200**, not `Failed to fetch`.

3. **After deploy:** users with an old broken SW may need **`factoryGame.hardReloadClient()`** or Ctrl+Shift+R once; `index.html` uses `updateViaCache: 'none'` and skip-waiting to pick up new SW faster.

---

## Related files

- `src/FactoryGame.Web/wwwroot/service-worker.published.js` — production SW logic
- `src/FactoryGame.Web/wwwroot/service-worker.js` — dev stub (no caching)
- `src/FactoryGame.Web/wwwroot/index.html` — registration, boot watchdog, `hardReloadClient`
- `src/FactoryGame.Web/Components/ResponsiveApp.razor` — viewport init fallback
- `releases.md` — version notes for 0.3.25–0.3.28
