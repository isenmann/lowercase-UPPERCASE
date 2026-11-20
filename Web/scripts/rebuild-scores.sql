-- After removing high score runs by hand: recount the finished runs and rebuild the fastest finish of each level
-- from those left. Equal times keep the run that finished first. Marks on the fastest finishes start again at 0.
UPDATE stats SET finished = (SELECT COUNT(*) FROM runs WHERE finished_at IS NOT NULL) WHERE id = 1;
DELETE FROM fastest;
INSERT INTO fastest (level, run_id, centis, name, finished_at, marked)
SELECT l.level, l.run_id, l.centis, r.name, r.finished_at, 0
FROM run_levels l JOIN runs r ON r.id = l.run_id
WHERE r.finished_at IS NOT NULL AND NOT EXISTS (
  SELECT 1 FROM run_levels o JOIN runs q ON q.id = o.run_id
  WHERE q.finished_at IS NOT NULL AND o.level = l.level
    AND (o.centis < l.centis OR (o.centis = l.centis AND q.finished_at < r.finished_at))
);
