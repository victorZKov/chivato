import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "../../hooks/useNavigate";
import { useAuth } from "../../hooks/useAuth";
import { driftApi, pipelinesApi, configApi } from "../../services/api";
import type { DriftStats, DriftRecord, Pipeline, AzureConnection, AdoConnection } from "../../services/api";
import { AnalysisProgress } from "../common/AnalysisProgress";
import "./Dashboard.css";

interface MetricCardProps {
  title: string;
  value: number;
  severity: "critical" | "high" | "medium" | "low" | "ok";
  icon: string;
  loading?: boolean;
}

function MetricCard({ title, value, severity, icon, loading }: MetricCardProps) {
  return (
    <div className={`metric-card metric-${severity}`}>
      <span className="metric-icon">{icon}</span>
      <div className="metric-content">
        <span className="metric-value">{loading ? "..." : value}</span>
        <span className="metric-title">{title}</span>
      </div>
    </div>
  );
}

interface DriftItemProps {
  id: string;
  severity: "critical" | "high" | "medium" | "low";
  description: string;
  pipeline: string;
  timestamp: string;
}

function DriftItem({ id, severity, description, pipeline, timestamp }: DriftItemProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const severityLabels = {
    critical: t("severity.critical", "CRITICAL"),
    high: t("severity.high", "HIGH"),
    medium: t("severity.medium", "MEDIUM"),
    low: t("severity.low", "LOW"),
  };

  return (
    <div className="drift-item" onClick={() => navigate(`/drift/${id}`)} style={{ cursor: "pointer" }}>
      <span className={`badge badge-${severity}`}>{severityLabels[severity]}</span>
      <span className="drift-description">{description}</span>
      <span className="drift-pipeline">{pipeline}</span>
      <span className="drift-timestamp">{timestamp}</span>
    </div>
  );
}

function formatTimeAgo(date: string, t: ReturnType<typeof useTranslation>["t"]): string {
  const now = new Date();
  const past = new Date(date);
  const diffMs = now.getTime() - past.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 60) return t("time.minutesAgo", { count: diffMins });
  if (diffHours < 24) return t("time.hoursAgo", { count: diffHours });
  if (diffDays < 7) return t("time.daysAgo", { count: diffDays });
  return t("time.weeksAgo", { count: Math.floor(diffDays / 7) });
}

export function Dashboard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [stats, setStats] = useState<DriftStats | null>(null);
  const [recentDrifts, setRecentDrifts] = useState<DriftRecord[]>([]);
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [azureConnections, setAzureConnections] = useState<AzureConnection[]>([]);
  const [adoConnections, setAdoConnections] = useState<AdoConnection[]>([]);

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    setLoading(true);
    setError(null);

    try {
      const [statsData, driftsData, pipelinesData, azureData, adoData] = await Promise.all([
        driftApi.getDriftStats(),
        driftApi.getDriftRecords(),
        pipelinesApi.getPipelines(),
        configApi.getAzureConnections(),
        configApi.getAdoConnections(),
      ]);

      setStats(statsData);
      setRecentDrifts(driftsData.slice(0, 5)); // Show only 5 most recent
      setPipelines(pipelinesData);
      setAzureConnections(azureData);
      setAdoConnections(adoData);
    } catch (err) {
      console.error("Error loading dashboard data:", err);
      setError(err instanceof Error ? err.message : "Failed to load data");
    } finally {
      setLoading(false);
    }
  };

  const getCredentialStatus = (conn: AzureConnection | AdoConnection) => {
    if (conn.status === "expired") {
      return { className: "status-error", text: t("credentials.expired") };
    }
    if (conn.status === "expiring" && conn.expiresAt) {
      return { className: "status-warning", text: `⚠ ${conn.expiresAt}` };
    }
    return { className: "status-ok", text: `✓ ${t("common.active")}` };
  };

  const activePipelines = pipelines.filter((p) => p.isActive).length;

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <h1>{t("dashboard.title")}</h1>
        <p className="text-muted">
          {t("auth.welcome")}, {user?.name}.{" "}
          {stats?.lastAnalysis
            ? `${t("dashboard.stats.lastScan")}: ${formatTimeAgo(stats.lastAnalysis, t)}`
            : t("dashboard.stats.lastScan") + ": " + t("pipelines.never")}
        </p>
      </div>

      {error && (
        <div className="error-banner">
          <span>{error}</span>
          <button className="btn btn-sm" onClick={loadDashboardData}>
            {t("common.retry")}
          </button>
        </div>
      )}

      <AnalysisProgress className="dashboard-analysis-progress" />

      <div className="metrics-grid">
        <MetricCard
          title={t("severity.critical", "Critical")}
          value={stats?.critical ?? 0}
          severity="critical"
          icon="🔴"
          loading={loading}
        />
        <MetricCard
          title={t("severity.high", "High")}
          value={stats?.high ?? 0}
          severity="high"
          icon="🟠"
          loading={loading}
        />
        <MetricCard
          title={t("severity.medium", "Medium")}
          value={stats?.medium ?? 0}
          severity="medium"
          icon="🟡"
          loading={loading}
        />
        <MetricCard
          title={t("dashboard.stats.activePipelines")}
          value={activePipelines}
          severity="ok"
          icon="🟢"
          loading={loading}
        />
      </div>

      <section className="drift-section">
        <div className="section-header">
          <h2>{t("dashboard.recentDrifts")}</h2>
          <button className="btn btn-outline" onClick={() => navigate("/drift")}>
            {t("dashboard.viewAll")}
          </button>
        </div>

        {loading ? (
          <div className="loading-placeholder">{t("common.loading")}</div>
        ) : recentDrifts.length === 0 ? (
          <p className="text-muted">{t("dashboard.noDrifts")}</p>
        ) : (
          <div className="drift-list">
            {recentDrifts.map((drift) => (
              <DriftItem
                key={drift.id}
                id={drift.id}
                severity={drift.severity.toLowerCase() as "critical" | "high" | "medium" | "low"}
                description={drift.description || `${drift.resourceName}: ${drift.property}`}
                pipeline={drift.pipelineName}
                timestamp={formatTimeAgo(drift.detectedAt, t)}
              />
            ))}
          </div>
        )}
      </section>

      <section className="credentials-section">
        <div className="section-header">
          <h2>{t("credentials.title", "Credentials Status")}</h2>
        </div>

        {loading ? (
          <div className="loading-placeholder">{t("common.loading")}</div>
        ) : (
          <div className="credentials-grid">
            {azureConnections.map((conn) => {
              const status = getCredentialStatus(conn);
              return (
                <div key={conn.id} className="credential-card">
                  <span className="credential-name">{conn.name}</span>
                  <span className="credential-type">{t("credentials.types.azure")}</span>
                  <span className={status.className}>{status.text}</span>
                </div>
              );
            })}
            {adoConnections.map((conn) => {
              const status = getCredentialStatus(conn);
              return (
                <div key={conn.id} className="credential-card">
                  <span className="credential-name">{conn.name}</span>
                  <span className="credential-type">{t("credentials.types.ado")}</span>
                  <span className={status.className}>{status.text}</span>
                </div>
              );
            })}
            {azureConnections.length === 0 && adoConnections.length === 0 && (
              <p className="text-muted">{t("credentials.noCredentials")}</p>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
