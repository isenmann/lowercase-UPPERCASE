// Checks the page's JavaScript against what the game's C# code makes of the same codes. test/vectors.json is
// written by the Unity menu item lowercase UPPERCASE > Shared Levels > Write Test Vectors
// (Assets/Editor/SharedLevelVectors.cs); rerun it after changing level codes, CustomLevelData or the builder.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  CURRENT_VERSION, MAX_JSON_BYTES, MAX_NAME_LENGTH, MAX_SECONDS, MAX_SENTENCE_LENGTH, MAX_TEXT_LENGTH, MIN_SECONDS,
  contentHash, decodeShareCode
} from "../public/sharecode.js";

const vectors = JSON.parse(readFileSync(new URL("vectors.json", import.meta.url), "utf8"));

test("the page's limits are the game's", () => {
  assert.deepEqual(
    {
      currentVersion: CURRENT_VERSION, maxJsonBytes: MAX_JSON_BYTES, maxNameLength: MAX_NAME_LENGTH,
      maxSentenceLength: MAX_SENTENCE_LENGTH, maxWordLength: MAX_TEXT_LENGTH, minSeconds: MIN_SECONDS,
      maxSeconds: MAX_SECONDS
    },
    vectors.limits);
});

test("every code reads as it does in the game, finished or not", async () => {
  const seen = new Set();
  for (const vector of vectors.codes) {
    const { json, error } = await decodeShareCode(vector.code);
    if (!vector.gameReads) {
      assert.ok(error, `${vector.label}: the game refuses it, the page took it`);
      seen.add("refused");
      continue;
    }

    assert.ok(json, `${vector.label}: the game reads it, the page says ${error}`);
    assert.equal(contentHash(json), vector.contentHash, `${vector.label}: content hash`);
    assert.equal(error ?? "None", vector.finished ? "None" : "NotFinished", vector.label);
    seen.add(vector.finished ? "finished" : "unfinished");
  }

  // A kind of verdict that never shows up would mean the vectors stopped testing it.
  assert.deepEqual([...seen].sort(), ["finished", "refused", "unfinished"]);
});

test("every built level's code reads, with its pieces in the same order", async () => {
  assert.ok(vectors.layouts.length > 0);
  for (const layout of vectors.layouts) {
    const { level, error } = await decodeShareCode(layout.code);
    assert.equal(error, undefined, layout.label);
    assert.deepEqual(level.pieces.map(piece => [piece.type, piece.kind]),
      layout.pieces.map(piece => [piece.type, piece.kind]), layout.label);
  }
});

test("the layouts cover every piece type and enemy kind", () => {
  const every = vectors.layouts.find(layout => layout.label === "every piece");
  const types = new Set(every.pieces.map(piece => piece.type === "enemy" ? `enemy ${piece.kind}` : piece.type));
  for (const type of ["word", "kerned", "heavy", "ink", "brick", "stickyWall", "tube", "spring", "door", "plate", "lift", "hooks",
    "capsLock", "capsKey", "wind", "checkpoint", "power", "hash", "exit", "hill", "cloud"]) {
    assert.ok(types.has(type), type);
  }

  for (const kind of ["walker", "spike", "armour", "hopper", "flier", "launcher", "drip", "spark", "fireBar", "eye",
    "curl", "swapper", "mimic", "cursor"]) {
    assert.ok(types.has(`enemy ${kind}`), kind);
  }

  for (const piece of every.pieces) {
    assert.ok(piece.parts.length > 0, `${piece.index} ${piece.type} ${piece.kind} drew nothing`);
  }
});
