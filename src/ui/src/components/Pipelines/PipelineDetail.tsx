import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from '../../hooks/useNavigate';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

interface PipelineDetails {
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
  lastScanAt: string | null;
  driftCount: number;
  recentDrifts: Array<{
    id: string;
    severity: string;
    resourceName: string;
    description: string;
    detectedAt: string;
  }>;
  recentScans: Array<{
    id: string;
    status: string;
    startedAt: string;
    driftCount: number;
    durationSeconds: number;
  }>;
}

interface PipelineDetailProps {
  id: string;
}

export function PipelineDetail({ id }: PipelineDetailProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [pipeline, setPipeline] = useState<PipelineDetails | null>(null);
  const [activeTab, setActiveTab] = useState<'overview' | 'drifts' | 'scans'>('overview');
  const [loading, setLoading] = useState(true);
  const [scanning, setScanning] = useState(false);

  useEffect(() => {
    if (id) {
      fetchPipeline();
    }
  }, [id]);

  const fetchPipeline = async () => {
    try {
      const response = await fetch(`${API_URL}/pipelines/${id}`);
      if (response.ok) {
        const data = await response.json();
        setPipeline(data);
      } else if (response.status === 404) {
        navigate('/pipelines');
      }
    } catch (error) {
      console.error('Failed to fetch pipeline:', error);
    } finally {
      setLoading(false);
    }
  };

  const triggerScan = async () => {
    setScanning(true);
    try {
      const response = await fetch(`${API_URL}/pipelines/${id}/scan`, {
        method: 'POST'
      });
      if (response.ok) {
        const data = await response.json();
        // Show notification or update UI
        console.log('Scan triggered:', data.correlationId);
      }
    } catch (error) {
      console.error('Failed to trigger scan:', error);
    } finally {
      setScanning(false);
    }
  };

  const getSeverityColor = (severity: string) => {
    switch (severity?.toLowerCase()) {
      case 'critical': return 'var(--danger)';
      case 'high': return 'var(--warning)';
      case 'medium': return 'var(--info)';
      case 'low': return 'var(--text-secondary)';
      default: return 'var(--text-tertiary)';
    }
  };

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'success': return 'var(--success)';
      case 'failed': return 'var(--danger)';
      default: return 'var(--text-secondary)';
    }
  };

  if (loading) {
    return <div className="loading">{t('common.loading')}</div>;
  }

  if (!pipeline) {
    return <div className="not-found">{t('pipelines.notFound')}</div>;
  }

  return (
    <div className="pipeline-detail">
      <header className="page-header">
        <div className="header-left">
          <button className="back-btn" onClick={() => navigate('/pipelines')}>
            ← {t('common.back')}
          </button>
          <h1>{pipeline.pipelineName}</h1>
          <span className={`status-badge ${pipeline.isActive ? 'active' : 'inactive'}`}>
            {pipeline.isActive ? t('pipelines.active') : t('pipelines.inactive')}
          </span>
        </div>
        <div className="header-actions">
          <a
            href={`${pipeline.organizationUrl}/${pipeline.projectName}/_build?definitionId=${pipeline.pipelineId}`}
            target="_blank"
            rel="noopener noreferrer"
            className="btn btn-secondary"
          >
            {t('pipelines.viewInAdo')}
          </a>
          <button
            className="btn btn-primary"
            onClick={triggerScan}
            disabled={scanning}
          >
            {scanning ? t('pipelines.scanning') : t('pipelines.scanNow')}
          </button>
        </div>
      </header>

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'overview' ? 'active' : ''}`}
          onClick={() => setActiveTab('overview')}
        >
          {t('pipelines.overview')}
        </button>
        <button
          className={`tab ${activeTab === 'drifts' ? 'active' : ''}`}
          onClick={() => setActiveTab('drifts')}
        >
          {t('pipelines.drifts')} ({pipeline.driftCount})
        </button>
        <button
          className={`tab ${activeTab === 'scans' ? 'active' : ''}`}
          onClick={() => setActiveTab('scans')}
        >
          {t('pipelines.scanHistory')}
        </button>
      </div>

      <div className="tab-content">
        {activeTab === 'overview' && (
          <div className="overview">
            <div className="info-grid">
              <div className="info-card">
                <h3>{t('pipelines.projectInfo')}</h3>
                <div className="info-row">
                  <label>{t('pipelines.project')}:</label>
                  <span>{pipeline.projectName}</span>
                </div>
                <div className="info-row">
                  <label>{t('pipelines.organization')}:</label>
                  <span>{pipeline.organizationUrl}</span>
                </div>
                <div className="info-row">
                  <label>{t('pipelines.pipelineId')}:</label>
                  <span>{pipeline.pipelineId}</span>
                </div>
              </div>

              <div className="info-card">
                <h3>{t('pipelines.connections')}</h3>
                <div className="info-row">
                  <label>{t('pipelines.adoConnection')}:</label>
                  <span>{pipeline.adoConnectionName || '-'}</span>
                </div>
                <div className="info-row">
                  <label>{t('pipelines.azureConnection')}:</label>
                  <span>{pipeline.azureConnectionName || '-'}</span>
                </div>
              </div>

              <div className="info-card">
                <h3>{t('pipelines.scanStatus')}</h3>
                <div className="info-row">
                  <label>{t('pipelines.lastScan')}:</label>
                  <span>
                    {pipeline.lastScanAt
                      ? new Date(pipeline.lastScanAt).toLocaleString()
                      : t('pipelines.neverScanned')}
                  </span>
                </div>
                <div className="info-row">
                  <label>{t('pipelines.currentDrifts')}:</label>
                  <span className={pipeline.driftCount > 0 ? 'highlight' : ''}>
                    {pipeline.driftCount}
                  </span>
                </div>
              </div>
            </div>

            {pipeline.recentDrifts.length > 0 && (
              <div className="recent-section">
                <h3>{t('pipelines.recentDrifts')}</h3>
                <table>
                  <thead>
                    <tr>
                      <th>{t('drift.severity')}</th>
                      <th>{t('drift.resource')}</th>
                      <th>{t('drift.description')}</th>
                      <th>{t('drift.detectedAt')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pipeline.recentDrifts.map(drift => (
                      <tr key={drift.id}>
                        <td>
                          <span
                            className="severity-badge"
                            style={{ backgroundColor: getSeverityColor(drift.severity) }}
                          >
                            {drift.severity}
                          </span>
                        </td>
                        <td>{drift.resourceName}</td>
                        <td>{drift.description}</td>
                        <td>{new Date(drift.detectedAt).toLocaleDateString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <button
                  className="btn btn-link"
                  onClick={() => setActiveTab('drifts')}
                >
                  {t('common.viewAll')}
                </button>
              </div>
            )}
          </div>
        )}

        {activeTab === 'drifts' && (
          <div className="drifts-tab">
            <table>
              <thead>
                <tr>
                  <th>{t('drift.severity')}</th>
                  <th>{t('drift.resource')}</th>
                  <th>{t('drift.property')}</th>
                  <th>{t('drift.expected')}</th>
                  <th>{t('drift.actual')}</th>
                  <th>{t('drift.detectedAt')}</th>
                </tr>
              </thead>
              <tbody>
                {pipeline.recentDrifts.map(drift => (
                  <tr key={drift.id}>
                    <td>
                      <span
                        className="severity-badge"
                        style={{ backgroundColor: getSeverityColor(drift.severity) }}
                      >
                        {drift.severity}
                      </span>
                    </td>
                    <td>{drift.resourceName}</td>
                    <td>-</td>
                    <td>-</td>
                    <td>-</td>
                    <td>{new Date(drift.detectedAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'scans' && (
          <div className="scans-tab">
            <table>
              <thead>
                <tr>
                  <th>{t('scans.status')}</th>
                  <th>{t('scans.startedAt')}</th>
                  <th>{t('scans.duration')}</th>
                  <th>{t('scans.driftsFound')}</th>
                </tr>
              </thead>
              <tbody>
                {pipeline.recentScans.map(scan => (
                  <tr key={scan.id}>
                    <td>
                      <span
                        className="status-badge-small"
                        style={{ color: getStatusColor(scan.status) }}
                      >
                        {scan.status}
                      </span>
                    </td>
                    <td>{new Date(scan.startedAt).toLocaleString()}</td>
                    <td>{scan.durationSeconds ? `${scan.durationSeconds}s` : '-'}</td>
                    <td>{scan.driftCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <style>{`
        .pipeline-detail {
          padding: 24px;
        }

        .page-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 24px;
          flex-wrap: wrap;
          gap: 16px;
        }

        .header-left {
          display: flex;
          align-items: center;
          gap: 16px;
        }

        .back-btn {
          background: none;
          border: none;
          color: var(--primary);
          cursor: pointer;
          font-size: 14px;
        }

        .header-actions {
          display: flex;
          gap: 8px;
        }

        .status-badge {
          padding: 4px 8px;
          border-radius: 4px;
          font-size: 12px;
        }

        .status-badge.active {
          background: var(--success);
          color: white;
        }

        .status-badge.inactive {
          background: var(--text-secondary);
          color: white;
        }

        .tabs {
          display: flex;
          gap: 4px;
          border-bottom: 1px solid var(--border-color);
          margin-bottom: 24px;
        }

        .tab {
          padding: 12px 24px;
          background: none;
          border: none;
          cursor: pointer;
          color: var(--text-secondary);
          border-bottom: 2px solid transparent;
          margin-bottom: -1px;
        }

        .tab.active {
          color: var(--primary);
          border-bottom-color: var(--primary);
        }

        .info-grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
          gap: 16px;
          margin-bottom: 24px;
        }

        .info-card {
          background: var(--surface-elevated);
          border-radius: 8px;
          padding: 16px;
        }

        .info-card h3 {
          margin: 0 0 16px 0;
          font-size: 14px;
          color: var(--text-secondary);
          text-transform: uppercase;
        }

        .info-row {
          display: flex;
          justify-content: space-between;
          margin-bottom: 8px;
        }

        .info-row label {
          color: var(--text-secondary);
        }

        .info-row .highlight {
          color: var(--warning);
          font-weight: 600;
        }

        .recent-section {
          margin-top: 24px;
        }

        .recent-section h3 {
          margin-bottom: 16px;
        }

        table {
          width: 100%;
          border-collapse: collapse;
        }

        th, td {
          padding: 12px;
          text-align: left;
          border-bottom: 1px solid var(--border-color);
        }

        .severity-badge {
          padding: 4px 8px;
          border-radius: 4px;
          color: white;
          font-size: 12px;
          font-weight: 600;
          text-transform: uppercase;
        }

        .btn {
          padding: 8px 16px;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 500;
        }

        .btn-primary {
          background: var(--primary);
          color: white;
        }

        .btn-secondary {
          background: var(--surface-secondary);
          color: var(--text-primary);
          text-decoration: none;
        }

        .btn-link {
          background: none;
          color: var(--primary);
          padding: 8px 0;
        }

        .btn:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        .loading, .not-found {
          text-align: center;
          padding: 48px;
          color: var(--text-secondary);
        }
      `}</style>
    </div>
  );
}

export default PipelineDetail;
