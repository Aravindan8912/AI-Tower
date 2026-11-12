import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class EnvironmentContext {
  readonly environmentId = signal<string | null>(sessionStorage.getItem('aat.env'));

  set(id: string | null): void {
    this.environmentId.set(id);
    if (id) sessionStorage.setItem('aat.env', id);
    else sessionStorage.removeItem('aat.env');
  }
}
