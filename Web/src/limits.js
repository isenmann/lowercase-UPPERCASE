// In their own file because a Worker's main module may only export handlers.

// Codes are gzipped JSON in base64. A big editor level is a few kilobytes; this leaves plenty of room, line breaks
// from a pasted code included.
export const MAX_BODY_LENGTH = 65536;
// Keeps a flood of generated levels from eating the free database quota. Checked against the highest
// rowid, which reads one row where counting would read them all, so it is a count of arrivals: removing
// an older level frees no room, but emptying the table starts again from 1.
export const MAX_LEVELS = 20000;
// Levels per request. Every request reads about this many rows, however long the list has become.
export const PAGE_SIZE = 50;

// Online high scores.
// A run's calls are small JSON objects.
export const RUN_BODY_LENGTH = 1024;
// A run must be finished within this long of its start; older unfinished runs are deleted by the daily cron.
export const RUN_MAX_MS = 3 * 60 * 60 * 1000;
// Server time between two level reports may fall this far short of the level clock: a report can travel faster
// than the one before it. The game's clock never runs faster than real time.
export const CLOCK_SLACK_MS = 3000;
// Runs per page of the world list, and how deep it can be paged.
export const SCORES_PAGE_DEFAULT = 10;
export const SCORES_PAGE_MAX = 100;
export const SCORES_MAX_OFFSET = 10000;
// A run is marked for a look when it reaches this share of what a perfect player could have scored in the same
// levels, or finishes a level within this factor of its floor time.
export const MARK_SHARE = 0.95;
export const MARK_FLOOR_FACTOR = 1.1;
