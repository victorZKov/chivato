import { useState, useEffect } from "react";
import { useRoles } from "../../hooks/useRoles";
import "./Configuration.css";

type TabType = "connections" | "timer" | "recipients" | "ai";

interface AzureConnection {
  id: string;
  name: string;
  tenantId: string;
  clientId: string;
  status: "active" | "expiring" | "expired";
  expiresAt?: string;
}

interface AdoConnection {
  id: string;
  name: string;
  organizationUrl: string;
  authType: "PAT" | "OAuth";
  status: "active" | "expiring" | "expired";
  expiresAt?: string;
}

interface AiConnection {
  id: string;
  name: string;
  endpoint: string;
  deploymentName: string;
  status: "active" | "inactive";
}

interface EmailRecipient {
  id: string;
  email: string;
  notifyOn: "always" | "drift_only" | "weekly";
  isActive: boolean;
}

interface TimerConfig {
  intervalHours: number;
  isEnabled: boolean;
  nextRunAt?: string;
}

export function Configuration() {
  const { isAdmin } = useRoles();
  const [activeTab, setActiveTab] = useState<TabType>("connections");
  const [loading, setLoading] = useState(true);

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
    try {
      // In production, these would be API calls
      setAzureConnections(mockAzureConnections);
      setAdoConnections(mockAdoConnections);
      setAiConnection(mockAiConnection);
      setEmailRecipients(mockEmailRecipients);
      setTimerConfig(mockTimerConfig);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteAzureConnection = async (id: string) => {
    if (!window.confirm("¿Eliminar esta conexión Azure?")) return;
    setAzureConnections((prev) => prev.filter((c) => c.id !== id));
  };

  const handleDeleteAdoConnection = async (id: string) => {
    if (!window.confirm("¿Eliminar esta conexión ADO?")) return;
    setAdoConnections((prev) => prev.filter((c) => c.id !== id));
  };

  const handleDeleteRecipient = async (id: string) => {
    if (!window.confirm("¿Eliminar este destinatario?")) return;
    setEmailRecipients((prev) => prev.filter((r) => r.id !== id));
  };

  const handleToggleRecipient = async (id: string) => {
    setEmailRecipients((prev) =>
      prev.map((r) => (r.id === id ? { ...r, isActive: !r.isActive } : r))
    );
  };

  const handleTimerChange = (field: keyof TimerConfig, value: number | boolean) => {
    setTimerConfig((prev) => ({ ...prev, [field]: value }));
  };

  const handleSaveTimer = async () => {
    console.log("Saving timer config:", timerConfig);
    alert("Configuración de timer guardada");
  };

  if (loading) {
    return <div className="config-loading">Cargando...</div>;
  }

  if (!isAdmin) {
    return (
      <div className="config-restricted">
        <h2>Acceso Restringido</h2>
        <p>Necesitas el rol de Administrador para acceder a esta sección.</p>
      </div>
    );
  }

  return (
    <div className="configuration">
      <div className="config-header">
        <h1>Configuración</h1>
        <p className="text-muted">Gestiona conexiones, timer y notificaciones</p>
      </div>

      <div className="config-tabs">
        <button
          className={`tab-btn ${activeTab === "connections" ? "active" : ""}`}
          onClick={() => setActiveTab("connections")}
        >
          Conexiones
        </button>
        <button
          className={`tab-btn ${activeTab === "timer" ? "active" : ""}`}
          onClick={() => setActiveTab("timer")}
        >
          Timer
        </button>
        <button
          className={`tab-btn ${activeTab === "recipients" ? "active" : ""}`}
          onClick={() => setActiveTab("recipients")}
        >
          Destinatarios
        </button>
        <button
          className={`tab-btn ${activeTab === "ai" ? "active" : ""}`}
          onClick={() => setActiveTab("ai")}
        >
          Azure AI
        </button>
      </div>

      <div className="config-content">
        {activeTab === "connections" && (
          <div className="connections-tab">
            {/* Azure Connections */}
            <section className="connection-section">
              <div className="section-header">
                <h2>Conexiones Azure</h2>
                <button className="btn btn-primary btn-sm" onClick={() => setShowAzureModal(true)}>
                  + Añadir
                </button>
              </div>

              {azureConnections.length === 0 ? (
                <p className="text-muted">No hay conexiones Azure configuradas</p>
              ) : (
                <div className="connection-list">
                  {azureConnections.map((conn) => (
                    <div key={conn.id} className="connection-card">
                      <div className="connection-info">
                        <span className="connection-name">{conn.name}</span>
                        <span className="connection-detail">Tenant: {conn.tenantId}</span>
                        <span className="connection-detail">Client: {conn.clientId}</span>
                      </div>
                      <div className="connection-status">
                        <span className={`status-badge status-${conn.status}`}>
                          {conn.status === "active" && "✓ Activo"}
                          {conn.status === "expiring" && `⚠ Expira ${conn.expiresAt}`}
                          {conn.status === "expired" && "✕ Expirado"}
                        </span>
                      </div>
                      <div className="connection-actions">
                        <button className="btn btn-ghost btn-sm">Editar</button>
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleDeleteAzureConnection(conn.id)}
                        >
                          Eliminar
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
                <h2>Conexiones Azure DevOps</h2>
                <button className="btn btn-primary btn-sm" onClick={() => setShowAdoModal(true)}>
                  + Añadir
                </button>
              </div>

              {adoConnections.length === 0 ? (
                <p className="text-muted">No hay conexiones ADO configuradas</p>
              ) : (
                <div className="connection-list">
                  {adoConnections.map((conn) => (
                    <div key={conn.id} className="connection-card">
                      <div className="connection-info">
                        <span className="connection-name">{conn.name}</span>
                        <span className="connection-detail">{conn.organizationUrl}</span>
                        <span className="connection-detail">Auth: {conn.authType}</span>
                      </div>
                      <div className="connection-status">
                        <span className={`status-badge status-${conn.status}`}>
                          {conn.status === "active" && "✓ Activo"}
                          {conn.status === "expiring" && `⚠ Expira ${conn.expiresAt}`}
                          {conn.status === "expired" && "✕ Expirado"}
                        </span>
                      </div>
                      <div className="connection-actions">
                        <button className="btn btn-ghost btn-sm">Editar</button>
                        <button
                          className="btn btn-ghost btn-sm"
                          onClick={() => handleDeleteAdoConnection(conn.id)}
                        >
                          Eliminar
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
              <h2>Configuración del Timer</h2>
              <p className="text-muted">
                Configura la frecuencia con la que se ejecuta el análisis de drift.
              </p>

              <div className="timer-form">
                <div className="form-group">
                  <label>Estado</label>
                  <div className="toggle-container">
                    <button
                      className={`toggle-btn ${timerConfig.isEnabled ? "active" : ""}`}
                      onClick={() => handleTimerChange("isEnabled", !timerConfig.isEnabled)}
                    >
                      <span className="toggle-slider" />
                    </button>
                    <span>{timerConfig.isEnabled ? "Activo" : "Pausado"}</span>
                  </div>
                </div>

                <div className="form-group">
                  <label>Intervalo de ejecución</label>
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
                    El análisis se ejecutará cada {timerConfig.intervalHours} horas
                  </p>
                </div>

                {timerConfig.nextRunAt && (
                  <div className="next-run">
                    <span className="text-muted">Próxima ejecución:</span>
                    <span>{timerConfig.nextRunAt}</span>
                  </div>
                )}

                <button className="btn btn-primary" onClick={handleSaveTimer}>
                  Guardar Cambios
                </button>
              </div>
            </div>
          </div>
        )}

        {activeTab === "recipients" && (
          <div className="recipients-tab">
            <div className="section-header">
              <h2>Destinatarios de Email</h2>
              <button className="btn btn-primary btn-sm" onClick={() => setShowRecipientModal(true)}>
                + Añadir
              </button>
            </div>

            {emailRecipients.length === 0 ? (
              <p className="text-muted">No hay destinatarios configurados</p>
            ) : (
              <div className="recipients-list">
                {emailRecipients.map((recipient) => (
                  <div key={recipient.id} className={`recipient-card ${!recipient.isActive ? "inactive" : ""}`}>
                    <div className="recipient-info">
                      <span className="recipient-email">{recipient.email}</span>
                      <span className="recipient-setting">
                        {recipient.notifyOn === "always" && "Todas las notificaciones"}
                        {recipient.notifyOn === "drift_only" && "Solo cuando hay drift"}
                        {recipient.notifyOn === "weekly" && "Resumen semanal"}
                      </span>
                    </div>
                    <div className="recipient-actions">
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => handleToggleRecipient(recipient.id)}
                      >
                        {recipient.isActive ? "Desactivar" : "Activar"}
                      </button>
                      <button
                        className="btn btn-ghost btn-sm"
                        onClick={() => handleDeleteRecipient(recipient.id)}
                      >
                        Eliminar
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
              <h2>Configuración Azure AI</h2>
              {!aiConnection && (
                <button className="btn btn-primary btn-sm" onClick={() => setShowAiModal(true)}>
                  Configurar
                </button>
              )}
            </div>

            {aiConnection ? (
              <div className="ai-card card">
                <div className="ai-info">
                  <h3>{aiConnection.name}</h3>
                  <div className="ai-details">
                    <div className="detail-row">
                      <span className="label">Endpoint:</span>
                      <span className="value">{aiConnection.endpoint}</span>
                    </div>
                    <div className="detail-row">
                      <span className="label">Deployment:</span>
                      <span className="value">{aiConnection.deploymentName}</span>
                    </div>
                    <div className="detail-row">
                      <span className="label">Estado:</span>
                      <span className={`status-badge status-${aiConnection.status}`}>
                        {aiConnection.status === "active" ? "✓ Activo" : "✕ Inactivo"}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="ai-actions">
                  <button className="btn btn-outline" onClick={() => setShowAiModal(true)}>
                    Editar
                  </button>
                  <button className="btn btn-ghost">Probar conexión</button>
                </div>
              </div>
            ) : (
              <div className="ai-empty card">
                <span className="empty-icon">🤖</span>
                <h3>Azure AI no configurado</h3>
                <p className="text-muted">
                  Configura Azure AI Foundry para habilitar el análisis inteligente de drift
                  con GPT.
                </p>
                <button className="btn btn-primary" onClick={() => setShowAiModal(true)}>
                  Configurar Azure AI
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Modals would go here - simplified for brevity */}
      {showAzureModal && (
        <ConnectionModal
          title="Nueva Conexión Azure"
          onClose={() => setShowAzureModal(false)}
          onSave={(data) => {
            console.log("Save Azure connection:", data);
            setShowAzureModal(false);
          }}
          fields={[
            { name: "name", label: "Nombre", type: "text", required: true },
            { name: "tenantId", label: "Tenant ID", type: "text", required: true },
            { name: "clientId", label: "Client ID", type: "text", required: true },
            { name: "clientSecret", label: "Client Secret", type: "password", required: true },
          ]}
        />
      )}

      {showAdoModal && (
        <ConnectionModal
          title="Nueva Conexión Azure DevOps"
          onClose={() => setShowAdoModal(false)}
          onSave={(data) => {
            console.log("Save ADO connection:", data);
            setShowAdoModal(false);
          }}
          fields={[
            { name: "name", label: "Nombre", type: "text", required: true },
            { name: "organizationUrl", label: "URL de Organización", type: "text", required: true, placeholder: "https://dev.azure.com/myorg" },
            { name: "pat", label: "Personal Access Token", type: "password", required: true },
          ]}
        />
      )}

      {showRecipientModal && (
        <ConnectionModal
          title="Nuevo Destinatario"
          onClose={() => setShowRecipientModal(false)}
          onSave={(data) => {
            console.log("Save recipient:", data);
            setShowRecipientModal(false);
          }}
          fields={[
            { name: "email", label: "Email", type: "email", required: true },
            { name: "notifyOn", label: "Notificar", type: "select", required: true, options: [
              { value: "always", label: "Todas las notificaciones" },
              { value: "drift_only", label: "Solo cuando hay drift" },
              { value: "weekly", label: "Resumen semanal" },
            ]},
          ]}
        />
      )}

      {showAiModal && (
        <ConnectionModal
          title="Configurar Azure AI"
          onClose={() => setShowAiModal(false)}
          onSave={(data) => {
            console.log("Save AI connection:", data);
            setShowAiModal(false);
          }}
          fields={[
            { name: "name", label: "Nombre", type: "text", required: true },
            { name: "endpoint", label: "Endpoint", type: "text", required: true, placeholder: "https://myai.openai.azure.com" },
            { name: "deploymentName", label: "Deployment Name", type: "text", required: true, placeholder: "gpt-5" },
            { name: "apiKey", label: "API Key", type: "password", required: true },
          ]}
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
}

function ConnectionModal({ title, onClose, onSave, fields }: ConnectionModalProps) {
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
          <button className="btn btn-ghost" onClick={onClose}>✕</button>
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
                  >
                    <option value="">Seleccionar...</option>
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
                  />
                )}
              </div>
            ))}
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-ghost" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn btn-primary">Guardar</button>
          </div>
        </form>
      </div>
    </div>
  );
}

// Mock data
const mockAzureConnections: AzureConnection[] = [
  {
    id: "1",
    name: "Producción",
    tenantId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    clientId: "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    status: "active",
  },
  {
    id: "2",
    name: "Staging",
    tenantId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    clientId: "zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz",
    status: "expiring",
    expiresAt: "en 12 días",
  },
];

const mockAdoConnections: AdoConnection[] = [
  {
    id: "1",
    name: "MyOrg Principal",
    organizationUrl: "https://dev.azure.com/myorg",
    authType: "PAT",
    status: "active",
  },
];

const mockAiConnection: AiConnection = {
  id: "1",
  name: "Azure OpenAI - GPT-5",
  endpoint: "https://chivato-ai.openai.azure.com",
  deploymentName: "gpt-5",
  status: "active",
};

const mockEmailRecipients: EmailRecipient[] = [
  { id: "1", email: "admin@empresa.com", notifyOn: "always", isActive: true },
  { id: "2", email: "devops@empresa.com", notifyOn: "drift_only", isActive: true },
  { id: "3", email: "manager@empresa.com", notifyOn: "weekly", isActive: false },
];

const mockTimerConfig: TimerConfig = {
  intervalHours: 24,
  isEnabled: true,
  nextRunAt: "Mañana a las 08:00",
};
