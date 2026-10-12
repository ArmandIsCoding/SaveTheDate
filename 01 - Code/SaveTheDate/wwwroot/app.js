// Focus and keyboard management for dialogs created by interactive components.
(() => {
  let activeDialog = null;
  let previousFocus = null;
  const focusable = (el) =>
    [
      ...el.querySelectorAll(
        'a[href],button:not([disabled]),input:not([disabled]),select,textarea,[tabindex="0"]',
      ),
    ].filter((x) => x.offsetParent !== null);
  new MutationObserver(() => {
    const dialog = document.querySelector('[role="dialog"]');
    if (dialog === activeDialog) return;
    if (dialog) {
      previousFocus = document.activeElement;
      activeDialog = dialog;
      focusable(dialog)[0]?.focus();
    } else {
      activeDialog = null;
      previousFocus?.focus();
    }
  }).observe(document.body, { childList: true, subtree: true });
  document.addEventListener("keydown", (e) => {
    if (!activeDialog) return;
    if (e.key === "Escape") {
      activeDialog.querySelector(".modal-close")?.click();
      return;
    }
    if (e.key !== "Tab") return;
    const items = focusable(activeDialog),
      first = items[0],
      last = items.at(-1);
    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last?.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first?.focus();
    }
  });
})();
