// Runs before every deploy: the high scores must not go live with the placeholder floor times in
// src/run-floors.json, or anyone could report a level finished in a few seconds.
import { readFileSync } from "node:fs";

const floors = JSON.parse(readFileSync(new URL("../src/run-floors.json", import.meta.url), "utf8"));
if (floors.placeholder !== false) {
  console.error("src/run-floors.json still holds placeholder floor times. Measure the levels, fill them in and set \"placeholder\": false.");
  process.exit(1);
}
