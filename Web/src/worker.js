import { decodeShareCode, levelKey } from "../public/sharecode.js";
import { MAX_BODY_LENGTH, MAX_LEVELS, PAGE_SIZE } from "./limits.js";
import { finishRun, listFastest, listScores, preflight, removeStaleRuns, reportLevel, startRun } from "./scores.js";

const RUN_CALL = /^\/api\/runs\/([0-9a-f]{32})\/(level|finish)$/;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === "/api/levels") {
      if (request.method === "GET") {
        return list(url, env);
      }
      return request.method === "POST" ? upload(request, env) : reply(405, { error: "NotAllowed" });
    }

    return highScores(request, env, url, Date.now()) ?? reply(404, { error: "NotFound" });
  },

  async scheduled(controller, env) {
    await removeStaleRuns(env, controller.scheduledTime);
  }
};

/** The high score calls (see scores.js), or undefined for any other address. */
function highScores(request, env, url, now) {
  const call = url.pathname.match(RUN_CALL);
  const known = call || ["/api/runs", "/api/scores", "/api/fastest"].includes(url.pathname);
  if (!known) return undefined;
  if (request.method === "OPTIONS") return preflight();

  if (url.pathname === "/api/scores" || url.pathname === "/api/fastest") {
    if (request.method !== "GET") return reply(405, { error: "NotAllowed" });
    return url.pathname === "/api/scores" ? listScores(url, env) : listFastest(env);
  }

  if (request.method !== "POST") return reply(405, { error: "NotAllowed" });
  if (!call) return startRun(request, env, url, now);
  return call[2] === "level" ? reportLevel(request, env, url, call[1], now) : finishRun(request, env, url, call[1], now);
}

/** One page, newest first. `next` goes into ?before= for the page after it, and is null on the last one. */
async function list(url, env) {
  const asked = Number(url.searchParams.get("before"));
  const before = Number.isSafeInteger(asked) && asked > 0 ? asked : Number.MAX_SAFE_INTEGER;
  // One row more than a page, only to learn whether another page follows.
  const { results } = await env.DB.prepare(
    "SELECT rowid AS id, code FROM levels WHERE rowid < ?1 ORDER BY rowid DESC LIMIT ?2"
  ).bind(before, PAGE_SIZE + 1).all();
  const page = results.slice(0, PAGE_SIZE);
  return reply(200, {
    codes: page.map(row => row.code),
    next: results.length > PAGE_SIZE ? page.at(-1).id : null
  });
}

async function upload(request, env) {
  const visitor = request.headers.get("CF-Connecting-IP") ?? "unknown";
  const { success } = await env.UPLOADS.limit({ key: visitor });
  if (!success) {
    return reply(429, { error: "TooFast" });
  }

  if (Number(request.headers.get("Content-Length")) > MAX_BODY_LENGTH) {
    return reply(400, { error: "TooBig" });
  }

  const body = await request.text();
  if (body.length > MAX_BODY_LENGTH) {
    return reply(400, { error: "TooBig" });
  }

  // The browser checked this too, but anyone can send anything to this address.
  const { code, json, error } = await decodeShareCode(body);
  if (error) {
    return reply(400, { error });
  }

  const key = await levelKey(json);
  const { meta } = await env.DB.prepare(
    `INSERT INTO levels (code, level_key)
     SELECT ?1, ?2 WHERE (SELECT COALESCE(MAX(rowid), 0) FROM levels) < ?3
     ON CONFLICT (level_key) DO NOTHING`
  ).bind(code, key, MAX_LEVELS).run();
  if (meta.changes > 0) {
    return reply(201, { code });
  }

  const first = await env.DB.prepare("SELECT code FROM levels WHERE level_key = ?1").bind(key).first();
  return first ? reply(409, { error: "Duplicate", code: first.code }) : reply(503, { error: "Full" });
}

function reply(status, body) {
  return Response.json(body, { status });
}
