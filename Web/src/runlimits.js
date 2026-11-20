// The limits of a run through the 15 levels, written by the game's editor to run-limits.json (menu
// "lowercase UPPERCASE/High Scores/Write Run Limits"), and the floor times kept by hand in run-floors.json.

/**
 * The text the hash is taken of, the same as RunLimits.Canonical in the game: "v<format>", the points per
 * enemy / # / second, then one line per level: scene seconds enemies collectibles.
 */
export function canonical(limits) {
  return [
    `v${limits.format}`,
    `${limits.pointsPerEnemy} ${limits.pointsPerCollectible} ${limits.pointsPerSecondLeft}`,
    ...limits.levels.map(l => `${l.scene} ${l.seconds} ${l.enemies} ${l.collectibles}`)
  ].join("\n");
}

/** The first 16 hex digits of the SHA-256 of canonical(limits), as RunLimits.HashOf. */
export async function hashOf(limits) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(canonical(limits)));
  return [...new Uint8Array(digest).slice(0, 8)].map(b => b.toString(16).padStart(2, "0")).join("");
}

/** Every enemy, every # and the whole clock left over, as RunLimits.MaxPoints. */
export function maxPoints(limits, level) {
  return level.enemies * limits.pointsPerEnemy
    + level.collectibles * limits.pointsPerCollectible
    + level.seconds * limits.pointsPerSecondLeft;
}
