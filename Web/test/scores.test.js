import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { beforeEach, mock, test } from "node:test";
import { CLOCK_SLACK_MS, RUN_MAX_MS } from "../src/limits.js";
import { cleanName, isRude } from "../src/names.js";
import * as workerModule from "../src/worker.js";
import { fakeD1 } from "./fake-d1.js";

const worker = workerModule.default;
const limits = JSON.parse(readFileSync(new URL("../src/run-limits.json", import.meta.url), "utf8"));
const floors = JSON.parse(readFileSync(new URL("../src/run-floors.json", import.meta.url), "utf8"));
const KEY = "test key";
const LAST = limits.levels.length;

let env;
let clock;
beforeEach(() => {
  env = {
    DB: fakeD1(),
    UPLOADS: { limit: async () => ({ success: true }) },
    SCORES: { limit: async () => ({ success: true }) },
    SCORE_KEY: KEY
  };
  clock = Date.UTC(2026, 10, 20, 12);
  mock.method(Date, "now", () => clock);
});

async function sign(method, path, body, key = KEY) {
  const encoder = new TextEncoder();
  const cryptoKey = await crypto.subtle.importKey("raw", encoder.encode(key), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign("HMAC", cryptoKey, encoder.encode(`${method} ${path}\n${body}`));
  return [...new Uint8Array(signature)].map(b => b.toString(16).padStart(2, "0")).join("");
}

async function post(path, value, { key = KEY, signature } = {}) {
  const body = typeof value === "string" ? value : JSON.stringify(value);
  const headers = { "Content-Type": "application/json", "X-Signature": signature ?? await sign("POST", path, body, key) };
  return worker.fetch(new Request(`https://example.test${path}`, { method: "POST", body, headers }), env);
}

const get = path => worker.fetch(new Request(`https://example.test${path}`), env);

async function expectReply(promise, status, body) {
  const response = await promise;
  assert.equal(response.status, status);
  const json = await response.json();
  if (body !== undefined) assert.deepEqual(json, body);
  return json;
}

async function startRun(extra = {}) {
  const { id } = await expectReply(post("/api/runs", { hash: limits.hash, platform: "linux", version: "1.0", ...extra }), 201);
  assert.match(id, /^[0-9a-f]{32}$/);
  return id;
}

/** A believable finish: some enemies and #, `used` seconds on the clock, and that much time passing first. */
function levelReport(number, { used = 20, earned = 300 } = {}) {
  const seconds = limits.levels[number - 1].seconds;
  const centisLeft = (seconds - used) * 100;
  return { level: number, points: earned + Math.ceil(centisLeft / 100) * limits.pointsPerSecondLeft, centisLeft };
}

async function playLevel(id, number, options = {}) {
  clock += ((options.used ?? 20) + 2) * 1000;
  return post(`/api/runs/${id}/level`, levelReport(number, options));
}

async function playRun(levels = LAST, options = {}) {
  const id = await startRun();
  for (let n = 1; n <= levels; n++) await expectReply(playLevel(id, n, options), 200, { levelsDone: n });
  return id;
}

const finish = (id, name, partialPoints = 0) => post(`/api/runs/${id}/finish`, { name, partialPoints });

test("the main module still exports only its handlers", () => {
  assert.deepEqual(Object.keys(workerModule), ["default"]);
  assert.deepEqual(Object.keys(worker).sort(), ["fetch", "scheduled"]);
});

test("a whole run goes on the list with the points the server added up", async () => {
  const id = await playRun();
  const perLevel = levelReport(1).points;
  const result = await expectReply(finish(id, "daniel"), 200);
  assert.deepEqual(result, { rank: 1, count: 1, total: perLevel * LAST, name: "daniel", nameRefused: false });

  const list = await expectReply(get("/api/scores"), 200);
  assert.equal(list.count, 1);
  assert.deepEqual(list.scores, [
    { rank: 1, name: "daniel", total: perLevel * LAST, levels: LAST, platform: "linux", date: "2026-11-20", marked: false }
  ]);
  assert.equal(list.you, null);
  assert.ok(!JSON.stringify(list).includes(id), "a run's id is its key and never shown");
});

test("a run from a build with other levels is turned away", async () => {
  await expectReply(post("/api/runs", { hash: "0000000000000000", platform: "web", version: "1.0" }), 409, { error: "OldVersion" });
  await expectReply(post("/api/runs", { hash: limits.hash, platform: "amiga", version: "1.0" }), 400, { error: "BadRequest" });
  await expectReply(post("/api/runs", { hash: limits.hash, platform: "web", version: "1.0 beta <b>" }), 400, { error: "BadRequest" });
  await expectReply(post("/api/runs", "not json"), 400, { error: "BadRequest" });
  await expectReply(post("/api/runs", "[1]"), 400, { error: "BadRequest" });
});

test("unsigned or wrongly signed calls change nothing, and without a key nothing is taken", async () => {
  const start = { hash: limits.hash, platform: "web", version: "1.0" };
  await expectReply(post("/api/runs", start, { signature: "" }), 401, { error: "BadSignature" });
  await expectReply(post("/api/runs", start, { key: "guessed" }), 401, { error: "BadSignature" });
  delete env.SCORE_KEY;
  await expectReply(post("/api/runs", start), 503, { error: "NotReady" });
  assert.equal(env.DB.raw.prepare("SELECT COUNT(*) AS n FROM runs").get().n, 0);
});

test("levels come one after another, from the first, each once", async () => {
  const id = await startRun();
  await expectReply(playLevel(id, 2), 409, { error: "WrongLevel" });
  await expectReply(playLevel(id, 1), 200);
  await expectReply(playLevel(id, 1), 409, { error: "WrongLevel" });
  await expectReply(playLevel(id, 3), 409, { error: "WrongLevel" });
  await expectReply(playLevel("0".repeat(32), 1), 404, { error: "NoRun" });
  await expectReply(post(`/api/runs/${id}/level`, { level: 99, points: 0, centisLeft: 0 }), 400, { error: "BadRequest" });
});

test("a level can't pay more than every enemy, every # and the time left", async () => {
  const level = limits.levels[0];
  const all = level.enemies * limits.pointsPerEnemy + level.collectibles * limits.pointsPerCollectible;
  const id = await startRun();
  clock += 60000;
  // 40.00 to 40.01 s left is worth 400 or 410; anything over that and everything is refused.
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: all + 420, centisLeft: 4000 }), 422, { error: "Impossible" });
  // Less than the time bonus alone can't be either.
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: 390, centisLeft: 4000 }), 422, { error: "Impossible" });
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: 405, centisLeft: 4000 }), 422, { error: "Impossible" });
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: all + 400, centisLeft: 4000 }), 200);
});

test("the banked seconds are rounded up from a clock rounded down to 1/100 s", async () => {
  const id = await startRun();
  clock += 60000;
  // 39.995 s left arrives as 3999 centiseconds; the game banks 40 seconds.
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: 400, centisLeft: 3999 }), 200);
});

test("a level finished faster than its floor, or than the server's clock, is refused", async () => {
  const floor = floors.floorSeconds[limits.levels[0].scene];
  const id = await startRun();
  const started = clock;
  await expectReply(playLevel(id, 1, { used: floor - 1 }), 422, { error: "TooFast" });

  // Claims 30 s on the clock, but only 20 s passed since the start.
  clock = started + 20000;
  await expectReply(post(`/api/runs/${id}/level`, levelReport(1, { used: 30 })), 422, { error: "TooFast" });
  // A report may arrive up to the slack early.
  clock = started + 30000 - CLOCK_SLACK_MS;
  await expectReply(post(`/api/runs/${id}/level`, levelReport(1, { used: 30 })), 200);
});

test("a game over adds what the last level paid so far, never more than it could", async () => {
  const id = await playRun(3);
  const fourth = limits.levels[3];
  const all = fourth.enemies * limits.pointsPerEnemy + fourth.collectibles * limits.pointsPerCollectible;
  await expectReply(finish(id, "a", all + 50), 422, { error: "Impossible" });
  await expectReply(finish(id, "a", 125), 422, { error: "Impossible" });
  const result = await expectReply(finish(id, "a", 150), 200);
  assert.equal(result.total, levelReport(1).points * 3 + 150);

  const after = await playRun();
  await expectReply(finish(after, "b", 50), 422, { error: "Impossible" });
});

test("a finished run takes no more calls", async () => {
  const id = await playRun(2);
  await expectReply(finish(id, "x"), 200);
  await expectReply(finish(id, "x"), 409, { error: "Finished" });
  await expectReply(playLevel(id, 3), 409, { error: "Finished" });
  assert.equal(env.DB.raw.prepare("SELECT finished FROM stats").get().finished, 1);
});

test("a run left too long expires, and the daily clean-up removes it but no finished one", async () => {
  const kept = await playRun(1);
  await expectReply(finish(kept, "kept"), 200);
  const left = await playRun(2);
  clock += RUN_MAX_MS + 1;
  await expectReply(playLevel(left, 3), 410, { error: "Expired" });

  await worker.scheduled({ scheduledTime: clock }, env);
  assert.deepEqual(env.DB.raw.prepare("SELECT id FROM runs").all().map(r => r.id), [kept]);
  assert.deepEqual(env.DB.raw.prepare("SELECT DISTINCT run_id FROM run_levels").all().map(r => r.run_id), [kept]);
});

test("rude or broken names are stored as ??? and the game is told", async () => {
  for (const [given, stored, refused] of [
    ["  Zoë 2  ", "Zoë 2", false],
    ["F.u.c.k", "???", true],
    ["sh1t", "???", true],
    ["thirteen chars", "???", true],
    ["<script>", "???", true],
    ["", "???", false]
  ]) {
    const id = await playRun(1);
    const result = await expectReply(finish(id, given), 200);
    assert.deepEqual([result.name, result.nameRefused], [stored, refused], given);
  }
});

test("the word filter leaves harmless words alone", () => {
  for (const name of ["class", "document", "grape", "heilig", "Scunthorpe", "assassin", "Dickens", "Sussex"]) {
    assert.equal(isRude(name), name === "Scunthorpe", name);
  }
  assert.equal(isRude("big ass"), true);
  assert.equal(cleanName("Nazi").name, "???");
});

test("equal totals keep the order they finished in, and a run sees its own place", async () => {
  const first = await playRun(1);
  await expectReply(finish(first, "first"), 200);
  const higher = await playRun(2);
  await expectReply(finish(higher, "higher"), 200);
  const second = await playRun(1);
  const result = await expectReply(finish(second, "second"), 200);
  assert.deepEqual([result.rank, result.count], [3, 3]);

  const page = await expectReply(get(`/api/scores?limit=2&run=${second}`), 200);
  assert.deepEqual(page.scores.map(s => [s.rank, s.name]), [[1, "higher"], [2, "first"]]);
  assert.deepEqual([page.you.rank, page.you.name], [3, "second"]);
  const next = await expectReply(get("/api/scores?offset=2&limit=2"), 200);
  assert.deepEqual(next.scores.map(s => [s.rank, s.name]), [[3, "second"]]);
});

test("unfinished runs are not on the list and don't count", async () => {
  await playRun(3);
  await expectReply(get("/api/scores"), 200, { count: 0, offset: 0, scores: [], you: null });
});

test("the fastest finish of each level, from finished runs only, the earlier one on a tie", async () => {
  const slow = await playRun(2, { used: 30 });
  await expectReply(finish(slow, "slow"), 200);
  const quick = await playRun(1, { used: 20 });
  const unfinishedQuicker = await playRun(1, { used: 10 });
  await expectReply(finish(quick, "quick"), 200);
  const tie = await playRun(1, { used: 20 });
  await expectReply(finish(tie, "tie"), 200);
  assert.ok(unfinishedQuicker);

  const { levels } = await expectReply(get("/api/fastest"), 200);
  assert.equal(levels.length, LAST);
  assert.deepEqual(levels[0], { level: 1, scene: "Overworld", best: { centis: 2000, name: "quick", date: "2026-11-20", marked: false } });
  assert.deepEqual([levels[1].best.name, levels[1].best.centis], ["slow", 3000]);
  assert.equal(levels[2].best, null);
});

test("runs near the maximum or near a floor time are marked", async () => {
  const level = limits.levels[0];
  const all = level.enemies * limits.pointsPerEnemy + level.collectibles * limits.pointsPerCollectible;
  const id = await startRun();
  clock += 60000;
  await expectReply(post(`/api/runs/${id}/level`, { level: 1, points: all + 400, centisLeft: 4000 }), 200);
  await expectReply(finish(id, "max"), 200);
  const { scores } = await expectReply(get("/api/scores"), 200);
  assert.equal(scores[0].marked, true);

  const floor = floors.floorSeconds[level.scene];
  const quick = await playRun(1, { used: floor });
  await expectReply(finish(quick, "quick"), 200);
  const { levels } = await expectReply(get("/api/fastest"), 200);
  assert.deepEqual([levels[0].best.name, levels[0].best.marked], ["quick", true]);
});

test("the web build may call from another site, and floods are slowed", async () => {
  const response = await worker.fetch(new Request("https://example.test/api/runs", { method: "OPTIONS" }), env);
  assert.equal(response.status, 204);
  assert.equal(response.headers.get("Access-Control-Allow-Origin"), "*");
  assert.match(response.headers.get("Access-Control-Allow-Headers"), /X-Signature/);
  assert.equal((await get("/api/scores")).headers.get("Access-Control-Allow-Origin"), "*");

  env.SCORES.limit = async () => ({ success: false });
  await expectReply(post("/api/runs", { hash: limits.hash, platform: "web", version: "1.0" }), 429, { error: "TooFast" });
});

test("other addresses and methods are refused", async () => {
  await expectReply(get("/api/runs"), 405, { error: "NotAllowed" });
  await expectReply(post("/api/scores", {}), 405, { error: "NotAllowed" });
  await expectReply(get("/api/nothing"), 404, { error: "NotFound" });
  await expectReply(post("/api/runs/abc/level", {}), 404, { error: "NotFound" });
});
