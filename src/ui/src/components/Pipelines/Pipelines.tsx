import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useRoles } from "../../hooks/useRoles";
import { useModalContext } from "../../contexts/ModalContext";
import { Modal } from "../common/Modal";
import { pipelinesApi, configApi } from "../../services/api";
import type { Pipeline, AzureConnection, AdoConnection } from "../../services/api";
import "./Pipelines.css";

interface AdoProject {
  name: string;
}

interface AdoPipeline {
  id: string;
  name: string;
}

export function Pipelines() {
  const { t } = useTranslation();
  const { isAdmin } = useRoles();
  const modal = useModalContext();
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [adoConnections, setAdoConnections] = useState<AdoConnection[]>([]);
  const [azureConnections, setAzureConnections] = useState<AzureConnection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showAddModal, setShowAddModal] = useState(false);

  // Form state
  const [selectedAdoConnection, setSelectedAdoConnection] = useState("");
  const [selectedAzureConnection, setSelectedAzureConnection] = useState("");
  const [projects, setProjects] = useState<AdoProject[]>([]);
  const [selectedProject, setSelectedProject] = useState("");
  const [availablePipelines, setAvailablePipelines] = useState<AdoPipeline[]>([]);
  const [selectedPipelines, setSelectedPipelines] = useState<string[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(false);
  const [loadingPipelines, setLoadingPipelines] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [pipelinesData, adoData, azureData] = await Promise.all([
        pipelinesApi.getPipelines(),
        configApi.getAdoConnections(),
        configApi.getAzureConnections(),
      ]);
      setPipelines(pipelinesData);
      setAdoConnections(adoData);
      setAzureConnections(azureData);
    } catch (err) {
      console.error("Error loading data:", err);
      setError(err instanceof Error ? err.message : "Failed to load data");
    } finally {
      setLoading(false);
    }
  };

  const handleAdoConnectionChange = async (connectionId: string) => {
    setSelectedAdoConnection(connectionId);
    setSelectedProject("");
    setAvailablePipelines([]);
    setSelectedPipelines([]);

    if (!connectionId) return;

    setLoadingProjects(true);
    try {
      const projectNames = await pipelinesApi.getAdoProjects(connectionId);
      setProjects(projectNames.map((name) => ({ name })));
    } catch (err) {
      console.error("Error loading projects:", err);
      modal.alert({
        message: t("errors.loadFailed"),
        variant: "error",
      });
    } finally {
      setLoadingProjects(false);
    }
  };

  const handleProjectChange = async (projectName: string) => {
    setSelectedProject(projectName);
    setAvailablePipelines([]);
    setSelectedPipelines([]);

    if (!projectName) return;

    setLoadingPipelines(true);
    try {
      const pipelines = await pipelinesApi.getAdoPipelines(selectedAdoConnection, projectName);
      setAvailablePipelines(pipelines);
    } catch (err) {
      console.error("Error loading pipelines:", err);
      modal.alert({
        message: t("errors.loadFailed"),
        variant: "error",
      });
    } finally {
      setLoadingPipelines(false);
    }
  };

  const handlePipelineToggle = (pipelineId: string) => {
    setSelectedPipelines((prev) =>
      prev.includes(pipelineId)
        ? prev.filter((id) => id !== pipelineId)
        : [...prev, pipelineId]
    );
  };

  const handleAddPipelines = async () => {
    try {
      await pipelinesApi.createPipelines({
        adoConnectionId: selectedAdoConnection,
        azureConnectionId: selectedAzureConnection,
        projectName: selectedProject,
        pipelineIds: selectedPipelines,
      });
      modal.alert({
        message: t("pipelines.addedCount", { count: selectedPipelines.length }),
        variant: "success",
      });
      setShowAddModal(false);
      resetForm();
      loadData(); // Reload pipeline list
    } catch (err) {
      console.error("Error adding pipelines:", err);
      modal.alert({
        message: t("errors.saveFailed"),
        variant: "error",
      });
    }
  };

  const handleToggleActive = async (pipelineId: string, isActive: boolean) => {
    try {
      await pipelinesApi.updatePipeline(pipelineId, { isActive: !isActive });
      setPipelines((prev) =>
        prev.map((p) => (p.id === pipelineId ? { ...p, isActive: !isActive } : p))
      );
    } catch (err) {
      console.error("Error toggling pipeline:", err);
      modal.alert({
        message: t("errors.saveFailed"),
        variant: "error",
      });
    }
  };

  const handleScan = async (pipelineId: string) => {
    try {
      const result = await pipelinesApi.scanPipeline(pipelineId);
      modal.alert({
        message: result.message || `Scan triggered. Drifts found: ${result.driftCount}`,
        variant: result.success ? "success" : "warning",
      });
    } catch (err) {
      console.error("Error scanning pipeline:", err);
      modal.alert({
        message: t("errors.generic"),
        variant: "error",
      });
    }
  };

  const handleDelete = async (pipelineId: string) => {
    const confirmed = await modal.confirm({
      title: t("pipelines.actions.delete"),
      message: t("pipelines.confirmDelete"),
      confirmText: t("common.delete"),
      cancelText: t("common.cancel"),
      variant: "danger",
    });

    if (!confirmed) return;

    try {
      await pipelinesApi.deletePipeline(pipelineId);
      setPipelines((prev) => prev.filter((p) => p.id !== pipelineId));
    } catch (err) {
      console.error("Error deleting pipeline:", err);
      modal.alert({
        message: t("errors.deleteFailed"),
        variant: "error",
      });
    }
  };

  const resetForm = () => {
    setSelectedAdoConnection("");
    setSelectedAzureConnection("");
    setSelectedProject("");
    setProjects([]);
    setAvailablePipelines([]);
    setSelectedPipelines([]);
  };

  if (loading) {
    return <div className="pipelines-loading">{t("common.loading")}</div>;
  }

  return (
    <div className="pipelines">
      <div className="pipelines-header">
        <div>
          <h1>{t("pipelines.title")}</h1>
          <p className="text-muted">{t("pipelines.subtitle")}</p>
        </div>
        {isAdmin && (
          <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
            + {t("pipelines.addPipeline")}
          </button>
        )}
      </div>

      {error && (
        <div className="error-banner">
          <span>{error}</span>
          <button className="btn btn-sm" onClick={loadData}>
            {t("common.retry")}
          </button>
        </div>
      )}

      {pipelines.length === 0 ? (
        <div className="pipelines-empty card">
          <span className="empty-icon">📋</span>
          <h3>{t("pipelines.noPipelines")}</h3>
          <p className="text-muted">{t("pipelines.addFirst")}</p>
          {isAdmin && (
            <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
              {t("pipelines.addPipeline")}
            </button>
          )}
        </div>
      ) : (
        <div className="pipelines-list">
          <div className="pipelines-table-header">
            <span>{t("pipelines.table.name")}</span>
            <span>{t("pipelines.table.project")}</span>
            <span>{t("pipelines.table.connection")}</span>
            <span>{t("pipelines.table.status")}</span>
            <span>{t("pipelines.table.lastScan")}</span>
            <span>{t("pipelines.table.driftCount")}</span>
            {isAdmin && <span>{t("common.actions")}</span>}
          </div>

          {pipelines.map((pipeline) => (
            <div key={pipeline.id} className={`pipeline-row ${!pipeline.isActive ? "inactive" : ""}`}>
              <div className="pipeline-name">
                <span className="name">{pipeline.pipelineName}</span>
                <span className="org text-muted">{pipeline.adoConnectionName}</span>
              </div>
              <span>{pipeline.projectName}</span>
              <span>{pipeline.azureConnectionName}</span>
              <span>
                <span className={`status-badge ${pipeline.isActive ? "status-active" : "status-inactive"}`}>
                  {pipeline.isActive ? t("common.active") : t("common.inactive")}
                </span>
              </span>
              <span className="text-muted">
                {pipeline.lastScanAt || t("pipelines.never", "Never")}
              </span>
              <span>
                {pipeline.driftCount !== undefined && pipeline.driftCount > 0 ? (
                  <span className="drift-count">
                    {t("pipelines.driftCountLabel", { count: pipeline.driftCount })}
                  </span>
                ) : (
                  <span className="text-muted">{t("pipelines.noDrift", "No drift")}</span>
                )}
              </span>
              {isAdmin && (
                <div className="pipeline-actions">
                  <button
                    className="btn btn-ghost btn-sm"
                    onClick={() => handleScan(pipeline.id)}
                    title={t("pipelines.actions.scan")}
                    disabled={!pipeline.isActive}
                  >
                    🔍
                  </button>
                  <button
                    className="btn btn-ghost btn-sm"
                    onClick={() => handleToggleActive(pipeline.id, pipeline.isActive)}
                    title={pipeline.isActive ? t("pipelines.actions.deactivate") : t("pipelines.actions.activate")}
                  >
                    {pipeline.isActive ? "⏸️" : "▶️"}
                  </button>
                  <button
                    className="btn btn-ghost btn-sm"
                    onClick={() => handleDelete(pipeline.id)}
                    title={t("pipelines.actions.delete")}
                  >
                    🗑️
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Add Pipeline Modal */}
      <Modal
        isOpen={showAddModal}
        onClose={() => setShowAddModal(false)}
        title={t("pipelines.modal.addTitle")}
        size="md"
      >
        <div className="add-pipeline-form">
          <div className="form-group">
            <label>{t("pipelines.modal.selectConnection")}</label>
            <select
              value={selectedAdoConnection}
              onChange={(e) => handleAdoConnectionChange(e.target.value)}
            >
              <option value="">{t("common.select", "Select...")}...</option>
              {adoConnections.map((conn) => (
                <option key={conn.id} value={conn.id}>
                  {conn.name}
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label>{t("configuration.connections.azure")}</label>
            <select
              value={selectedAzureConnection}
              onChange={(e) => setSelectedAzureConnection(e.target.value)}
            >
              <option value="">{t("common.select", "Select...")}...</option>
              {azureConnections.map((conn) => (
                <option key={conn.id} value={conn.id}>
                  {conn.name}
                </option>
              ))}
            </select>
          </div>

          {selectedAdoConnection && (
            <div className="form-group">
              <label>{t("pipelines.modal.selectProject")}</label>
              {loadingProjects ? (
                <div className="loading-inline">{t("pipelines.modal.loadingProjects")}</div>
              ) : (
                <select
                  value={selectedProject}
                  onChange={(e) => handleProjectChange(e.target.value)}
                >
                  <option value="">{t("common.select", "Select...")}...</option>
                  {projects.map((project) => (
                    <option key={project.name} value={project.name}>
                      {project.name}
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}

          {selectedProject && (
            <div className="form-group">
              <label>{t("pipelines.modal.selectPipelines")}</label>
              {loadingPipelines ? (
                <div className="loading-inline">{t("pipelines.modal.loadingPipelines")}</div>
              ) : (
                <div className="pipeline-checklist">
                  {availablePipelines.length === 0 ? (
                    <p className="text-muted">{t("pipelines.modal.noPipelinesFound")}</p>
                  ) : (
                    availablePipelines.map((pipeline) => (
                      <label key={pipeline.id} className="checkbox-item">
                        <input
                          type="checkbox"
                          checked={selectedPipelines.includes(pipeline.id)}
                          onChange={() => handlePipelineToggle(pipeline.id)}
                        />
                        <span>{pipeline.name}</span>
                      </label>
                    ))
                  )}
                </div>
              )}
            </div>
          )}

          <div className="modal-form-actions">
            <button className="btn btn-ghost" onClick={() => setShowAddModal(false)}>
              {t("common.cancel")}
            </button>
            <button
              className="btn btn-primary"
              disabled={
                !selectedAdoConnection ||
                !selectedAzureConnection ||
                !selectedProject ||
                selectedPipelines.length === 0
              }
              onClick={handleAddPipelines}
            >
              {t("common.add")} {selectedPipelines.length > 0 ? `(${selectedPipelines.length})` : ""}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
