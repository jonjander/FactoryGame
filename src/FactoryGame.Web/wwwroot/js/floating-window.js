const activeDrag = {
    dotNetRef: null,
    mode: null,
    startX: 0,
    startY: 0,
    originX: 0,
    originY: 0,
    originW: 0,
    originH: 0
};

function onPointerMove(e) {
    if (!activeDrag.dotNetRef) return;
    if (e.cancelable) e.preventDefault();
    const dx = e.clientX - activeDrag.startX;
    const dy = e.clientY - activeDrag.startY;
    if (activeDrag.mode === "drag") {
        activeDrag.dotNetRef.invokeMethodAsync(
            "OnDragMove",
            activeDrag.originX + dx,
            activeDrag.originY + dy);
    } else if (activeDrag.mode === "resize") {
        activeDrag.dotNetRef.invokeMethodAsync(
            "OnResizeMove",
            activeDrag.originW + dx,
            activeDrag.originH + dy);
    }
}

function onPointerUp() {
    stopPointerDrag();
}

export function startPointerDrag(ref, mode, clientX, clientY, originX, originY, originW, originH) {
    stopPointerDrag();
    activeDrag.dotNetRef = ref;
    activeDrag.mode = mode;
    activeDrag.startX = clientX;
    activeDrag.startY = clientY;
    activeDrag.originX = originX;
    activeDrag.originY = originY;
    activeDrag.originW = originW;
    activeDrag.originH = originH;
    document.addEventListener("pointermove", onPointerMove);
    document.addEventListener("pointerup", onPointerUp);
    document.addEventListener("pointercancel", onPointerUp);
}

export function stopPointerDrag() {
    document.removeEventListener("pointermove", onPointerMove);
    document.removeEventListener("pointerup", onPointerUp);
    document.removeEventListener("pointercancel", onPointerUp);
    activeDrag.dotNetRef = null;
    activeDrag.mode = null;
}
