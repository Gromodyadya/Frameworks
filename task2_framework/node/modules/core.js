export default {
  name: "Core",
  requires: [],
  register(container) {
    container.addSingleton("clock", () => ({ now: () => new Date().toISOString() }));
    container.addSingleton("storage", () => {
      const values = [];
      return {
        add(v) { values.push(v); },
        all() { return values.slice(); }
      };
    });
  },
  async init(container) {}
};
