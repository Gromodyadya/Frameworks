import fs from "node:fs/promises";
import path from "node:path";
import { ModuleLoadError } from "./errors.js";

export async function loadModulesFromConfig(configPath, modulesDir) {
  const raw = await fs.readFile(configPath, "utf8");
  const cfg = JSON.parse(raw);

  const files = cfg.modules ?? [];
  const loaded = new Map();

  for (const file of files) {
    const full = path.resolve(modulesDir, file);
    const mod = await import(full);
    const moduleObj = mod.default;

    if (!moduleObj || typeof moduleObj.name !== "string") {
      throw new ModuleLoadError(`Некорректный модуль, файл ${file}`);
    }

    loaded.set(moduleObj.name.toLowerCase(), moduleObj);
  }

  return loaded;
}

export function buildOrder(all, enabledNames) {
  const enabled = new Map();

  for (const name of enabledNames) {
    const key = name.toLowerCase();
    const moduleObj = all.get(key);
    if (!moduleObj) {
      throw new ModuleLoadError(`Модуль не найден, имя модуля ${name}`);
    }
    enabled.set(key, moduleObj);
  }

  for (const moduleObj of enabled.values()) {
    const req = moduleObj.requires ?? [];
    for (const r of req) {
      if (!enabled.has(r.toLowerCase())) {
        throw new ModuleLoadError(`Не хватает модуля для зависимости, модуль ${moduleObj.name} требует ${r}`);
      }
    }
  }

  const indeg = new Map();
  const edges = new Map();

  for (const [k, m] of enabled) {
    indeg.set(k, 0);
    edges.set(k, []);
  }

  for (const [k, m] of enabled) {
    const req = m.requires ?? [];
    for (const r0 of req) {
      const r = r0.toLowerCase();
      edges.get(r).push(k);
      indeg.set(k, indeg.get(k) + 1);
    }
  }

  const q = [];
  for (const [k, v] of indeg) {
    if (v === 0) q.push(k);
  }

  const result = [];
  while (q.length > 0) {
    const k = q.shift();
    result.push(enabled.get(k));
    for (const to of edges.get(k)) {
      indeg.set(to, indeg.get(to) - 1);
      if (indeg.get(to) === 0) q.push(to);
    }
  }

  if (result.length !== enabled.size) {
    const stuck = [];
    for (const [k, v] of indeg) {
      if (v > 0) stuck.push(enabled.get(k).name);
    }
    throw new ModuleLoadError(`Обнаружена циклическая зависимость модулей, проблемные модули ${stuck.join(", ")}`);
  }

  return result;
}
