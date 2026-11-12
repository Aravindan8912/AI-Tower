import { Component, computed, signal } from '@angular/core';

type PlanId = 'monitor' | 'tower-month' | 'tower-year';

interface Plan {
  id: PlanId;
  name: string;
  price: string;
  cadence: string;
  detail: string;
  items: string[];
  checkout: boolean;
}

const storageKey = 'aat.sandboxPlan';

const plans: Plan[] = [
  {
    id: 'monitor',
    name: 'Monitor',
    price: '$0',
    cadence: 'local',
    detail: 'The tower that is running now.',
    items: ['One organization', 'Agent heartbeats', 'Execution metadata', 'Host samples'],
    checkout: false,
  },
  {
    id: 'tower-month',
    name: 'Analytic Tower',
    price: '$49',
    cadence: 'per month',
    detail: 'Sandbox subscription, billed monthly.',
    items: ['Everything in Monitor', '24 hour token split', 'Model share', 'Sandbox checkout'],
    checkout: true,
  },
  {
    id: 'tower-year',
    name: 'Analytic Tower',
    price: '$470',
    cadence: 'per year',
    detail: 'Sandbox subscription, billed yearly.',
    items: ['Everything in the monthly plan', 'One sandbox approval for the year'],
    checkout: true,
  },
];

@Component({
  selector: 'app-billing',
  template: `
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-2xl font-semibold">Billing</h2>
        <p class="mt-1 max-w-2xl text-sm text-muted">PayPal Sandbox preview. Choosing a plan updates this browser only. PayPal is not contacted and no card is charged.</p>
      </div>
      <p class="rounded border border-amber-700/60 bg-amber-950/40 px-3 py-1 text-xs text-amber-100">Sandbox</p>
    </div>

    <section class="mt-6 rounded border border-line bg-panel p-4">
      <p class="text-xs text-muted">Current selection</p>
      @if (active(); as current) {
        <p class="mt-2 text-lg">{{ current.name }} · {{ current.price }} {{ current.cadence }}</p>
        <p class="mt-1 text-sm text-muted">Saved in this browser as a sandbox preview.</p>
        <button class="mt-3 text-sm text-accent" type="button" (click)="clear()">Clear selection</button>
      } @else {
        <p class="mt-2 text-lg">No sandbox subscription selected</p>
        <p class="mt-1 text-sm text-muted">Monitor stays available. A paid plan appears here after the sandbox preview is approved.</p>
      }
    </section>

    <div class="mt-6 grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
      <div class="grid gap-3 md:grid-cols-3">
        @for (plan of plans; track plan.id) {
          <article class="flex flex-col rounded border p-4" [class]="plan.id === selected() ? 'border-accent bg-panel' : 'border-line bg-panel'">
            <p class="text-sm text-muted">{{ plan.checkout ? 'Subscription' : 'Included' }}</p>
            <h3 class="mt-2 text-lg font-semibold">{{ plan.name }}</h3>
            <p class="mt-3 text-3xl tabular-nums">{{ plan.price }}</p>
            <p class="text-xs text-muted">{{ plan.cadence }}</p>
            <p class="mt-3 text-sm">{{ plan.detail }}</p>
            <ul class="mt-3 flex-1 space-y-1 text-sm text-muted">
              @for (item of plan.items; track item) {
                <li>{{ item }}</li>
              }
            </ul>
            <button
              class="mt-4 rounded px-3 py-2 text-sm font-medium"
              [class]="plan.id === selected() ? 'bg-ink text-accent' : 'bg-accent text-ink'"
              type="button"
              (click)="choose(plan.id)"
            >
              {{ plan.id === activeId() ? 'Selected' : plan.checkout ? 'Review in sandbox' : 'Use Monitor' }}
            </button>
          </article>
        }
      </div>

      <aside class="h-fit rounded border border-line bg-panel p-4">
        <p class="text-xs tracking-wide text-muted">Checkout</p>
        <h3 class="mt-2 text-lg font-semibold">{{ selectedPlan().name }}</h3>
        <p class="mt-1 text-sm text-muted">{{ selectedPlan().detail }}</p>
        <dl class="mt-4 space-y-2 text-sm">
          <div class="flex justify-between gap-3"><dt class="text-muted">Price</dt><dd class="tabular-nums">{{ selectedPlan().price }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">Cadence</dt><dd>{{ selectedPlan().cadence }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">Due today</dt><dd class="tabular-nums">{{ selectedPlan().checkout ? selectedPlan().price : '$0' }}</dd></div>
        </dl>
        @if (selectedPlan().checkout) {
          <button class="mt-5 flex w-full items-center justify-center gap-2 rounded bg-[#ffc439] px-3 py-3 text-sm font-semibold text-[#003087]" type="button" (click)="openCheckout()">
            <span class="italic">PayPal</span>
            <span>Sandbox</span>
          </button>
          <p class="mt-2 text-xs text-muted">Opens a local approval preview. It does not open PayPal and does not ask for a PayPal password.</p>
        } @else {
          <button class="mt-5 w-full rounded bg-accent px-3 py-3 text-sm font-semibold text-ink" type="button" (click)="approve()">Keep Monitor</button>
          <p class="mt-2 text-xs text-muted">Monitor has no sandbox charge.</p>
        }
      </aside>
    </div>

    @if (checkoutOpen()) {
      <div class="fixed inset-0 z-20 flex items-center justify-center bg-black/60 p-4" role="presentation" (click)="closeCheckout()">
        <div class="w-full max-w-md rounded-lg bg-white p-6 text-[#1a1a1a]" role="dialog" aria-labelledby="sandbox-title" (click)="$event.stopPropagation()">
          <div class="flex items-center justify-between">
            <p class="text-2xl font-semibold italic text-[#003087]">PayPal</p>
            <span class="rounded bg-[#fff4d6] px-2 py-1 text-xs font-medium text-[#6b4e00]">Sandbox</span>
          </div>
          <h3 id="sandbox-title" class="mt-4 text-lg font-semibold">Approve {{ selectedPlan().name }}</h3>
          <p class="mt-1 text-sm text-[#3d3d3d]">{{ selectedPlan().price }} {{ selectedPlan().cadence }} for {{ organizationName() }}.</p>
          <div class="mt-4 rounded border border-[#d9d9d9] bg-[#f7f7f7] p-3 text-sm">
            <p>Buyer: sandbox-buyer@personal.example</p>
            <p class="mt-1 text-[#5c5c5c]">This sheet is drawn by the dashboard. No account is signed in.</p>
          </div>
          <div class="mt-5 flex gap-2">
            <button class="flex-1 rounded bg-[#003087] px-3 py-2 text-sm font-semibold text-white" type="button" (click)="approve()">Approve</button>
            <button class="rounded border border-[#c5c5c5] px-3 py-2 text-sm" type="button" (click)="closeCheckout()">Cancel</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class BillingPage {
  readonly plans = plans;
  readonly selected = signal<PlanId>(this.read() ?? 'tower-month');
  readonly activeId = signal<PlanId | null>(this.read());
  readonly checkoutOpen = signal(false);
  readonly selectedPlan = computed(() => this.plans.find((plan) => plan.id === this.selected()) ?? this.plans[1]);
  readonly active = computed(() => this.plans.find((plan) => plan.id === this.activeId()) ?? null);

  choose(id: PlanId): void {
    this.selected.set(id);
  }

  openCheckout(): void {
    this.checkoutOpen.set(true);
  }

  closeCheckout(): void {
    this.checkoutOpen.set(false);
  }

  approve(): void {
    const id = this.selected();
    sessionStorage.setItem(storageKey, id);
    this.activeId.set(id);
    this.checkoutOpen.set(false);
  }

  clear(): void {
    sessionStorage.removeItem(storageKey);
    this.activeId.set(null);
  }

  organizationName(): string {
    const raw = sessionStorage.getItem('aat.session');
    if (!raw) return 'this organization';
    try {
      const session = JSON.parse(raw) as { organizationName?: string };
      return session.organizationName || 'this organization';
    } catch {
      return 'this organization';
    }
  }

  private read(): PlanId | null {
    const value = sessionStorage.getItem(storageKey);
    return value === 'monitor' || value === 'tower-month' || value === 'tower-year' ? value : null;
  }
}
