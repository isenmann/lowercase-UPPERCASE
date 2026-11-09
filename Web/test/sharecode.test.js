import assert from "node:assert/strict";
import { test } from "node:test";
import { gzipSync } from "node:zlib";
import {
  ERROR_MESSAGES, MAX_JSON_BYTES, MAX_PIECES, blankNameAndHash, contentHash, decodeShareCode, levelKey
} from "../public/sharecode.js";
import { levelJson, makeCode, makeLevel } from "./make-code.js";

const errorOf = async text => (await decodeShareCode(text)).error ?? "None";

test("gzipped and plain codes both read, and give back the game's own JSON", async () => {
  const json = levelJson(makeLevel(), { space: 1 });
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

const piece = changes => ({ ...makeLevel().pieces[0], ...changes });

test("levels the editor couldn't have made are refused", async () => {
  const bad = [
    { name: "x".repeat(25) },
    { name: 7 },
    { sentenceBefore: "x".repeat(20), sentenceAfter: "y".repeat(20) },
    { missingLetter: "b" },
    { seconds: 5 },
    { seconds: 601 },
    { seconds: "60" },
    { ceiling: "yes" },
    { startX: 1e7 },
    { startY: null },
    { pieces: {} },
    { pieces: Array.from({ length: MAX_PIECES + 1 }, () => piece()) },
    { pieces: [null] },
    { pieces: [[]] },
    { pieces: [piece({ type: undefined })] },
    { pieces: [piece({ type: 3 })] },
    { pieces: [piece({ text: "x".repeat(41) })] },
    { pieces: [piece({ x: -2e6 })] },
    { pieces: [piece({ value: "80" })] },
    { pieces: [piece({ count: -1 })] },
    { pieces: [piece({ count: 1.5 })] },
    { pieces: [piece({ flip: 1 })] }
  ];
  for (const changes of bad) {
    assert.equal(await errorOf(makeCode(makeLevel(changes))), "InvalidLevel", JSON.stringify(changes).slice(0, 80));
  }

  const { finishedHash, ...unmarked } = makeLevel();
  assert.equal(await errorOf(makeCode(unmarked, { finished: false })), "InvalidLevel");
});

test("the longest of everything the editor allows still passes", async () => {
  const level = makeLevel({
    name: "x".repeat(24),
    sentenceBefore: "b".repeat(20),
    sentenceAfter: "a".repeat(19),
    seconds: 600,
    pieces: [piece({ text: "w".repeat(40) }), ...Array.from({ length: MAX_PIECES - 1 }, () => piece())]
  });
  assert.equal(await errorOf(makeCode(level)), "None");
});

test("missing fields and unknown piece types pass, as the game takes them", async () => {
  const level = makeLevel({ pieces: [{ type: "somethingNew", x: 5 }, { type: "word" }] });
  delete level.seconds;
  delete level.ceiling;
  assert.equal(await errorOf(makeCode(level)), "None");
});

test("only levels their maker finished pass, and renaming keeps the mark", async () => {
  assert.equal(await errorOf(makeCode(makeLevel(), { finished: false })), "NotFinished");

  const finished = JSON.parse(levelJson(makeLevel()));
  assert.equal(await errorOf(makeCode(JSON.stringify({ ...finished, name: "Renamed" }))), "None");
  assert.equal(await errorOf(makeCode(JSON.stringify({ ...finished, seconds: 65 }))), "NotFinished");
  const moved = { ...finished, pieces: [{ ...finished.pieces[0], x: 41 }, finished.pieces[1]] };
  assert.equal(await errorOf(makeCode(JSON.stringify(moved))), "NotFinished");
});

test("the content hash is FNV-1a over UTF-16 chars, as eight hex digits", () => {
  // FNV-1a test values for "" and "a"; "\u00fc" and "\ud83d\ude00" check the chars past ASCII.
  assert.equal(contentHash(""), "811c9dc5");
  assert.equal(contentHash("a"), "e40c292c");
  assert.equal(contentHash('{"name":"x","finishedHash":"y"}'), contentHash('{"name":"","finishedHash":""}'));
  assert.notEqual(contentHash("\u00fc"), contentHash("\u00fd"));
  assert.match(contentHash("\ud83d\ude00"), /^[0-9a-f]{8}$/);
});

test("every error a code can give has words for the player", async () => {
  for (const error of ["NotACode", "Damaged", "TooBig", "UnknownVersion", "InvalidLevel", "NotFinished"]) {
    assert.equal(typeof ERROR_MESSAGES[error], "string", error);
  }
});
