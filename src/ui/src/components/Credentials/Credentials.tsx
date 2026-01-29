import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api';

interface Credential {
  id: string;
  name: string;
  type: string;
  expiresAt: string | null;
  daysUntilExpiration: number;
  status: string;
  lastTestedAt: string | null;
  lastTestResult: string | null;
}

export function Credentials() {
  const { t } = useTranslation();
  const [credentials, setCredentials] = useState<Credential[]>([]);
  const [expiring, setExpiring] = useState<Credential[]>([]);
  const [loading, setLoading] = useState(true);
  const [testing, setTesting] = useState<string | null>(null);
  const [rotateModal, setRotateModal] = useState<Credential | null>(null);
  const [newSecret, setNewSecret] = useState('');
  const [newExpiry, setNewExpiry] = useState('');

  useEffect(() => {
    fetchCredentials();
    fetchExpiring();
  }, []);

  const fetchCredentials = async () => {
    try {
      const response = await fetch(`${API_URL}/credentials`);
      if (response.ok) {
        const data = await response.json();
        setCredentials(data);
      }
    } catch (error) {
      console.error('Failed to fetch credentials:', error);
    } finally {
      setLoading(false);
    }
  };

  const fetchExpiring = async () => {
    try {
      const response = await fetch(`${API_URL}/credentials/expiring?days=30`);
      if (response.ok) {
        const data = await response.json();
        setExpiring(data);
      }
    } catch (error) {
      console.error('Failed to fetch expiring:', error);
    }
  };

  const testCredential = async (cred: Credential) => {
    setTesting(cred.id);
    try {
      const response = await fetch(`${API_URL}/credentials/${cred.type}/${cred.id}/test`, {
        method: 'POST'
      });
      const result = await response.json();

      // Update credential in list with test result
      setCredentials(prev =>
        prev.map(c =>
          c.id === cred.id
            ? { ...c, lastTestedAt: result.testedAt, lastTestResult: result.success ? 'success' : 'failed' }
            : c
        )
      );
    } catch (error) {
      console.error('Test failed:', error);
    } finally {
      setTesting(null);
    }
  };

  const rotateCredential = async () => {
    if (!rotateModal || !newSecret) return;

    try {
      const response = await fetch(
        `${API_URL}/credentials/${rotateModal.type}/${rotateModal.id}/rotate`,
        {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            newSecret,
            expiresAt: newExpiry || null
          })
        }
      );

      if (response.ok) {
        setRotateModal(null);
        setNewSecret('');
        setNewExpiry('');
        fetchCredentials();
        fetchExpiring();
      }
    } catch (error) {
      console.error('Rotation failed:', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'ok': return 'var(--success)';
      case 'warning': return 'var(--warning)';
      case 'danger': return 'var(--danger)';
      case 'expired': return 'var(--danger)';
      default: return 'var(--text-secondary)';
    }
  };

  const getTypeIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'azure': return '☁️';
      case 'ado': return '⚡';
      case 'ai': return '🤖';
      case 'email': return '📧';
      default: return '🔑';
    }
  };

  if (loading) {
    return <div className="loading">{t('common.loading')}</div>;
  }

  return (
    <div className="credentials">
      <header className="page-header">
        <h1>{t('credentials.title')}</h1>
      </header>

      {expiring.length > 0 && (
        <div className="alert alert-warning">
          <strong>{t('credentials.expirationWarning')}</strong>
          <p>
            {t('credentials.expiringCount', { count: expiring.length })}
          </p>
          <div className="expiring-list">
            {expiring.map(cred => (
              <div key={cred.id} className="expiring-item">
                <span>{cred.name}</span>
                <span className="days">
                  {cred.daysUntilExpiration <= 0
                    ? t('credentials.expired')
                    : t('credentials.daysRemaining', { days: cred.daysUntilExpiration })}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="credentials-grid">
        {credentials.map(cred => (
          <div key={cred.id} className="credential-card">
            <div className="card-header">
              <span className="type-icon">{getTypeIcon(cred.type)}</span>
              <div className="card-title">
                <h3>{cred.name}</h3>
                <span className="type-label">{cred.type}</span>
              </div>
              <span
                className="status-indicator"
                style={{ backgroundColor: getStatusColor(cred.status) }}
                title={cred.status}
              />
            </div>

            <div className="card-body">
              {cred.expiresAt && (
                <div className="info-row">
                  <label>{t('credentials.expires')}:</label>
                  <span className={cred.daysUntilExpiration <= 7 ? 'danger' : ''}>
                    {new Date(cred.expiresAt).toLocaleDateString()}
                    {cred.daysUntilExpiration > 0 && (
                      <small> ({cred.daysUntilExpiration} {t('credentials.days')})</small>
                    )}
                  </span>
                </div>
              )}

              {cred.lastTestedAt && (
                <div className="info-row">
                  <label>{t('credentials.lastTested')}:</label>
                  <span>
                    {new Date(cred.lastTestedAt).toLocaleString()}
                    {cred.lastTestResult && (
                      <span className={`test-result ${cred.lastTestResult}`}>
                        {cred.lastTestResult === 'success' ? '✓' : '✗'}
                      </span>
                    )}
                  </span>
                </div>
              )}
            </div>

            <div className="card-actions">
              <button
                className="btn btn-secondary"
                onClick={() => testCredential(cred)}
                disabled={testing === cred.id}
              >
                {testing === cred.id ? t('credentials.testing') : t('credentials.test')}
              </button>
              <button
                className="btn btn-primary"
                onClick={() => setRotateModal(cred)}
              >
                {t('credentials.rotate')}
              </button>
            </div>
          </div>
        ))}
      </div>

      {rotateModal && (
        <div className="modal-overlay" onClick={() => setRotateModal(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{t('credentials.rotateTitle', { name: rotateModal.name })}</h2>
              <button className="close-btn" onClick={() => setRotateModal(null)}>&times;</button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>{t('credentials.newSecret')}</label>
                <input
                  type="password"
                  value={newSecret}
                  onChange={(e) => setNewSecret(e.target.value)}
                  placeholder={t('credentials.enterNewSecret')}
                />
              </div>
              <div className="form-group">
                <label>{t('credentials.newExpiry')}</label>
                <input
                  type="date"
                  value={newExpiry}
                  onChange={(e) => setNewExpiry(e.target.value)}
                />
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setRotateModal(null)}>
                {t('common.cancel')}
              </button>
              <button
                className="btn btn-primary"
                onClick={rotateCredential}
                disabled={!newSecret}
              >
                {t('credentials.rotate')}
              </button>
            </div>
          </div>
        </div>
      )}

      <style>{`
        .credentials {
          padding: 24px;
        }

        .page-header {
          margin-bottom: 24px;
        }

        .alert {
          padding: 16px;
          border-radius: 8px;
          margin-bottom: 24px;
        }

        .alert-warning {
          background: rgba(var(--warning-rgb), 0.1);
          border: 1px solid var(--warning);
        }

        .expiring-list {
          margin-top: 12px;
        }

        .expiring-item {
          display: flex;
          justify-content: space-between;
          padding: 8px 0;
          border-bottom: 1px solid rgba(var(--warning-rgb), 0.2);
        }

        .expiring-item .days {
          color: var(--danger);
          font-weight: 500;
        }

        .credentials-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
          gap: 16px;
        }

        .credential-card {
          background: var(--surface-elevated);
          border-radius: 8px;
          border: 1px solid var(--border-color);
          overflow: hidden;
        }

        .card-header {
          display: flex;
          align-items: center;
          gap: 12px;
          padding: 16px;
          border-bottom: 1px solid var(--border-color);
        }

        .type-icon {
          font-size: 24px;
        }

        .card-title {
          flex: 1;
        }

        .card-title h3 {
          margin: 0;
          font-size: 16px;
        }

        .type-label {
          font-size: 12px;
          color: var(--text-secondary);
        }

        .status-indicator {
          width: 12px;
          height: 12px;
          border-radius: 50%;
        }

        .card-body {
          padding: 16px;
        }

        .info-row {
          display: flex;
          justify-content: space-between;
          margin-bottom: 8px;
        }

        .info-row label {
          color: var(--text-secondary);
        }

        .info-row .danger {
          color: var(--danger);
        }

        .test-result {
          margin-left: 8px;
        }

        .test-result.success {
          color: var(--success);
        }

        .test-result.failed {
          color: var(--danger);
        }

        .card-actions {
          display: flex;
          gap: 8px;
          padding: 16px;
          border-top: 1px solid var(--border-color);
        }

        .btn {
          flex: 1;
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
        }

        .btn:disabled {
          opacity: 0.5;
          cursor: not-allowed;
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
          max-width: 400px;
          width: 90%;
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

        .form-group {
          margin-bottom: 16px;
        }

        .form-group label {
          display: block;
          margin-bottom: 8px;
          font-weight: 500;
        }

        .form-group input {
          width: 100%;
          padding: 10px;
          border: 1px solid var(--border-color);
          border-radius: 4px;
          background: var(--surface);
          color: var(--text-primary);
        }

        .modal-footer {
          display: flex;
          gap: 8px;
          padding: 16px;
          border-top: 1px solid var(--border-color);
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

export default Credentials;
