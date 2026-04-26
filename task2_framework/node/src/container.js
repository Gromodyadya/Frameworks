export class Container {
  constructor() {
    this._factories = new Map();
    this._singletons = new Map();
  }

  addSingleton(key, factory) {
    this._factories.set(key, { kind: "singleton", factory });
  }

  addTransient(key, factory) {
    this._factories.set(key, { kind: "transient", factory });
  }

  get(key) {
    const entry = this._factories.get(key);
    if (!entry) {
      throw new Error(`Служба не зарегистрирована, имя ${key}`);
    }

    if (entry.kind === "singleton") {
      if (!this._singletons.has(key)) {
        this._singletons.set(key, entry.factory(this));
      }
      return this._singletons.get(key);
    }

    return entry.factory(this);
  }

  getMany(prefix) {
    const res = [];
    for (const [key] of this._factories) {
      if (key.startsWith(prefix)) {
        res.push(this.get(key));
      }
    }
    return res;
  }
}
