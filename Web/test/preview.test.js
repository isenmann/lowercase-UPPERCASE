// Checks the preview against what the game built for the levels in test/vectors.json (see vectors.test.js).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { FACES, layoutLevel, preferredWidth, renderSvg } from "../public/preview.js";
import { decodeShareCode } from "../public/sharecode.js";

const vectors = JSON.parse(readFileSync(new URL("vectors.json", import.meta.url), "utf8"));

/** TextMesh Pro's own measurements, as the game exported them. */
const metrics = {
  fonts: new Map(vectors.fonts.map(font => [font.name, new Map(font.characters.map(c => [c.character, c]))])),
  kerningPairs: new Map(vectors.fonts.map(font => [font.name, new Map(font.kerning.map(k => [k.pair, k.advance]))])),
  character(font, c) {
    return this.fonts.get(font)?.get(c) ?? null;
  },
  kerning(font, first, second) {
    return this.kerningPairs.get(font)?.get(first + second) ?? 0;
  }
};

// Page pixels. Positions come from the same measurements as the game's, so only rounding is left.
const TOLERANCE = 0.75;

const near = (actual, expected, label) =>
  assert.ok(Math.abs(actual - expected) <= TOLERANCE, `${label}: ${actual} is not near ${expected}`);

test("the font lines are the game's", () => {
  for (const font of vectors.fonts) {
    const { ascentLine, descentLine, meanLine, lineHeight } = font;
    assert.deepEqual(FACES[font.name], { ascentLine, descentLine, meanLine, lineHeight }, font.name);
  }
});

test("preferred widths round up the way TextMesh Pro's do", () => {
  for (const font of vectors.fonts) {
    for (const sample of font.samples) {
      // Measured at 100 px, where Space Mono's widths land exactly on a hundredth and the rounding goes either way.
      const width = preferredWidth(metrics, font.name, 100, sample.text) / 100;
      assert.ok(Math.abs(width - sample.preferred) <= 0.0073, `${font.name} "${sample.text}": ${width} vs ${sample.preferred}`);
    }
  }

  assert.equal(Math.round(preferredWidth(metrics, "SpaceMono-Regular", 72, "c") * 100) / 100, 44.64);
});

/** The parts the game drew, in a form to compare: what and where. */
function compareParts(actual, expected, label) {
  assert.equal(actual.length, expected.length,
    `${label}: ${actual.length} parts, the game drew ${expected.length}: ` +
    JSON.stringify(expected.map(part => part.text || part.name)));
  expected.forEach((part, i) => {
    const mine = actual[i];
    const where = `${label} part ${i} (${part.name})`;
    if (part.shape === "text") {
      assert.equal(mine.shape, "text", where);
      assert.equal(mine.text, part.text, where);
      assert.equal(mine.font, part.font, where);
      assert.equal(mine.sizePx, part.sizePx, where);
      assert.equal(mine.italic, part.italic, where);
      assert.equal(mine.align === "center" ? "Baseline" : "BaselineLeft", part.alignment, where);
    } else {
      assert.equal(mine.shape, "rect", where);
    }

    assert.equal(mine.color, part.color, where);
    assert.equal(mine.order, part.order, where);
    for (const key of ["xPx", "yPx", "left", "top", "right", "bottom"]) {
      near(mine[key], part[key], `${where} ${key}`);
    }
  });
}

for (const vector of vectors.layouts) {
  test(`"${vector.label}" is laid out as the game builds it`, async () => {
    const { level } = await decodeShareCode(vector.code);
    const layout = layoutLevel(level, metrics);
    near(layout.rightPx, vector.rightPx, "right edge");
    compareParts(layout.others, vector.others, "others");
    for (const piece of vector.pieces) {
      compareParts(layout.pieces[piece.index].parts, piece.parts, `piece ${piece.index} ${piece.type} ${piece.kind}`);
    }
  });
}

test("plates link to doors and keys to locks", async () => {
  const every = vectors.layouts.find(vector => vector.label === "every piece");
  const { level } = await decodeShareCode(every.code);
  const { links } = layoutLevel(level, metrics);
  assert.equal(links.length, 2);
});

test("enemies with nothing under them are left out, as in the game", () => {
  const layout = layoutLevel({ pieces: [{ type: "enemy", kind: "walker", x: 100, y: 300 }] }, metrics);
  assert.deepEqual(layout.pieces[0].parts, []);
});

test("characters the measurements lack, and unknown pieces, still lay out", () => {
  const layout = layoutLevel({
    pieces: [
      { type: "word", x: 0, y: 520, text: "中文", kind: "solid" },
      { type: "somethingNew", x: 10, y: 10 },
      null
    ]
  }, metrics);
  assert.equal(layout.pieces[0].parts.length, 1);
  assert.deepEqual(layout.pieces[1].parts, []);
});

test("the SVG draws every part and escapes what the maker typed", async () => {
  const awkward = vectors.layouts.find(vector => vector.label === "awkward text");
  const { level } = await decodeShareCode(awkward.code);
  const layout = layoutLevel(level, metrics);
  const svg = renderSvg(layout, { title: level.name });
  const texts = [...layout.others, ...layout.pieces.flatMap(piece => piece.parts), ...layout.start]
    .filter(part => part.shape === "text");
  assert.equal(svg.match(/<text /g).length, texts.length);
  assert.ok(svg.includes("a &quot;quoted&quot; back\\slash"));
  assert.ok(svg.includes('aria-label="say &quot;hi&quot; \\'));
  assert.ok(!/<(?!\/?(svg|rect|text|tspan|line)\b)/.test(svg), "only SVG elements");
});

test("the power block's colour tag becomes a coloured span", () => {
  const layout = layoutLevel({ pieces: [{ type: "power", x: 0, y: 300 }] }, metrics);
  const svg = renderSvg(layout);
  assert.ok(svg.includes('[<tspan fill="#FFD23F">*</tspan>]'), svg);
});
