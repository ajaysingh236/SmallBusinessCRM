export type LeadStatus = 'New' | 'Contacted' | 'DemoScheduled' | 'ProposalSent' | 'ClosedWon' | 'ClosedLost';

export type ActivityType = 'Note' | 'Call' | 'Email' | 'Meeting' | 'StatusChanged' | 'System';

export interface ApplicationUser {
  id: number;
  fullName: string;
  email: string;
  tenantId: string;
  isActive: boolean;
}

export interface CrmTask {
  id: number;
  title: string;
  dueDateUtc: string;
  isCompleted: boolean;
  reminderSent: boolean;
  leadId: number;
  assignedUserId?: number;
}

export interface Lead {
  id: number;
  title: string;
  companyName: string;
  contactName: string;
  email?: string;
  phone?: string;
  value: number;
  status: LeadStatus;
  priorityScore: number;
  assignedUserId?: number;
  assignedUser?: ApplicationUser;
  createdAt: string;
  lastUpdatedUtc: string;
  isDeleted: boolean;
  deletedAtUtc?: string;
  tasks?: CrmTask[];
}

export interface ActivityLog {
  id: number;
  type: ActivityType;
  message: string;
  occurredUtc: string;
  lead?: string;
  user?: string;
}

export interface DashboardReport {
  totalLeads: number;
  openPipeline: number;
  conversionRate: number;
  overdueTasks: number;
  workload: { user: string; leads: number }[];
  pipeline: { status: string; count: number; value: number }[];
}

export interface DealVelocity {
  month: string;
  createdCount: number;
  createdValue: number;
  wonCount: number;
  wonValue: number;
}

export interface FunnelStage {
  stage: string;
  count: number;
  value: number;
  percentage: number;
}

export interface ActivityDistribution {
  type: string;
  count: number;
  percentage: number;
}

export interface AnalyticsReport {
  monthlyDealVelocity: DealVelocity[];
  leadConversionFunnel: FunnelStage[];
  lostDeals: { count: number; value: number };
  salesActivityDistribution: ActivityDistribution[];
}

export interface JwtLoginResponse {
  token: string;
  email: string;
  fullName: string;
  roles: string[];
  expiresAtUtc: string;
}

export interface AuditLog {
  id: number;
  action: string;
  entityName: string;
  entityId: string;
  occurredUtc: string;
  actor?: string;
  oldValuesJson?: string;
  newValuesJson?: string;
}
