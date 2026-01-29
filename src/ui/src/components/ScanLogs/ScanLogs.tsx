import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

interface ScanLog {
  id: string;
  pipelineId: string;
  pipelineName: string;
  startedAt: string;
  completedAt: string | null;
  status: string;
  driftCount: number;
  durationSeconds: number | null;
  triggeredBy: string;
  errorMessage: string | null;
  resourcesScanned: number;
}

interface ScanStats {
  total: number;
  success: number;
  failed: number;
  avgDurationSeconds: number;
}

export function ScanLogs() {
  const { t } = useTranslation();
  const [scans, setScans] = useState<ScanLog[]>([]);
  const [stats, setStats] = useState<ScanStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filters, setFilters] = useState({
    status: '',
    pipelineId: '',
    from: '',
    to: ''
  });
  const [expandedId, setExpandedId] = useState<string | null>(null);

  useEffect(() => {
    fetchStats();
  }, []);

  useEffect(() => {
    fetchScans();
  }, [page, filters]);

  const fetchStats = async () => {
    try {
      const response = await fetch(`${API_URL}/scans/stats`);
      if (response.ok) {
        const data = await response.json();
        setStats(data);
      }
    } catch (error) {
      console.error('Failed to fetch stats:', error);
    }
  };

  const fetchScans = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: '20'
      });
      if (filters.status) params.append('status', filters.status);
      if (filters.pipelineId) params.append('pipelineId', filters.pipelineId);
      if (filters.from) params.append('from', filters.from);
      if (filters.to) params.append('to', filters.to);

      const response = await fetch(`${API_URL}/scans?${params}`);
      if (response.ok) {
        const data = await response.json();
        setScans(data.items);
        setTotalPages(Math.ceil(data.total / 20));
      }
    } catch (error) {
      console.error('Failed to fetch scans:', error);
    } finally {
      setLoading(false);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'success': return 'var(--success)';
      case 'failed': return 'var(--danger)';
      case 'running': return 'var(--info)';
      default: return 'var(--text-secondary)';
    }
  };

  const formatDuration = (seconds: number | null) => {
    if (seconds === null) return '-';
    if (seconds < 60) return `${seconds}s`;
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}m ${secs}s`;
  };

  return (
    <div className="scan-logs">
      <header className="page-header">
        <h1>{t('scans.title')}</h1>
      </header>

      {stats && (
        <div className="stats-cards">
          <div className="stat-card total">
            <span className="stat-value">{stats.total}</span>
            <span className="stat-label">{t('scans.totalScans')}</span>
          </div>
          <div className="stat-card success">
            <span className="stat-value">{stats.success}</span>
            <span className="stat-label">{t('scans.successful')}</span>
          </div>
          <div className="stat-card failed">
            <span className="stat-value">{stats.failed}</span>
            <span className="stat-label">{t('scans.failed')}</span>
          </div>
          <div className="stat-card duration">
            <span className="stat-value">{formatDuration(stats.avgDurationSeconds)}</span>
            <span className="stat-label">{t('scans.avgDuration')}</span>
          </div>
        </div>
      )}

      <div className="filters">
        <select
          value={filters.status}
          onChange={(e) => setFilters({ ...filters, status: e.target.value })}
        >
          <option value="">{t('scans.allStatuses')}</option>
          <option value="success">{t('scans.successful')}</option>
          <option value="failed">{t('scans.failed')}</option>
          <option value="running">{t('scans.running')}</option>
        </select>

        <input
          type="date"
          value={filters.from}
          onChange={(e) => setFilters({ ...filters, from: e.target.value })}
        />
        <input
          type="date"
          value={filters.to}
          onChange={(e) => setFilters({ ...filters, to: e.target.value })}
        />
      </div>

      {loading ? (
        <div className="loading">{t('common.loading')}</div>
      ) : (
        <div className="scan-table">
          <table>
            <thead>
              <tr>
                <th></th>
                <th>{t('scans.status')}</th>
                <th>{t('scans.pipeline')}</th>
                <th>{t('scans.startedAt')}</th>
                <th>{t('scans.duration')}</th>
                <th>{t('scans.driftsFound')}</th>
                <th>{t('scans.triggeredBy')}</th>
              </tr>
            </thead>
            <tbody>
              {scans.map((scan) => (
                <React.Fragment key={scan.id}>
                  <tr
                    onClick={() => setExpandedId(expandedId === scan.id ? null : scan.id)}
                    className={scan.status === 'failed' ? 'error-row' : ''}
                  >
                    <td className="expand-cell">
                      {scan.errorMessage && (
                        <span className="expand-icon">{expandedId === scan.id ? '▼' : '▶'}</span>
                      )}
                    </td>
                    <td>
                      <span
                        className="status-badge"
                        style={{ backgroundColor: getStatusColor(scan.status) }}
                      >
                        {scan.status}
                      </span>
                    </td>
                    <td>{scan.pipelineName}</td>
                    <td>{new Date(scan.startedAt).toLocaleString()}</td>
                    <td>{formatDuration(scan.durationSeconds)}</td>
                    <td>{scan.driftCount}</td>
                    <td>{scan.triggeredBy}</td>
                  </tr>
                  {expandedId === scan.id && scan.errorMessage && (
                    <tr className="error-details">
                      <td colSpan={7}>
                        <div className="error-message">
                          <strong>{t('scans.error')}:</strong> {scan.errorMessage}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button disabled={page === 1} onClick={() => setPage(page - 1)}>
              {t('common.previous')}
            </button>
            <span>{t('common.pageOf', { page, total: totalPages })}</span>
            <button disabled={page === totalPages} onClick={() => setPage(page + 1)}>
              {t('common.next')}
            </button>
          </div>
        </div>
      )}

      <style>{`
        .scan-logs {
          padding: 24px;
        }

        .page-header {
          margin-bottom: 24px;
        }

        .stats-cards {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
          gap: 16px;
          margin-bottom: 24px;
        }

        .stat-card {
          background: var(--surface-elevated);
          border-radius: 8px;
          padding: 16px;
          text-align: center;
        }

        .stat-value {
          display: block;
          font-size: 32px;
          font-weight: 700;
        }

        .stat-label {
          font-size: 14px;
          color: var(--text-secondary);
        }

        .stat-card.success .stat-value { color: var(--success); }
        .stat-card.failed .stat-value { color: var(--danger); }

        .filters {
          display: flex;
          gap: 12px;
          margin-bottom: 16px;
          flex-wrap: wrap;
        }

        .filters select, .filters input {
          padding: 8px 12px;
          border: 1px solid var(--border-color);
          border-radius: 4px;
          background: var(--surface);
          color: var(--text-primary);
        }

        .scan-table table {
          width: 100%;
          border-collapse: collapse;
        }

        .scan-table th, .scan-table td {
          padding: 12px;
          text-align: left;
          border-bottom: 1px solid var(--border-color);
        }

        .scan-table tbody tr {
          cursor: pointer;
        }

        .scan-table tbody tr:hover {
          background: var(--surface-hover);
        }

        .error-row {
          background: rgba(var(--danger-rgb), 0.1);
        }

        .expand-cell {
          width: 30px;
        }

        .expand-icon {
          color: var(--text-secondary);
          font-size: 12px;
        }

        .status-badge {
          padding: 4px 8px;
          border-radius: 4px;
          color: white;
          font-size: 12px;
          font-weight: 600;
          text-transform: uppercase;
        }

        .error-details td {
          background: rgba(var(--danger-rgb), 0.05);
          padding: 0 12px 12px 42px;
        }

        .error-message {
          color: var(--danger);
          font-size: 14px;
        }

        .pagination {
          display: flex;
          justify-content: center;
          align-items: center;
          gap: 16px;
          margin-top: 16px;
        }

        .pagination button {
          padding: 8px 16px;
          border: 1px solid var(--border-color);
          border-radius: 4px;
          background: var(--surface);
          cursor: pointer;
        }

        .pagination button:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        .loading {
          text-align: center;
          padding: 48px;
          color: var(--text-secondary);
        }
      `}</style>
    </div>
  );
}

export default ScanLogs;
