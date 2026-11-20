// Online high scores. The game starts a run at the first level, reports each level as it is finished and
// finishes the run with a name. The server never trusts a total: it checks every report against the level's
// limits (run-limits.json, written by the game's editor), its floor time (run-floors.json, kept by hand) and
// its own clock, and adds the points up itself.

import limits from "./run-limits.json" with { type: "json" };
import floors from "./run-floors.json" with { type: "json" };
import {
  CLOCK_SLACK_MS, MARK_FLOOR_FACTOR, MARK_SHARE, RUN_BODY_LENGTH, RUN_MAX_MS,
  SCORES_MAX_OFFSET, SCORES_PAGE_DEFAULT, SCORES_PAGE_MAX
} from "./limits.js";
import { cleanName } from "./names.js";

export const PLATFORMS = ["windows", "linux", "mac", "web"];
const VERSION = /^[0-9A-Za-z._-]{1,20}$/;
const LEVEL_COUNT = limits.levels.length;

/** Points a level can pay apart from the time left: every enemy and every #. */
const earnable = level => level.enemies * limits.pointsPerEnemy + level.collectibles * limits.pointsPerCollectible;
const floorCentis = level => Math.round(floors.floorSeconds[level.scene] * 100);

// Anyone may call these from anywhere: nothing rides on cookies, and the web build runs on itch.io's domain.
const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, X-Signature",
  "Access-Control-Max-Age": "86400"
};

export const preflight = () => new Response(null, { status: 204, headers: CORS });

function reply(status, body) {
  return Response.json(body, { status, headers: CORS });
}

const isCount = value => Number.isSafeInteger(value) && value >= 0;

/**
 * The body as JSON, once the size, the signature and the rate limit are fine. The signature is an HMAC-SHA256,
 * in hex, of "<METHOD> <path>\n<body>" under the key the game is built with (secret SCORE_KEY). It only stops
 * casual tampering: anyone who takes the game apart has the key.
 */
async function readRequest(request, env, url) {
  if (!env.SCORE_KEY) return { error: reply(503, { error: "NotReady" }) };
  if (Number(request.headers.get("Content-Length")) > RUN_BODY_LENGTH) return { error: reply(400, { error: "TooBig" }) };
  const text = await request.text();
  if (text.length > RUN_BODY_LENGTH) return { error: reply(400, { error: "TooBig" }) };

  if (!(await signatureFits(env.SCORE_KEY, `${request.method} ${url.pathname}\n${text}`, request.headers.get("X-Signature")))) {
    return { error: reply(401, { error: "BadSignature" }) };
  }

  const visitor = request.headers.get("CF-Connecting-IP") ?? "unknown";
  const { success } = await env.SCORES.limit({ key: visitor });
  if (!success) return { error: reply(429, { error: "TooFast" }) };

  try {
    const body = JSON.parse(text);
    if (body !== null && typeof body === "object" && !Array.isArray(body)) return { body };
  } catch {
    // Falls through to the same answer as any other body that isn't an object.
  }
  return { error: reply(400, { error: "BadRequest" }) };
}

async function signatureFits(secret, message, signature) {
  if (typeof signature !== "string" || !/^[0-9a-f]{64}$/.test(signature)) return false;
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["verify"]);
  const bytes = new Uint8Array(signature.match(/../g).map(pair => parseInt(pair, 16)));
  return crypto.subtle.verify("HMAC", key, bytes, encoder.encode(message));
}

/** POST /api/runs {hash, platform, version} → 201 {id}. Called as the first level starts. */
export async function startRun(request, env, url, now) {
  const { body, error } = await readRequest(request, env, url);
  if (error) return error;
  if (body.hash !== limits.hash) return reply(409, { error: "OldVersion" });
  if (!PLATFORMS.includes(body.platform) || typeof body.version !== "string" || !VERSION.test(body.version)) {
    return reply(400, { error: "BadRequest" });
  }

  const id = [...crypto.getRandomValues(new Uint8Array(16))].map(b => b.toString(16).padStart(2, "0")).join("");
  await env.DB.prepare(
    "INSERT INTO runs (id, started_at, last_at, platform, version) VALUES (?1, ?2, ?2, ?3, ?4)"
  ).bind(id, now, body.platform, body.version).run();
  return reply(201, { id });
}

/** The run, or the reply that says why it can't take another call. */
async function openRun(env, id, now) {
  const run = await env.DB.prepare(
    "SELECT started_at, last_at, levels_done, points, max_reachable, finished_at FROM runs WHERE id = ?1"
  ).bind(id).first();
  if (!run) return { error: reply(404, { error: "NoRun" }) };
  if (run.finished_at !== null) return { error: reply(409, { error: "Finished" }) };
  if (now - run.started_at > RUN_MAX_MS) return { error: reply(410, { error: "Expired" }) };
  return { run };
}

/**
 * POST /api/runs/{id}/level {level, points, centisLeft} → 200 {levelsDone}. Sent as a level is finished, before
 * the time left is banked: `points` is everything the level paid including the time bonus, `centisLeft` the
 * clock in 1/100 s, rounded down.
 */
export async function reportLevel(request, env, url, id, now) {
  const { body, error } = await readRequest(request, env, url);
  if (error) return error;
  const { level: number, points, centisLeft } = body;
  if (!Number.isSafeInteger(number) || number < 1 || number > LEVEL_COUNT || !isCount(points) || !isCount(centisLeft)) {
    return reply(400, { error: "BadRequest" });
  }

  const { run, error: closed } = await openRun(env, id, now);
  if (closed) return closed;
  if (number !== run.levels_done + 1) return reply(409, { error: "WrongLevel" });

  const level = limits.levels[number - 1];
  const clock = level.seconds * 100;
  if (centisLeft > clock) return reply(400, { error: "BadRequest" });

  // The game banks Mathf.CeilToInt(TimeLeft) seconds; centisLeft is TimeLeft rounded down, so the true clock
  // lies between it and the next 1/100 s.
  const bonusLow = Math.ceil(centisLeft / 100) * limits.pointsPerSecondLeft;
  const bonusHigh = Math.min(Math.ceil((centisLeft + 1) / 100), level.seconds) * limits.pointsPerSecondLeft;
  if (points % 10 !== 0 || points < bonusLow || points > earnable(level) + bonusHigh) {
    return reply(422, { error: "Impossible" });
  }

  const centis = clock - centisLeft;
  if (centis < floorCentis(level)) return reply(422, { error: "TooFast" });
  if (now - run.last_at < centis * 10 - CLOCK_SLACK_MS) return reply(422, { error: "TooFast" });

  const [moved] = await env.DB.batch([
    env.DB.prepare(
      `UPDATE runs SET levels_done = ?1, points = points + ?2, max_reachable = max_reachable + ?3, last_at = ?4
       WHERE id = ?5 AND levels_done = ?6 AND finished_at IS NULL`
    ).bind(number, points, earnable(level) + bonusLow, now, id, number - 1),
    env.DB.prepare(
      "INSERT OR IGNORE INTO run_levels (run_id, level, points, centis) VALUES (?1, ?2, ?3, ?4)"
    ).bind(id, number, points, centis)
  ]);
  if (moved.meta.changes === 0) return reply(409, { error: "WrongLevel" });
  return reply(200, { levelsDone: number });
}

/**
 * POST /api/runs/{id}/finish {name, partialPoints} → 200 {rank, count, total, name, nameRefused}. Sent after the
 * name is typed, at game over or after the last level. `partialPoints` is what the level a game over stopped in
 * had paid so far (no time bonus there); 0 after the last level.
 */
export async function finishRun(request, env, url, id, now) {
  const { body, error } = await readRequest(request, env, url);
  if (error) return error;
  const partial = body.partialPoints ?? 0;
  if (!isCount(partial)) return reply(400, { error: "BadRequest" });

  const { run, error: closed } = await openRun(env, id, now);
  if (closed) return closed;

  const stoppedIn = run.levels_done < LEVEL_COUNT ? limits.levels[run.levels_done] : null;
  if (partial % 50 !== 0 || partial > (stoppedIn ? earnable(stoppedIn) : 0)) return reply(422, { error: "Impossible" });

  const total = run.points + partial;
  const { results: done } = await env.DB.prepare(
    "SELECT level, centis FROM run_levels WHERE run_id = ?1 ORDER BY level"
  ).bind(id).all();
  const nearFloor = row => row.centis < floorCentis(limits.levels[row.level - 1]) * MARK_FLOOR_FACTOR;
  // Judged on the finished levels only: a perfect run that dies at the start of its last level is still worth a look.
  const marked = (run.max_reachable > 0 && run.points >= run.max_reachable * MARK_SHARE) || done.some(nearFloor);
  const { name, refused } = cleanName(body.name);

  const statements = [
    env.DB.prepare(
      "UPDATE runs SET finished_at = ?1, name = ?2, total = ?3, marked = ?4 WHERE id = ?5 AND finished_at IS NULL"
    ).bind(now, name, total, marked ? 1 : 0, id),
    // changes() is the run update's: 1 the first time, 0 if a second finish lost the race.
    env.DB.prepare("UPDATE stats SET finished = finished + changes() WHERE id = 1"),
    ...done.map(row => env.DB.prepare(
      `INSERT INTO fastest (level, run_id, centis, name, finished_at, marked)
       SELECT ?1, ?2, ?3, ?4, ?5, ?6 WHERE (SELECT finished_at FROM runs WHERE id = ?2) = ?5
       ON CONFLICT (level) DO UPDATE SET run_id = excluded.run_id, centis = excluded.centis, name = excluded.name,
         finished_at = excluded.finished_at, marked = excluded.marked
       WHERE excluded.centis < fastest.centis`
    ).bind(row.level, id, row.centis, name, now, nearFloor(row) ? 1 : 0))
  ];
  const [finished] = await env.DB.batch(statements);
  if (finished.meta.changes === 0) return reply(409, { error: "Finished" });

  const { rank, count } = await rankOf(env, total, now);
  return reply(200, { rank, count, total, name, nameRefused: refused });
}

/** Place among finished runs; equal totals keep the order they finished in. */
async function rankOf(env, total, finishedAt) {
  const above = await env.DB.prepare(
    `SELECT COUNT(*) AS n FROM runs WHERE finished_at IS NOT NULL
     AND (total > ?1 OR (total = ?1 AND finished_at < ?2))`
  ).bind(total, finishedAt).first();
  const stats = await env.DB.prepare("SELECT finished FROM stats WHERE id = 1").first();
  return { rank: above.n + 1, count: stats?.finished ?? 0 };
}

const day = ms => new Date(ms).toISOString().slice(0, 10);

/**
 * GET /api/scores?offset=0&limit=10&run=<id> → {count, offset, scores, you}. `you` is the given run's own row
 * and rank (the game shows it under the top 10), or null.
 */
export async function listScores(url, env) {
  const read = (key, fallback, max) => {
    const value = Number(url.searchParams.get(key) ?? fallback);
    return Number.isSafeInteger(value) && value >= 0 ? Math.min(value, max) : fallback;
  };
  const offset = read("offset", 0, SCORES_MAX_OFFSET);
  const limit = Math.max(1, read("limit", SCORES_PAGE_DEFAULT, SCORES_PAGE_MAX));

  const { results } = await env.DB.prepare(
    `SELECT name, total, levels_done, platform, finished_at, marked FROM runs WHERE finished_at IS NOT NULL
     ORDER BY total DESC, finished_at LIMIT ?1 OFFSET ?2`
  ).bind(limit, offset).all();
  const scores = results.map((row, i) => ({ rank: offset + i + 1, ...publicRow(row) }));

  let you = null;
  const id = url.searchParams.get("run");
  if (id && /^[0-9a-f]{32}$/.test(id)) {
    const row = await env.DB.prepare(
      "SELECT name, total, levels_done, platform, finished_at, marked FROM runs WHERE id = ?1 AND finished_at IS NOT NULL"
    ).bind(id).first();
    if (row) you = { rank: (await rankOf(env, row.total, row.finished_at)).rank, ...publicRow(row) };
  }

  const stats = await env.DB.prepare("SELECT finished FROM stats WHERE id = 1").first();
  return reply(200, { count: stats?.finished ?? 0, offset, scores, you });
}

function publicRow(row) {
  return {
    name: row.name,
    total: row.total,
    levels: row.levels_done,
    platform: row.platform,
    date: day(row.finished_at),
    marked: row.marked === 1
  };
}

/** GET /api/fastest → {levels}: per level in play order, its fastest finish or null. */
export async function listFastest(env) {
  const { results } = await env.DB.prepare(
    "SELECT level, centis, name, finished_at, marked FROM fastest ORDER BY level"
  ).all();
  const byLevel = new Map(results.map(row => [row.level, row]));
  return reply(200, {
    levels: limits.levels.map((level, i) => {
      const row = byLevel.get(i + 1);
      return {
        level: i + 1,
        scene: level.scene,
        best: row ? { centis: row.centis, name: row.name, date: day(row.finished_at), marked: row.marked === 1 } : null
      };
    })
  });
}

/** The daily cron: unfinished runs older than RUN_MAX_MS can never finish, so they go. */
export async function removeStaleRuns(env, now) {
  const before = now - RUN_MAX_MS;
  await env.DB.batch([
    env.DB.prepare(
      "DELETE FROM run_levels WHERE run_id IN (SELECT id FROM runs WHERE finished_at IS NULL AND started_at < ?1)"
    ).bind(before),
    env.DB.prepare("DELETE FROM runs WHERE finished_at IS NULL AND started_at < ?1").bind(before)
  ]);
}
