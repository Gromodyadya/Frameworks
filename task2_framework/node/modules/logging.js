export default {
  name: "Logging",
  requires: ["Core"],
  register(container) {
    container.addSingleton("action.logging", () => ({
      title: "Проверка журнала событий",
      async execute() {
        console.log("Сообщение из модуля журналирования");
      }
    }));
  },
  async init(container) {}
};
