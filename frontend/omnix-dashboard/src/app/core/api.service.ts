import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AgentActivity,
  AgentDetail,
  AgentItem,
  AuditItem,
  EnvironmentItem,
  ExecutionItem,
  HardwareSeries,
  ModelAnalytic,
  Overview,
  Paged,
  Session,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  register(body: {
    organizationName: string;
    email: string;
    displayName: string;
    password: string;
  }): Observable<Session> {
    return this.http.post<Session>('/api/v1/auth/register', body);
  }

  login(email: string, password: string): Observable<Session> {
    return this.http.post<Session>('/api/v1/auth/login', { email, password });
  }

  environments(): Observable<EnvironmentItem[]> {
    return this.http.get<EnvironmentItem[]>('/api/v1/environments');
  }

  createEnvironment(name: string, type: string): Observable<EnvironmentItem> {
    return this.http.post<EnvironmentItem>('/api/v1/environments', { name, type });
  }

  agents(): Observable<AgentItem[]> {
    return this.http.get<AgentItem[]>('/api/v1/agents');
  }

  agent(id: string): Observable<AgentDetail> {
    return this.http.get<AgentDetail>(`/api/v1/agents/${id}`);
  }

  agentActivity(id: string): Observable<AgentActivity> {
    return this.http.get<AgentActivity>(`/api/v1/analytics/agents/${id}`);
  }

  createEnrollmentToken(environmentId: string, agentName: string): Observable<{ token: string; expiresAtUtc: string }> {
    return this.http.post<{ token: string; expiresAtUtc: string }>('/api/v1/agents/enrollment-tokens', {
      environmentId,
      agentName,
    });
  }

  revokeAgent(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/agents/${id}/revoke`, {});
  }

  executions(filters: Record<string, string | number | undefined>): Observable<Paged<ExecutionItem>> {
    return this.http.get<Paged<ExecutionItem>>('/api/v1/executions', { params: this.params(filters) });
  }

  overview(filters: Record<string, string | undefined>): Observable<Overview> {
    return this.http.get<Overview>('/api/v1/analytics/overview', { params: this.params(filters) });
  }

  models(filters: Record<string, string | undefined>): Observable<ModelAnalytic[]> {
    return this.http.get<ModelAnalytic[]>('/api/v1/analytics/models', { params: this.params(filters) });
  }

  hardware(filters: Record<string, string | undefined>): Observable<HardwareSeries> {
    return this.http.get<HardwareSeries>('/api/v1/analytics/hardware', { params: this.params(filters) });
  }

  audit(page: number): Observable<Paged<AuditItem>> {
    return this.http.get<Paged<AuditItem>>('/api/v1/audit', { params: { page, pageSize: 25 } });
  }

  private params(filters: Record<string, string | number | undefined>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== '') params = params.set(key, String(value));
    }
    return params;
  }
}
