import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/auth.interceptor';
import { AuditItem } from '../core/models';

@Component({
  selector: 'app-audit',
  imports: [DatePipe],
  template: `
    <h2 class="text-2xl font-semibold">Audit</h2>
    <p class="mt-1 text-sm text-muted">Administrative actions for this organization. Payloads and secrets are not recorded.</p>
    @if (error()) {
      <p class="mt-4 text-sm text-red-300">{{ error() }}</p>
    }
    <div class="mt-4 overflow-x-auto rounded border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-panel text-muted">
          <tr>
            <th class="px-3 py-2">When (UTC)</th>
            <th class="px-3 py-2">Action</th>
            <th class="px-3 py-2">Target</th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.id) {
            <tr class="border-t border-line">
              <td class="px-3 py-2">{{ item.timestampUtc | date: 'yyyy-MM-dd HH:mm:ss' : 'UTC' }}</td>
              <td class="px-3 py-2">{{ item.action }}</td>
              <td class="px-3 py-2">{{ item.target }}</td>
            </tr>
          } @empty {
            <tr><td class="px-3 py-6 text-muted" colspan="3">No audit events yet.</td></tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class AuditPage {
  private readonly api = inject(ApiService);
  readonly items = signal<AuditItem[]>([]);
  readonly error = signal('');

  constructor() {
    this.api.audit(1).subscribe({
      next: (page) => this.items.set(page.items),
      error: (err: unknown) => this.error.set(errorMessage(err)),
    });
  }
}
