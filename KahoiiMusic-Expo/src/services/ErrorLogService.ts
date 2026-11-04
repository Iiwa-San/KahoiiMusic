import { ErrorLogItem } from '../types/music';

export class ErrorLogService {
  private static instance: ErrorLogService;
  public logs: ErrorLogItem[] = [];
  private listeners: Set<() => void> = new Set();

  public static getInstance(): ErrorLogService {
    if (!ErrorLogService.instance) {
      ErrorLogService.instance = new ErrorLogService();
    }
    return ErrorLogService.instance;
  }

  public log(title: string, message: string, category: string = 'Hệ thống') {
    const item: ErrorLogItem = {
      id: `${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,
      title,
      message,
      category,
      timestamp: Date.now(),
    };
    this.logs.unshift(item);
    if (this.logs.length > 50) {
      this.logs.pop();
    }
    this.notify();
    console.warn(`[Kahoii Error Log] [${category}] ${title}: ${message}`);
  }

  public clear() {
    this.logs = [];
    this.notify();
  }

  public subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify() {
    this.listeners.forEach((l) => l());
  }
}

