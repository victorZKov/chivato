import { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "../../hooks/useNavigate";
import { useRoles } from "../../hooks/useRoles";
import { useModalContext } from "../../contexts/ModalContext";
import { useToast } from "../common/Toast";
import { useNotifications } from "../../contexts/NotificationsContext";
import { pipelinesApi, configApi, scansApi, driftApi } from "../../services/api";
import type { Pipeline, UpdatePipelineInput, AdoConnection, ScanLogItem, ScanDriftItem, DriftRecord } from "../../services/api";
import { Modal } from "../common/Modal";
import { MetricCard } from "../common/MetricCard";
import { formatTimeAgo } from "../../utils/formatTime";
import "./PipelineDetail.css";

// SignalR event types - must match backend events
interface AnalysisProgressEvent {
  type: string;
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  stage: string;
  progress: number;
  message: string;
  timestamp: string;
}

interface AnalysisCompletedEvent {
  type: string;
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  status?: string;
  driftCount?: number;
  overallRisk?: string;
  summary?: {
    totalDrifts: number;
    critical: number;
    high: number;
    medium: number;
    low: number;
    durationSeconds: number;
  };
  timestamp: string;
}

interface AnalysisFailedEvent {
  type: string;
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  error: string;
  timestamp: string;
}

interface PipelineDetailData extends Pipeline {
  createdAt?: string;
  updatedAt?: string;
}

interface PipelineDetailProps {
  id: string;
}

export function PipelineDetail({ id }: PipelineDetailProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { isAdmin } = useRoles();
  const modal = useModalContext();
  const toast = useToast();

  const [pipeline, setPipeline] = useState<PipelineDetailData | null>(null);
  const [loading, setLoading] = useState(true);
  const [scanning, setScanning] = useState(false);
  const [activeTab, setActiveTab] = useState<"overview" | "drifts" | "scans">("overview");
  const [toggling, setToggling] = useState(false);

  // SignalR for real-time updates (via NotificationsContext)
  const { onProgress, onCompleted, onFailed } = useNotifications();

  // Connection edit state
  const [editingConnection, setEditingConnection] = useState(false);
  const [adoConnections, setAdoConnections] = useState<AdoConnection[]>([]);
  const [selectedAdoConnectionId, setSelectedAdoConnectionId] = useState("");
  const [editPlanOnlyParam, setEditPlanOnlyParam] = useState("");
  const [savingConnection, setSavingConnection] = useState(false);

  // Scans state
  const [scans, setScans] = useState<ScanLogItem[]>([]);
  const [scansLoading, setScansLoading] = useState(false);
  const [scansTotal, setScansTotal] = useState(0);

  // Drifts state
  const [drifts, setDrifts] = useState<DriftRecord[]>([]);
  const [driftsLoading, setDriftsLoading] = useState(false);

  // Scan detail modal state
  const [selectedScan, setSelectedScan] = useState<ScanLogItem | null>(null);
  const [scanDrifts, setScanDrifts] = useState<ScanDriftItem[]>([]);
  const [scanDriftsLoading, setScanDriftsLoading] = useState(false);

  // Drift detail modal state
  const [selectedDrift, setSelectedDrift] = useState<DriftRecord | null>(null);

  // Define fetch functions first (before useEffects that use them)
  const fetchScans = useCallback(async () => {
    setScansLoading(true);
    try {
      const result = await scansApi.getScans(id);
      setScans(result.items);
      setScansTotal(result.total);
    } catch (error) {
      console.error("Failed to fetch scans:", error);
    } finally {
      setScansLoading(false);
    }
  }, [id]);

  const fetchDrifts = useCallback(async () => {
    setDriftsLoading(true);
    try {
      const result = await driftApi.getDriftRecords({ pipelineId: id });
      setDrifts(result);
    } catch (error) {
      console.error("Failed to fetch drifts:", error);
    } finally {
      setDriftsLoading(false);
    }
  }, [id]);

  const fetchPipeline = useCallback(async () => {
    try {
      const data = await pipelinesApi.getPipeline(id);
      const pipelineData = data as unknown as PipelineDetailData;
      setPipeline(pipelineData);
      // If pipeline is currently scanning, set scanning state
      if (pipelineData.lastScanStatus?.toLowerCase() === "running") {
        setScanning(true);
      }
    } catch (error: unknown) {
      console.error("Failed to fetch pipeline:", error);
      if (error instanceof Error && error.message.includes("404")) {
        navigate("/pipelines");
      }
    } finally {
      setLoading(false);
    }
  }, [id, navigate]);

  // Listen for analysis events from SignalR
  useEffect(() => {
    const handleAnalysisProgress = (event: AnalysisProgressEvent) => {
      if (event.pipelineId === id) {
        setPipeline(prev => prev ? {
          ...prev,
          lastScanStatus: "Running",
          lastScanAt: event.timestamp,
          lastScanError: undefined
        } : null);
        setScanning(true);
        // Only show toast for initial progress (stage starting)
        if (event.progress === 0) {
          toast.info(t("pipelines.detail.scanStarted", "Scan started: ") + event.stage);
        }
      }
    };

    const handleAnalysisCompleted = (event: AnalysisCompletedEvent) => {
      console.log("[PipelineDetail] Received analysisCompleted:", event, "Current pipeline ID:", id);
      if (event.pipelineId === id) {
        console.log("[PipelineDetail] Pipeline ID matches, updating state");
        const driftCount = event.summary?.totalDrifts ?? event.driftCount ?? 0;
        setPipeline(prev => prev ? {
          ...prev,
          lastScanStatus: event.status === "CompletedWithErrors" ? "CompletedWithErrors" : "Success",
          lastScanAt: event.timestamp,
          driftCount: driftCount,
          lastScanSummary: event.summary ? `${driftCount} drifts (${event.summary.critical} critical)` : undefined,
          lastScanError: event.status === "CompletedWithErrors" ? "Pipeline had errors but analysis completed" : undefined
        } : null);
        setScanning(false);
        // Show appropriate toast based on status
        if (event.status === "CompletedWithErrors") {
          toast.warning(t("pipelines.detail.scanCompletedWithErrors", "Scan completed with errors"));
        } else {
          toast.success(t("pipelines.detail.scanCompleted", "Scan completed"));
        }
        // Always refresh data after completion
        fetchScans();
        fetchDrifts();
        fetchPipeline();
      }
    };

    const handleAnalysisFailed = (event: AnalysisFailedEvent) => {
      console.log("[PipelineDetail] Received analysisFailed:", event, "Current pipeline ID:", id);
      if (event.pipelineId === id) {
        console.log("[PipelineDetail] Pipeline ID matches, updating state to Failed");
        // Truncate long error messages for display
        const shortError = event.error?.length > 100 
          ? event.error.substring(0, 100) + "..." 
          : event.error;
        setPipeline(prev => prev ? {
          ...prev,
          lastScanStatus: "Failed",
          lastScanAt: event.timestamp,
          lastScanError: event.error
        } : null);
        setScanning(false);
        toast.error(t("pipelines.detail.scanFailed", "Scan failed: ") + shortError);
        // Refresh all data after failure
        fetchScans();
        fetchPipeline();
      }
    };

    const unsubscribeProgress = onProgress(handleAnalysisProgress);
    const unsubscribeCompleted = onCompleted(handleAnalysisCompleted);
    const unsubscribeFailed = onFailed(handleAnalysisFailed);

    return () => {
      unsubscribeProgress();
      unsubscribeCompleted();
      unsubscribeFailed();
    };
  }, [id, onProgress, onCompleted, onFailed, toast, t, fetchScans, fetchDrifts, fetchPipeline]);

  useEffect(() => {
    if (id) {
      fetchPipeline();
      // Also fetch scans to populate stats in overview
      fetchScans();
    }
  }, [id, fetchPipeline, fetchScans]);

  // Fetch drifts when tab changes to drifts, refetch scans when tab changes to scans
  useEffect(() => {
    if (!id) return;
    if (activeTab === "scans") fetchScans();
    if (activeTab === "drifts") fetchDrifts();
  }, [activeTab, id, fetchScans, fetchDrifts]);

  const openScanDetail = async (scan: ScanLogItem) => {
    setSelectedScan(scan);
    setScanDrifts([]);
    setScanDriftsLoading(true);
    try {
      const driftResults = await scansApi.getScanDrifts(scan.id);
      setScanDrifts(driftResults);
    } catch (error) {
      console.error("Failed to fetch scan drifts:", error);
    } finally {
      setScanDriftsLoading(false);
    }
  };

  const closeScanDetail = () => {
    setSelectedScan(null);
    setScanDrifts([]);
  };

  const handleScan = async () => {
    if (scanning) return; // Prevent double-click
    setScanning(true);
    try {
      await pipelinesApi.scanPipeline(id);
      toast.info(t("pipelines.detail.scanTriggered"));
      // Update pipeline status locally while waiting for SignalR
      setPipeline(prev => prev ? {
        ...prev,
        lastScanStatus: "Running",
        lastScanAt: new Date().toISOString()
      } : null);
      // Note: scanning will be set to false by SignalR events (scanCompleted/scanFailed)
    } catch (error) {
      console.error("Failed to trigger scan:", error);
      toast.error(t("errors.generic"));
      setScanning(false); // Only reset on error
    }
  };

  const handleToggle = async () => {
    if (!pipeline) return;
    setToggling(true);
    try {
      if (pipeline.isActive) {
        await pipelinesApi.deactivatePipeline(id);
      } else {
        await pipelinesApi.activatePipeline(id);
      }
      setPipeline({ ...pipeline, isActive: !pipeline.isActive });
    } catch (error) {
      console.error("Failed to toggle pipeline:", error);
      toast.error(t("errors.generic"));
    } finally {
      setToggling(false);
    }
  };

  const handleDelete = async () => {
    const confirmed = await modal.confirm({
      title: t("pipelines.actions.delete"),
      message: t("pipelines.detail.dangerDescription"),
      confirmText: t("common.delete"),
      cancelText: t("common.cancel"),
      variant: "danger",
    });
    if (!confirmed) return;

    try {
      await pipelinesApi.deletePipeline(id);
      navigate("/pipelines");
    } catch (error) {
      console.error("Failed to delete pipeline:", error);
      toast.error(t("errors.deleteFailed"));
    }
  };

  const startEditingConnection = async () => {
    if (!pipeline) return;
    setSelectedAdoConnectionId(pipeline.adoConnectionId);
    setEditPlanOnlyParam(pipeline.planOnlyParameter || "");
    setEditingConnection(true);
    try {
      const connections = await configApi.getAdoConnections();
      setAdoConnections(connections);
    } catch (error) {
      console.error("Failed to load ADO connections:", error);
      toast.error(t("errors.loadFailed"));
    }
  };

  const cancelEditingConnection = () => {
    setEditingConnection(false);
  };

  const saveConnection = async () => {
    if (!pipeline) return;

    const updates: Partial<UpdatePipelineInput> = {};
    if (selectedAdoConnectionId && selectedAdoConnectionId !== pipeline.adoConnectionId) {
      updates.adoConnectionId = selectedAdoConnectionId;
    }
    if (editPlanOnlyParam !== (pipeline.planOnlyParameter || "")) {
      updates.planOnlyParameter = editPlanOnlyParam || undefined;
    }

    if (Object.keys(updates).length === 0) {
      setEditingConnection(false);
      return;
    }

    setSavingConnection(true);
    try {
      await pipelinesApi.updatePipeline(id, updates);
      const selectedConn = adoConnections.find((c) => c.id === selectedAdoConnectionId);
      setPipeline({
        ...pipeline,
        ...(updates.adoConnectionId && {
          adoConnectionId: selectedAdoConnectionId,
          adoConnectionName: selectedConn?.name || pipeline.adoConnectionName,
        }),
        ...(updates.planOnlyParameter !== undefined && {
          planOnlyParameter: editPlanOnlyParam,
        }),
      });
      setEditingConnection(false);
      toast.success(t("pipelines.detail.configSaved"));
    } catch (error) {
      console.error("Failed to save connection:", error);
      toast.error(error instanceof Error ? error.message : t("errors.saveFailed"));
    } finally {
      setSavingConnection(false);
    }
  };

  const driftCount = pipeline?.driftCount ?? 0;

  const getDriftSeverity = (): "critical" | "high" | "medium" | "ok" => {
    if (driftCount >= 4) return "critical";
    if (driftCount >= 1) return "high";
    return "ok";
  };

  const getHealthLabel = (): string => {
    if (driftCount === 0) return t("pipelines.detail.healthy");
    return t("pipelines.detail.unhealthy");
  };

  const getHealthSeverity = (): "ok" | "high" => {
    return driftCount === 0 ? "ok" : "high";
  };

  const getSeverityClass = (severity: string): string => {
    switch (severity?.toLowerCase()) {
      case "critical": return "pd-severity-critical";
      case "high": return "pd-severity-high";
      case "medium": return "pd-severity-medium";
      case "low": return "pd-severity-low";
      default: return "pd-severity-low";
    }
  };

  const getScanStatusClass = (status: string): string => {
    switch (status?.toLowerCase()) {
      case "completed":
      case "success": return "success";
      case "failed": return "failed";
      case "running": return "running";
      default: return "";
    }
  };

  const adoUrl = pipeline
    ? `${pipeline.organizationUrl}/${pipeline.projectName}/_build?definitionId=${pipeline.pipelineId}`
    : "#";

  if (loading) {
    return <div className="pd-loading">{t("common.loading")}</div>;
  }

  if (!pipeline) {
    return (
      <div className="pd-not-found">
        <span className="pd-not-found-text">{t("pipelines.notFound")}</span>
        <button className="btn btn-outline" onClick={() => navigate("/pipelines")}>
          {t("common.back")}
        </button>
      </div>
    );
  }

  return (
    <div className="pd-page">
      {/* Zone 1: Breadcrumb */}
      <nav className="pd-breadcrumb">
        <a onClick={() => navigate("/pipelines")}>{t("nav.pipelines")}</a>
        <span className="pd-breadcrumb-separator">/</span>
        <span className="pd-breadcrumb-current">{pipeline.pipelineName}</span>
      </nav>

      {/* Zone 2: Hero Header */}
      <div className="pd-hero">
        <div className="pd-hero-top">
          <div className="pd-hero-info">
            <h1 className="pd-hero-title">{pipeline.pipelineName}</h1>
            <p className="pd-hero-subtitle">
              {pipeline.projectName}
              {" · "}
              <a href={adoUrl} target="_blank" rel="noopener noreferrer">
                {pipeline.organizationUrl}
              </a>
            </p>
            <div className="pd-hero-meta">
              <span className={`pd-status-badge ${pipeline.isActive ? "active" : "inactive"}`}>
                {pipeline.isActive ? t("pipelines.active") : t("pipelines.inactive")}
              </span>
              {/* Scan Status Badge */}
              {pipeline.lastScanStatus && (
                <span className={`pd-scan-status-badge ${pipeline.lastScanStatus.toLowerCase()}`}>
                  {pipeline.lastScanStatus === "Running" && "🔄 "}
                  {pipeline.lastScanStatus === "Success" && "✅ "}
                  {pipeline.lastScanStatus === "Failed" && "❌ "}
                  {t(`pipelines.scanStatus.${pipeline.lastScanStatus.toLowerCase()}`, pipeline.lastScanStatus)}
                </span>
              )}
              {pipeline.createdAt && (
                <span className="pd-meta-item">
                  {t("common.created")} {formatTimeAgo(pipeline.createdAt, t)}
                </span>
              )}
              {pipeline.updatedAt && (
                <span className="pd-meta-item">
                  {t("common.updated")} {formatTimeAgo(pipeline.updatedAt, t)}
                </span>
              )}
            </div>
          </div>
          <div className="pd-hero-actions">
            {isAdmin && (
              <label className="pd-toggle" title={pipeline.isActive ? t("pipelines.actions.deactivate") : t("pipelines.actions.activate")}>
                <input
                  type="checkbox"
                  checked={pipeline.isActive}
                  onChange={handleToggle}
                  disabled={toggling}
                />
                <span className="pd-toggle-track" />
              </label>
            )}
            <button
              className={`btn ${scanning ? "btn-secondary" : "btn-primary"}`}
              onClick={handleScan}
              disabled={scanning}
              title={scanning ? t("pipelines.scanInProgress", "Scan in progress...") : t("pipelines.scanNow")}
            >
              {scanning ? (
                <>
                  <span className="btn-spinner">🔄</span>
                  {t("pipelines.scanning")}
                </>
              ) : (
                t("pipelines.scanNow")
              )}
            </button>
            <a
              href={adoUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="btn btn-outline"
            >
              {t("pipelines.viewInAdo")}
            </a>
          </div>
        </div>
      </div>

      {/* Zone 3: Metrics */}
      <div className="pd-metrics">
        <MetricCard
          title={t("pipelines.detail.activeDrifts")}
          value={driftCount}
          severity={getDriftSeverity()}
          icon={driftCount === 0 ? "✅" : "⚠️"}
          loading={loading}
        />
        <MetricCard
          title={t("pipelines.lastScan")}
          value={pipeline.lastScanAt ? formatTimeAgo(pipeline.lastScanAt, t) : t("pipelines.never")}
          severity="ok"
          icon="🔍"
          loading={loading}
        />
        <MetricCard
          title={t("pipelines.detail.totalScans")}
          value={scansTotal}
          severity="ok"
          icon="📊"
          loading={loading}
        />
        <MetricCard
          title={t("pipelines.detail.health")}
          value={getHealthLabel()}
          severity={getHealthSeverity()}
          icon={driftCount === 0 ? "💚" : "🔶"}
          loading={loading}
        />
      </div>

      {/* Zone 4: Tabs */}
      <div className="pd-tabs">
        <div className="pd-tab-bar">
          <button
            className={`pd-tab ${activeTab === "overview" ? "active" : ""}`}
            onClick={() => setActiveTab("overview")}
          >
            {t("pipelines.overview")}
          </button>
          <button
            className={`pd-tab ${activeTab === "drifts" ? "active" : ""}`}
            onClick={() => setActiveTab("drifts")}
          >
            {t("pipelines.drifts")}
            <span className="pd-tab-count">{driftCount}</span>
          </button>
          <button
            className={`pd-tab ${activeTab === "scans" ? "active" : ""}`}
            onClick={() => setActiveTab("scans")}
          >
            {t("pipelines.scanHistory")}
          </button>
        </div>

        <div className="pd-tab-content">
          {activeTab === "overview" && (
              <div className="pd-card">
                <div className="pd-card-header">
                  <h3>{t("pipelines.detail.connectionsInfo")}</h3>
                  {isAdmin && !editingConnection && (
                    <button className="btn btn-sm btn-outline" onClick={startEditingConnection}>
                      {t("common.edit")}
                    </button>
                  )}
                </div>
                <div className="pd-card-body">
                  <div className="pd-config-row">
                    <span className="pd-config-label">{t("pipelines.adoConnection")}</span>
                    {editingConnection ? (
                      <select
                        className="pd-config-input"
                        value={selectedAdoConnectionId}
                        onChange={(e) => setSelectedAdoConnectionId(e.target.value)}
                      >
                        {adoConnections.length === 0 && (
                          <option value="">{t("common.loading")}</option>
                        )}
                        {adoConnections.map((conn) => (
                          <option key={conn.id} value={conn.id}>
                            {conn.name}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <span className="pd-config-value">
                        {pipeline.adoConnectionName || "-"}
                      </span>
                    )}
                  </div>
                  <div className="pd-config-row">
                    <span className="pd-config-label">{t("pipelines.pipelineId")}</span>
                    <span className="pd-config-value">{pipeline.pipelineId}</span>
                  </div>
                  <div className="pd-config-row">
                    <span className="pd-config-label">{t("pipelines.project")}</span>
                    <span className="pd-config-value">{pipeline.projectName}</span>
                  </div>
                  <div className="pd-config-row">
                    <span className="pd-config-label">{t("pipelines.organization")}</span>
                    <span className="pd-config-value">{pipeline.organizationUrl}</span>
                  </div>
                  <div className="pd-config-row">
                    <span className="pd-config-label">{t("pipelines.detail.planOnlyParam")}</span>
                    {editingConnection ? (
                      <input
                        className="pd-config-input"
                        value={editPlanOnlyParam}
                        onChange={(e) => setEditPlanOnlyParam(e.target.value)}
                        placeholder="PLAN_ONLY"
                      />
                    ) : (
                      <span className={`pd-config-value ${!pipeline.planOnlyParameter ? "not-set" : ""}`}>
                        {pipeline.planOnlyParameter || t("pipelines.detail.notConfigured")}
                      </span>
                    )}
                  </div>
                </div>
                {editingConnection && (
                  <div className="pd-card-actions">
                    <button className="btn btn-primary btn-sm" onClick={saveConnection} disabled={savingConnection}>
                      {savingConnection ? t("common.loading") : t("common.save")}
                    </button>
                    <button className="btn btn-outline btn-sm" onClick={cancelEditingConnection} disabled={savingConnection}>
                      {t("common.cancel")}
                    </button>
                  </div>
                )}
              </div>
          )}

          {activeTab === "drifts" && (
            <>
              {driftsLoading ? (
                <div className="pd-loading">{t("common.loading")}</div>
              ) : drifts.length > 0 ? (
                <table className="pd-table">
                  <thead>
                    <tr>
                      <th>{t("drift.severity")}</th>
                      <th>{t("drift.resource")}</th>
                      <th>{t("drift.description")}</th>
                      <th>{t("drift.detectedAt")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {drifts.map((drift) => (
                      <tr key={drift.id} onClick={() => setSelectedDrift(drift)} style={{ cursor: "pointer" }}>
                        <td>
                          <span className={`pd-severity-badge ${getSeverityClass(drift.severity)}`}>
                            {drift.severity}
                          </span>
                        </td>
                        <td>
                          <div>{drift.resourceName || drift.resourceType}</div>
                          {drift.resourceName && <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>{drift.resourceType}</div>}
                        </td>
                        <td>{drift.description}</td>
                        <td>{formatTimeAgo(drift.detectedAt, t)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <div className="pd-empty-state">
                  <span className="pd-empty-icon">✅</span>
                  <span className="pd-empty-title">{t("pipelines.noDrifts")}</span>
                  <span className="pd-empty-text">{t("pipelines.detail.noDriftsDescription")}</span>
                  <button className="btn btn-primary" onClick={handleScan} disabled={scanning}>
                    {scanning ? t("pipelines.scanning") : t("pipelines.scanNow")}
                  </button>
                </div>
              )}
            </>
          )}

          {activeTab === "scans" && (
            <>
              {scansLoading ? (
                <div className="pd-loading">{t("common.loading")}</div>
              ) : scans.length > 0 ? (
                <table className="pd-table">
                  <thead>
                    <tr>
                      <th>{t("scans.status")}</th>
                      <th>{t("scans.startedAt")}</th>
                      <th>{t("scans.duration")}</th>
                      <th>{t("scans.driftsFound")}</th>
                      <th>{t("scans.table.triggeredBy")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {scans.map((scan) => (
                      <tr key={scan.id} onClick={() => openScanDetail(scan)} style={{ cursor: "pointer" }}>
                        <td>
                          <span className={`pd-scan-status ${getScanStatusClass(scan.status)}`}>
                            {t(`scans.status.${scan.status}`, scan.status)}
                          </span>
                        </td>
                        <td>{formatTimeAgo(scan.startedAt, t)}</td>
                        <td>{scan.durationSeconds ? `${scan.durationSeconds}s` : "-"}</td>
                        <td>{scan.driftCount ?? 0}</td>
                        <td>{scan.triggeredBy || "-"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <div className="pd-empty-state">
                  <span className="pd-empty-icon">📋</span>
                  <span className="pd-empty-title">{t("pipelines.noScans")}</span>
                  <span className="pd-empty-text">{t("pipelines.detail.noScansDescription")}</span>
                  <button className="btn btn-primary" onClick={handleScan} disabled={scanning}>
                    {scanning ? t("pipelines.scanning") : t("pipelines.scanNow")}
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>

      {/* Zone 5: Danger Zone (admin only) */}
      {isAdmin && (
        <div className="pd-danger-zone">
          <div className="pd-danger-header">{t("pipelines.detail.dangerZone")}</div>
          <div className="pd-danger-body">
            <span className="pd-danger-text">{t("pipelines.detail.dangerDescription")}</span>
            <button className="btn btn-danger" onClick={handleDelete}>
              {t("pipelines.detail.deletePipeline")}
            </button>
          </div>
        </div>
      )}

      {/* Scan Detail Modal */}
      <Modal isOpen={!!selectedScan} onClose={closeScanDetail} title={t("scans.detail.title")} size="lg">
        {selectedScan && (
          <div className="pd-scan-detail">
            <div className="pd-scan-detail-grid">
              <div className="pd-config-row">
                <span className="pd-config-label">{t("scans.status")}</span>
                <span className={`pd-scan-status ${getScanStatusClass(selectedScan.status)}`}>
                  {t(`scans.status.${selectedScan.status}`, selectedScan.status)}
                </span>
              </div>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("scans.startedAt")}</span>
                <span className="pd-config-value">{new Date(selectedScan.startedAt).toLocaleString()}</span>
              </div>
              {selectedScan.completedAt && (
                <div className="pd-config-row">
                  <span className="pd-config-label">{t("scans.table.completedAt")}</span>
                  <span className="pd-config-value">{new Date(selectedScan.completedAt).toLocaleString()}</span>
                </div>
              )}
              <div className="pd-config-row">
                <span className="pd-config-label">{t("scans.duration")}</span>
                <span className="pd-config-value">{selectedScan.durationSeconds ? `${selectedScan.durationSeconds}s` : "-"}</span>
              </div>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("scans.driftsFound")}</span>
                <span className="pd-config-value">{selectedScan.driftCount ?? 0}</span>
              </div>
              {selectedScan.triggeredBy && (
                <div className="pd-config-row">
                  <span className="pd-config-label">{t("scans.table.triggeredBy")}</span>
                  <span className="pd-config-value">{selectedScan.triggeredBy}</span>
                </div>
              )}
              {selectedScan.errorMessage && (
                <div className="pd-config-row">
                  <span className="pd-config-label">{t("scans.detail.errorMessage")}</span>
                  <span className="pd-config-value" style={{ color: "var(--error)" }}>{selectedScan.errorMessage}</span>
                </div>
              )}
            </div>

            <h4 style={{ margin: "var(--space-6) 0 var(--space-3)" }}>{t("scans.driftsFound")}</h4>
            {scanDriftsLoading ? (
              <div className="pd-loading" style={{ minHeight: "100px" }}>{t("common.loading")}</div>
            ) : scanDrifts.length > 0 ? (
              <table className="pd-table">
                <thead>
                  <tr>
                    <th>{t("drift.severity")}</th>
                    <th>{t("drift.resource")}</th>
                    <th>{t("drift.description")}</th>
                  </tr>
                </thead>
                <tbody>
                  {scanDrifts.map((drift) => (
                    <tr key={drift.id}>
                      <td>
                        <span className={`pd-severity-badge ${getSeverityClass(drift.severity)}`}>
                          {drift.severity}
                        </span>
                      </td>
                      <td>
                        <div>{drift.resourceName}</div>
                        <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>{drift.resourceType}</div>
                      </td>
                      <td>
                        <div>{drift.description}</div>
                        {drift.recommendation && (
                          <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)", marginTop: "var(--space-1)" }}>
                            {drift.recommendation}
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p style={{ color: "var(--text-muted)", textAlign: "center", padding: "var(--space-6) 0" }}>
                {t("pipelines.noDrifts")}
              </p>
            )}
          </div>
        )}
      </Modal>

      {/* Drift Detail Modal */}
      <Modal
        isOpen={!!selectedDrift}
        onClose={() => setSelectedDrift(null)}
        title={t("drift.detail.title")}
        size="lg"
      >
        {selectedDrift && (
          <div className="pd-drift-detail">
            <div className="pd-drift-header">
              <span className={`pd-severity-badge ${getSeverityClass(selectedDrift.severity)}`}>
                {selectedDrift.severity}
              </span>
              <span className="pd-drift-category">{selectedDrift.category}</span>
            </div>

            <div className="pd-drift-section">
              <h4>{t("drift.detail.resource")}</h4>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("drift.table.resourceType")}</span>
                <span className="pd-config-value">{selectedDrift.resourceType}</span>
              </div>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("drift.table.resourceName")}</span>
                <span className="pd-config-value">{selectedDrift.resourceName || "-"}</span>
              </div>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("drift.table.resourceId")}</span>
                <span className="pd-config-value" style={{ wordBreak: "break-all" }}>{selectedDrift.resourceId}</span>
              </div>
            </div>

            <div className="pd-drift-section">
              <h4>{t("drift.detail.description")}</h4>
              <p className="pd-drift-description">{selectedDrift.description}</p>
            </div>

            <div className="pd-drift-section">
              <h4>{t("drift.detail.changes")}</h4>
              <div className="pd-config-row">
                <span className="pd-config-label">{t("drift.table.property")}</span>
                <span className="pd-config-value"><code>{selectedDrift.property}</code></span>
              </div>
              <div className="pd-drift-diff">
                <div className="pd-drift-expected">
                  <span className="pd-drift-diff-label">{t("drift.table.expected")}</span>
                  <pre>{selectedDrift.expectedValue}</pre>
                </div>
                <div className="pd-drift-actual">
                  <span className="pd-drift-diff-label">{t("drift.table.actual")}</span>
                  <pre>{selectedDrift.actualValue}</pre>
                </div>
              </div>
            </div>

            {selectedDrift.recommendation && (
              <div className="pd-drift-section">
                <h4>{t("drift.detail.recommendation")}</h4>
                <div className="pd-drift-recommendation">
                  <pre>{selectedDrift.recommendation}</pre>
                </div>
              </div>
            )}

            <div className="pd-drift-footer">
              <span className="pd-drift-detected">
                {t("drift.detectedAt")}: {new Date(selectedDrift.detectedAt).toLocaleString()}
              </span>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}

export default PipelineDetail;
