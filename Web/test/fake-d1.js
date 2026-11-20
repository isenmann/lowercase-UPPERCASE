import { readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";

/** The real schema and SQL on a real SQLite, behind the few D1 calls the Worker makes. */
export function fakeD1() {
  const database = new DatabaseSync(":memory:");
  database.exec(readFileSync(new URL("../schema.sql", import.meta.url), "utf8"));
  return {
    raw: database,
    prepare(sql) {
      const statement = database.prepare(sql);
      const bound = (...values) => ({
        all: async () => ({ results: statement.all(...values) }),
        first: async () => statement.get(...values) ?? null,
        run: async () => ({ meta: { changes: Number(statement.run(...values).changes) } })
      });
      return { ...bound(), bind: bound };
    },
    /** Like D1: in order, in one transaction, all or nothing. */
    async batch(statements) {
      database.exec("BEGIN");
      try {
        const results = [];
        for (const statement of statements) results.push(await statement.run());
        database.exec("COMMIT");
        return results;
      } catch (error) {
        database.exec("ROLLBACK");
        throw error;
      }
    }
  };
}
