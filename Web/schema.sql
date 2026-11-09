-- The code is all that is kept. The key only stops the same level coming back under another name,
-- and the implicit rowid gives the order of arrival.
CREATE TABLE IF NOT EXISTS levels (
  code TEXT NOT NULL,
  level_key TEXT NOT NULL UNIQUE
);
