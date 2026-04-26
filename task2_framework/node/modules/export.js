import fs from "node:fs/promises";
import path from "node:path";

export default {
  name: "Export",
  requires: ["Core", "Validation"],
  register(container) {
    container.addSingleton("action.export", () => {
      const storage = container.get("storage");
      return {
        title: "Экспорт данных в файл",
        async execute() {
          const lines = storage.all();
          const out = path.resolve(process.cwd(), "export.txt");
          await fs.writeFile(out, lines.join("\n"), "utf8");
        }
      };
    });
  },
  async init(container) {}
};
