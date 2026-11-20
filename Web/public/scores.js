// The high scores page: the world list of runs (GET /api/scores) and the fastest finish of each level
// (GET /api/fastest), both written by the game through the Worker (src/scores.js).

// A short first page, so the fastest times are not far down on a phone; more come in bigger steps.
const FIRST_PAGE = 25;
const PAGE_SIZE = 50;
const PLATFORMS = { windows: "Windows", linux: "Linux", mac: "Mac", web: "Browser" };
const MARK_TITLE = "Close to everything a run could reach: worth a second look";

const element = id => document.getElementById(id);
const runs = element("runs").tBodies[0];
const fastest = element("fastest").tBodies[0];
const more = element("more");
const count = element("count");

let offset = 0;
let loading = false;

/** A table cell holding text, never markup: names come from players. */
function cell(text, className) {
  const td = document.createElement("td");
  td.textContent = text;
  if (className) td.className = className;
  return td;
}

function nameCell(name, marked) {
  const td = cell(name, "name");
  if (marked) {
    const mark = document.createElement("span");
    mark.className = "mark";
    mark.textContent = " ●";
    mark.title = MARK_TITLE;
    td.append(mark);
  }
  return td;
}

function message(body, text, columns) {
  const row = document.createElement("tr");
  const td = cell(text, "empty");
  td.colSpan = columns;
  row.append(td);
  body.replaceChildren(row);
}

const seconds = centis => `${(centis / 100).toFixed(2)} s`;

async function loadRuns() {
  if (loading) return;
  loading = true;
  more.disabled = true;
  try {
    const response = await fetch(`/api/scores?offset=${offset}&limit=${offset === 0 ? FIRST_PAGE : PAGE_SIZE}`);
    if (!response.ok) throw new Error(response.status);
    const page = await response.json();
    if (offset === 0) runs.replaceChildren();
    for (const run of page.scores) {
      const row = document.createElement("tr");
      row.append(
        cell(run.rank, "num"),
        nameCell(run.name, run.marked),
        cell(run.total.toLocaleString("en"), "num score"),
        cell(`${run.levels}/15`, "num"),
        cell(PLATFORMS[run.platform] ?? run.platform, "wide"),
        cell(run.date, "wide")
      );
      runs.append(row);
    }
    offset += page.scores.length;
    count.textContent = page.count === 1 ? "1 run" : `${page.count.toLocaleString("en")} runs`;
    if (page.count === 0) message(runs, "No runs yet. Play the game from the first level to be the first.", 6);
    more.hidden = offset >= page.count || page.scores.length === 0;
  } catch {
    if (offset === 0) message(runs, "The runs could not be loaded. Try again in a moment.", 6);
    count.textContent = "";
  } finally {
    loading = false;
    more.disabled = false;
  }
}

async function loadFastest() {
  try {
    const response = await fetch("/api/fastest");
    if (!response.ok) throw new Error(response.status);
    const { levels } = await response.json();
    fastest.replaceChildren(...levels.map(level => {
      const row = document.createElement("tr");
      const best = level.best;
      row.append(
        cell(`${level.level} · ${level.scene}`, "level"),
        cell(best ? seconds(best.centis) : "-", "num score"),
        best ? nameCell(best.name, best.marked) : cell("", "name"),
        cell(best ? best.date : "", "wide")
      );
      if (!best) row.className = "open";
      return row;
    }));
  } catch {
    message(fastest, "The times could not be loaded. Try again in a moment.", 4);
  }
}

more.addEventListener("click", loadRuns);
loadRuns();
loadFastest();
