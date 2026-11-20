import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { canonical, hashOf, maxPoints } from "../src/runlimits.js";

const limits = JSON.parse(readFileSync(new URL("../src/run-limits.json", import.meta.url), "utf8"));
const floors = JSON.parse(readFileSync(new URL("../src/run-floors.json", import.meta.url), "utf8"));

test("the file's hash is the hash of its levels, as the game takes it", async () => {
  assert.equal(await hashOf(limits), limits.hash);
});

test("the canonical text starts with the format and the points", () => {
  const lines = canonical(limits).split("\n");
  assert.equal(lines[0], "v1");
  assert.equal(lines[1], "100 50 10");
  assert.equal(lines.length, 2 + limits.levels.length);
});

test("the run is fifteen levels, from Overworld to Final", () => {
  assert.equal(limits.levels.length, 15);
  assert.equal(limits.levels[0].scene, "Overworld");
  assert.equal(limits.levels.at(-1).scene, "Final");
});

test("each level's maximum is every enemy, every # and the whole clock", () => {
  for (const level of limits.levels) {
    assert.equal(level.maxPoints, maxPoints(limits, level), level.scene);
  }
});

test("every level has a floor time shorter than its clock, and no others", () => {
  assert.deepEqual(Object.keys(floors.floorSeconds).sort(), limits.levels.map(l => l.scene).sort());
  for (const level of limits.levels) {
    const floor = floors.floorSeconds[level.scene];
    assert.ok(floor > 0 && floor < level.seconds, `${level.scene}: ${floor}`);
  }
});

test("a changed count gives another hash", async () => {
  const changed = structuredClone(limits);
  changed.levels[3].collectibles += 1;
  assert.notEqual(await hashOf(changed), limits.hash);
});
