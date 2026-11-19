import { decodeShareCode, ERROR_MESSAGES } from "./sharecode.js";
import { PAGE_HEIGHT, canvasMetrics, layoutLevel, renderSvg } from "./preview.js";

const API = "/api/levels";
const MESSAGES = {
  ...ERROR_MESSAGES,
  TooFast: "That was a lot of uploads. Wait a minute and try again.",
  Full: "The page is full for now. Try again later.",
  Failed: "The upload failed. Try again in a moment."
};

// The first stretch of a level that a row's picture shows: four times as wide as it is tall, like the picture.
const THUMB_WIDTH = PAGE_HEIGHT * 4;
// How fast the large picture drifts along a level, in screen pixels a second, and how long it waits after a touch.
const DRIFT_SPEED = 40;
const DRIFT_PAUSE_MS = 4000;

const element = id => document.getElementById(id);
const list = element("list");
const more = element("more");

/** Newest first: { code, level, row, layout } per shared level loaded so far. */
let entries = [];
let selected = -1;
/** Where the next page starts, as the server said. Null before the first page and after the last. */
let next = null;
let loading = false;

// Text is measured with the page's own fonts, so nothing is laid out before they have loaded.
const metrics = Promise.all(['400 100px "Space Mono"', '700 100px "Space Mono"', '400 100px "Archivo Black"']
  .map(font => document.fonts.load(font)))
  .catch(() => {})
  .then(() => canvasMetrics(document.createElement("canvas")));

const displayName = level => (typeof level.name === "string" && level.name.trim()) || "untitled";

// ---------------------------------------------------------------- what a level holds

const THINGS = [
  [piece => piece.type === "word" && piece.kind === "ice", "ice"],
  [piece => piece.type === "word" && piece.kind === "hot", "hot words"],
  [piece => piece.type === "word" && piece.kind === "sticky", "sticky words"],
  [piece => piece.type === "word" && piece.kind === "plank", "hyphen bridges"],
  [piece => piece.type === "word" && piece.kind === "float", "floating words"],
  [piece => piece.type === "kerned", "loose letters"],
  [piece => piece.type === "heavy", "heavy words"],
  [piece => piece.type === "brick" || (piece.type === "word" && piece.kind === "cracked"), "bricks"],
  [piece => piece.type === "stickyWall", "sticky walls"],
  [piece => piece.type === "tube", "stacked walls"],
  [piece => piece.type === "spring", "springs"],
  [piece => piece.type === "door" || piece.type === "plate", "doors"],
  [piece => piece.type === "lift", "lifts"],
  [piece => piece.type === "hooks", "comma hooks"],
  [piece => piece.type === "capsLock", "caps lock"],
  [piece => piece.type === "wind", "wind"],
  [piece => piece.type === "power", "power blocks"],
  [piece => piece.type === "checkpoint", "checkpoints"]
];

/** The palette's names and glyphs. */
const ENEMIES = {
  walker: ["&", "walker"], spike: ["^", "spike"], armour: ["§", "armour"], hopper: ["!", "hopper"],
  flier: ["~", "flier"], launcher: ["«", "launcher"], drip: ["'", "drip"], spark: ["°", "spark"],
  fireBar: ["o·", "fire bar"], eye: ["ö", "eye"], curl: ["@", "curl"], swapper: ["↕", "case swapper"],
  mimic: ["b", "mimic"], cursor: ["|", "cursor"]
};

const pieces = level => (Array.isArray(level.pieces) ? level.pieces : []).filter(piece => piece && typeof piece === "object");

function chip(text, glyph = "", kind = "") {
  const span = document.createElement("span");
  span.className = kind ? `chip ${kind}` : "chip";
  if (glyph) {
    const b = document.createElement("b");
    b.textContent = glyph;
    span.append(b);
  }

  span.append(text);
  return span;
}

function facts(entry) {
  const { level, layout } = entry;
  const seconds = typeof level.seconds === "number" && level.seconds > 0 ? level.seconds : 60;
  const screens = Math.max(1, Math.round((layout.rightPx + 200) / 1280 * 10) / 10);
  const hashes = pieces(level).filter(piece => piece.type === "hash").length;
  // Label first, then the value in bold: "letter A", "time 60 s".
  const result = [
    chip("letter", level.missingLetter === "A" ? "A" : "a", "fact"),
    chip("time", `${Math.round(seconds)} s`, "fact"),
    chip("length", `${screens} ${screens === 1 ? "screen" : "screens"}`, "fact")
  ];
  if (hashes > 0) {
    result.push(chip("to collect", `${hashes} #`, "fact"));
  }

  if (level.ceiling === true) {
    result.push(chip("ceiling", "", "fact"));
  }

  return result;
}

function contents(level) {
  const all = pieces(level);
  const things = THINGS.filter(([test]) => all.some(test)).map(([, name]) => chip(name));
  const kinds = new Set(all.filter(piece => piece.type === "enemy").map(piece => piece.kind));
  const enemies = Object.keys(ENEMIES).filter(kind => kinds.has(kind))
    .map(kind => chip(` ${ENEMIES[kind][1]}`, ENEMIES[kind][0], "enemy"));
  return [...things, ...enemies];
}

/** The sentence with the missing letter in yellow, as the game's top bar shows it. */
function fillSentence(target, level) {
  const mark = document.createElement("mark");
  mark.textContent = level.missingLetter === "A" ? "A" : "a";
  target.replaceChildren(String(level.sentenceBefore ?? ""), mark, String(level.sentenceAfter ?? ""));
}

// ---------------------------------------------------------------- pictures

function svgElement(markup) {
  const template = document.createElement("template");
  template.innerHTML = markup;
  return template.content.firstElementChild;
}

async function layoutOf(entry) {
  entry.layout ??= layoutLevel(entry.level, await metrics);
  return entry.layout;
}

/** The start of a level, small, for its row. */
async function fillThumb(target, entry) {
  const svg = svgElement(renderSvg(await layoutOf(entry), { width: THUMB_WIDTH }));
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("preserveAspectRatio", "xMinYMid slice");
  target.replaceChildren(svg);
}

/**
 * A strip showing a whole level at its height, which drifts along it and back like the picture in the game's My
 * levels, and stops for a while when someone drags, scrolls or points at it.
 */
class Strip {
  constructor(container) {
    this.container = container;
    this.direction = 1;
    this.pausedUntil = 0;
    this.last = 0;
    this.position = 0;
    const pause = () => {
      this.pausedUntil = performance.now() + DRIFT_PAUSE_MS;
    };
    for (const name of ["wheel", "touchstart", "keydown", "focus"]) {
      container.addEventListener(name, pause, { passive: true });
    }

    container.addEventListener("pointermove", pause);
    this.dragging(pause);
    this.reduced = matchMedia("(prefers-reduced-motion: reduce)");
    new ResizeObserver(() => this.fit()).observe(container);
    requestAnimationFrame(time => this.frame(time));
  }

  /** Mouse drags scroll it; touch scrolls it natively. */
  dragging(pause) {
    let from = null;
    this.container.addEventListener("pointerdown", event => {
      if (event.pointerType !== "mouse" || event.button !== 0) {
        return;
      }

      from = { x: event.clientX, scroll: this.container.scrollLeft };
      this.container.setPointerCapture(event.pointerId);
      this.container.classList.add("dragging");
      pause();
    });
    this.container.addEventListener("pointermove", event => {
      if (from) {
        this.container.scrollLeft = from.scroll - (event.clientX - from.x);
        this.position = this.container.scrollLeft;
      }
    });
    const stop = () => {
      from = null;
      this.container.classList.remove("dragging");
    };
    this.container.addEventListener("pointerup", stop);
    this.container.addEventListener("pointercancel", stop);
  }

  show(layout, title) {
    const svg = svgElement(renderSvg(layout, { title }));
    this.container.replaceChildren(svg);
    this.fit();
    this.container.scrollLeft = 0;
    this.position = 0;
    this.direction = 1;
    this.pausedUntil = performance.now() + 1500;
  }

  /** Sizes the picture to the strip's height, leaving room for its scrollbar. */
  fit() {
    const svg = this.container.firstElementChild;
    if (!svg) {
      return;
    }

    const [, , width, height] = svg.getAttribute("viewBox").split(" ").map(Number);
    const shown = this.container.clientHeight;
    svg.setAttribute("height", String(shown));
    svg.setAttribute("width", String(Math.round(shown * width / height)));
  }

  frame(time) {
    const seconds = Math.min(0.1, (time - this.last) / 1000);
    this.last = time;
    const room = this.container.scrollWidth - this.container.clientWidth;
    if (room > 0 && time > this.pausedUntil && !this.reduced.matches && document.visibilityState === "visible") {
      if (Math.abs(this.position - this.container.scrollLeft) > 2) {
        this.position = this.container.scrollLeft; // someone scrolled it
      }

      this.position += this.direction * DRIFT_SPEED * seconds;
      if (this.position <= 0 || this.position >= room) {
        this.position = Math.max(0, Math.min(room, this.position));
        this.direction = -this.direction;
        this.pausedUntil = time + 1200;
      }

      this.container.scrollLeft = this.position;
    }

    requestAnimationFrame(next => this.frame(next));
  }
}

const strip = new Strip(element("strip"));
const pastedStrip = new Strip(element("pasted-strip"));

// ---------------------------------------------------------------- the list

function makeRow(entry) {
  const row = document.createElement("button");
  row.type = "button";
  row.className = "row";
  row.tabIndex = -1;
  const thumb = document.createElement("span");
  thumb.className = "thumb";
  const title = document.createElement("span");
  title.className = "title";
  title.textContent = displayName(entry.level);
  const sentence = document.createElement("span");
  sentence.className = "sentence";
  fillSentence(sentence, entry.level);
  row.append(thumb, title, sentence);
  row.addEventListener("click", () => select(entries.indexOf(entry)));
  fillThumb(thumb, entry);
  return row;
}

function showCount() {
  element("count").textContent = entries.length === 0
    ? "No levels yet. Paste a level code above to add the first one."
    : "Newest first";
}

async function select(index, focus = false) {
  if (entries[selected]) {
    entries[selected].row.setAttribute("aria-current", "false");
    entries[selected].row.tabIndex = -1;
  }

  selected = index;
  const entry = entries[index];
  element("detail").hidden = !entry;
  if (!entry) {
    return;
  }

  const { level, row } = entry;
  row.setAttribute("aria-current", "true");
  row.tabIndex = 0;
  row.scrollIntoView({ block: "nearest" });
  if (focus) {
    row.focus();
  }

  element("name").textContent = displayName(level);
  fillSentence(element("sentence"), level);
  element("code").textContent = entry.code;
  element("copy").textContent = "Copy code";
  element("chips").replaceChildren(...contents(level));
  const layout = await layoutOf(entry);
  if (selected === index) {
    element("facts").replaceChildren(...facts(entry));
    strip.show(layout, displayName(level));
  }
}

/** A code from the server, read the way the upload was checked. A code from a newer game stays out of the list. */
async function toEntry(code) {
  const { level, error } = await decodeShareCode(code);
  return error ? null : { code, level };
}

/** Loads the next page once the button at the end of the list comes near the visible part. */
const nearEnd = new IntersectionObserver(changes => {
  if (changes.some(change => change.isIntersecting)) {
    loadMore();
  }
}, { root: list, rootMargin: "0px 0px 300px 0px" });

async function loadMore() {
  if (loading) {
    return;
  }

  loading = true;
  more.disabled = true;
  more.textContent = "Loading...";
  nearEnd.unobserve(more);
  try {
    const response = await fetch(next === null ? API : `${API}?before=${next}`);
    if (!response.ok) {
      throw new Error(String(response.status));
    }

    const page = await response.json();
    // A level shown at the top after a duplicate upload can arrive again here, further down the list.
    const codes = page.codes.filter(code => !entries.some(entry => entry.code === code));
    const fresh = (await Promise.all(codes.map(toEntry))).filter(Boolean);
    for (const entry of fresh) {
      entry.row = makeRow(entry);
    }

    more.before(...fresh.map(entry => entry.row));
    entries.push(...fresh);
    next = page.next;
    more.hidden = next === null;
    showCount();
    if (selected < 0 && entries.length > 0) {
      select(0);
    }

    if (next !== null) {
      nearEnd.observe(more); // reports at once if the button is still near, so a short page cannot stall
    }
  } catch {
    if (entries.length === 0) {
      element("count").textContent = "The levels could not be loaded.";
    }

    more.textContent = "Try again"; // by click only: retrying by itself would hammer a server in trouble
    more.disabled = false;
  } finally {
    loading = false;
  }
}

/** The level the server knows, added to the top of the list if this page has not shown it yet. */
async function showLevel(code) {
  const known = entries.findIndex(entry => entry.code === code);
  if (known >= 0) {
    select(known);
    return entries[known];
  }

  const entry = await toEntry(code);
  entry.row = makeRow(entry);
  list.prepend(entry.row);
  if (selected >= 0) {
    selected++;
  }

  entries.unshift(entry);
  showCount();
  select(0);
  return entry;
}

// ---------------------------------------------------------------- adding a level

function say(text, mood = "") {
  const message = element("message");
  message.textContent = text;
  message.className = mood ? `message ${mood}` : "message";
}

const HINT = element("message").textContent.replace(/\s+/g, " ").trim();
let checking = 0;

/** Shows what a pasted code holds before anything is sent, and why it can't go up when it can't. */
async function check() {
  const input = element("input");
  const ticket = ++checking;
  if (!input.value.trim()) {
    element("pasted").hidden = true;
    say(HINT);
    return;
  }

  const result = await decodeShareCode(input.value);
  if (ticket !== checking) {
    return; // a later paste overtook this one
  }

  if (!result.level) {
    element("pasted").hidden = true;
    say(MESSAGES[result.error], "bad");
    return;
  }

  const entry = { code: result.code, level: result.level };
  element("pasted-name").textContent = displayName(entry.level);
  fillSentence(element("pasted-sentence"), entry.level);
  element("pasted").hidden = false;
  pastedStrip.show(await layoutOf(entry), displayName(entry.level));
  if (result.error) {
    say(MESSAGES[result.error], "bad");
  } else {
    say("This is the level the code holds. Upload it to share it here.", "good");
  }
}

async function upload(event) {
  event.preventDefault();
  const input = element("input");
  if (!input.value.trim()) {
    say("Paste a level code first.", "bad");
    input.focus();
    return;
  }

  const { error } = await decodeShareCode(input.value);
  if (error) {
    say(MESSAGES[error], "bad");
    input.focus();
    return;
  }

  element("send").disabled = true;
  try {
    const response = await fetch(API, { method: "POST", body: input.value });
    const result = await response.json();
    if (response.status === 201) {
      const { level } = await showLevel(result.code);
      input.value = "";
      element("pasted").hidden = true;
      say(`${displayName(level)} is on the page. Thanks for sharing!`, "good");
    } else if (result.error === "Duplicate") {
      const { level } = await showLevel(result.code);
      say(`This level is already here as ${displayName(level)}.`, "bad");
    } else {
      say(MESSAGES[result.error] ?? MESSAGES.Failed, "bad");
    }
  } catch {
    say(MESSAGES.Failed, "bad");
  } finally {
    element("send").disabled = false;
  }
}

async function copy() {
  const button = element("copy");
  try {
    await navigator.clipboard.writeText(entries[selected].code);
    button.textContent = "Copied";
  } catch {
    getSelection().selectAllChildren(element("code"));
    button.textContent = "Code selected: copy it with Ctrl+C";
  }
}

list.addEventListener("keydown", event => {
  if (event.key === "ArrowDown" || event.key === "ArrowUp") {
    event.preventDefault();
    const to = selected + (event.key === "ArrowDown" ? 1 : -1);
    if (entries[to]) {
      select(to, true);
    }
  }
});
element("input").addEventListener("input", check);
element("form").addEventListener("submit", upload);
element("copy").addEventListener("click", copy);
more.addEventListener("click", loadMore);
loadMore();
