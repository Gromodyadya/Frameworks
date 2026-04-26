import test from "node:test";
import assert from "node:assert/strict";
import { buildOrder } from "../src/moduleLoader.js";
import { ModuleLoadError } from "../src/errors.js";

test("Порядок запуска учитывает зависимости", () => {
  const all = new Map();
  all.set("a", { name: "A", requires: [] });
  all.set("b", { name: "B", requires: ["A"] });
  all.set("c", { name: "C", requires: ["B"] });

  const order = buildOrder(all, ["A", "B", "C"]);
  assert.deepEqual(order.map(x => x.name), ["A", "B", "C"]);
});

test("Отсутствующий модуль даёт понятную ошибку", () => {
  const all = new Map();
  all.set("a", { name: "A", requires: [] });

  assert.throws(
    () => buildOrder(all, ["A", "B"]),
    (e) => e instanceof ModuleLoadError && e.message.includes("Модуль не найден")
  );
});

test("Цикл зависимостей обнаруживается", () => {
  const all = new Map();
  all.set("a", { name: "A", requires: ["B"] });
  all.set("b", { name: "B", requires: ["A"] });

  assert.throws(
    () => buildOrder(all, ["A", "B"]),
    (e) => e instanceof ModuleLoadError && e.message.toLowerCase().includes("циклическая")
  );
});
