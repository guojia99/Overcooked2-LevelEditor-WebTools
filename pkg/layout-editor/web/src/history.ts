export class HistoryStack<T> {
  private undoStack: T[] = [];
  private redoStack: T[] = [];

  constructor(private limit = 20) {}

  push(snapshot: T): void {
    this.undoStack.push(snapshot);
    this.redoStack = [];
    this.trim();
  }

  undo(current: T): T | undefined {
    const s = this.undoStack.pop();
    if (s === undefined) return undefined;
    this.redoStack.push(current);
    this.trim();
    return s;
  }

  redo(current: T): T | undefined {
    const s = this.redoStack.pop();
    if (s === undefined) return undefined;
    this.undoStack.push(current);
    this.trim();
    return s;
  }

  get size(): number {
    return this.undoStack.length;
  }

  get redoSize(): number {
    return this.redoStack.length;
  }

  clear(): void {
    this.undoStack = [];
    this.redoStack = [];
  }

  private trim(): void {
    while (this.undoStack.length + this.redoStack.length > this.limit) {
      if (this.redoStack.length > 0) this.redoStack.shift();
      else this.undoStack.shift();
    }
  }
}
