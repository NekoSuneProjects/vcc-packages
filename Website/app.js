// The listing builder fills in this URL when it renders the page.
const LISTING_URL = "{{ listingInfo.Url }}";

const grid = document.getElementById("package-grid");
const cards = grid ? Array.from(grid.querySelectorAll(".card")) : [];
const searchInput = document.getElementById("package-search");
const resultCount = document.getElementById("result-count");
const emptyState = document.getElementById("empty-state");
const noPackages = document.getElementById("no-packages");
const toast = document.getElementById("toast");

/* ---------- categories ---------- */

function categoryFor(id) {
  const name = (id || "").toLowerCase();
  if (name === "com.nekosune.worlds" || name.includes(".world-") || name.includes(".world.")) return "world";
  if (name === "com.nekosune.avatars" || name.includes(".avatar-") || name.includes(".avatar.")) return "avatar";
  return "shared";
}

const CATEGORY_LABELS = { avatar: "Avatar", world: "World", shared: "Shared" };

cards.forEach((card) => {
  const category = categoryFor(card.dataset.packageId);
  card.dataset.category = category;

  const chip = card.querySelector("[data-category-chip]");
  if (chip) {
    chip.textContent = CATEGORY_LABELS[category];
    chip.classList.add(`chip--${category}`);
  }

  // Hide empty dependency / keyword blocks.
  const deps = card.querySelector("[data-deps]");
  if (deps && !deps.querySelector("li")) deps.remove();
  const keywords = card.querySelector("[data-keywords]");
  if (keywords && !keywords.querySelector("li")) keywords.remove();
  const meta = card.querySelector(".card__meta");
  if (meta && !meta.querySelector("div")) meta.remove();

  card.dataset.search = card.textContent.replace(/\s+/g, " ").toLowerCase();
});

/* ---------- stats ---------- */

function setStat(key, value) {
  const el = document.querySelector(`[data-stat="${key}"]`);
  if (el) el.textContent = String(value);
}

setStat("total", cards.length);
["avatar", "world", "shared"].forEach((key) => {
  setStat(key, cards.filter((card) => card.dataset.category === key).length);
});

/* ---------- filtering ---------- */

function selectedCategory() {
  const checked = document.querySelector('input[name="category"]:checked');
  return checked ? checked.value : "all";
}

function applyFilters() {
  const query = (searchInput?.value || "").trim().toLowerCase();
  const category = selectedCategory();
  let visible = 0;

  cards.forEach((card) => {
    const matchesCategory = category === "all" || card.dataset.category === category;
    const matchesQuery = !query || card.dataset.search.includes(query);
    const show = matchesCategory && matchesQuery;
    card.hidden = !show;
    if (show) visible++;
  });

  if (noPackages) noPackages.hidden = cards.length !== 0;
  if (emptyState) emptyState.hidden = cards.length === 0 || visible !== 0;
  if (resultCount) {
    resultCount.textContent = cards.length === 0
      ? ""
      : `${visible} of ${cards.length} package${cards.length === 1 ? "" : "s"}`;
  }
}

searchInput?.addEventListener("input", applyFilters);
document.querySelectorAll('input[name="category"]').forEach((radio) => {
  radio.addEventListener("change", applyFilters);
});

document.getElementById("reset-filters")?.addEventListener("click", () => {
  if (searchInput) searchInput.value = "";
  const all = document.querySelector('input[name="category"][value="all"]');
  if (all) all.checked = true;
  applyFilters();
  searchInput?.focus();
});

document.addEventListener("keydown", (event) => {
  if (event.key === "/" && document.activeElement?.tagName !== "INPUT") {
    event.preventDefault();
    searchInput?.focus();
  }
});

applyFilters();

/* ---------- toast ---------- */

let toastTimer;
function showToast(message, isError = false) {
  if (!toast) return;
  toast.textContent = message;
  toast.classList.toggle("toast--error", isError);
  toast.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toast.hidden = true; }, 2400);
}

/* ---------- clipboard ---------- */

async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    // Fallback for browsers / contexts without the async clipboard API.
    const temp = document.createElement("textarea");
    temp.value = text;
    temp.setAttribute("readonly", "");
    temp.style.position = "fixed";
    temp.style.opacity = "0";
    document.body.appendChild(temp);
    temp.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    temp.remove();
    return ok;
  }
}

function flashButton(button) {
  const label = button.querySelector("span");
  if (!label) return;
  const original = label.textContent;
  button.classList.add("is-done");
  label.textContent = "Copied";
  setTimeout(() => {
    label.textContent = original;
    button.classList.remove("is-done");
  }, 1600);
}

document.querySelectorAll("[data-copy-target]").forEach((button) => {
  button.addEventListener("click", async () => {
    const field = document.getElementById(button.dataset.copyTarget);
    if (!field) return;
    field.select();
    const ok = await copyText(field.value);
    if (ok) flashButton(button);
    showToast(ok ? "Repository URL copied" : "Copy failed — select the URL and copy it manually", !ok);
  });
});

document.querySelectorAll("[data-copy-text]").forEach((button) => {
  button.addEventListener("click", async () => {
    const ok = await copyText(button.dataset.copyText);
    if (ok) flashButton(button);
    showToast(ok ? `Copied ${button.dataset.copyText}` : "Copy failed", !ok);
  });
});

/* ---------- add to VCC ---------- */

function addToVcc() {
  window.location.assign(`vcc://vpm/addRepo?url=${encodeURIComponent(LISTING_URL)}`);
  showToast("Opening VRChat Creator Companion…");
}

document.querySelectorAll("[data-add-vcc]").forEach((button) => {
  button.addEventListener("click", addToVcc);
});
