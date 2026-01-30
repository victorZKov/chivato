import { msalInstance } from "../auth/authConfig";

const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:7071/api";

interface RequestOptions extends RequestInit {
  authenticated?: boolean;
}

async function getAccessToken(): Promise<string | null> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) return null;

  try {
    const response = await msalInstance.acquireTokenSilent({
      scopes: [`api://${import.meta.env.VITE_ENTRA_CLIENT_ID}/access_as_user`],
      account: accounts[0],
    });
    return response.accessToken;
  } catch (error) {
    console.error("Error acquiring token:", error);
    return null;
  }
}

async function apiRequest<T>(
  endpoint: string,
  options: RequestOptions = {}
): Promise<T> {
  const { authenticated = true, ...fetchOptions } = options;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(fetchOptions.headers as Record<string, string>),
  };

  if (authenticated) {
    const token = await getAccessToken();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...fetchOptions,
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(error.message || error.error || `HTTP ${response.status}`);
  }

  return response.json();
}

// Configuration API
export const configApi = {
  // Timer
  getTimerConfig: () => apiRequest<TimerConfig>("/config/timer"),
  updateTimerConfig: (config: Partial<TimerConfig>) =>
    apiRequest("/config/timer", { method: "PUT", body: JSON.stringify(config) }),

  // Azure Connections
  getAzureConnections: () => apiRequest<AzureConnection[]>("/config/azure"),
  createAzureConnection: (data: CreateAzureConnectionInput) =>
    apiRequest("/config/azure", { method: "POST", body: JSON.stringify(data) }),
  updateAzureConnection: (id: string, data: Partial<CreateAzureConnectionInput>) =>
    apiRequest(`/config/azure/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  deleteAzureConnection: (id: string) =>
    apiRequest(`/config/azure/${id}`, { method: "DELETE" }),
  testAzureConnection: (id: string) =>
    apiRequest<{ success: boolean }>(`/config/azure/${id}/test`, { method: "POST" }),

  // ADO Connections
  getAdoConnections: () => apiRequest<AdoConnection[]>("/config/ado"),
  createAdoConnection: (data: CreateAdoConnectionInput) =>
    apiRequest("/config/ado", { method: "POST", body: JSON.stringify(data) }),
  updateAdoConnection: (id: string, data: Partial<CreateAdoConnectionInput>) =>
    apiRequest(`/config/ado/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  deleteAdoConnection: (id: string) =>
    apiRequest(`/config/ado/${id}`, { method: "DELETE" }),
  testAdoConnection: (id: string) =>
    apiRequest<{ success: boolean }>(`/config/ado/${id}/test`, { method: "POST" }),

  // AI Connection
  getAiConnection: () => apiRequest<AiConnection | null>("/config/ai"),
  saveAiConnection: (data: CreateAiConnectionInput) =>
    apiRequest("/config/ai", { method: "POST", body: JSON.stringify(data) }),
  testAiConnection: () =>
    apiRequest<{ success: boolean }>("/config/ai/test", { method: "POST" }),

  // Email Recipients
  getEmailRecipients: () => apiRequest<EmailRecipient[]>("/config/recipients"),
  createEmailRecipient: (data: CreateEmailRecipientInput) =>
    apiRequest("/config/recipients", { method: "POST", body: JSON.stringify(data) }),
  updateEmailRecipient: (id: string, data: Partial<CreateEmailRecipientInput>) =>
    apiRequest(`/config/recipients/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  deleteEmailRecipient: (id: string) =>
    apiRequest(`/config/recipients/${id}`, { method: "DELETE" }),
};

// Pipelines API
export const pipelinesApi = {
  getPipelines: () => apiRequest<Pipeline[]>("/pipelines"),
  createPipelines: (data: CreatePipelinesInput) =>
    apiRequest("/pipelines", { method: "POST", body: JSON.stringify(data) }),
  updatePipeline: (id: string, data: Partial<UpdatePipelineInput>) =>
    apiRequest(`/pipelines/${id}`, { method: "PUT", body: JSON.stringify(data) }),
  deletePipeline: (id: string) =>
    apiRequest(`/pipelines/${id}`, { method: "DELETE" }),
  scanPipeline: (id: string) =>
    apiRequest<ScanResult>(`/pipelines/${id}/scan`, { method: "POST" }),

  // ADO data (for pipeline selection)
  getAdoProjects: (connectionId: string) =>
    apiRequest<string[]>(`/ado/${connectionId}/projects`),
  getAdoPipelines: (connectionId: string, project: string) =>
    apiRequest<{ id: string; name: string }[]>(`/ado/${connectionId}/projects/${encodeURIComponent(project)}/pipelines`),
};

// Paginated response type
interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// Drift API
export const driftApi = {
  getDriftRecords: async (params?: { from?: string; to?: string; severity?: string }): Promise<DriftRecord[]> => {
    const query = new URLSearchParams();
    if (params?.from) query.set("from", params.from);
    if (params?.to) query.set("to", params.to);
    if (params?.severity) query.set("severity", params.severity);
    const result = await apiRequest<PagedResult<DriftRecord>>(`/drift?${query}`);
    return result.items;
  },
  getDriftStats: () => apiRequest<DriftStats>("/drift/stats"),
  triggerAnalysis: () =>
    apiRequest<{ message: string }>("/drift/analyze", { method: "POST" }),
};

// Billing API
export const billingApi = {
  getPlans: () => apiRequest<Plan[]>("/billing/plans", { authenticated: false }),
  getSubscription: () => apiRequest<SubscriptionData>("/billing/subscription"),
  createCheckout: (data: CheckoutInput) =>
    apiRequest<CheckoutSession>("/billing/checkout", { method: "POST", body: JSON.stringify(data) }),
  cancelSubscription: () =>
    apiRequest("/billing/subscription/cancel", { method: "POST" }),
  getInvoices: () => apiRequest<Invoice[]>("/billing/invoices"),
  getBillingInfo: () => apiRequest<BillingInfo>("/billing/info"),
  updateBillingInfo: (data: BillingInfoInput) =>
    apiRequest("/billing/info", { method: "PUT", body: JSON.stringify(data) }),
};

// Types
export interface TimerConfig {
  intervalHours: number;
  isEnabled: boolean;
  nextRunAt?: string;
}

export interface AzureConnection {
  id: string;
  name: string;
  tenantId: string;
  subscriptionId: string;
  clientId: string;
  status: "Connected" | "Error" | "Unknown";
  lastTestedAt?: string;
  lastTestError?: string;
  isDefault: boolean;
}

export interface CreateAzureConnectionInput {
  name: string;
  tenantId: string;
  subscriptionId: string;
  clientId: string;
  clientSecret: string;
}

export interface AdoConnection {
  id: string;
  name: string;
  organizationUrl: string;
  authType: "PAT" | "OAuth";
  status: "active" | "expiring" | "expired";
  expiresAt?: string;
}

export interface CreateAdoConnectionInput {
  name: string;
  organizationUrl: string;
  pat: string;
  expiresAt?: string;
}

export interface AiConnection {
  id: string;
  name: string;
  endpoint: string;
  deploymentName: string;
  status: "active" | "inactive";
}

export interface CreateAiConnectionInput {
  name: string;
  endpoint: string;
  deploymentName: string;
  apiKey: string;
}

export interface EmailRecipient {
  id: string;
  email: string;
  notifyOn: "always" | "drift_only" | "weekly";
  isActive: boolean;
}

export interface CreateEmailRecipientInput {
  email: string;
  notifyOn: "always" | "drift_only" | "weekly";
}

export interface Pipeline {
  id: string;
  pipelineName: string;
  pipelineId: string;
  projectName: string;
  organizationUrl: string;
  adoConnectionId: string;
  adoConnectionName: string;
  azureConnectionId: string;
  azureConnectionName: string;
  isActive: boolean;
  lastScanAt?: string;
  driftCount?: number;
}

export interface CreatePipelinesInput {
  adoConnectionId: string;
  azureConnectionId: string;
  projectName: string;
  pipelineIds: string[];
}

export interface UpdatePipelineInput {
  isActive?: boolean;
  azureConnectionId?: string;
}

export interface ScanResult {
  success: boolean;
  driftCount: number;
  message?: string;
}

export interface DriftRecord {
  id: string;
  pipelineId: string;
  pipelineName: string;
  resourceId: string;
  resourceType: string;
  resourceName: string;
  property: string;
  expectedValue: string;
  actualValue: string;
  severity: "CRITICAL" | "HIGH" | "MEDIUM" | "LOW" | "INFO";
  category: string;
  description: string;
  recommendation: string;
  detectedAt: string;
}

export interface DriftStats {
  total: number;
  critical: number;
  high: number;
  medium: number;
  low: number;
  lastAnalysis?: string;
}

export interface Plan {
  id: string;
  code: string;
  name: string;
  description: string;
  priceMonthly: number;
  priceYearly: number;
  currency: string;
  limits: {
    maxPipelines: number;
    maxSubscriptions: number;
    maxResourceGroups: number;
    retentionDays: number;
    aiAnalysisEnabled: boolean;
    emailReportsEnabled: boolean;
  };
}

export interface SubscriptionData {
  hasSubscription: boolean;
  subscription?: {
    id: string;
    status: string;
    billingCycle: string;
    currentPeriodStart: string;
    currentPeriodEnd: string;
    cancelAtPeriodEnd: boolean;
  };
  plan?: {
    id: string;
    code: string;
    name: string;
    priceMonthly: number;
    priceYearly: number;
  };
}

export interface CheckoutInput {
  planId: string;
  billingCycle: "Monthly" | "Yearly";
  successUrl?: string;
  cancelUrl?: string;
}

export interface CheckoutSession {
  sessionId: string;
  checkoutUrl: string;
  status: string;
}

export interface Invoice {
  id: string;
  invoiceNumber: string;
  status: string;
  subtotal: number;
  tax: number;
  total: number;
  currency: string;
  paidAt?: string;
  createdAt: string;
}

export interface BillingInfo {
  hasBillingInfo: boolean;
  email?: string;
  companyName?: string;
  vatNumber?: string;
  country?: string;
  addressLine1?: string;
  city?: string;
  postalCode?: string;
}

export interface BillingInfoInput {
  email?: string;
  companyName?: string;
  vatNumber?: string;
  country?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
}
