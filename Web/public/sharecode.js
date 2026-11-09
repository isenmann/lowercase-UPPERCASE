// Level codes in JavaScript, for the browser and the Worker. A port of LevelShareCode.cs and the parts of
// CustomLevelData it needs; the C# code is the reference.

export const PREFIX = "luc1:";
export const PLAIN_PREFIX = "luc0:";
/** CustomLevelData.CurrentVersion: the newest level format this page reads. */
export const CURRENT_VERSION = 1;
/** LevelShareCode.MaxJsonBytes: far more than any level made in the editor. */
export const MAX_JSON_BYTES = 1 << 20;

// The same characters as char.IsWhiteSpace, which LevelShareCode strips: codes pasted from chats break over lines.
const WHITESPACE = /[\t-\r \x85\xA0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]/g;
const BASE64 = /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/;

// What the editor lets a maker type or set (LevelSettings, LevelEditor.MaxWordLength). A code past these was not made
// in the editor, and would only stretch the page's layout.
export const MAX_NAME_LENGTH = 24;
export const MAX_SENTENCE_LENGTH = 40;
export const MAX_TEXT_LENGTH = 40;
export const MIN_SECONDS = 10;
export const MAX_SECONDS = 600;
// Far more than anyone places by hand; it keeps the preview and the Worker's work small.
export const MAX_PIECES = 1000;
// Pixels either way from the page's origin, far beyond any level's length.
export const MAX_COORDINATE = 1000000;
const MAX_NAME_FIELD = 32;

/** The player-facing words for each error. The first four are LevelShareCode's own. */
export const ERROR_MESSAGES = {
  NotACode: "That isn't a level code. Codes start with \"luc\".",
  Damaged: "The code is cut short or has extra characters in it.",
  TooBig: "The code holds far more than a level.",
  UnknownVersion: "This level comes from a newer version of the game than this page knows.",
  InvalidLevel: "This code holds a level the editor couldn't have made.",
  NotFinished: "Not finished yet: reach the exit in a test play from the start, then share the level again."
};

/**
 * Returns { code, level, json } or { error }. `code` is the code without whitespace, `level` the parsed level and
 * `json` the text inside the code, exactly as the game wrote it. Only levels finished by their maker pass: the game
 * marks a level finished when a test play from the start reaches the exit, and any later edit clears the mark.
 */
export async function decodeShareCode(text) {
  const code = String(text ?? "").replace(WHITESPACE, "");
  const packed = code.startsWith(PREFIX);
  if (!packed && !code.startsWith(PLAIN_PREFIX)) {
    return { error: "NotACode" };
  }

  const body = code.slice(PREFIX.length);
  if (!BASE64.test(body)) {
    return { error: "Damaged" };
  }

  const bytes = Uint8Array.from(atob(body), character => character.charCodeAt(0));
  let json;
  try {
    // Not fatal, like Encoding.UTF8.GetString: broken bytes become U+FFFD there too.
    json = new TextDecoder().decode(packed ? await gunzip(bytes) : bytes);
  } catch (error) {
    return { error: error instanceof RangeError ? "TooBig" : "Damaged" };
  }

  if (json.length > MAX_JSON_BYTES) {
    return { error: "TooBig" };
  }

  let level;
  try {
    level = JSON.parse(json);
  } catch {
    return { error: "Damaged" };
  }

  if (level === null || typeof level !== "object" || Array.isArray(level)) {
    return { error: "Damaged" };
  }

  // A missing version reads as the field's default in JsonUtility, which is the current one.
  const version = level.version ?? CURRENT_VERSION;
  if (!Number.isInteger(version)) {
    return { error: "Damaged" };
  }

  if (version < 1 || version > CURRENT_VERSION) {
    return { error: "UnknownVersion" };
  }

  if (!isValid(level)) {
    return { error: "InvalidLevel" };
  }

  if (level.finishedHash !== contentHash(json)) {
    return { error: "NotFinished" };
  }

  return { code, level, json };
}

// Fields may be missing: JsonUtility leaves those at their defaults, as the game does.
const isText = (value, maxLength) => value === undefined || (typeof value === "string" && value.length <= maxLength);
const isNumber = value => value === undefined ||
  (typeof value === "number" && Number.isFinite(value) && Math.abs(value) <= MAX_COORDINATE);
const isFlag = value => value === undefined || typeof value === "boolean";

function isValid(level) {
  const before = level.sentenceBefore ?? "", after = level.sentenceAfter ?? "";
  return isText(level.name, MAX_NAME_LENGTH) &&
    isText(before, MAX_SENTENCE_LENGTH) && isText(after, MAX_SENTENCE_LENGTH) &&
    before.length + 1 + after.length <= MAX_SENTENCE_LENGTH &&
    (level.missingLetter === undefined || level.missingLetter === "a" || level.missingLetter === "A") &&
    (level.seconds === undefined ||
      (typeof level.seconds === "number" && level.seconds >= MIN_SECONDS && level.seconds <= MAX_SECONDS)) &&
    isFlag(level.ceiling) && isNumber(level.startX) && isNumber(level.startY) &&
    typeof level.finishedHash === "string" &&
    Array.isArray(level.pieces) && level.pieces.length <= MAX_PIECES && level.pieces.every(isValidPiece);
}

/** Unknown types pass, as in the game, which skips them so a level from a newer version still opens. */
function isValidPiece(piece) {
  return piece !== null && typeof piece === "object" && !Array.isArray(piece) &&
    typeof piece.type === "string" && piece.type.length <= MAX_NAME_FIELD &&
    isText(piece.text, MAX_TEXT_LENGTH) && isText(piece.kind, MAX_NAME_FIELD) &&
    [piece.x, piece.y, piece.x2, piece.y2, piece.value, piece.delay].every(isNumber) &&
    (piece.count === undefined || (Number.isInteger(piece.count) && piece.count >= 0 && piece.count <= MAX_PIECES)) &&
    isFlag(piece.flip);
}

/**
 * CustomLevelData.ContentHash: FNV-1a, 32 bits, over the level's JSON without its name and finished hash, as eight
 * hex digits. C# walks the string's UTF-16 chars, which is what charCodeAt gives.
 */
export function contentHash(json) {
  const text = blankNameAndHash(json);
  let hash = 2166136261;
  for (let i = 0; i < text.length; i++) {
    hash = Math.imul(hash ^ text.charCodeAt(i), 16777619) >>> 0;
  }

  return hash.toString(16).padStart(8, "0");
}

/** Unzips, giving up with a RangeError once the result outgrows any real level (a small code can unzip to gigabytes). */
async function gunzip(bytes) {
  const reader = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("gzip")).getReader();
  const chunks = [];
  let total = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    total += value.length;
    if (total > MAX_JSON_BYTES) {
      reader.cancel().catch(() => {});
      throw new RangeError("too big");
    }

    chunks.push(value);
  }

  const result = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.length;
  }

  return result;
}

const BLANKED_KEYS = new Set(["name", "finishedHash"]);

/**
 * The level's JSON with the top-level "name" and "finishedHash" strings emptied, and every other character left as
 * the game wrote it. CustomLevelData.ContentHash hashes this same text, so working on the text rather than
 * serialising the level again keeps JsonUtility's spelling of numbers out of the question.
 */
export function blankNameAndHash(json) {
  let result = "";
  let copied = 0;
  let depth = 0;
  let i = 0;
  while (i < json.length) {
    const character = json[i];
    if (character === "\"") {
      const end = stringEnd(json, i);
      if (depth === 1) {
        let after = skipSpace(json, end);
        if (json[after] === ":" && BLANKED_KEYS.has(JSON.parse(json.slice(i, end)))) {
          after = skipSpace(json, after + 1);
          if (json[after] === "\"") {
            const valueEnd = stringEnd(json, after);
            result += json.slice(copied, after) + "\"\"";
            copied = valueEnd;
            i = valueEnd;
            continue;
          }
        }
      }

      i = end;
      continue;
    }

    if (character === "{" || character === "[") {
      depth++;
    } else if (character === "}" || character === "]") {
      depth--;
    }

    i++;
  }

  return result + json.slice(copied);
}

/** The index just past the string that starts at `start`. */
function stringEnd(text, start) {
  let i = start + 1;
  while (i < text.length) {
    if (text[i] === "\\") {
      i += 2;
    } else if (text[i] === "\"") {
      return i + 1;
    } else {
      i++;
    }
  }

  return text.length;
}

function skipSpace(text, start) {
  let i = start;
  while (i < text.length && " \t\r\n".includes(text[i])) {
    i++;
  }

  return i;
}

/**
 * Two codes with the same key hold the same level, whatever its name, whether it was finished, or how the code was
 * packed. SHA-256 rather than the game's 32-bit hash, which would likely collide somewhere among thousands of levels.
 */
export async function levelKey(json) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(blankNameAndHash(json)));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}
