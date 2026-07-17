let dotNetRef = null;
let moveHandler = null;
let upHandler = null;

export function startPointerDrag(ref) {
    stopPointerDrag();
    dotNetRef = ref;
    moveHandler = (e) => {
        if (e.cancelable) {
            e.preventDefault();
        }
        dotNetRef.invokeMethodAsync("OnDocumentPointerMove", e.clientX, e.clientY);
    };
    upHandler = (e) => {
        if (e.cancelable) {
            e.preventDefault();
        }
        const refSnapshot = dotNetRef;
        stopPointerDrag();
        refSnapshot.invokeMethodAsync("OnDocumentPointerUp", e.clientX, e.clientY);
    };
    document.addEventListener("pointermove", moveHandler);
    document.addEventListener("pointerup", upHandler);
    document.addEventListener("pointercancel", upHandler);
}

export function stopPointerDrag() {
    if (moveHandler) {
        document.removeEventListener("pointermove", moveHandler);
        moveHandler = null;
    }
    if (upHandler) {
        document.removeEventListener("pointerup", upHandler);
        document.removeEventListener("pointercancel", upHandler);
        upHandler = null;
    }
    dotNetRef = null;
}

/** Returns { machineId, port, isOutput } for a port circle under the pointer, or null. */
export function findPortAtPoint(svg, clientX, clientY) {
    if (!svg) {
        return null;
    }
    const el = document.elementFromPoint(clientX, clientY);
    if (!el) {
        return null;
    }
    const circle = el.closest?.("[data-fg-port]");
    if (!circle || !svg.contains(circle)) {
        return null;
    }
    const machineId = circle.getAttribute("data-fg-machine");
    const port = circle.getAttribute("data-fg-port");
    const dir = circle.getAttribute("data-fg-dir");
    if (!machineId || !port || !dir) {
        return null;
    }
    return { machineId, port, isOutput: dir === "out" };
}

export function toSvgPoint(svg, clientX, clientY) {
    if (!svg) {
        return { x: 0, y: 0 };
    }
    const ctm = svg.getScreenCTM();
    if (!ctm) {
        return { x: 0, y: 0 };
    }
    const pt = svg.createSVGPoint();
    pt.x = clientX;
    pt.y = clientY;
    const mapped = pt.matrixTransform(ctm.inverse());
    return { x: mapped.x, y: mapped.y };
}

const viewportState = {
    wrap: null,
    svg: null,
    dotNetRef: null,
    wheelHandler: null,
    pointerDownHandler: null,
    pointerMoveHandler: null,
    pointerUpHandler: null,
    pointerCancelHandler: null,
    panActive: false,
    panLastX: 0,
    panLastY: 0,
    panButton: 0,
    pointers: new Map(),
    pinchStartDistance: 0,
    pinchStartZoom: 1
};

function getSvgClientSize(svg) {
    const rect = svg.getBoundingClientRect();
    return { width: rect.width || 1, height: rect.height || 1 };
}

function isPanTrigger(e) {
    // Middle or right mouse button; touch handled via pinch/pan with 1 finger on empty area
    return e.button === 1 || e.button === 2;
}

function isInteractiveTarget(svg, target) {
    if (!target || !svg.contains(target)) {
        return false;
    }
    if (target === svg) {
        return false;
    }
    return !!target.closest?.(
        "[data-fg-machine], [data-fg-port], .fg-pipe-hit, .fg-machine-remove, button"
    );
}

function pointerCount() {
    return viewportState.pointers.size;
}

function pinchDistance() {
    const pts = [...viewportState.pointers.values()];
    if (pts.length < 2) {
        return 0;
    }
    const dx = pts[1].x - pts[0].x;
    const dy = pts[1].y - pts[0].y;
    return Math.hypot(dx, dy);
}

function pinchCenter() {
    const pts = [...viewportState.pointers.values()];
    if (pts.length < 2) {
        return { x: 0, y: 0 };
    }
    return { x: (pts[0].x + pts[1].x) / 2, y: (pts[0].y + pts[1].y) / 2 };
}

export function attachViewport(wrap, svg, ref) {
    detachViewport();
    if (!wrap || !svg || !ref) {
        return;
    }

    viewportState.wrap = wrap;
    viewportState.svg = svg;
    viewportState.dotNetRef = ref;

    viewportState.wheelHandler = (e) => {
        if (!viewportState.dotNetRef) {
            return;
        }
        e.preventDefault();
        const { width, height } = getSvgClientSize(svg);
        viewportState.dotNetRef.invokeMethodAsync(
            "OnViewportWheel",
            e.clientX,
            e.clientY,
            e.deltaY,
            width,
            height);
    };

    viewportState.pointerDownHandler = (e) => {
        viewportState.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

        if (pointerCount() === 2) {
            viewportState.pinchStartDistance = pinchDistance();
            viewportState.panActive = false;
            if (wrap.setPointerCapture) {
                try {
                    wrap.setPointerCapture(e.pointerId);
                } catch {
                    /* ignore */
                }
            }
            return;
        }

        if (pointerCount() > 2) {
            return;
        }

        const interactive = isInteractiveTarget(svg, e.target);
        const panTrigger = isPanTrigger(e) || (e.button === 0 && e.altKey && !interactive);

        const touchPan = e.pointerType === "touch" && e.button === 0 && !interactive;

        if ((panTrigger || touchPan) && !interactive) {
            e.preventDefault();
            viewportState.panActive = true;
            viewportState.panLastX = e.clientX;
            viewportState.panLastY = e.clientY;
            viewportState.panButton = e.button;
            if (wrap.setPointerCapture) {
                try {
                    wrap.setPointerCapture(e.pointerId);
                } catch {
                    /* ignore */
                }
            }
        }
    };

    viewportState.pointerMoveHandler = (e) => {
        if (!viewportState.pointers.has(e.pointerId)) {
            return;
        }
        viewportState.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

        if (pointerCount() >= 2 && viewportState.pinchStartDistance > 0) {
            e.preventDefault();
            const dist = pinchDistance();
            const scale = dist / viewportState.pinchStartDistance;
            const center = pinchCenter();
            const { width, height } = getSvgClientSize(svg);
            viewportState.dotNetRef?.invokeMethodAsync(
                "OnViewportPinch",
                center.x,
                center.y,
                scale,
                width,
                height);
            viewportState.pinchStartDistance = dist;
            return;
        }

        if (!viewportState.panActive) {
            return;
        }

        e.preventDefault();
        const dx = e.clientX - viewportState.panLastX;
        const dy = e.clientY - viewportState.panLastY;
        viewportState.panLastX = e.clientX;
        viewportState.panLastY = e.clientY;
        const { width, height } = getSvgClientSize(svg);
        viewportState.dotNetRef?.invokeMethodAsync(
            "OnViewportPanDelta",
            dx,
            dy,
            width,
            height);
    };

    const endPointer = (e) => {
        viewportState.pointers.delete(e.pointerId);
        if (pointerCount() < 2) {
            viewportState.pinchStartDistance = 0;
        }
        if (pointerCount() === 0) {
            viewportState.panActive = false;
        }
        if (wrap.releasePointerCapture) {
            try {
                wrap.releasePointerCapture(e.pointerId);
            } catch {
                /* ignore */
            }
        }
    };

    viewportState.pointerUpHandler = endPointer;
    viewportState.pointerCancelHandler = endPointer;

    wrap.addEventListener("wheel", viewportState.wheelHandler, { passive: false });
    wrap.addEventListener("pointerdown", viewportState.pointerDownHandler);
    wrap.addEventListener("pointermove", viewportState.pointerMoveHandler);
    wrap.addEventListener("pointerup", viewportState.pointerUpHandler);
    wrap.addEventListener("pointercancel", viewportState.pointerCancelHandler);
    wrap.addEventListener("contextmenu", (e) => {
        if (viewportState.panActive || isPanTrigger(e)) {
            e.preventDefault();
        }
    });
}

export function detachViewport() {
    const s = viewportState;
    if (s.wrap && s.wheelHandler) {
        s.wrap.removeEventListener("wheel", s.wheelHandler);
    }
    if (s.wrap && s.pointerDownHandler) {
        s.wrap.removeEventListener("pointerdown", s.pointerDownHandler);
        s.wrap.removeEventListener("pointermove", s.pointerMoveHandler);
        s.wrap.removeEventListener("pointerup", s.pointerUpHandler);
        s.wrap.removeEventListener("pointercancel", s.pointerCancelHandler);
    }
    s.wrap = null;
    s.svg = null;
    s.dotNetRef = null;
    s.wheelHandler = null;
    s.pointerDownHandler = null;
    s.pointerMoveHandler = null;
    s.pointerUpHandler = null;
    s.pointerCancelHandler = null;
    s.panActive = false;
    s.pointers.clear();
    s.pinchStartDistance = 0;
}
