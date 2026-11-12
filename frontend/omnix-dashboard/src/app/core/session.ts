import { Injectable, signal } from '@angular/core';
import { Session } from './models';

const storageKey = 'aat.session';

@Injectable({ providedIn: 'root' })
export class SessionStore {
  readonly session = signal<Session | null>(this.read());

  set(session: Session): void {
    sessionStorage.setItem(storageKey, JSON.stringify(session));
    this.session.set(session);
  }

  clear(): void {
    sessionStorage.removeItem(storageKey);
    this.session.set(null);
  }

  private read(): Session | null {
    const raw = sessionStorage.getItem(storageKey);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as Session;
    } catch {
      return null;
    }
  }
}
