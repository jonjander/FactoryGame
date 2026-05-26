let dotNetRef = null;
let moveHandler = null;
let upHandler = null;

export function startPointerDrag(ref) {
    stopPointerDrag();
    dotNetRef = ref;
    moveHandler = (e) => {
        dotNetRef.invokeMethodAsync("OnDocumentPointerMove", e.clientX, e.clientY);
    };
    upHandler = (e) => {
        dotNetRef.invokeMethodAsync("OnDocumentPointerUp");
        stopPointerDrag();
    };
    document.addEventListener("mousemove", moveHandler);
    document.addEventListener("mouseup", upHandler);
}

export function stopPointerDrag() {
    if (moveHandler) {
        document.removeEventListener("mousemove", moveHandler);
        moveHandler = null;
    }
    if (upHandler) {
        document.removeEventListener("mouseup", upHandler);
        upHandler = null;
    }
    dotNetRef = null;
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
