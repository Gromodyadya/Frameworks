export default {
  name: "Validation",
  requires: ["Core"],
  register(container) {
    container.addSingleton("action.validation", () => {
      const storage = container.get("storage");
      return {
        title: "Проверка правил данных",
        async execute() {
          const value = "пример";
          if (value.length < 3) {
            throw new Error("Значение слишком короткое");
          }
          storage.add(value);
        }
      };
    });
  },
  async init(container) {}
};
