import { gzipSync } from "node:zlib";

/** A small level with the fields in the order JsonUtility writes them. */
export function makeLevel(changes = {}) {
  return {
    version: 1,
    name: "Test",
    sentenceBefore: "the c",
    missingLetter: "a",
    sentenceAfter: "t",
    seconds: 60.0,
    ceiling: false,
    startX: 110.0,
    startY: 520.0,
    pieces: [
      { type: "word", x: 40, y: 520, text: "a page of my own", kind: "solid", x2: 0, y2: 0, count: 0, value: 0, delay: 0, flip: false },
      { type: "exit", x: 600, y: 520, text: "c_t", kind: "a", x2: 0, y2: 0, count: 0, value: 0, delay: 0, flip: false }
    ],
    finishedHash: "",
    ...changes
  };
}

/** A code the way LevelShareCode.Encode makes it: gzipped, or plain with `plain`. Takes a level or its JSON. */
export function makeCode(level = makeLevel(), { plain = false } = {}) {
  const json = typeof level === "string" ? level : JSON.stringify(level);
  const bytes = plain ? Buffer.from(json, "utf8") : gzipSync(json);
  return (plain ? "luc0:" : "luc1:") + bytes.toString("base64");
}
