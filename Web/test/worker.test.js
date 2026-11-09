import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import { beforeEach, test } from "node:test";
import { MAX_BODY_LENGTH, MAX_LEVELS, PAGE_SIZE } from "../src/limits.js";
import * as workerModule from "../src/worker.js";
import { makeCode, makeLevel } from "./make-code.js";

const worker = workerModule.default;

/** The real schema and SQL on a real SQLite, behind the few D1 calls the Worker makes. */
function fakeD1() {
  const database = new DatabaseSync(":memory:");
  database.exec(readFileSync(new URL("../schema.sql", import.meta.url), "utf8"));
  return {
    raw: database,
    prepare(sql) {
      const statement = database.prepare(sql);
      const bound = (...values) => ({
        all: async () => ({ results: statement.all(...values) }),
        first: async () => statement.get(...values) ?? null,
        run: async () => ({ meta: { changes: Number(statement.run(...values).changes) } })
      });
      return { ...bound(), bind: bound };
    }
  };
}

const CODE_A = makeCode(makeLevel({ name: "First" }));
const CODE_A_RENAMED = makeCode(makeLevel({ name: "Copycat" }));
const CODE_B = makeCode(makeLevel({ name: "Second", startX: 200 }));

let env;
beforeEach(() => {
  env = { DB: fakeD1(), UPLOADS: { limit: async () => ({ success: true }) } };
});

const send = (method, body, path = "/api/levels") =>
  worker.fetch(new Request(`https://example.test${path}`, { method, body }), env);

async function expectReply(promise, status, body) {
  const response = await promise;
  assert.equal(response.status, status);
  assert.deepEqual(await response.json(), body);
}

test("the main module exports only its handler, or Cloudflare refuses to start it", () => {
  assert.deepEqual(Object.keys(workerModule), ["default"]);
});

test("an empty list, then uploads newest first, without the whitespace of the paste", async () => {
  await expectReply(send("GET"), 200, { codes: [], next: null });
  await expectReply(send("POST", ` ${CODE_A.slice(0, 9)}\n${CODE_A.slice(9)} `), 201, { code: CODE_A });
  await expectReply(send("POST", CODE_B), 201, { code: CODE_B });
  await expectReply(send("GET"), 200, { codes: [CODE_B, CODE_A], next: null });
});

/** Rows the list can return as they are; only an upload has to be a real code. */
function storeRows(count) {
  const insert = env.DB.raw.prepare("INSERT INTO levels (code, level_key) VALUES (?, ?)");
  for (let i = 1; i <= count; i++) {
    insert.run(`code${i}`, String(i));
  }
}

const newestFirst = (from, count) => Array.from({ length: count }, (_, i) => `code${from - i}`);

test("the list comes in pages that add up to every level exactly once", async () => {
  const total = PAGE_SIZE * 2 + 3;
  storeRows(total);
  await expectReply(send("GET"), 200, { codes: newestFirst(total, PAGE_SIZE), next: total - PAGE_SIZE + 1 });
  await expectReply(send("GET", undefined, `/api/levels?before=${total - PAGE_SIZE + 1}`), 200, {
    codes: newestFirst(total - PAGE_SIZE, PAGE_SIZE),
    next: 4
  });
  await expectReply(send("GET", undefined, "/api/levels?before=4"), 200, { codes: newestFirst(3, 3), next: null });
});

test("a list of exactly one page has no page after it", async () => {
  storeRows(PAGE_SIZE);
  await expectReply(send("GET"), 200, { codes: newestFirst(PAGE_SIZE, PAGE_SIZE), next: null });
});

test("an upload while someone scrolls does not move the pages they have yet to load", async () => {
  storeRows(PAGE_SIZE + 2);
  const { next } = await (await send("GET")).json();
  await send("POST", CODE_A);
  await expectReply(send("GET", undefined, `/api/levels?before=${next}`), 200, {
    codes: newestFirst(2, 2),
    next: null
  });
});

test("a nonsense cursor gives the first page", async () => {
  storeRows(1);
  for (const before of ["hello", "-5", "1.5", "1e999"]) {
    await expectReply(send("GET", undefined, `/api/levels?before=${before}`), 200, { codes: ["code1"], next: null });
  }
});

test("the same level again stays out, and the first upload keeps its name", async () => {
  await send("POST", CODE_A);
  await expectReply(send("POST", CODE_A_RENAMED), 409, { error: "Duplicate", code: CODE_A });
  await expectReply(send("POST", CODE_A), 409, { error: "Duplicate", code: CODE_A });
  await expectReply(send("GET"), 200, { codes: [CODE_A], next: null });
});

test("the server does its own check and stores nothing bad", async () => {
  await expectReply(send("POST", "hello letters"), 400, { error: "NotACode" });
  await expectReply(send("POST", CODE_A.slice(0, -7)), 400, { error: "Damaged" });
  await expectReply(send("POST", makeCode(makeLevel({ version: 2 }))), 400, { error: "UnknownVersion" });
  await expectReply(send("POST", CODE_A + " ".repeat(MAX_BODY_LENGTH)), 400, { error: "TooBig" });
  await expectReply(send("GET"), 200, { codes: [], next: null });
});

test("too many uploads are refused before anything is stored", async () => {
  env.UPLOADS.limit = async () => ({ success: false });
  await expectReply(send("POST", CODE_A), 429, { error: "TooFast" });
  await expectReply(send("GET"), 200, { codes: [], next: null });
});

test("a full page refuses new levels but still recognises known ones", async () => {
  await send("POST", CODE_A);
  const insert = env.DB.raw.prepare("INSERT INTO levels (code, level_key) VALUES ('x', ?)");
  for (let i = 1; i < MAX_LEVELS; i++) {
    insert.run(String(i));
  }

  await expectReply(send("POST", CODE_B), 503, { error: "Full" });
  await expectReply(send("POST", CODE_A_RENAMED), 409, { error: "Duplicate", code: CODE_A });
});

test("emptying a full page makes room again", async () => {
  storeRows(MAX_LEVELS);
  await expectReply(send("POST", CODE_B), 503, { error: "Full" });
  env.DB.raw.exec("DELETE FROM levels");
  await expectReply(send("POST", CODE_B), 201, { code: CODE_B });
});

test("other addresses and methods are refused", async () => {
  await expectReply(send("GET", undefined, "/api/other"), 404, { error: "NotFound" });
  await expectReply(send("DELETE"), 405, { error: "NotAllowed" });
});
