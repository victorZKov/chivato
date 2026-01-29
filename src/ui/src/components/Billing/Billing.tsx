import { useState, useEffect } from "react";
import { useRoles } from "../../hooks/useRoles";
import "./Billing.css";

interface Plan {
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

interface Subscription {
  id: string;
  status: string;
  billingCycle: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEndsAt?: string;
  cancelledAt?: string;
  cancelAtPeriodEnd: boolean;
}

interface SubscriptionData {
  hasSubscription: boolean;
  subscription?: Subscription;
  plan?: {
    id: string;
    code: string;
    name: string;
    priceMonthly: number;
    priceYearly: number;
  };
}

interface Invoice {
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

interface BillingInfo {
  hasBillingInfo: boolean;
  email?: string;
  companyName?: string;
  vatNumber?: string;
  country?: string;
  addressLine1?: string;
  city?: string;
  postalCode?: string;
}

type TabType = "subscription" | "invoices" | "billing-info";

export function Billing() {
  const { isAdmin } = useRoles();
  const [activeTab, setActiveTab] = useState<TabType>("subscription");
  const [plans, setPlans] = useState<Plan[]>([]);
  const [subscription, setSubscription] = useState<SubscriptionData | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [billingInfo, setBillingInfo] = useState<BillingInfo | null>(null);
  const [billingCycle, setBillingCycle] = useState<"Monthly" | "Yearly">("Monthly");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      // In production, these would be actual API calls
      // For now, using mock data
      setPlans(mockPlans);
      setSubscription(mockSubscription);
      setInvoices(mockInvoices);
      setBillingInfo(mockBillingInfo);
    } catch (error) {
      console.error("Error loading billing data:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSelectPlan = async (planId: string) => {
    // Create checkout session
    console.log("Selected plan:", planId, "Cycle:", billingCycle);
    // In production: redirect to Mollie checkout
    alert(`Redirigiendo a checkout para el plan ${planId}...`);
  };

  const handleCancelSubscription = async () => {
    if (window.confirm("¿Estás seguro de que quieres cancelar tu suscripción?")) {
      console.log("Cancelling subscription");
      alert("Suscripción cancelada al final del período actual");
    }
  };

  if (loading) {
    return <div className="billing-loading">Cargando...</div>;
  }

  return (
    <div className="billing">
      <div className="billing-header">
        <h1>Billing</h1>
        <p className="text-muted">Gestiona tu suscripción y facturación</p>
      </div>

      <div className="billing-tabs">
        <button
          className={`tab-btn ${activeTab === "subscription" ? "active" : ""}`}
          onClick={() => setActiveTab("subscription")}
        >
          Suscripción
        </button>
        <button
          className={`tab-btn ${activeTab === "invoices" ? "active" : ""}`}
          onClick={() => setActiveTab("invoices")}
        >
          Facturas
        </button>
        {isAdmin && (
          <button
            className={`tab-btn ${activeTab === "billing-info" ? "active" : ""}`}
            onClick={() => setActiveTab("billing-info")}
          >
            Datos de Facturación
          </button>
        )}
      </div>

      <div className="billing-content">
        {activeTab === "subscription" && (
          <div className="subscription-tab">
            {/* Current Subscription */}
            {subscription?.hasSubscription && subscription.subscription && (
              <div className="current-subscription card">
                <h2>Plan Actual</h2>
                <div className="subscription-details">
                  <div className="plan-badge">
                    <span className="plan-name">{subscription.plan?.name}</span>
                    <span className={`status-badge status-${subscription.subscription.status.toLowerCase()}`}>
                      {subscription.subscription.status}
                    </span>
                  </div>
                  <div className="subscription-info">
                    <p>
                      <strong>Ciclo:</strong> {subscription.subscription.billingCycle === "Monthly" ? "Mensual" : "Anual"}
                    </p>
                    <p>
                      <strong>Próxima renovación:</strong>{" "}
                      {new Date(subscription.subscription.currentPeriodEnd).toLocaleDateString("es-ES")}
                    </p>
                    {subscription.subscription.cancelAtPeriodEnd && (
                      <p className="cancel-notice">
                        ⚠️ Tu suscripción se cancelará al final del período actual
                      </p>
                    )}
                  </div>
                  {isAdmin && !subscription.subscription.cancelAtPeriodEnd && (
                    <button className="btn btn-outline" onClick={handleCancelSubscription}>
                      Cancelar Suscripción
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* Plan Selector */}
            <div className="plan-selector">
              <div className="selector-header">
                <h2>{subscription?.hasSubscription ? "Cambiar Plan" : "Selecciona un Plan"}</h2>
                <div className="billing-cycle-toggle">
                  <button
                    className={`cycle-btn ${billingCycle === "Monthly" ? "active" : ""}`}
                    onClick={() => setBillingCycle("Monthly")}
                  >
                    Mensual
                  </button>
                  <button
                    className={`cycle-btn ${billingCycle === "Yearly" ? "active" : ""}`}
                    onClick={() => setBillingCycle("Yearly")}
                  >
                    Anual <span className="discount">-20%</span>
                  </button>
                </div>
              </div>

              <div className="plans-grid">
                {plans.map((plan) => (
                  <div
                    key={plan.id}
                    className={`plan-card ${subscription?.plan?.id === plan.id ? "current" : ""}`}
                  >
                    <div className="plan-header">
                      <h3>{plan.name}</h3>
                      <p className="plan-description">{plan.description}</p>
                    </div>
                    <div className="plan-price">
                      <span className="price">
                        €{billingCycle === "Monthly" ? plan.priceMonthly : plan.priceYearly / 12}
                      </span>
                      <span className="period">/mes</span>
                      {billingCycle === "Yearly" && (
                        <p className="yearly-total">€{plan.priceYearly}/año</p>
                      )}
                    </div>
                    <ul className="plan-features">
                      <li>✓ {plan.limits.maxPipelines} pipelines</li>
                      <li>✓ {plan.limits.maxSubscriptions} subscripciones Azure</li>
                      <li>✓ {plan.limits.maxResourceGroups} resource groups</li>
                      <li>✓ {plan.limits.retentionDays} días de retención</li>
                      {plan.limits.aiAnalysisEnabled && <li>✓ Análisis con AI</li>}
                      {plan.limits.emailReportsEnabled && <li>✓ Reportes por email</li>}
                    </ul>
                    {isAdmin && (
                      <button
                        className={`btn ${subscription?.plan?.id === plan.id ? "btn-secondary" : "btn-primary"}`}
                        onClick={() => handleSelectPlan(plan.id)}
                        disabled={subscription?.plan?.id === plan.id}
                      >
                        {subscription?.plan?.id === plan.id ? "Plan Actual" : "Seleccionar"}
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {activeTab === "invoices" && (
          <div className="invoices-tab">
            <h2>Historial de Facturas</h2>
            {invoices.length === 0 ? (
              <p className="no-invoices">No hay facturas disponibles</p>
            ) : (
              <table className="invoices-table">
                <thead>
                  <tr>
                    <th>Número</th>
                    <th>Fecha</th>
                    <th>Total</th>
                    <th>Estado</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {invoices.map((invoice) => (
                    <tr key={invoice.id}>
                      <td>{invoice.invoiceNumber}</td>
                      <td>{new Date(invoice.createdAt).toLocaleDateString("es-ES")}</td>
                      <td>€{invoice.total.toFixed(2)}</td>
                      <td>
                        <span className={`status-badge status-${invoice.status.toLowerCase()}`}>
                          {invoice.status}
                        </span>
                      </td>
                      <td>
                        <button className="btn btn-ghost btn-sm">Descargar</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {activeTab === "billing-info" && isAdmin && (
          <div className="billing-info-tab">
            <h2>Datos de Facturación</h2>
            <form className="billing-form" onSubmit={(e) => e.preventDefault()}>
              <div className="form-row">
                <div className="form-group">
                  <label>Email de facturación</label>
                  <input
                    type="email"
                    defaultValue={billingInfo?.email}
                    placeholder="facturacion@empresa.com"
                  />
                </div>
                <div className="form-group">
                  <label>Nombre de empresa</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.companyName}
                    placeholder="Mi Empresa S.L."
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>NIF/VAT</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.vatNumber}
                    placeholder="ESB12345678"
                  />
                </div>
                <div className="form-group">
                  <label>País</label>
                  <select defaultValue={billingInfo?.country || "ES"}>
                    <option value="ES">España</option>
                    <option value="PT">Portugal</option>
                    <option value="FR">Francia</option>
                    <option value="DE">Alemania</option>
                    <option value="IT">Italia</option>
                    <option value="NL">Países Bajos</option>
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label>Dirección</label>
                <input
                  type="text"
                  defaultValue={billingInfo?.addressLine1}
                  placeholder="Calle Principal 123"
                />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>Ciudad</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.city}
                    placeholder="Madrid"
                  />
                </div>
                <div className="form-group">
                  <label>Código Postal</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.postalCode}
                    placeholder="28001"
                  />
                </div>
              </div>
              <button type="submit" className="btn btn-primary">
                Guardar Cambios
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}

// Mock data for development
const mockPlans: Plan[] = [
  {
    id: "plan-starter",
    code: "starter",
    name: "Starter",
    description: "Para equipos pequeños",
    priceMonthly: 29,
    priceYearly: 290,
    currency: "EUR",
    limits: {
      maxPipelines: 5,
      maxSubscriptions: 2,
      maxResourceGroups: 10,
      retentionDays: 30,
      aiAnalysisEnabled: false,
      emailReportsEnabled: true,
    },
  },
  {
    id: "plan-pro",
    code: "pro",
    name: "Pro",
    description: "Para equipos en crecimiento",
    priceMonthly: 79,
    priceYearly: 790,
    currency: "EUR",
    limits: {
      maxPipelines: 20,
      maxSubscriptions: 5,
      maxResourceGroups: 50,
      retentionDays: 90,
      aiAnalysisEnabled: true,
      emailReportsEnabled: true,
    },
  },
  {
    id: "plan-enterprise",
    code: "enterprise",
    name: "Enterprise",
    description: "Para grandes organizaciones",
    priceMonthly: 199,
    priceYearly: 1990,
    currency: "EUR",
    limits: {
      maxPipelines: 100,
      maxSubscriptions: 20,
      maxResourceGroups: 200,
      retentionDays: 365,
      aiAnalysisEnabled: true,
      emailReportsEnabled: true,
    },
  },
];

const mockSubscription: SubscriptionData = {
  hasSubscription: true,
  subscription: {
    id: "sub-123",
    status: "Active",
    billingCycle: "Monthly",
    currentPeriodStart: "2026-01-01T00:00:00Z",
    currentPeriodEnd: "2026-02-01T00:00:00Z",
    cancelAtPeriodEnd: false,
  },
  plan: {
    id: "plan-pro",
    code: "pro",
    name: "Pro",
    priceMonthly: 79,
    priceYearly: 790,
  },
};

const mockInvoices: Invoice[] = [
  {
    id: "inv-1",
    invoiceNumber: "CHIVATO-2026-00001",
    status: "Paid",
    subtotal: 79,
    tax: 16.59,
    total: 95.59,
    currency: "EUR",
    paidAt: "2026-01-01T10:00:00Z",
    createdAt: "2026-01-01T00:00:00Z",
  },
  {
    id: "inv-2",
    invoiceNumber: "CHIVATO-2025-00012",
    status: "Paid",
    subtotal: 79,
    tax: 16.59,
    total: 95.59,
    currency: "EUR",
    paidAt: "2025-12-01T10:00:00Z",
    createdAt: "2025-12-01T00:00:00Z",
  },
];

const mockBillingInfo: BillingInfo = {
  hasBillingInfo: true,
  email: "billing@empresa.com",
  companyName: "Mi Empresa S.L.",
  vatNumber: "ESB12345678",
  country: "ES",
  addressLine1: "Calle Principal 123",
  city: "Madrid",
  postalCode: "28001",
};
