-- The code is all that is kept. The key only stops the same level coming back under another name,
-- and the implicit rowid gives the order of arrival.
CREATE TABLE IF NOT EXISTS levels (
  code TEXT NOT NULL,
  level_key TEXT NOT NULL UNIQUE
);

-- Online high scores. A run is one go at the 15 levels from the first. Its id is random and secret: it is
-- what lets the game report for that run, so it is never shown. A run counts once it is finished (a name
-- given at game over or after the last level); unfinished runs are deleted by the daily cron.
CREATE TABLE IF NOT EXISTS runs (
  id TEXT PRIMARY KEY,
  started_at INTEGER NOT NULL,      -- server time, ms
  last_at INTEGER NOT NULL,         -- server time of the start or of the last level report, ms
  levels_done INTEGER NOT NULL DEFAULT 0,
  points INTEGER NOT NULL DEFAULT 0,        -- the finished levels' points, added up by the server
  max_reachable INTEGER NOT NULL DEFAULT 0, -- every enemy and # of the same levels, plus the time bonus the run got
  platform TEXT NOT NULL,
  version TEXT NOT NULL,
  finished_at INTEGER,
  name TEXT,
  total INTEGER,                    -- points + the level a game over stopped in
  marked INTEGER NOT NULL DEFAULT 0 -- close to the maximum or to a floor time: worth a look
);
CREATE INDEX IF NOT EXISTS runs_board ON runs (total DESC, finished_at) WHERE finished_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS runs_unfinished ON runs (started_at) WHERE finished_at IS NULL;

-- One row per finished level of a run.
CREATE TABLE IF NOT EXISTS run_levels (
  run_id TEXT NOT NULL,
  level INTEGER NOT NULL,           -- 1 to 15
  points INTEGER NOT NULL,
  centis INTEGER NOT NULL,          -- time used on the level clock, 1/100 s
  PRIMARY KEY (run_id, level)
);

-- The fastest finish of each level among finished runs, kept up to date as runs finish so the page reads
-- 15 rows. After deleting runs by hand, rebuild it (see README).
CREATE TABLE IF NOT EXISTS fastest (
  level INTEGER PRIMARY KEY,
  run_id TEXT NOT NULL,
  centis INTEGER NOT NULL,
  name TEXT NOT NULL,
  finished_at INTEGER NOT NULL,
  marked INTEGER NOT NULL DEFAULT 0
);

-- How many finished runs there are, so a rank's "of 512" doesn't count every row each time.
CREATE TABLE IF NOT EXISTS stats (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  finished INTEGER NOT NULL
);
INSERT OR IGNORE INTO stats (id, finished) VALUES (1, 0);
