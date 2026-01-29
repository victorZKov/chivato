import { useTranslation } from "react-i18next";
import { useAuth } from "../../hooks/useAuth";
import "./Dashboard.css";

interface MetricCardProps {
  title: string;
  value: number;
  severity: "critical" | "high" | "medium" | "low" | "ok";
  icon: string;
}

function MetricCard({ title, value, severity, icon }: MetricCardProps) {
  return (
    <div className={`metric-card metric-${severity}`}>
      <span className="metric-icon">{icon}</span>
      <div className="metric-content">
        <span className="metric-value">{value}</span>
        <span className="metric-title">{title}</span>
      </div>
    </div>
  );
}

interface DriftItemProps {
  severity: "critical" | "high" | "medium" | "low";
  description: string;
  pipeline: string;
  timestamp: string;
}

function DriftItem({ severity, description, pipeline, timestamp }: DriftItemProps) {
  const { t } = useTranslation();

  const severityLabels = {
    critical: t("severity.critical", "CRITICAL"),
    high: t("severity.high", "HIGH"),
    medium: t("severity.medium", "MEDIUM"),
    low: t("severity.low", "LOW"),
  };

  return (
    <div className="drift-item">
      <span className={`badge badge-${severity}`}>{severityLabels[severity]}</span>
      <span className="drift-description">{description}</span>
      <span className="drift-pipeline">{pipeline}</span>
      <span className="drift-timestamp">{timestamp}</span>
    </div>
  );
}

export function Dashboard() {
  const { t } = useTranslation();
  const { user } = useAuth();

  // TODO: Replace with real data from API
  const mockMetrics = {
    critical: 2,
    high: 5,
    medium: 12,
    ok: 156,
  };

  const mockDriftItems: DriftItemProps[] = [
    {
      severity: "critical",
      description: t("driftExamples.appServiceSku"),
      pipeline: "infra-prod",
      timestamp: t("time.hoursAgo", { count: 2 }),
    },
    {
      severity: "high",
      description: t("driftExamples.storageRedundancy"),
      pipeline: "storage-main",
      timestamp: t("time.hoursAgo", { count: 4 }),
    },
    {
      severity: "medium",
      description: t("driftExamples.missingTags"),
      pipeline: "infra-dev",
      timestamp: t("time.hoursAgo", { count: 6 }),
    },
    {
      severity: "low",
      description: t("driftExamples.diagnosticsDisabled"),
      pipeline: "monitoring",
      timestamp: t("time.hoursAgo", { count: 12 }),
    },
  ];

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <h1>{t("dashboard.title")}</h1>
        <p className="text-muted">
          {t("auth.welcome")}, {user?.name}. {t("dashboard.stats.lastScan")}: {t("time.minutesAgo", { count: 30 })}
        </p>
      </div>

      <div className="metrics-grid">
        <MetricCard
          title={t("severity.critical", "Critical")}
          value={mockMetrics.critical}
          severity="critical"
          icon="🔴"
        />
        <MetricCard
          title={t("severity.high", "High")}
          value={mockMetrics.high}
          severity="high"
          icon="🟠"
        />
        <MetricCard
          title={t("severity.medium", "Medium")}
          value={mockMetrics.medium}
          severity="medium"
          icon="🟡"
        />
        <MetricCard title="OK" value={mockMetrics.ok} severity="ok" icon="🟢" />
      </div>

      <section className="drift-section">
        <div className="section-header">
          <h2>{t("dashboard.recentDrifts")}</h2>
          <button className="btn btn-outline">{t("dashboard.viewAll")}</button>
        </div>

        <div className="drift-list">
          {mockDriftItems.map((item, index) => (
            <DriftItem key={index} {...item} />
          ))}
        </div>
      </section>

      <section className="credentials-section">
        <div className="section-header">
          <h2>{t("credentials.title", "Credentials Status")}</h2>
        </div>

        <div className="credentials-grid">
          <div className="credential-card">
            <span className="credential-name">Azure SP (Production)</span>
            <span className="status-ok">✓ {t("credentials.daysRemaining", { count: 45 })}</span>
          </div>
          <div className="credential-card">
            <span className="credential-name">ADO PAT</span>
            <span className="status-warning">⚠ {t("credentials.daysRemaining", { count: 12 })}</span>
          </div>
          <div className="credential-card">
            <span className="credential-name">Azure AI</span>
            <span className="status-ok">✓ {t("common.active")}</span>
          </div>
        </div>
      </section>
    </div>
  );
}
