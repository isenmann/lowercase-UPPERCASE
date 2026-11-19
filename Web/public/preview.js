// Level previews: where every piece of a level stands and what it draws, worked out the way the game builds it, and
// an SVG of the result. A port of PrototypeBuilder.BuildCustomLevel and the builders it calls, as a frozen level
// looks in the game: enemies stand still where they start and nothing has moved yet. test/vectors.json holds what the
// game itself built for a few levels, and the tests check this file against it.
//
// Positions are page pixels: a 1280 x 720 page, x to the right, y downwards, the ground surface at 520. Text is laid
// out from character measurements per pixel of font size (`metrics`): TextMesh Pro's own in the tests, and the
// browser's from the same fonts on the page.

export const PAGE_HEIGHT = 720;
const PIXELS_PER_UNIT = 72;
const GROUND = 520;

export const FONTS = {
  mono: "SpaceMono-Regular",
  monoBold: "SpaceMono-Bold",
  black: "ArchivoBlack-Regular"
};

/** Font lines per pixel of font size, upwards from the baseline, as TextMesh Pro reads them. */
export const FACES = {
  "SpaceMono-Regular": { ascentLine: 1.12, descentLine: -0.361, meanLine: 0.5, lineHeight: 1.481 },
  "SpaceMono-Bold": { ascentLine: 1.12, descentLine: -0.361, meanLine: 0.5, lineHeight: 1.481 },
  "ArchivoBlack-Regular": { ascentLine: 0.878, descentLine: -0.21, meanLine: 0.5333, lineHeight: 1.088 }
};

/** How the page draws each font: CSS family and weight. */
export const FONT_FACES = {
  "SpaceMono-Regular": { family: "Space Mono", weight: 400 },
  "SpaceMono-Bold": { family: "Space Mono", weight: 700 },
  "ArchivoBlack-Regular": { family: "Archivo Black", weight: 400 }
};

/** The game's colours, as ColorUtility writes them: #RRGGBB, or #RRGGBBAA when see-through. */
export const COLOURS = {
  background: "#111318",
  ruledLine: "#1A1D24",
  paper: "#F2F0EA",
  muted: "#9AA0AE",
  player: "#FFD23F",
  interactive: "#5CD3F5",
  danger: "#FF4D5E",
  breakable: "#6B7180",
  hot: "#FF8A3D",
  scenery: "#F2F0EB1A",
  liftMark: "#5CD3F52E",
  floatRipple: "#5CD3F559",
  floatEnd: "#5CD3F51A",
  windMark: "#5CD3F526",
  inkTint: "#4B5BD614",
  inkSurface: "#7B8BFF80",
  capsTint: "#FF8A3D08",
  capsLabel: "#FF8A3D4C"
};

// Kinds of word block, for what finds what: enemies stand on blocks, plates open doors, sparks leap from hot words.
const HEAVY = "heavy", HEAVY_FONT_PX = 108;
const SOLID = "solid", HOT = "hot", DOOR = "door", UP = "up", COMMA = "comma";
// Word blocks fitted to their ink, as WordBlock.IsPunctuation says, rather than to the font's lines.
const PUNCTUATION = new Set(["spring", COMMA, "hyphen", "period", "power"]);

const RISER_MARK = "¡";
const FLOAT_RISE_PX = 270;
const DEFAULT_WIND_PX = 600;
const DEFAULT_INK_WIDTH_PX = 400, DEFAULT_INK_DEPTH_PX = 150, MIN_INK_DEPTH_PX = 48;
const INK_REACH_PX = 8, INK_SURFACE_DROP_PX = 12, INK_TILDE_PX = 40 * 0.6;
const WIND_TOP_PX = 64;
const WIND_FULL_MARKS_BOTTOM_PX = 544;
const WIND_ROW_PX = 96;
const WIND_MARK_SPACING_PX = 160;
const WIND_BOX_ROW_PX = 48;
const WIND_BOX_MARK_SPACING_PX = 80;
const WIND_MARK_DROP_PX = 10;
const WIND_MARK_WIDTH_PX = 22;
const MIN_WIND_BOX_PX = 96;
const BRICK_LETTERS = "b\nr\ni\nc\nk\nb\nr\ni\nc\nk";
const STICKY_LETTERS = "s\nt\ni\nc\nk\ny";
const DOOR_LETTERS = "d\no\no\nr\nd\no\no\nr";
const COLUMN_LINE_SPACING = -63;
const TUBE_FONT_PX = 48;
const TUBE_LINE_SPACING = -85;
const LAUNCHER_LINE_SPACING = -88;
const KERNING_STEP_PX = 82;
const HOOK_SPACING_PX = 240;
const CEILING_BASELINE = 210;
const CUSTOM_CEILING_OVERHANG_PX = 400;
const COLUMN_WIDTH_PX = 46 * 0.6;
const GOAL_LINE_PX = 0.04 * PIXELS_PER_UNIT;

// The glyph, size and colour of each enemy that is one glyph standing somewhere.
const ENEMIES = {
  walker: ["&", 48], spike: ["^", 72], armour: ["§", 64], hopper: ["!", 56], curl: ["@", 56],
  swapper: ["↕", 64], mimic: ["b", 56], spark: ["°", 72], flier: ["~", 72], drip: ["'", 72],
  eye: ["ö", 64], cursor: ["|", 260]
};
// Enemies that stand on the word under them, and are left out when there is none.
const GROUNDED = new Set(["walker", "spike", "armour", "swapper", "mimic", "curl", "hopper"]);

// ---------------------------------------------------------------- measuring text

const stripTags = text => text.replace(/<[^>]*>/g, "");

/** Measurements of a character the metrics lack: a plain letter's box. */
const FALLBACK = { advance: 0.612, visible: true, inkLeft: 0.05, inkRight: 0.56, inkBottom: 0, inkTop: 0.52 };

function character(metrics, font, c) {
  return metrics.character(font, c) ?? (c.trim() === "" ? { ...FALLBACK, visible: false } : FALLBACK);
}

const lineAdvance = (font, size, lineSpacing) => (FACES[font].lineHeight + lineSpacing / 100) * size;

/** How far each character of a line starts from the line's start, with kerning; the last entry is the line's end. */
function offsets(metrics, font, size, line) {
  const result = [0];
  let x = 0;
  for (let i = 0; i < line.length; i++) {
    x += character(metrics, font, line[i]).advance * size;
    if (i + 1 < line.length) {
      x += (metrics.kerning?.(font, line[i], line[i + 1]) ?? 0) * size;
    }

    result.push(x);
  }

  return result;
}

/** The width of a line in pixels: its advances, leaving out trailing whitespace as TextMesh Pro does. */
function lineWidth(metrics, font, size, line) {
  return offsets(metrics, font, size, line.trimEnd()).at(-1);
}

/**
 * TextMesh Pro's preferred width, which words and the exit are spaced by. It rounds up to the next hundredth of a unit
 * and adds one more: "c" at 72 px is 44.64 px wide, not 44.06.
 */
export function preferredWidth(metrics, font, size, text) {
  let widest = 0;
  for (const line of stripTags(text).split("\n")) {
    const units = lineWidth(metrics, font, size, line) / PIXELS_PER_UNIT;
    widest = Math.max(widest, Math.floor(units * 100 + 1) / 100 * PIXELS_PER_UNIT);
  }

  return widest;
}

/**
 * The tight box around the visible glyphs, from the origin: x to the right, y upwards from the first baseline. With
 * "center" the lines are centred on the origin, as the enemies' glyphs are. Null when nothing shows.
 */
function inkBox(metrics, font, size, text, lineSpacing = 0, centred = false) {
  let box = null;
  stripTags(text).split("\n").forEach((line, row) => {
    const start = centred ? -lineWidth(metrics, font, size, line) / 2 : 0;
    const at = offsets(metrics, font, size, line);
    const baseline = -row * lineAdvance(font, size, lineSpacing);
    for (let i = 0; i < line.length; i++) {
      const glyph = character(metrics, font, line[i]);
      const x = start + at[i];
      if (glyph.visible) {
        const left = x + glyph.inkLeft * size, right = x + glyph.inkRight * size;
        const bottom = baseline + glyph.inkBottom * size, top = baseline + glyph.inkTop * size;
        box = box === null ? { left, right, bottom, top } : {
          left: Math.min(box.left, left), right: Math.max(box.right, right),
          bottom: Math.min(box.bottom, bottom), top: Math.max(box.top, top)
        };
      }
    }
  });
  return box;
}

// ---------------------------------------------------------------- what gets drawn

/**
 * Text, standing on its first baseline at (x, y). `align` is "left" (x is the left edge) or "center" (the lines are
 * centred on x). Its box is TextMesh Pro's textBounds: the advances across, the font's ascent and descent up and down.
 */
function textPart(metrics, { text, font, size, color, x, y, order = 0, align = "left", lineSpacing = 0, italic = false }) {
  const lines = stripTags(text).split("\n");
  const widths = lines.map(line => lineWidth(metrics, font, size, line));
  const widest = Math.max(...widths);
  const left = align === "center" ? x - widest / 2 : x;
  const lastBaseline = y + (lines.length - 1) * lineAdvance(font, size, lineSpacing);
  return {
    shape: "text", text, font, sizePx: size, color, italic, align, lineSpacing, order, xPx: x, yPx: y,
    left, right: left + widest, top: y - FACES[font].ascentLine * size, bottom: lastBaseline - FACES[font].descentLine * size
  };
}

function rectPart({ cx, cy, width, height, color, order }) {
  return {
    shape: "rect", color, order, xPx: cx, yPx: cy,
    left: cx - width / 2, right: cx + width / 2, top: cy - height / 2, bottom: cy + height / 2
  };
}

// ---------------------------------------------------------------- building a level

const number = value => typeof value === "number" && Number.isFinite(value) ? value : 0;
const string = value => typeof value === "string" ? value : "";
const whole = value => Number.isInteger(value) ? value : 0;

/**
 * Lays a level out as the game builds it. Returns { pieces, others, blocks, links, start, rightPx }: `pieces[i]`
 * holds the parts that level.pieces[i] draws (none for a piece the game skips), `others` the ceiling, `start` the
 * letter, `links` the plate-to-door and key-to-lock lines the editor draws, and `rightPx` the furthest right edge.
 */
export function layoutLevel(level, metrics) {
  const pieces = (Array.isArray(level.pieces) ? level.pieces : []).map((piece, index) => ({
    index,
    type: string(piece?.type),
    kind: string(piece?.kind),
    parts: []
  }));
  const builder = new Builder(metrics);

  // Terrain first, because plates, keys and enemies find what they belong to among it. The order within each phase
  // is by x, as in the game, and it decides which of two equally near words wins.
  const placed = (Array.isArray(level.pieces) ? level.pieces : [])
    .map((piece, index) => ({ piece, index }))
    .filter(({ piece }) => piece !== null && typeof piece === "object")
    .sort((a, b) => number(a.piece.x) - number(b.piece.x));
  const late = ({ piece }) => ["plate", "capsKey", "enemy"].includes(piece.type);

  let right = 1280;
  for (const { piece, index } of placed.filter(entry => !late(entry))) {
    right = Math.max(right, builder.build(piece, pieces[index].parts));
  }

  const others = [];
  if (level.ceiling === true) {
    builder.ceiling(right + CUSTOM_CEILING_OVERHANG_PX, others);
  }

  for (const { piece, index } of placed.filter(late)) {
    right = Math.max(right, builder.build(piece, pieces[index].parts));
  }

  const startX = typeof level.startX === "number" ? level.startX : 110;
  const startY = typeof level.startY === "number" ? level.startY : GROUND;
  const start = [];
  builder.glyphAt(start, startX, startY, "a", 56, FONTS.monoBold, COLOURS.player, 10);
  return { pieces, others, blocks: builder.blocks, links: builder.links, start, rightPx: right };
}

class Builder {
  constructor(metrics) {
    this.metrics = metrics;
    /** Word blocks in the order they were built, with their colliders in page pixels. */
    this.blocks = [];
    this.zones = [];
    this.links = [];
  }

  /** Builds one piece into `parts` and returns its right edge, as BuildPiece does. */
  build(piece, parts) {
    const x = number(piece.x), y = number(piece.y), text = string(piece.text);
    const count = fallback => whole(piece.count) > 0 ? piece.count : fallback;
    const value = fallback => number(piece.value) !== 0 ? piece.value : fallback;
    switch (piece.type) {
      case "word":
        return text.trim().length === 0 ? x : this.customWord(parts, text, x, y, string(piece.kind), number(piece.y2)).right;
      case "brick":
        this.column(parts, columnLetters(text, BRICK_LETTERS), x, y, 46, "cracked", COLOURS.breakable);
        return x + COLUMN_WIDTH_PX;
      case "stickyWall":
        return this.column(parts, columnLetters(text, STICKY_LETTERS), x, y, 72, "sticky", COLOURS.interactive).right;
      case "checkpoint":
        parts.push(this.text({ text: ";", font: FONTS.monoBold, size: 72, color: COLOURS.muted, x, y: y - 14, order: -1 }));
        return x + 44;
      case "power":
        this.topped(parts, "[<color=#FFD23F>*</color>]", x, y, 72, FONTS.monoBold, COLOURS.interactive, "power");
        return x + 130;
      case "hash":
        this.hash(parts, x, y);
        return x + 26;
      case "exit":
        return this.exit(parts, text, x, y, string(piece.kind));
      case "tube": {
        const word = text.trim().length > 0 ? text.trim() : "tube";
        this.stacked(parts, word, x, count(3), y);
        return x + Math.max(4, word.length) * TUBE_FONT_PX * 0.6;
      }
      case "kerned":
        return this.kerned(parts, text, x, y);
      case "spring":
        this.topped(parts, "!", x - 84 * 0.3, y, 84, FONTS.monoBold, COLOURS.interactive, "spring", value(80));
        return x + 40;
      case "door":
        return this.column(parts, DOOR_LETTERS, x, y, 72, DOOR, COLOURS.hot).right;
      case "plate": {
        const plate = this.topped(parts, ".", x - 96 * 0.306, y, 96, FONTS.monoBold, COLOURS.interactive, "period", 70);
        const door = this.nearest(x, y, block => block.kind === DOOR);
        if (door) {
          this.links.push({ from: centre(plate), to: centre(door), color: COLOURS.interactive });
        }

        return x + 40;
      }
      case "lift": {
        const top = number(piece.y2) !== 0 ? piece.y2 : y - 300;
        return this.lift(parts, x, top, y);
      }
      case "hooks": {
        const hooks = count(1);
        for (let i = 0; i < hooks; i++) {
          this.hook(parts, x + i * HOOK_SPACING_PX, y);
        }

        return x + (hooks - 1) * HOOK_SPACING_PX + 60;
      }
      case "capsLock": {
        const x2 = number(piece.x2);
        this.capsLock(parts, x, x2 > x ? x2 : x + 800);
        return Math.max(x, x2);
      }
      case "heavy": {
        if (text.trim().length === 0) {
          return x;
        }

        return this.standing(parts, text, x, y, HEAVY_FONT_PX, FONTS.black, COLOURS.interactive, HEAVY, 0).right;
      }
      case "capsKey":
        this.capsKey(parts, x, y);
        return x + 90;
      case "ink": {
        const x2 = number(piece.x2), y2 = number(piece.y2);
        const right = x2 > x ? x2 : x + DEFAULT_INK_WIDTH_PX;
        const surface = y2 !== 0 ? Math.min(y2, y - MIN_INK_DEPTH_PX) : y - DEFAULT_INK_DEPTH_PX;
        this.inkPool(parts, x, right, surface, y);
        return right;
      }
      case "wind": {
        const x2 = number(piece.x2);
        const right = x2 > x ? x2 : x + DEFAULT_WIND_PX;
        // y2 is the top of a box whose bottom is y; 0 blows the whole height of the page.
        const y2 = number(piece.y2);
        if (y2 !== 0) {
          this.wind(parts, x, right, piece.flip === true, Math.min(y2, y - MIN_WIND_BOX_PX), y, true);
        } else {
          this.wind(parts, x, right, piece.flip === true, WIND_TOP_PX, WIND_FULL_MARKS_BOTTOM_PX, false);
        }

        return right;
      }
      case "hill":
        this.hill(parts, x, count(3));
        return x;
      case "cloud":
        parts.push(this.text({ text: "cloud", font: FONTS.mono, size: 40, color: COLOURS.scenery, x, y, order: -5 }));
        return x;
      case "enemy":
        return this.enemy(parts, piece, x, y, count, value);
      default:
        return x; // a newer game's piece: skipped, as the game does
    }
  }

  text(options) {
    return textPart(this.metrics, options);
  }

  /** A word block: text that can be stood on, with its collider worked out the way WordBlock.FitCollider does. */
  block(parts, { text, font, size, color, x, y, kind, lineSpacing = 0, italic = false, padWidth = 0, padHeight = 0 }) {
    const part = this.text({ text, font, size, color, x, y, lineSpacing, italic });
    parts.push(part);
    const box = this.collider(text, font, size, lineSpacing, kind, padWidth, padHeight);
    const block = {
      kind, trigger: kind === COMMA || kind === UP, part,
      left: x + box.left, right: x + box.right, top: y - box.top, bottom: y - box.bottom
    };
    this.blocks.push(block);
    return block;
  }

  /** The collider in the text's own space (y upwards from the first baseline), before the block is moved. */
  collider(text, font, size, lineSpacing, kind, padWidth, padHeight) {
    if (stripTags(text).includes("\n") || PUNCTUATION.has(kind)) {
      const box = inkBox(this.metrics, font, size, text, lineSpacing) ?? { left: 0, right: 0, bottom: 0, top: 0 };
      if (padWidth > 0) {
        const middle = (box.left + box.right) / 2;
        box.left = middle - padWidth / 2;
        box.right = middle + padWidth / 2;
      }

      if (padHeight > 0) {
        const middle = (box.bottom + box.top) / 2;
        box.bottom = middle - padHeight / 2;
        box.top = middle + padHeight / 2;
      }

      return box;
    }

    // A heavy word stands on its descenders, so the tail of a y rests on the floor, not in it.
    const bottom = kind === HEAVY ? FACES[font].descentLine * size : 0;
    return { left: 0, right: preferredWidth(this.metrics, font, size, text), bottom, top: FACES[font].meanLine * size };
  }

  /** A block moved up or down until the top of its collider is at `surface` (MoveTopTo). */
  topped(parts, text, x, surface, size, font, color, kind, padWidth = 0, italic = false) {
    const box = this.collider(text, font, size, 0, kind, padWidth, 0);
    return this.block(parts, { text, font, size, color, x, y: surface + box.top, kind, italic, padWidth });
  }

  /** A block moved until the bottom of its collider is at `bottom`, as columns, tubes and launchers stand. */
  standing(parts, text, x, bottom, size, font, color, kind, lineSpacing) {
    const box = this.collider(text, font, size, lineSpacing, kind, 0, 0);
    return this.block(parts, { text, font, size, color, x, y: bottom + box.bottom, kind, lineSpacing });
  }

  customWord(parts, text, x, surface, kind, otherEnd) {
    switch (kind) {
      case "float": return this.floatWord(parts, text, x, surface, otherEnd !== 0 ? otherEnd : surface - FLOAT_RISE_PX);
      case "ice": return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.interactive, "ice", 0, true);
      case "hot": return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.hot, HOT);
      case "sticky": return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.interactive, "sticky");
      case "plank": return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.interactive, "hyphen");
      case "cracked": return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.breakable, "cracked");
      default: return this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.paper, SOLID);
    }
  }

  /** A floating word at rest, with the ripple under it and a ghost of the ripple at each end of its travel (FloatWord). */
  floatWord(parts, text, x, surface, otherEnd) {
    const block = this.topped(parts, text, x, surface, 72, FONTS.mono, COLOURS.interactive, "float");
    const tildes = "~".repeat(Math.min(36, Math.max(3, Math.round(text.trim().length * 1.8))));
    const ripple = block.part.yPx + 26;
    parts.push(this.text({ text: tildes, font: FONTS.mono, size: 40, color: COLOURS.floatRipple, x, y: ripple }));
    for (const end of [ripple, ripple + otherEnd - surface]) {
      parts.push(this.text({ text: tildes, font: FONTS.mono, size: 40, color: COLOURS.floatEnd, x, y: end, order: -2 }));
    }

    return block;
  }

  column(parts, letters, x, bottom, size, kind, color) {
    return this.standing(parts, letters, x, bottom, size, FONTS.mono, color, kind, COLUMN_LINE_SPACING);
  }

  stacked(parts, word, x, rows, bottom) {
    const content = Array(rows).fill(word).join("\n");
    this.standing(parts, content, x, bottom, TUBE_FONT_PX, FONTS.mono, COLOURS.muted, SOLID, TUBE_LINE_SPACING);
  }

  /** Loose letters, with a riser in every hole marked by an inverted exclamation mark. */
  kerned(parts, text, x, surface) {
    const holes = [];
    let letters = "";
    for (const c of text.replace(/ /g, "").split("")) {
      if (c === RISER_MARK) {
        if (letters.length > 0) {
          holes.push(letters.length - 1);
        }
      } else {
        letters += c;
      }
    }

    if (letters.length === 0) {
      return x;
    }

    const blocks = letters.split("").map((letter, i) =>
      this.topped(parts, letter, x + i * KERNING_STEP_PX, surface, 72, FONTS.mono, COLOURS.paper, SOLID));
    const used = [...new Set(holes.filter(hole => hole < letters.length - 1))];
    for (const hole of used) {
      const middle = (blocks[hole].right + blocks[hole + 1].left) / 2;
      this.glyphAt(parts, middle, blocks[hole].top, RISER_MARK, 64, FONTS.monoBold, COLOURS.danger);
    }

    return blocks.at(-1).right;
  }

  hash(parts, x, y) {
    // The glyph is moved until its ink is centred on the point (Collectible.CentreOnInk).
    const ink = inkBox(this.metrics, FONTS.monoBold, 58, "#", 0, true);
    const middleX = ink ? (ink.left + ink.right) / 2 : 0, middleY = ink ? (ink.bottom + ink.top) / 2 : 0;
    parts.push(this.text({
      text: "#", font: FONTS.monoBold, size: 58, color: COLOURS.player, x: x - middleX, y: y + middleY, order: 5,
      align: "center"
    }));
  }

  exit(parts, text, x, baseline, kind) {
    const blank = text.indexOf("_");
    const before = blank < 0 ? text : text.slice(0, blank);
    const after = blank < 0 ? "" : text.slice(blank + 1);
    const upper = kind === "A";
    const font = upper ? FONTS.black : FONTS.mono;
    const size = upper ? 88 : 72;
    const blankWidth = upper ? 84 : 40;
    const blankHeight = upper ? 90 : 52;

    const add = (content, left) => {
      if (content.length > 0) {
        parts.push(this.text({ text: content, font, size, color: COLOURS.paper, x: left, y: baseline }));
      }
    };
    add(before, x);
    const blankLeft = x + preferredWidth(this.metrics, font, size, before) + 6;
    const top = baseline - blankHeight;
    const cx = blankLeft + blankWidth / 2, cy = top + blankHeight / 2;
    for (const [rx, ry, width, height] of [
      [cx, top, blankWidth, GOAL_LINE_PX], [cx, top + blankHeight, blankWidth, GOAL_LINE_PX],
      [blankLeft, cy, GOAL_LINE_PX, blankHeight], [blankLeft + blankWidth, cy, GOAL_LINE_PX, blankHeight]
    ]) {
      parts.push(rectPart({ cx: rx, cy: ry, width, height, color: COLOURS.player, order: 1 }));
    }

    add(after, blankLeft + blankWidth + 6);
    const charPx = upper ? 88 * 0.6 : 72 * 0.6;
    return x + (before.length + after.length) * charPx + (upper ? 96 : 52);
  }

  lift(parts, x, top, from) {
    const bottom = from + 65;
    this.block(parts, { text: "up", font: FONTS.mono, size: 72, color: COLOURS.interactive, x: x + 20, y: bottom, kind: UP });
    for (let row = bottom - 85; row > top + 40; row -= 80) {
      parts.push(this.text({ text: "up", font: FONTS.mono, size: 72, color: COLOURS.liftMark, x: x + 20, y: row }));
    }

    return x + 150;
  }

  hook(parts, cx, cy) {
    const box = this.collider(",", FONTS.monoBold, 96, 0, COMMA, 110, 110);
    const middleX = (box.left + box.right) / 2, middleY = (box.bottom + box.top) / 2;
    this.block(parts, {
      text: ",", font: FONTS.monoBold, size: 96, color: COLOURS.interactive, x: cx - middleX, y: cy + middleY,
      kind: COMMA, padWidth: 110, padHeight: 110
    });
  }

  capsLock(parts, left, right) {
    parts.push(rectPart({
      cx: (left + right) / 2, cy: 352, width: right - left, height: 576, color: COLOURS.capsTint, order: -5
    }));
    const label = this.text({ text: "LOCK", font: FONTS.black, size: 96, color: COLOURS.capsLabel, x: left + 20, y: 300, order: -4 });
    parts.push(label);
    this.zones.push({ middle: (left + right) / 2, label });
  }

  /** A pool of ink: its tint, as deep as its trigger, and a ~ line on its surface (InkPool). */
  inkPool(parts, left, right, surface, bottom) {
    const reach = bottom + INK_REACH_PX;
    parts.push(rectPart({
      cx: (left + right) / 2, cy: (surface + reach) / 2, width: right - left, height: reach - surface,
      color: COLOURS.inkTint, order: -5
    }));
    const tildes = "~".repeat(Math.max(1, Math.floor((right - left) / INK_TILDE_PX)));
    parts.push(this.text({
      text: tildes, font: FONTS.mono, size: 40, color: COLOURS.inkSurface, x: left, y: surface + INK_SURFACE_DROP_PX, order: -4
    }));
  }

  /** Wind's marks in staggered rows, spaced to fit its width and height a whole number of times (WindStretch). */
  wind(parts, left, right, blowsLeft, top, bottom, boxed) {
    // A mark's left edge runs over the width less its own, so all of it stays inside; a box is filled more densely.
    const run = Math.max(1, right - left - WIND_MARK_WIDTH_PX);
    const rows = Math.max(1, Math.floor((bottom - top) / (boxed ? WIND_BOX_ROW_PX : WIND_ROW_PX) + 0.5));
    const rowSpacing = (bottom - top) / rows;
    const perRow = Math.max(1, Math.floor(run / (boxed ? WIND_BOX_MARK_SPACING_PX : WIND_MARK_SPACING_PX) + 0.5));
    const spacing = run / perRow;
    const glyph = blowsLeft ? "«" : "»";
    for (let row = 0; row < rows; row++) {
      const y = top + (row + 0.5) * rowSpacing + WIND_MARK_DROP_PX;
      for (let i = 0; i < perRow; i++) {
        const x = left + (i + (row % 2 === 0 ? 0.25 : 0.75)) * spacing;
        parts.push(this.text({ text: glyph, font: FONTS.mono, size: 40, color: COLOURS.windMark, x, y, order: -3 }));
      }
    }
  }

  capsKey(parts, x, baseline) {
    const key = this.text({ text: "key", font: FONTS.monoBold, size: 40, color: COLOURS.player, x, y: baseline, order: 5 });
    parts.push(key);
    let nearest = null;
    for (const zone of this.zones) {
      if (nearest === null || Math.abs(zone.middle - x) < Math.abs(nearest.middle - x)) {
        nearest = zone;
      }
    }

    if (nearest) {
      this.links.push({ from: centre(key), to: centre(nearest.label), color: COLOURS.hot });
    }
  }

  hill(parts, left, rows) {
    for (let row = 0; row < rows; row++) {
      const content = Array(rows - row).fill("hill").join(" ");
      parts.push(this.text({
        text: content, font: FONTS.mono, size: 36, color: COLOURS.scenery, x: left + row * 5 * 36 * 0.6 * 0.5,
        y: GROUND - 8 - row * 30, order: -5
      }));
    }
  }

  ceiling(right, parts) {
    const word = "ceiling ", left = -60;
    const count = Math.ceil((right - left) / (word.length * 72 * 0.6));
    const content = word.repeat(count).trimEnd();
    this.block(parts, { text: content, font: FONTS.mono, size: 72, color: COLOURS.breakable, x: left, y: CEILING_BASELINE, kind: SOLID });
  }

  enemy(parts, piece, x, y, count, value) {
    const kind = string(piece.kind);
    if (GROUNDED.has(kind)) {
      const ground = this.nearest(x, y, block => block.kind !== DOOR, true);
      if (!ground) {
        return x;
      }

      this.glyphAt(parts, x, ground.top, ENEMIES[kind][0], ENEMIES[kind][1], FONTS.monoBold, COLOURS.danger);
      return x + 40;
    }

    switch (kind) {
      case "spark": {
        const hot = this.nearest(x, y, block => block.kind === HOT, true);
        if (!hot) {
          return x;
        }

        this.glyphAt(parts, x, hot.top, ENEMIES.spark[0], ENEMIES.spark[1], FONTS.monoBold, COLOURS.hot);
        return x + 40;
      }
      case "flier":
      case "drip":
      case "eye":
        this.glyphAt(parts, x, y, ENEMIES[kind][0], ENEMIES[kind][1], FONTS.monoBold, COLOURS.danger);
        return kind === "flier" ? x + Math.max(0, number(piece.value)) + 40 : x + 40;
      case "launcher": {
        const mark = piece.flip === true ? "«" : "»";
        const letters = Array(count(2)).fill(mark).join("\n");
        // Its arrow waits hidden in front of it until the level runs.
        return this.standing(parts, letters, x, y, 72, FONTS.mono, COLOURS.paper, SOLID, LAUNCHER_LINE_SPACING).right;
      }
      case "fireBar": {
        const o = this.topped(parts, "o", x, y, 72, FONTS.mono, COLOURS.hot, HOT);
        const middle = centre(o);
        for (let i = 0; i < count(4); i++) {
          this.glyphAt(parts, middle.x, middle.y, "·", 72, FONTS.monoBold, COLOURS.hot);
        }

        return x + 60;
      }
      case "cursor": {
        const x2 = number(piece.x2);
        const end = x2 > x ? x2 : x + 1500;
        this.glyphAt(parts, x, y + 0.6 * PIXELS_PER_UNIT, ENEMIES.cursor[0], ENEMIES.cursor[1], FONTS.monoBold, COLOURS.danger);
        return end;
      }
      default:
        return x;
    }
  }

  /** A glyph centred on x with the bottom of its ink at `feet`, as an enemy lifts its glyph when it wakes. */
  glyphAt(parts, x, feet, glyph, size, font, color, order = 8) {
    const ink = inkBox(this.metrics, font, size, glyph, 0, true);
    const part = this.text({ text: glyph, font, size, color, x, y: feet + (ink ? ink.bottom : 0), order, align: "center" });
    parts.push(part);
    return part;
  }

  /**
   * The nearest block that matches. Below: only blocks under the point (tops at most 16 px above it), by how far down
   * their top is. Otherwise any, by distance across. Triggers (hooks, lifts) never count.
   */
  nearest(x, y, match, below = false) {
    let best = null, bestDistance = Infinity;
    for (const block of this.blocks) {
      if (block.trigger || !match(block)) {
        continue;
      }

      let distance;
      if (below) {
        if (x < block.left || x > block.right || block.top < y - 16) {
          continue;
        }

        distance = block.top - y;
      } else {
        distance = Math.abs((block.left + block.right) / 2 - x);
      }

      if (distance < bestDistance) {
        bestDistance = distance;
        best = block;
      }
    }

    return best;
  }
}

const centre = box => ({ x: (box.left + box.right) / 2, y: (box.top + box.bottom) / 2 });

/** A column's letters, one per line. Typed as a plain word, it is stood on end. */
function columnLetters(text, fallback) {
  const letters = text.replace(/ /g, "").replace(/\r/g, "");
  if (letters.length === 0) {
    return fallback;
  }

  return letters.includes("\n") ? letters : letters.split("").join("\n");
}

// ---------------------------------------------------------------- measuring in the browser

/**
 * Character measurements from the browser, drawing with the page's fonts at 100 px. The fonts must have loaded
 * first (document.fonts.load), or the browser measures a stand-in. Kerning is what a pair measures beyond its two
 * characters.
 */
export function canvasMetrics(canvas) {
  const context = canvas.getContext("2d");
  const cache = new Map();
  const width = (font, text) => {
    const key = `${font} width ${text}`;
    if (!cache.has(key)) {
      const face = FONT_FACES[font];
      context.font = `${face.weight} 100px "${face.family}"`;
      cache.set(key, context.measureText(text).width / 100);
    }

    return cache.get(key);
  };
  return {
    kerning(font, first, second) {
      const kern = width(font, first + second) - width(font, first) - width(font, second);
      return Math.abs(kern) < 0.0005 ? 0 : kern;
    },
    character(font, c) {
      const key = `${font} ${c}`;
      if (!cache.has(key)) {
        const face = FONT_FACES[font];
        context.font = `${face.weight} 100px "${face.family}"`;
        const measured = context.measureText(c);
        cache.set(key, {
          advance: measured.width / 100,
          visible: c.trim() !== "",
          inkLeft: -measured.actualBoundingBoxLeft / 100,
          inkRight: measured.actualBoundingBoxRight / 100,
          inkBottom: -measured.actualBoundingBoxDescent / 100,
          inkTop: measured.actualBoundingBoxAscent / 100
        });
      }

      return cache.get(key);
    }
  };
}

// ---------------------------------------------------------------- drawing

const escape = text => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
const round = value => Math.round(value * 100) / 100;

/** A colour as SVG takes it: "#RRGGBB" for fill and an opacity, since not every renderer reads #RRGGBBAA. */
function paint(color, attribute = "fill") {
  if (color.length === 9) {
    const opacity = round(parseInt(color.slice(7), 16) / 255);
    return `${attribute}="${color.slice(0, 7)}" ${attribute}-opacity="${opacity}"`;
  }

  return `${attribute}="${color}"`;
}

/** One line of text as tspans, with the colour tags the power block uses turned into fills. */
function spans(line) {
  let result = "";
  const pattern = /<color=(#[0-9A-Fa-f]{6,8})>(.*?)<\/color>/g;
  let last = 0;
  for (const match of line.matchAll(pattern)) {
    result += escape(stripTags(line.slice(last, match.index)));
    result += `<tspan ${paint(match[1].toUpperCase())}>${escape(stripTags(match[2]))}</tspan>`;
    last = match.index + match[0].length;
  }

  return result + escape(stripTags(line.slice(last)));
}

function drawText(part) {
  const face = FONT_FACES[part.font];
  const advance = round(lineAdvance(part.font, part.sizePx, part.lineSpacing));
  const lines = part.text.split("\n");
  const attributes = [
    `x="${round(part.xPx)}"`, `y="${round(part.yPx)}"`, `font-family="${face.family}"`, `font-weight="${face.weight}"`,
    `font-size="${part.sizePx}"`, paint(part.color), `xml:space="preserve"`
  ];
  if (part.align === "center") {
    attributes.push(`text-anchor="middle"`);
  }

  if (part.italic) {
    attributes.push(`font-style="italic"`);
  }

  const body = lines.length === 1 ? spans(lines[0]) : lines
    .map((line, row) => `<tspan x="${round(part.xPx)}" dy="${row === 0 ? 0 : advance}">${spans(line)}</tspan>`).join("");
  return `<text ${attributes.join(" ")}>${body}</text>`;
}

function drawRect(part) {
  return `<rect x="${round(part.left)}" y="${round(part.top)}" width="${round(part.right - part.left)}" ` +
    `height="${round(part.bottom - part.top)}" ${paint(part.color)}/>`;
}

/**
 * The level as an SVG document: the page with its ruled lines, then every part in the game's drawing order, the
 * links between plates and doors, and keys and locks, and the letter at its start. `left` and `width` choose the
 * stretch drawn, in page pixels; the whole level by default.
 */
export function renderSvg(layout, { left = -60, width = layout.rightPx + 200 - left, title = "" } = {}) {
  const parts = [...layout.others, ...layout.pieces.flatMap(piece => piece.parts), ...layout.start]
    .map((part, index) => ({ part, index }))
    .sort((a, b) => a.part.order - b.part.order || a.index - b.index)
    .map(({ part }) => part.shape === "text" ? drawText(part) : drawRect(part));

  const lines = [];
  for (let y = 89; y < PAGE_HEIGHT; y += 90) {
    lines.push(`<rect x="${round(left)}" y="${y}" width="${round(width)}" height="1" fill="${COLOURS.ruledLine}"/>`);
  }

  const links = layout.links.map(({ from, to, color }) =>
    `<line x1="${round(from.x)}" y1="${round(from.y)}" x2="${round(to.x)}" y2="${round(to.y)}" ` +
    `${paint(color, "stroke")} stroke-width="3" stroke-dasharray="10 8"/>`);

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${round(left)} 0 ${round(width)} ${PAGE_HEIGHT}" ` +
    `role="img"${title ? ` aria-label="${escape(title)}"` : ""}>` +
    `<rect x="${round(left)}" y="0" width="${round(width)}" height="${PAGE_HEIGHT}" fill="${COLOURS.background}"/>` +
    lines.join("") + parts.join("") + links.join("") + `</svg>`;
}
