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

/**
 * Returns { code, level, json } or { error }. `code` is the code without whitespace, `level` the parsed level and
 * `json` the text inside the code, exactly as the game wrote it.
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

  return { code, level, json };
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
