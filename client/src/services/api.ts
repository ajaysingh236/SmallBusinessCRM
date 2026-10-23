import {
  Lead,
  LeadStatus,
  DashboardReport,
  AnalyticsReport,
  ActivityLog,
  JwtLoginResponse,
  AuditLog,
  LeadScoringPrediction,
  EmailTracking,
  CalendarStatus,
  CalendarEvent
} from '../types';

const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

function getAuthToken(): string | null {
  return localStorage.getItem('crm_jwt_token');
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers || {});
  const token = getAuthToken();

  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const config: RequestInit = {
    ...options,
    headers,
    credentials: 'include'
  };

  const response = await fetch(`${API_BASE}${endpoint}`, config);

 if (response.status === 401) {
  // Only clear authentication when a protected API request fails.
  // A failed login should show its error without triggering a logout event.
  if (endpoint !== '/account/token') {
    localStorage.removeItem('crm_jwt_token');
    localStorage.removeItem('crm_user');
    window.dispatchEvent(new Event('auth:unauthorized'));
  }

  const errorBody = await response.json().catch(() => ({
    message: 'Unauthorized'
  }));

  throw new Error(errorBody.message || 'Unauthorized');
}

  if (!response.ok) {
    const errorBody = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(errorBody.message || `Request failed with status ${response.status}`);
  }

  if (response.status === 204) {
    return {} as T;
  }

  return response.json();
}

export const api = {
  // Auth
  async login(email: string, password: string): Promise<JwtLoginResponse> {
    return request<JwtLoginResponse>('/account/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
  },

  async logout(): Promise<void> {
    try {
      await request('/account/logout', { method: 'POST' });
    } finally {
      localStorage.removeItem('crm_jwt_token');
      localStorage.removeItem('crm_user');
    }
  },

  // Leads
  async getLeads(): Promise<Lead[]> {
    return request<Lead[]>('/api/leads');
  },

  async getLead(id: number): Promise<Lead> {
    return request<Lead>(`/api/leads/${id}`);
  },

  async updateStage(id: number, status: LeadStatus): Promise<{ id: number; status: LeadStatus }> {
    return request<{ id: number; status: LeadStatus }>(`/api/leads/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status })
    });
  },

  async updateLeadStatus(id: number, status: LeadStatus): Promise<{ id: number; status: LeadStatus }> {
    return this.updateStage(id, status);
  },

  async createLead(data: {
    title: string;
    companyName: string;
    contactName: string;
    email?: string;
    phone?: string;
    value: number;
    assignedUserId?: number;
  }): Promise<Lead> {
    return request<Lead>('/api/leads', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });
  },

  async deleteLead(id: number): Promise<void> {
    return request<void>(`/api/leads/${id}`, { method: 'DELETE' });
  },

  async restoreLead(id: number): Promise<Lead> {
    return request<Lead>(`/api/leads/${id}/restore`, { method: 'POST' });
  },

  // Reports & Analytics
  async getDashboardReport(): Promise<DashboardReport> {
    return request<DashboardReport>('/api/reports/dashboard');
  },

  async getAnalyticsReport(): Promise<AnalyticsReport> {
    return request<AnalyticsReport>('/api/reports/analytics');
  },

  async recalculateScores(): Promise<{ processed: number }> {
    return request<{ processed: number }>('/api/leadscoring/recalculate-all', {
      method: 'POST'
    });
  },

  async recalculateLeadScore(leadId: number, strategy?: string): Promise<LeadScoringPrediction> {
    const query = strategy ? `?strategy=${encodeURIComponent(strategy)}` : '';
    return request<LeadScoringPrediction>(`/api/leadscoring/recalculate/${leadId}${query}`, {
      method: 'POST'
    });
  },

  async getLeadPrediction(leadId: number): Promise<LeadScoringPrediction> {
    return request<LeadScoringPrediction>(`/api/leadscoring/prediction/${leadId}`);
  },

  // Email Tracking & Drip Workflows
  async getLeadEmailTracking(leadId: number): Promise<EmailTracking[]> {
    return request<EmailTracking[]>(`/api/email/tracking/${leadId}`);
  },

  async triggerDripStep(leadId: number): Promise<{ trackingId: string; stepName: string; subject: string; recipient: string }> {
    return request<{ trackingId: string; stepName: string; subject: string; recipient: string }>(
      `/api/email/drip/trigger/${leadId}`,
      { method: 'POST' }
    );
  },

  // Calendar Integrations
  async getCalendarStatus(): Promise<CalendarStatus> {
    return request<CalendarStatus>('/api/calendar/status');
  },

  async connectCalendar(provider: string): Promise<{ message: string; provider: string; isSyncEnabled: boolean }> {
    return request<{ message: string; provider: string; isSyncEnabled: boolean }>('/api/calendar/connect', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider })
    });
  },

  async syncCalendar(): Promise<{ syncedCount: number; events: CalendarEvent[] }> {
    return request<{ syncedCount: number; events: CalendarEvent[] }>('/api/calendar/sync', {
      method: 'POST'
    });
  },

  // Webhook Notifications
  async testWebhook(target: string, message: string): Promise<{ target: string; dispatched: boolean }> {
    return request<{ target: string; dispatched: boolean }>('/api/webhooks/test', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ target, message })
    });
  },

  // Activities
  async getRecentActivities(page = 1, pageSize = 20): Promise<{ page: number; pageSize: number; total: number; data: ActivityLog[] }> {
    return request<{ page: number; pageSize: number; total: number; data: ActivityLog[] }>(
      `/api/activityfeed/recent?page=${page}&pageSize=${pageSize}`
    );
  },

  // Admin
  async getAuditLogs(take = 100): Promise<AuditLog[]> {
    return request<AuditLog[]>(`/api/admin/audit-logs?take=${take}`);
  },

  async getTrash(): Promise<Lead[]> {
    return request<Lead[]>('/api/admin/trash');
  },

  async createUser(data: { fullName: string; email: string; password: string; role: string; tenantId?: string }): Promise<{ id: number; fullName: string; email: string }> {
    return request<{ id: number; fullName: string; email: string }>('/api/admin/users', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });
  },

  async importLeads(file: File): Promise<{ imported: number; skipped: number }> {
    const formData = new FormData();
    formData.append('file', file);
    return request<{ imported: number; skipped: number }>('/api/data/leads/import', {
      method: 'POST',
      body: formData
    });
  }
};

export const money = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0);
