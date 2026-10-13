let libraryPromise;

function loadPlaces(apiKey) {
  if (globalThis.google?.maps?.importLibrary) return google.maps.importLibrary("places");
  if (libraryPromise) return libraryPromise;
  libraryPromise = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    const callback = "saveTheDateMapsLoaded";
    const timer = setTimeout(() => fail(), 20000);
    const fail = () => {
      clearTimeout(timer);
      script.remove();
      libraryPromise = undefined;
      reject(new Error("Google Maps is unavailable"));
    };
    globalThis[callback] = () => {
      clearTimeout(timer);
      google.maps.importLibrary("places").then(resolve, reject);
    };
    script.src = "https://maps.googleapis.com/maps/api/js?" + new URLSearchParams({
      key: apiKey, loading: "async", callback, v: "weekly", language: "es", region: "AR"
    });
    script.async = true;
    script.onerror = fail;
    document.head.append(script);
  });
  return libraryPromise;
}

export async function mount(host, apiKey, receiver) {
  const { PlaceAutocompleteElement } = await loadPlaces(apiKey);
  if (!host.isConnected) return;
  const widget = new PlaceAutocompleteElement();
  widget.placeholder = "Buscá un salón, una iglesia o una dirección…";
  widget.setAttribute("aria-label", "Buscar un lugar en Google Maps");
  const controller = new AbortController();
  const options = { signal: controller.signal };
  let sequence = 0;
  const notify = (method, ...args) => {
    if (!controller.signal.aborted && host.isConnected)
      return receiver.invokeMethodAsync(method, ...args).catch(() => {});
  };
  widget.addEventListener("gmp-select", async ({ placePrediction }) => {
    const current = ++sequence;
    try {
      const place = placePrediction.toPlace();
      await place.fetchFields({ fields: ["id", "formattedAddress"] });
      if (current !== sequence) return;
      await notify("SelectPlace", place.id, place.formattedAddress || "Lugar seleccionado en Google Maps");
    } catch {
      if (current === sequence) await notify("SearchFailed");
    }
  }, options);
  widget.addEventListener("gmp-error", () => notify("SearchFailed"), options);
  host.replaceChildren(widget);
  // Blazor can remove a module while a Places request is still in flight.
  // Clean up in the browser, where the DOM lifetime can be observed reliably.
  const observer = new MutationObserver(() => {
    if (!host.isConnected) {
      sequence++;
      controller.abort();
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });
}
