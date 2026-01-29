import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

interface DriftRecord {
  id: string;
  pipelineId: string;
  pipelineName: string;
  severity: string;
  resourceId: string;
  resourceType: string;
  resourceName: string;
  property: string;
  expectedValue: string;
  actualValue: string;
  description: string;
  recommendation: string;
  category: string;
  detectedAt: string;
}

interface DriftStats {
  total: number;
  critical: number;
  high: number;
  medium: number;
  low: number;
  lastAnalysis: string | null;
}

export function DriftHistory() {
  const { t } = useTranslation();
  const [drifts, setDrifts] = useState<DriftRecord[]>([]);
  const [stats, setStats] = useState<DriftStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filters, setFilters] = useState({
    severity: '',
    pipelineId: '',
    from: '',
    to: ''
  });
  const [selectedDrift, setSelectedDrift] = useState<DriftRecord | null>(null);

  useEffect(() => {
    fetchStats();
  }, []);

  useEffect(() => {
    fetchDrifts();
  }, [page, filters]);

  const fetchStats = async () => {
    try {
      const response = await fetch(`${API_URL}/drift/stats`);
      if (response.ok) {
        const data = await response.json();
        setStats(data);
      }
    } catch (error) {
      console.error('Failed to fetch stats:', error);
    }
  };

  const fetchDrifts = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: '20'
      });
      if (filters.severity) params.append('severity', filters.severity);
      if (filters.pipelineId) params.append('pipelineId', filters.pipelineId);
      if (filters.from) params.append('from', filters.from);
      if (filters.to) params.append('to', filters.to);

      const response = await fetch(`${API_URL}/drift?${params}`);
      if (response.ok) {
        const data = await response.json();
        setDrifts(data.items);
        setTotalPages(Math.ceil(data.total / 20));
      }
    } catch (error) {
      console.error('Failed to fetch drifts:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleExport = async (format: 'csv' | 'json') => {
    try {
      const response = await fetch(`${API_URL}/drift/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...filters, format })
      });

      if (response.ok) {
        if (format === 'csv') {
          const blob = await response.blob();
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `drift-export-${new Date().toISOString().split('T')[0]}.csv`;
          a.click();
        } else {
          const data = await response.json();
          const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `drift-export-${new Date().toISOString().split('T')[0]}.json`;
          a.click();
        }
      }
    } catch (error) {
      console.error('Export failed:', error);
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

  return (
    <div className="drift-history">
      <header className="page-header">
        <h1>{t('drift.title')}</h1>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={() => handleExport('csv')}>
            {t('common.exportCsv')}
          </button>
          <button className="btn btn-secondary" onClick={() => handleExport('json')}>
            {t('common.exportJson')}
          </button>
        </div>
      </header>

      {stats && (
        <div className="stats-cards">
          <div className="stat-card total">
            <span className="stat-value">{stats.total}</span>
            <span className="stat-label">{t('drift.totalDrifts')}</span>
          </div>
          <div className="stat-card critical">
            <span className="stat-value">{stats.critical}</span>
            <span className="stat-label">{t('drift.critical')}</span>
          </div>
          <div className="stat-card high">
            <span className="stat-value">{stats.high}</span>
            <span className="stat-label">{t('drift.high')}</span>
          </div>
          <div className="stat-card medium">
            <span className="stat-value">{stats.medium}</span>
            <span className="stat-label">{t('drift.medium')}</span>
          </div>
          <div className="stat-card low">
            <span className="stat-value">{stats.low}</span>
            <span className="stat-label">{t('drift.low')}</span>
          </div>
        </div>
      )}

      <div className="filters">
        <select
          value={filters.severity}
          onChange={(e) => setFilters({ ...filters, severity: e.target.value })}
        >
          <option value="">{t('drift.allSeverities')}</option>
          <option value="critical">{t('drift.critical')}</option>
          <option value="high">{t('drift.high')}</option>
          <option value="medium">{t('drift.medium')}</option>
          <option value="low">{t('drift.low')}</option>
        </select>

        <input
          type="date"
          value={filters.from}
          onChange={(e) => setFilters({ ...filters, from: e.target.value })}
          placeholder={t('common.from')}
        />
        <input
          type="date"
          value={filters.to}
          onChange={(e) => setFilters({ ...filters, to: e.target.value })}
          placeholder={t('common.to')}
        />
      </div>

      {loading ? (
        <div className="loading">{t('common.loading')}</div>
      ) : (
        <div className="drift-table">
          <table>
            <thead>
              <tr>
                <th>{t('drift.severity')}</th>
                <th>{t('drift.pipeline')}</th>
                <th>{t('drift.resource')}</th>
                <th>{t('drift.property')}</th>
                <th>{t('drift.category')}</th>
                <th>{t('drift.detectedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {drifts.map((drift) => (
                <tr key={drift.id} onClick={() => setSelectedDrift(drift)}>
                  <td>
                    <span
                      className="severity-badge"
                      style={{ backgroundColor: getSeverityColor(drift.severity) }}
                    >
                      {drift.severity}
                    </span>
                  </td>
                  <td>{drift.pipelineName}</td>
                  <td>{drift.resourceName}</td>
                  <td>{drift.property}</td>
                  <td>{drift.category}</td>
                  <td>{new Date(drift.detectedAt).toLocaleDateString()}</td>
                </tr>
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

      {selectedDrift && (
        <div className="modal-overlay" onClick={() => setSelectedDrift(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{t('drift.details')}</h2>
              <button className="close-btn" onClick={() => setSelectedDrift(null)}>&times;</button>
            </div>
            <div className="modal-body">
              <div className="detail-row">
                <label>{t('drift.severity')}:</label>
                <span
                  className="severity-badge"
                  style={{ backgroundColor: getSeverityColor(selectedDrift.severity) }}
                >
                  {selectedDrift.severity}
                </span>
              </div>
              <div className="detail-row">
                <label>{t('drift.resource')}:</label>
                <span>{selectedDrift.resourceName} ({selectedDrift.resourceType})</span>
              </div>
              <div className="detail-row">
                <label>{t('drift.property')}:</label>
                <span>{selectedDrift.property}</span>
              </div>
              <div className="detail-row">
                <label>{t('drift.expected')}:</label>
                <code>{selectedDrift.expectedValue}</code>
              </div>
              <div className="detail-row">
                <label>{t('drift.actual')}:</label>
                <code>{selectedDrift.actualValue}</code>
              </div>
              <div className="detail-row">
                <label>{t('drift.description')}:</label>
                <p>{selectedDrift.description}</p>
              </div>
              <div className="detail-row">
                <label>{t('drift.recommendation')}:</label>
                <p>{selectedDrift.recommendation}</p>
              </div>
            </div>
          </div>
        </div>
      )}

      <style>{`
        .drift-history {
          padding: 24px;
        }

        .page-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 24px;
        }

        .header-actions {
          display: flex;
          gap: 8px;
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

        .stat-card.critical .stat-value { color: var(--danger); }
        .stat-card.high .stat-value { color: var(--warning); }
        .stat-card.medium .stat-value { color: var(--info); }
        .stat-card.low .stat-value { color: var(--text-secondary); }

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

        .drift-table table {
          width: 100%;
          border-collapse: collapse;
        }

        .drift-table th, .drift-table td {
          padding: 12px;
          text-align: left;
          border-bottom: 1px solid var(--border-color);
        }

        .drift-table tbody tr {
          cursor: pointer;
        }

        .drift-table tbody tr:hover {
          background: var(--surface-hover);
        }

        .severity-badge {
          padding: 4px 8px;
          border-radius: 4px;
          color: white;
          font-size: 12px;
          font-weight: 600;
          text-transform: uppercase;
        }

        .pagination {
          display: flex;
          justify-content: center;
          align-items: center;
          gap: 16px;
          margin-top: 16px;
        }

        .modal-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0, 0, 0, 0.5);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 1000;
        }

        .modal {
          background: var(--surface);
          border-radius: 8px;
          max-width: 600px;
          width: 90%;
          max-height: 80vh;
          overflow: auto;
        }

        .modal-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 16px;
          border-bottom: 1px solid var(--border-color);
        }

        .close-btn {
          background: none;
          border: none;
          font-size: 24px;
          cursor: pointer;
          color: var(--text-secondary);
        }

        .modal-body {
          padding: 16px;
        }

        .detail-row {
          margin-bottom: 16px;
        }

        .detail-row label {
          display: block;
          font-weight: 600;
          margin-bottom: 4px;
          color: var(--text-secondary);
        }

        .detail-row code {
          display: block;
          background: var(--surface-secondary);
          padding: 8px;
          border-radius: 4px;
          font-family: monospace;
          word-break: break-all;
        }

        .loading {
          text-align: center;
          padding: 48px;
          color: var(--text-secondary);
        }

        .btn {
          padding: 8px 16px;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: 500;
        }

        .btn-secondary {
          background: var(--surface-secondary);
          color: var(--text-primary);
        }
      `}</style>
    </div>
  );
}

export default DriftHistory;
