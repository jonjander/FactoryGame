let dotNetRef = null;
let mediaQuery = null;
let changeHandler = null;

export function attach(ref, query) {
    stop();
    dotNetRef = ref;
    mediaQuery = window.matchMedia(query);
    changeHandler = () => {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("OnViewportChanged", mediaQuery.matches);
        }
    };
    mediaQuery.addEventListener("change", changeHandler);
    changeHandler();
}

export function stop() {
    if (mediaQuery && changeHandler) {
        mediaQuery.removeEventListener("change", changeHandler);
    }
    dotNetRef = null;
    mediaQuery = null;
    changeHandler = null;
}
