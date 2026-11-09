import assert from "node:assert/strict";
import { test } from "node:test";
import { gzipSync } from "node:zlib";
import { MAX_JSON_BYTES, blankNameAndHash, decodeShareCode, levelKey } from "../public/sharecode.js";
import { makeCode, makeLevel } from "./make-code.js";

const errorOf = async text => (await decodeShareCode(text)).error ?? "None";

test("gzipped and plain codes both read, and give back the game's own JSON", async () => {
  const json = JSON.stringify(makeLevel(), null, 1);
  for (const plain of [false, true]) {
    const { level, json: inside, error } = await decodeShareCode(makeCode(json, { plain }));
    assert.equal(error, undefined);
    assert.equal(inside, json);
    assert.equal(level.name, "Test");
  }
});

test("a code may hold whitespace, and comes back without it", async () => {
  const code = makeCode();
  const pasted = ` ${code.slice(0, 12)}\n${code.slice(12, 40)}\r\n\t${code.slice(40)} `;
  assert.equal((await decodeShareCode(pasted)).code, code);
});

test("things that are not codes, or are broken ones, say so", async () => {
  const code = makeCode();
  assert.equal(await errorOf(""), "NotACode");
  assert.equal(await errorOf("hello"), "NotACode");
  assert.equal(await errorOf("SR1-AAAA"), "NotACode");
  assert.equal(await errorOf(code.slice(0, -7)), "Damaged");
  assert.equal(await errorOf(code + "!"), "Damaged");
  assert.equal(await errorOf("luc1:" + Buffer.from("not gzip").toString("base64")), "Damaged");
  assert.equal(await errorOf(makeCode("not json", { plain: true })), "Damaged");
  assert.equal(await errorOf(makeCode("[1, 2]")), "Damaged");
  assert.equal(await errorOf(makeCode("null")), "Damaged");
});

test("a code that unzips to far more than a level is refused without unzipping it all", async () => {
  const bomb = "luc1:" + gzipSync(" ".repeat(MAX_JSON_BYTES * 8)).toString("base64");
  assert.equal(await errorOf(bomb), "TooBig");
});

test("levels from a newer game are told apart from broken ones", async () => {
  assert.equal(await errorOf(makeCode(makeLevel({ version: 2 }))), "UnknownVersion");
  assert.equal(await errorOf(makeCode(makeLevel({ version: 0 }))), "UnknownVersion");
  assert.equal(await errorOf(makeCode(makeLevel({ version: "1" }))), "Damaged");
  const { version, ...unversioned } = makeLevel();
  assert.equal(await errorOf(makeCode(unversioned)), "None");
});

test("only the top-level name and finished hash are blanked, and nothing else moves", () => {
  const json = '{"version":1,"name":"say \\"hi\\" \\\\ ü","pieces":[{"name":"kept","text":"name"}],' +
    '"finishedHash" : "1a2b3c4d"}';
  assert.equal(blankNameAndHash(json),
    '{"version":1,"name":"","pieces":[{"name":"kept","text":"name"}],"finishedHash" : ""}');
});

test("the level key ignores name, finished hash and packing, but not the level", async () => {
  const key = async (level, plain = false) => levelKey((await decodeShareCode(makeCode(level, { plain }))).json);
  const plainKey = await key(makeLevel());
  assert.equal(await key(makeLevel({ name: "Copycat", finishedHash: "deadbeef" })), plainKey);
  assert.equal(await key(makeLevel(), true), plainKey);
  assert.notEqual(await key(makeLevel({ startX: 111 })), plainKey);
  assert.notEqual(await key(makeLevel({ missingLetter: "A" })), plainKey);
});
