export class ModuleLoadError extends Error {
  constructor(message) {
    super(message);
    this.name = "ModuleLoadError";
  }
}
