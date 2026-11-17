// Fails if an operationId in either spec changed, disappeared, or is duplicated.
// Baseline lives in operation-ids.lock.json. Run with --update to accept intentional changes.
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { createRequire } from "node:module";

const { parse } = createRequire(import.meta.url)("./../typespec/node_modules/yaml");
const specs = {
  typespec: "typespec/openapi.yaml",
  aspnet: "aspnet/TaskApi/TaskApi.json",
};
const lockFile = "operation-ids.lock.json";

const current = {};
const errors = [];
for (const [name, path] of Object.entries(specs)) {
  const doc = parse(readFileSync(path, "utf8")); // YAML parser also reads JSON
  const ids = {}; // "GET /tasks" -> operationId
  const seen = new Map();
  for (const [route, item] of Object.entries(doc.paths ?? {}))
    for (const [method, op] of Object.entries(item)) {
      if (!op?.responses) continue;
      const key = `${method.toUpperCase()} ${route}`;
      if (!op.operationId) { errors.push(`${name}: ${key} has no operationId`); continue; }
      if (seen.has(op.operationId))
        errors.push(`${name}: duplicate operationId '${op.operationId}' (${seen.get(op.operationId)} and ${key})`);
      seen.set(op.operationId, key);
      ids[key] = op.operationId;
    }
  current[name] = ids;
}

if (process.argv.includes("--update")) {
  if (errors.length) { console.error(errors.join("\n")); process.exit(1); }
  writeFileSync(lockFile, JSON.stringify(current, null, 2) + "\n");
  console.log(`Updated ${lockFile}`);
  process.exit(0);
}

if (!existsSync(lockFile)) errors.push(`${lockFile} missing; run with --update`);
else {
  const locked = JSON.parse(readFileSync(lockFile, "utf8"));
  for (const [name, ids] of Object.entries(locked))
    for (const [key, id] of Object.entries(ids)) {
      const now = current[name]?.[key];
      if (!now) errors.push(`${name}: ${key} (${id}) disappeared`);
      else if (now !== id) errors.push(`${name}: ${key} operationId changed '${id}' -> '${now}'`);
    }
}

if (errors.length) { console.error("operationId check failed:\n- " + errors.join("\n- ")); process.exit(1); }
console.log("operationIds OK");
