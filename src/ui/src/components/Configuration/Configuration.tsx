import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useRoles } from "../../hooks/useRoles";
import { configApi } from "../../services/api";
import { useToast, ToastContainer } from "../common/Toast";
import type {
  AzureConnection,
  AdoConnection,
  AiConnection,
  EmailRecipient,
  TimerConfig,
  CreateAzureConnectionInput,
  CreateAdoConnectionInput,
  CreateAiConnectionInput,
  CreateEmailRecipientInput,
} from "../../services/api";
import "./Configuration.css";

type TabType = "connections" | "timer" | "recipients" | "ai";

export function Configuration() {
  const { t } = useTranslation();
  const { isAdmin } = useRoles();
  const toast = useToast();
  const [activeTab, setActiveTab] = useState<TabType>("connections");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Data state
  const [azureConnections, setAzureConnections] = useState<AzureConnection[]>([]);
  const [adoConnections, setAdoConnections] = useState<AdoConnection[]>([]);
  const [aiConnection, setAiConnection] = useState<AiConnection | null>(null);
  const [emailRecipients, setEmailRecipients] = useState<EmailRecipient[]>([]);
  const [timerConfig, setTimerConfig] = useState<TimerConfig>({ intervalHours: 24, isEnabled: true });

  // Modal state
  const [showAzureModal, setShowAzureModal] = useState(false);
  const [showAdoModal, setShowAdoModal] = useState(false);
  const [showAiModal, setShowAiModal] = useState(false);
  const [showRecipientModal, setShowRecipientModal] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [timerData, azureData, adoData, recipientsData, aiData] = await Promise.all([
        configApi.getTimerConfig(),
        configApi.getAzureConnections(),
        configApi.getAdoConnections(),
        configApi.getEmailRecipients(),
        configApi.getAiConnection(),
      ]);
      setTimerConfig(timerData);
      setAzureConnections(azureData);
      setAdoConnections(adoData);
      setEmailRecipients(recipientsData);
      setAiConnection(aiData);
    } catch (err) {
      console.error("Error loading configuration:", err);
      setError(err instanceof Error ? err.message : "Failed to load configuration");
    } finally {
      setLoading(false);
    }
  };

  const handleSaveAzureConnection = async (data: Record<string, string>) => {
    setSaving(true);
    try {
      const input: CreateAzureConnectionInput = {
        name: data.name,
        tenantId: data.tenantId,
        subscriptionId: data.subscriptionId,
        clientId: data.clientId,
        clientSecret: data.clientSecret,
      };
      await configApi.createAzureConnection(input);
      setShowAzureModal(false);
      loadData();
    } catch (err) {
      console.error("Error saving Azure connection:", err);
      toast.error(t("errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const handleSaveAdoConnection = async (data: Record<string, string>) => {
    setSaving(true);
    try {
      const input: CreateAdoConnectionInput = {
        name: data.name,
        organizationUrl: data.organizationUrl,
        pat: data.pat,
      };
      await configApi.createAdoConnection(input);
      setShowAdoModal(false);
      loadData();
    } catch (err) {
      console.error("Error saving ADO connection:", err);
      toast.error(t("errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const handleSaveAiConnection = async (data: Record<string, string>) => {
    setSaving(true);
    try {
      const input: CreateAiConnectionInput = {
        name: data.name,
        endpoint: data.endpoint,
        deploymentName: data.deploymentName,
        apiKey: data.apiKey,
      };
      await configApi.saveAiConnection(input);
      setShowAiModal(false);
      loadData();
    } catch (err) {
      console.error("Error saving AI connection:", err);
      toast.error(t("errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const handleSaveRecipient = async (data: Record<string, string>) => {
    setSaving(true);
    try {
      const input: CreateEmailRecipientInput = {
        email: data.email,
        notifyOn: data.notifyOn as "always" | "drift_only" | "weekly",
      };
      await configApi.createEmailRecipient(input);
      setShowRecipientModal(false);
      loadData();
    } catch (err) {
      console.error("Error saving recipient:", err);
      toast.error(t("errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteAzureConnection = async (id: string) => {
    if (!window.confirm(t("configuration.confirmDelete.azure"))) return;
    try {
      await configApi.deleteAzureConnection(id);
      setAzureConnections((prev) => prev.filter((c) => c.id !== id));
    } catch (err) {
      console.error("Error deleting Azure connection:", err);
      toast.error(t("errors.deleteFailed"));
    }
  };

  const handleDeleteAdoConnection = async (id: string) => {
    if (!window.confirm(t("configuration.confirmDelete.ado"))) return;
    try {
      await configApi.deleteAdoConnection(id);
      setAdoConnections((prev) => prev.filter((c) => c.id !== id));
    } catch (err) {
      console.error("Error deleting ADO connection:", err);
      toast.error(t("errors.deleteFailed"));
    }
  };

  const handleDeleteRecipient = async (id: string) => {
    if (!window.confirm(t("configuration.confirmDelete.recipient"))) return;
    try {
      await configApi.deleteEmailRecipient(id);
      setEmailRecipients((prev) => prev.filter((r) => r.id !== id));
    } catch (err) {
      console.error("Error deleting recipient:", err);
      toast.error(t("errors.deleteFailed"));
    }
  };

  const handleToggleRecipient = async (id: string, currentActive: boolean) => {
    try {
      await configApi.updateEmailRecipient(id, { notifyOn: currentActive ? "drift_only" : "always" });
      setEmailRecipients((prev) =>
        prev.map((r) => (r.id === id ? { ...r, isActive: !r.isActive } : r))
      );
    } catch (err) {
      console.error("Error toggling recipient:", err);
      toast.error(t("errors.saveFailed"));
    }
  };

  const handleTimerChange = (field: keyof TimerConfig, value: number | boolean) => {
    setTimerConfig((prev) => ({ ...prev, [field]: value }));
  };

  const handleSaveTimer = async () => {
    setSaving(true);
    try {
      await configApi.updateTimerConfig(timerConfig);
      toast.success(t("configuration.timer.saved"));
    } catch (err) {
      console.error("Error saving timer:", err);
      toast.error(t("errors.saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  const handleTestAzureConnection = async (id: string) => {
    try {
      const result = await configApi.testAzureConnection(id);
      if (result.success) {
        toast.success(t("configuration.connections.connectionSuccess"));
      } else {
        toast.error(t("configuration.connections.connectionFailed"));
      }
    } catch (err) {
      toast.error(t("configuration.connections.connectionFailed"));
    }
  };

  const handleTestAdoConnection = async (id: string) => {
    try {
      const result = await configApi.testAdoConnection(id);
      if (result.success) {
        toast.success(t("configuration.connections.connectionSuccess"));
      } else {
        toast.error(t("configuration.connections.connectionFailed"));
      }
    } catch (err) {
      toast.error(t("configuration.connections.connectionFailed"));
    }
  };

  const handleTestAiConnection = async () => {
    try {
      const result = await configApi.testAiConnection();
      if (result.success) {
        toast.success(t("configuration.connections.connectionSuccess"));
      } else {
        toast.error(t("configuration.connections.connectionFailed"));
      }
    } catch (err) {
      toast.error(t("configuration.connections.connectionFailed"));
    }
  };

  const getStatusText = (status: "active" | "expiring" | "expired", expiresAt?: string) => {
    switch (status) {
      case "active":
        return `✓ ${t("configuration.status.active")}`;
      case "expiring":
        return `⚠ ${t("configuration.status.expiring", { date: expiresAt })}`;
      case "expired":
        return `✕ ${t("configuration.status.expired")}`;
    }
  };

  const getNotifyOnText = (notifyOn: "always" | "drift_only" | "weekly") => {
    switch (notifyOn) {
      case "always":
        return t("configuration.recipients.notifyOn.always");
      case "drift_only":
        return t("configuration.recipients.notifyOn.driftOnly");
      case "weekly":
        return t("configuration.recipients.notifyOn.weekly");
    }
  };

  if (loading) {
    return <div className="config-loading">{t("common.loading")}</div>;
  }

  if (!isAdmin) {
    return (
      <div className="config-restricted">
        <h2>{t("configuration.restrictedAccess")}</h2>
        <p>{t("configuration.restrictedMessage")}</p>
      </div>
    );
  }

  return (
    <div className="configuration">
      <ToastContainer toasts={toast.toasts} onRemove={toast.removeToast} />
      <div className="config-header">
        <h1>{t("configuration.title")}</h1>
        <p className="text-muted">{t("configuration.subtitle")}</p>
      </div>

      {error && (
        <div className="error-banner">
          <span>{error}</span>
          <button className="btn btn-sm" onClick={loadData}>
            {t("common.retry")}
          </button>
        </div>
      )}

      <div className="config-tabs">
        <button
          className={`tab-btn ${activeTab === "connections" ? "active" : ""}`}
          onClick={() => setActiveTab("connections")}
        >
          {t("configuration.tabs.connections")}
        </button>
        <button
          className={`tab-btn ${activeTab === "timer" ? "active" : ""}`}
          onClick={() => setActiveTab("timer")}
        >
          {t("configuration.tabs.timer")}
        </button>
        <button
          className={`tab-btn ${activeTab === "recipients" ? "active" : ""}`}
          onClick={() => setActiveTab("recipients")}
        >
          {t("configuration.tabs.recipients")}
        </button>
        <button
          className={`tab-btn ${activeTab === "ai" ? "active" : ""}`}
          onClick={() => setActiveTab("ai")}
        >
          {t("configuration.tabs.ai")}
        </button>
      </div>

      <div className="config-content">
        {activeTab === "connections" && (
          <div className="connections-tab">
            {/* Azure Connections */}
            <section className="connection-section">
              <div className="section-header">
                <h2>{t("configuration.connections.azure")}</h2>
                <button className="btn btn-primary btn-sm" onClick={() => setShowAzureModal(true)}>
                  + {t("common.add")}
                </button>
              </div>

              {azureConnections.length === 0 ? (
                <p className="text-muted">{t("configuration.connections.noAzure")}</p>
              ) : (
                <div className="connection-list">
                  {azureConnections.map((conn) => (
                    <div key={conn.id} className="connection-card">
                      <div className="connection-info">
                        <span className="connection-name">{conn.name}</span>
                        <span className="connection-detail">{t("configuration.connections.tenant")}: {conn.tenantId}</span>
                        <span className="connection-detail">{t("configuration.connections.client")}: {conn.clientId}</span>
                      </div>
                      <div className="connection-status">
                        <span className={`status-badge status-${conn.status}`}>
                          {getStatusText(conn.status, conn.expiresAt)}
                        </span>
                      </div>
                      <div className="connection-actions">
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleTestAzureConnection(conn.id)}
                        >
                          {t("configuration.connections.testConnection")}
                        </button>
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleDeleteAzureConnection(conn.id)}
                        >
                          {t("common.delete")}
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>

            {/* ADO Connections */}
            <section className="connection-section">
              <div className="section-header">
                <h2>{t("configuration.connections.ado")}</h2>
                <button className="btn btn-primary btn-sm" onClick={() => setShowAdoModal(true)}>
                  + {t("common.add")}
                </button>
              </div>

              {adoConnections.length === 0 ? (
                <p className="text-muted">{t("configuration.connections.noAdo")}</p>
              ) : (
                <div className="connection-list">
                  {adoConnections.map((conn) => (
                    <div key={conn.id} className="connection-card">
                      <div className="connection-info">
                        <span className="connection-name">{conn.name}</span>
                        <span className="connection-detail">{conn.organizationUrl}</span>
                        <span className="connection-detail">{t("configuration.connections.auth")}: {conn.authType}</span>
                      </div>
                      <div className="connection-status">
                        <span className={`status-badge status-${conn.status}`}>
                          {getStatusText(conn.status, conn.expiresAt)}
                        </span>
                      </div>
                      <div className="connection-actions">
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleTestAdoConnection(conn.id)}
                        >
                          {t("configuration.connections.testConnection")}
                        </button>
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleDeleteAdoConnection(conn.id)}
                        >
                          {t("common.delete")}
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          </div>
        )}

        {activeTab === "timer" && (
          <div className="timer-tab">
            <div className="card">
              <h2>{t("configuration.timer.title")}</h2>
              <p className="text-muted">
                {t("configuration.timer.description")}
              </p>

              <div className="timer-form">
                <div className="form-group">
                  <label>{t("configuration.timer.status")}</label>
                  <div className="toggle-container">
                    <button
                      className={`toggle-btn ${timerConfig.isEnabled ? "active" : ""}`}
                      onClick={() => handleTimerChange("isEnabled", !timerConfig.isEnabled)}
                    >
                      <span className="toggle-slider" />
                    </button>
                    <span>{timerConfig.isEnabled ? t("common.active") : t("configuration.timer.paused")}</span>
                  </div>
                </div>

                <div className="form-group">
                  <label>{t("configuration.timer.interval")}</label>
                  <div className="interval-selector">
                    {[1, 6, 12, 24, 48, 168].map((hours) => (
                      <button
                        key={hours}
                        className={`interval-btn ${timerConfig.intervalHours === hours ? "active" : ""}`}
                        onClick={() => handleTimerChange("intervalHours", hours)}
                      >
                        {hours < 24 ? `${hours}h` : `${hours / 24}d`}
                      </button>
                    ))}
                  </div>
                  <p className="text-muted text-sm">
                    {t("configuration.timer.runEveryHours", { hours: timerConfig.intervalHours })}
                  </p>
                </div>

                {timerConfig.nextRunAt && (
                  <div className="next-run">
                    <span className="text-muted">{t("configuration.timer.nextRun")}</span>
                    <span>{timerConfig.nextRunAt}</span>
                  </div>
                )}

                <button className="btn btn-primary" onClick={handleSaveTimer}>
                  {t("configuration.timer.saveChanges")}
                </button>
              </div>
            </div>
          </div>
        )}

        {activeTab === "recipients" && (
          <div className="recipients-tab">
            <div className="section-header">
              <h2>{t("configuration.recipients.title")}</h2>
              <button className="btn btn-primary btn-sm" onClick={() => setShowRecipientModal(true)}>
                + {t("common.add")}
              </button>
            </div>

            {emailRecipients.length === 0 ? (
              <p className="text-muted">{t("configuration.recipients.noRecipients")}</p>
            ) : (
              <div className="recipients-list">
                {emailRecipients.map((recipient) => (
                  <div key={recipient.id} className={`recipient-card ${!recipient.isActive ? "inactive" : ""}`}>
                    <div className="recipient-info">
                      <span className="recipient-email">{recipient.email}</span>
                      <span className="recipient-setting">
                        {getNotifyOnText(recipient.notifyOn)}
                      </span>
                    </div>
                    <div className="recipient-actions">
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => handleToggleRecipient(recipient.id, recipient.isActive)}
                      >
                        {recipient.isActive ? t("common.disabled") : t("common.enabled")}
                      </button>
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => handleDeleteRecipient(recipient.id)}
                      >
                        {t("common.delete")}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {activeTab === "ai" && (
          <div className="ai-tab">
            <div className="section-header">
              <h2>{t("configuration.ai.title")}</h2>
              {!aiConnection && (
                <button className="btn btn-primary btn-sm" onClick={() => setShowAiModal(true)}>
                  {t("configuration.ai.configure")}
                </button>
              )}
            </div>

            {aiConnection ? (
              <div className="ai-card card">
                <div className="ai-info">
                  <h3>{aiConnection.name}</h3>
                  <div className="ai-details">
                    <div className="detail-row">
                      <span className="label">{t("configuration.ai.endpoint")}:</span>
                      <span className="value">{aiConnection.endpoint}</span>
                    </div>
                    <div className="detail-row">
                      <span className="label">{t("configuration.ai.deployment")}:</span>
                      <span className="value">{aiConnection.deploymentName}</span>
                    </div>
                    <div className="detail-row">
                      <span className="label">{t("common.status")}:</span>
                      <span className={`status-badge status-${aiConnection.status}`}>
                        {aiConnection.status === "active" ? `✓ ${t("common.active")}` : `✕ ${t("common.inactive")}`}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="ai-actions">
                  <button className="btn btn-outline" onClick={() => setShowAiModal(true)}>
                    {t("common.edit")}
                  </button>
                  <button className="btn btn-ghost" onClick={handleTestAiConnection}>
                    {t("configuration.ai.testConnection")}
                  </button>
                </div>
              </div>
            ) : (
              <div className="ai-empty card">
                <span className="empty-icon">🤖</span>
                <h3>{t("configuration.ai.notConfigured")}</h3>
                <p className="text-muted">
                  {t("configuration.ai.description")}
                </p>
                <button className="btn btn-primary" onClick={() => setShowAiModal(true)}>
                  {t("configuration.ai.configureAzureAi")}
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Modals */}
      {showAzureModal && (
        <ConnectionModal
          title={t("configuration.modal.newAzure")}
          onClose={() => setShowAzureModal(false)}
          onSave={handleSaveAzureConnection}
          saving={saving}
          fields={[
            { name: "name", label: t("configuration.fields.name"), type: "text", required: true },
            { name: "tenantId", label: t("configuration.fields.tenantId"), type: "text", required: true, placeholder: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" },
            { name: "subscriptionId", label: t("configuration.fields.subscriptionId"), type: "text", required: true, placeholder: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" },
            { name: "clientId", label: t("configuration.fields.clientId"), type: "text", required: true, placeholder: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" },
            { name: "clientSecret", label: t("configuration.fields.clientSecret"), type: "password", required: true },
          ]}
          t={t}
        />
      )}

      {showAdoModal && (
        <ConnectionModal
          title={t("configuration.modal.newAdo")}
          onClose={() => setShowAdoModal(false)}
          onSave={handleSaveAdoConnection}
          saving={saving}
          fields={[
            { name: "name", label: t("configuration.fields.name"), type: "text", required: true },
            { name: "organizationUrl", label: t("configuration.fields.organizationUrl"), type: "text", required: true, placeholder: "https://dev.azure.com/myorg" },
            { name: "pat", label: t("configuration.fields.pat"), type: "password", required: true },
          ]}
          t={t}
        />
      )}

      {showRecipientModal && (
        <ConnectionModal
          title={t("configuration.modal.newRecipient")}
          onClose={() => setShowRecipientModal(false)}
          onSave={handleSaveRecipient}
          saving={saving}
          fields={[
            { name: "email", label: "Email", type: "email", required: true },
            { name: "notifyOn", label: t("configuration.recipients.notifyOn.label"), type: "select", required: true, options: [
              { value: "always", label: t("configuration.recipients.notifyOn.always") },
              { value: "drift_only", label: t("configuration.recipients.notifyOn.driftOnly") },
              { value: "weekly", label: t("configuration.recipients.notifyOn.weekly") },
            ]},
          ]}
          t={t}
        />
      )}

      {showAiModal && (
        <ConnectionModal
          title={t("configuration.modal.configureAi")}
          onClose={() => setShowAiModal(false)}
          onSave={handleSaveAiConnection}
          saving={saving}
          fields={[
            { name: "name", label: t("configuration.fields.name"), type: "text", required: true },
            { name: "endpoint", label: t("configuration.ai.endpoint"), type: "text", required: true, placeholder: "https://myai.openai.azure.com" },
            { name: "deploymentName", label: t("configuration.fields.deploymentName"), type: "text", required: true, placeholder: "gpt-5" },
            { name: "apiKey", label: t("configuration.ai.apiKey"), type: "password", required: true },
          ]}
          t={t}
        />
      )}
    </div>
  );
}

// Generic Modal Component
interface FieldConfig {
  name: string;
  label: string;
  type: "text" | "password" | "email" | "select";
  required?: boolean;
  placeholder?: string;
  options?: { value: string; label: string }[];
}

interface ConnectionModalProps {
  title: string;
  onClose: () => void;
  onSave: (data: Record<string, string>) => void;
  fields: FieldConfig[];
  t: (key: string) => string;
  saving?: boolean;
}

function ConnectionModal({ title, onClose, onSave, fields, t, saving }: ConnectionModalProps) {
  const [formData, setFormData] = useState<Record<string, string>>({});

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{title}</h2>
          <button className="btn btn-ghost" onClick={onClose} disabled={saving}>✕</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {fields.map((field) => (
              <div key={field.name} className="form-group">
                <label>{field.label} {field.required && <span className="required">*</span>}</label>
                {field.type === "select" ? (
                  <select
                    value={formData[field.name] || ""}
                    onChange={(e) => setFormData({ ...formData, [field.name]: e.target.value })}
                    required={field.required}
                    disabled={saving}
                  >
                    <option value="">{t("common.select")}...</option>
                    {field.options?.map((opt) => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                ) : (
                  <input
                    type={field.type}
                    value={formData[field.name] || ""}
                    onChange={(e) => setFormData({ ...formData, [field.name]: e.target.value })}
                    placeholder={field.placeholder}
                    required={field.required}
                    disabled={saving}
                  />
                )}
              </div>
            ))}
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-ghost" onClick={onClose} disabled={saving}>{t("common.cancel")}</button>
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? t("common.loading") : t("common.save")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
