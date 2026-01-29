import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useRoles } from "../../hooks/useRoles";
import { useModalContext } from "../../contexts/ModalContext";
import { Modal } from "../common/Modal";
import "./Pipelines.css";

interface Pipeline {
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

interface AdoConnection {
  id: string;
  name: string;
  organizationUrl: string;
}

interface AzureConnection {
  id: string;
  name: string;
}

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
    try {
      // In production, these would be API calls
      setPipelines(mockPipelines);
      setAdoConnections(mockAdoConnections);
      setAzureConnections(mockAzureConnections);
    } catch (error) {
      console.error("Error loading data:", error);
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
      // In production: fetch projects from ADO via API
      await new Promise((resolve) => setTimeout(resolve, 500));
      setProjects(mockProjects);
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
      // In production: fetch pipelines from ADO via API
      await new Promise((resolve) => setTimeout(resolve, 500));
      setAvailablePipelines(mockAvailablePipelines);
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
    console.log("Adding pipelines:", {
      adoConnection: selectedAdoConnection,
      azureConnection: selectedAzureConnection,
      project: selectedProject,
      pipelines: selectedPipelines,
    });
    // In production: call API to add pipelines
    modal.alert({
      message: t("pipelines.addedCount", { count: selectedPipelines.length }),
      variant: "success",
    });
    setShowAddModal(false);
    resetForm();
  };

  const handleToggleActive = async (pipelineId: string, isActive: boolean) => {
    console.log("Toggle pipeline:", pipelineId, "to", !isActive);
    setPipelines((prev) =>
      prev.map((p) => (p.id === pipelineId ? { ...p, isActive: !isActive } : p))
    );
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

    console.log("Delete pipeline:", pipelineId);
    setPipelines((prev) => prev.filter((p) => p.id !== pipelineId));
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

// Mock data for development
const mockPipelines: Pipeline[] = [
  {
    id: "1",
    pipelineName: "infra-prod-deploy",
    pipelineId: "123",
    projectName: "Infrastructure",
    organizationUrl: "https://dev.azure.com/myorg",
    adoConnectionId: "ado-1",
    adoConnectionName: "MyOrg ADO",
    azureConnectionId: "azure-1",
    azureConnectionName: "Prod Subscription",
    isActive: true,
    lastScanAt: "2h ago",
    driftCount: 3,
  },
  {
    id: "2",
    pipelineName: "infra-staging-deploy",
    pipelineId: "124",
    projectName: "Infrastructure",
    organizationUrl: "https://dev.azure.com/myorg",
    adoConnectionId: "ado-1",
    adoConnectionName: "MyOrg ADO",
    azureConnectionId: "azure-2",
    azureConnectionName: "Staging Subscription",
    isActive: true,
    lastScanAt: "2h ago",
    driftCount: 0,
  },
  {
    id: "3",
    pipelineName: "storage-setup",
    pipelineId: "125",
    projectName: "Storage",
    organizationUrl: "https://dev.azure.com/myorg",
    adoConnectionId: "ado-1",
    adoConnectionName: "MyOrg ADO",
    azureConnectionId: "azure-1",
    azureConnectionName: "Prod Subscription",
    isActive: false,
    lastScanAt: "1 week ago",
    driftCount: 1,
  },
];

const mockAdoConnections: AdoConnection[] = [
  { id: "ado-1", name: "MyOrg ADO", organizationUrl: "https://dev.azure.com/myorg" },
  { id: "ado-2", name: "SecondOrg", organizationUrl: "https://dev.azure.com/secondorg" },
];

const mockAzureConnections: AzureConnection[] = [
  { id: "azure-1", name: "Prod Subscription" },
  { id: "azure-2", name: "Staging Subscription" },
  { id: "azure-3", name: "Dev Subscription" },
];

const mockProjects: AdoProject[] = [
  { name: "Infrastructure" },
  { name: "Application" },
  { name: "Storage" },
  { name: "Networking" },
];

const mockAvailablePipelines: AdoPipeline[] = [
  { id: "p1", name: "infra-main-deploy" },
  { id: "p2", name: "infra-staging-deploy" },
  { id: "p3", name: "infra-dev-deploy" },
  { id: "p4", name: "bicep-validation" },
  { id: "p5", name: "terraform-plan" },
];
