import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { Shell } from './layout/shell';
import { AgentDetailPage } from './pages/agent-detail';
import { AgentsPage } from './pages/agents';
import { AuditPage } from './pages/audit';
import { BillingPage } from './pages/billing';
import { ExecutionsPage } from './pages/executions';
import { HardwarePage } from './pages/hardware';
import { LoginPage } from './pages/login';
import { ModelsPage } from './pages/models';
import { OverviewPage } from './pages/overview';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'overview' },
      { path: 'overview', component: OverviewPage },
      { path: 'agents', component: AgentsPage },
      { path: 'agents/:agentId', component: AgentDetailPage },
      { path: 'executions', component: ExecutionsPage },
      { path: 'models', component: ModelsPage },
      { path: 'hardware', component: HardwarePage },
      { path: 'audit', component: AuditPage },
      { path: 'billing', component: BillingPage },
    ],
  },
  { path: '**', redirectTo: '' },
];
