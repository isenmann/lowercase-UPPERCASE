// Names on the world high scores: the game's rules (HighScores.IsAllowedInName, MaxNameLength), checked again
// here, plus a small word filter. A refused name is stored as "???"; the player's own table keeps it.

export const MAX_NAME_LENGTH = 12;
export const UNNAMED = "???";

// char.IsLetterOrDigit, a space or one of -_.!?
const ALLOWED = /^[\p{L}\p{Nd} \-_.!?]+$/u;

// Found anywhere in the name once it is squeezed (see squeeze), so "xFUCKx" and "f.u.c.k" are caught too.
const ANYWHERE = [
  "fuck", "shit", "cunt", "nigg", "faggot", "retard", "whore", "slut", "bitch", "pussy", "penis", "vagina",
  "dildo", "hitler", "porn", "wank", "twat", "asshole", "kkk",
  "arschloch", "fotze", "wichser", "schwuchtel", "neger", "hurensohn", "schlampe", "nutte", "kanake", "judensau",
  "siegheil", "missgeburt", "spast", "ficken"
];

// Only as a whole word, because they hide inside harmless ones (class, document, grape, heilig).
const WHOLE_WORD = ["ass", "cum", "fag", "dick", "cock", "tits", "rape", "nazi", "hure", "fick", "heil", "sex"];

const LOOKALIKES = { 0: "o", 1: "i", 3: "e", 4: "a", 5: "s", 7: "t", 8: "b", 9: "g", "!": "i" };

/** Lower case, look-alike digits as letters, accents dropped. */
function plain(text) {
  return [...text.normalize("NFKD").replace(/\p{M}/gu, "").toLowerCase()].map(c => LOOKALIKES[c] ?? c).join("");
}

/** Only the letters, run together. */
const squeeze = text => plain(text).replace(/[^\p{L}]/gu, "");

export function isRude(name) {
  const squeezed = squeeze(name);
  if (ANYWHERE.some(word => squeezed.includes(word))) return true;
  const words = plain(name).split(/[^\p{L}]+/u);
  return words.some(word => WHOLE_WORD.includes(word)) || WHOLE_WORD.includes(squeezed);
}

/**
 * The name to store: the one given if it follows the game's rules and passes the filter, else "???".
 * `refused` tells the game to say so.
 */
export function cleanName(name) {
  const trimmed = typeof name === "string" ? name.trim() : "";
  const fits = trimmed.length > 0 && [...trimmed].length <= MAX_NAME_LENGTH && ALLOWED.test(trimmed);
  if (!fits) return { name: UNNAMED, refused: trimmed.length > 0 };
  return isRude(trimmed) ? { name: UNNAMED, refused: true } : { name: trimmed, refused: false };
}
