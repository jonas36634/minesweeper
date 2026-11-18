window.kp = {
  get: (key) => localStorage.getItem("kernport." + key),
  set: (key, value) => localStorage.setItem("kernport." + key, value),
  theme: (dark) =>
    (document.documentElement.dataset.theme = dark ? "dark" : "light"),
  focus: (id) => document.getElementById(id)?.focus(),
  modal: () => {
    const d = document.querySelector("dialog");
    if (d && !d.open) d.showModal();
  },
  boardKey: null,
};
document.addEventListener("keydown", (e) => {
  const cell = e.target.closest("[data-cell]");
  if (!cell) return;
  if (
    [
      "ArrowLeft",
      "ArrowRight",
      "ArrowUp",
      "ArrowDown",
      "Home",
      "End",
      " ",
      "Enter",
      "f",
      "F",
    ].includes(e.key)
  ) {
    e.preventDefault();
    if (!e.repeat || e.key.startsWith("Arrow"))
      kp.boardKey?.invokeMethodAsync("Key", Number(cell.dataset.cell), e.key);
  }
});
kp.wire = (ref) => (kp.boardKey = ref);
let tap;
document.addEventListener(
  "pointerdown",
  (e) => {
    const cell = e.target.closest("[data-cell]");
    tap = cell
      ? { id: e.pointerId, x: e.clientX, y: e.clientY, moved: false }
      : null;
  },
  true,
);
document.addEventListener(
  "pointermove",
  (e) => {
    if (
      tap?.id === e.pointerId &&
      Math.hypot(e.clientX - tap.x, e.clientY - tap.y) > 7
    )
      tap.moved = true;
  },
  true,
);
document.addEventListener(
  "pointercancel",
  () => {
    if (tap) tap.moved = true;
  },
  true,
);
document.addEventListener(
  "click",
  (e) => {
    if (e.detail && tap?.moved && e.target.closest("[data-cell]")) {
      e.preventDefault();
      e.stopImmediatePropagation();
    }
    tap = null;
  },
  true,
);
document.addEventListener("visibilitychange", () => {
  if (document.hidden) kp.boardKey?.invokeMethodAsync("Checkpoint");
});
