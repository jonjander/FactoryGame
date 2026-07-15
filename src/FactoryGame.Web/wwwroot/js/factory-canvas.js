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
