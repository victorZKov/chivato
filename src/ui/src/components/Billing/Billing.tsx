import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
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
  const { t, i18n } = useTranslation();
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
    alert(t("billing.redirectingCheckout", { planId }));
  };

  const handleCancelSubscription = async () => {
    if (window.confirm(t("billing.confirmCancel"))) {
      console.log("Cancelling subscription");
      alert(t("billing.cancelledMessage"));
    }
  };

  if (loading) {
    return <div className="billing-loading">{t("common.loading")}</div>;
  }

  return (
    <div className="billing">
      <div className="billing-header">
        <h1>{t("billing.title")}</h1>
        <p className="text-muted">{t("billing.subtitle")}</p>
      </div>

      <div className="billing-tabs">
        <button
          className={`tab-btn ${activeTab === "subscription" ? "active" : ""}`}
          onClick={() => setActiveTab("subscription")}
        >
          {t("billing.tabs.subscription")}
        </button>
        <button
          className={`tab-btn ${activeTab === "invoices" ? "active" : ""}`}
          onClick={() => setActiveTab("invoices")}
        >
          {t("billing.tabs.invoices")}
        </button>
        {isAdmin && (
          <button
            className={`tab-btn ${activeTab === "billing-info" ? "active" : ""}`}
            onClick={() => setActiveTab("billing-info")}
          >
            {t("billing.tabs.billingInfo")}
          </button>
        )}
      </div>

      <div className="billing-content">
        {activeTab === "subscription" && (
          <div className="subscription-tab">
            {/* Current Subscription */}
            {subscription?.hasSubscription && subscription.subscription && (
              <div className="current-subscription card">
                <h2>{t("billing.currentPlan")}</h2>
                <div className="subscription-details">
                  <div className="plan-badge">
                    <span className="plan-name">{subscription.plan?.name}</span>
                    <span className={`status-badge status-${subscription.subscription.status.toLowerCase()}`}>
                      {subscription.subscription.status}
                    </span>
                  </div>
                  <div className="subscription-info">
                    <p>
                      <strong>{t("billing.cycle")}:</strong> {subscription.subscription.billingCycle === "Monthly" ? t("billing.monthly") : t("billing.yearly")}
                    </p>
                    <p>
                      <strong>{t("billing.nextRenewal")}:</strong>{" "}
                      {new Date(subscription.subscription.currentPeriodEnd).toLocaleDateString(i18n.language === "es" ? "es-ES" : "en-US")}
                    </p>
                    {subscription.subscription.cancelAtPeriodEnd && (
                      <p className="cancel-notice">
                        ⚠️ {t("billing.cancelNotice")}
                      </p>
                    )}
                  </div>
                  {isAdmin && !subscription.subscription.cancelAtPeriodEnd && (
                    <button className="btn btn-outline" onClick={handleCancelSubscription}>
                      {t("billing.cancelSubscription")}
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* Plan Selector */}
            <div className="plan-selector">
              <div className="selector-header">
                <h2>{subscription?.hasSubscription ? t("billing.changePlan") : t("billing.selectPlan")}</h2>
                <div className="billing-cycle-toggle">
                  <button
                    className={`cycle-btn ${billingCycle === "Monthly" ? "active" : ""}`}
                    onClick={() => setBillingCycle("Monthly")}
                  >
                    {t("billing.monthly")}
                  </button>
                  <button
                    className={`cycle-btn ${billingCycle === "Yearly" ? "active" : ""}`}
                    onClick={() => setBillingCycle("Yearly")}
                  >
                    {t("billing.yearly")} <span className="discount">{t("billing.discount")}</span>
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
                      <h3>{t(`billing.plans.${plan.code}.name`)}</h3>
                      <p className="plan-description">{t(`billing.plans.${plan.code}.description`)}</p>
                    </div>
                    <div className="plan-price">
                      <span className="price">
                        €{billingCycle === "Monthly" ? plan.priceMonthly : plan.priceYearly / 12}
                      </span>
                      <span className="period">{t("billing.perMonth")}</span>
                      {billingCycle === "Yearly" && (
                        <p className="yearly-total">€{plan.priceYearly}{t("billing.perYear")}</p>
                      )}
                    </div>
                    <ul className="plan-features">
                      <li>✓ {t("billing.features.pipelines", { count: plan.limits.maxPipelines })}</li>
                      <li>✓ {t("billing.features.subscriptions", { count: plan.limits.maxSubscriptions })}</li>
                      <li>✓ {t("billing.features.resourceGroups", { count: plan.limits.maxResourceGroups })}</li>
                      <li>✓ {t("billing.features.retention", { count: plan.limits.retentionDays })}</li>
                      {plan.limits.aiAnalysisEnabled && <li>✓ {t("billing.features.aiAnalysis")}</li>}
                      {plan.limits.emailReportsEnabled && <li>✓ {t("billing.features.emailReports")}</li>}
                    </ul>
                    {isAdmin && (
                      <button
                        className={`btn ${subscription?.plan?.id === plan.id ? "btn-secondary" : "btn-primary"}`}
                        onClick={() => handleSelectPlan(plan.id)}
                        disabled={subscription?.plan?.id === plan.id}
                      >
                        {subscription?.plan?.id === plan.id ? t("billing.currentPlan") : t("billing.select")}
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
            <h2>{t("billing.invoices.title")}</h2>
            {invoices.length === 0 ? (
              <p className="no-invoices">{t("billing.invoices.noInvoices")}</p>
            ) : (
              <table className="invoices-table">
                <thead>
                  <tr>
                    <th>{t("billing.invoices.number")}</th>
                    <th>{t("billing.invoices.date")}</th>
                    <th>{t("billing.invoices.total")}</th>
                    <th>{t("billing.invoices.status")}</th>
                    <th>{t("billing.invoices.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {invoices.map((invoice) => (
                    <tr key={invoice.id}>
                      <td>{invoice.invoiceNumber}</td>
                      <td>{new Date(invoice.createdAt).toLocaleDateString(i18n.language === "es" ? "es-ES" : "en-US")}</td>
                      <td>€{invoice.total.toFixed(2)}</td>
                      <td>
                        <span className={`status-badge status-${invoice.status.toLowerCase()}`}>
                          {t(`billing.invoices.${invoice.status.toLowerCase()}`)}
                        </span>
                      </td>
                      <td>
                        <button className="btn btn-ghost btn-sm">{t("billing.invoices.download")}</button>
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
            <h2>{t("billing.billingInfo.title")}</h2>
            <form className="billing-form" onSubmit={(e) => e.preventDefault()}>
              <div className="form-row">
                <div className="form-group">
                  <label>{t("billing.billingInfo.email")}</label>
                  <input
                    type="email"
                    defaultValue={billingInfo?.email}
                    placeholder="billing@company.com"
                  />
                </div>
                <div className="form-group">
                  <label>{t("billing.billingInfo.companyName")}</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.companyName}
                    placeholder="My Company Ltd."
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>{t("billing.billingInfo.vatNumber")}</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.vatNumber}
                    placeholder="ESB12345678"
                  />
                </div>
                <div className="form-group">
                  <label>{t("billing.billingInfo.country")}</label>
                  <select defaultValue={billingInfo?.country || "ES"}>
                    <option value="ES">{t("billing.countries.ES")}</option>
                    <option value="PT">{t("billing.countries.PT")}</option>
                    <option value="FR">{t("billing.countries.FR")}</option>
                    <option value="DE">{t("billing.countries.DE")}</option>
                    <option value="IT">{t("billing.countries.IT")}</option>
                    <option value="NL">{t("billing.countries.NL")}</option>
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label>{t("billing.billingInfo.address")}</label>
                <input
                  type="text"
                  defaultValue={billingInfo?.addressLine1}
                  placeholder="123 Main Street"
                />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>{t("billing.billingInfo.city")}</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.city}
                    placeholder="Madrid"
                  />
                </div>
                <div className="form-group">
                  <label>{t("billing.billingInfo.postalCode")}</label>
                  <input
                    type="text"
                    defaultValue={billingInfo?.postalCode}
                    placeholder="28001"
                  />
                </div>
              </div>
              <button type="submit" className="btn btn-primary">
                {t("billing.billingInfo.saveChanges")}
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
