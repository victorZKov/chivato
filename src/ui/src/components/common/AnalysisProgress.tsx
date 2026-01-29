import React from 'react';
import { useNotifications } from '../../contexts/NotificationsContext';
import { useTranslation } from 'react-i18next';

interface AnalysisProgressProps {
  className?: string;
  showAll?: boolean;
}

export function AnalysisProgress({ className = '', showAll = false }: AnalysisProgressProps) {
  const { t } = useTranslation();
  const { activeAnalyses, isConnected } = useNotifications();

  const analyses = Array.from(activeAnalyses.values());

  if (!isConnected) {
    return null;
  }

  if (analyses.length === 0) {
    return null;
  }

  const displayAnalyses = showAll ? analyses : analyses.slice(0, 3);

  return (
    <div className={`analysis-progress ${className}`}>
      {displayAnalyses.map((analysis) => (
        <div
          key={analysis.correlationId}
          className="analysis-progress-item"
        >
          <div className="analysis-progress-header">
            <span className="pipeline-name">{analysis.pipelineName}</span>
            <span className="progress-percentage">{analysis.progress}%</span>
          </div>

          <div className="progress-bar-container">
            <div
              className="progress-bar"
              style={{ width: `${analysis.progress}%` }}
            />
          </div>

          <div className="analysis-progress-footer">
            <span className="stage">{getStageLabel(analysis.stage, t)}</span>
            <span className="message">{analysis.message}</span>
          </div>
        </div>
      ))}

      {!showAll && analyses.length > 3 && (
        <div className="more-analyses">
          {t('analysis.moreRunning', { count: analyses.length - 3 })}
        </div>
      )}

      <style>{`
        .analysis-progress {
          display: flex;
          flex-direction: column;
          gap: 12px;
        }

        .analysis-progress-item {
          background: var(--surface-elevated);
          border: 1px solid var(--border-color);
          border-radius: 8px;
          padding: 12px;
        }

        .analysis-progress-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 8px;
        }

        .pipeline-name {
          font-weight: 500;
          color: var(--text-primary);
        }

        .progress-percentage {
          font-size: 12px;
          font-weight: 600;
          color: var(--primary);
        }

        .progress-bar-container {
          height: 6px;
          background: var(--surface-secondary);
          border-radius: 3px;
          overflow: hidden;
          margin-bottom: 8px;
        }

        .progress-bar {
          height: 100%;
          background: linear-gradient(90deg, var(--primary), var(--primary-light));
          border-radius: 3px;
          transition: width 0.3s ease;
        }

        .analysis-progress-footer {
          display: flex;
          justify-content: space-between;
          align-items: center;
          font-size: 12px;
        }

        .stage {
          color: var(--text-secondary);
          text-transform: capitalize;
        }

        .message {
          color: var(--text-tertiary);
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
          max-width: 60%;
        }

        .more-analyses {
          text-align: center;
          font-size: 12px;
          color: var(--text-secondary);
          padding: 8px;
        }
      `}</style>
    </div>
  );
}

function getStageLabel(stage: string, t: (key: string) => string): string {
  const stageLabels: Record<string, string> = {
    scanning_pipeline: t('analysis.stages.scanning'),
    fetching_resources: t('analysis.stages.fetching'),
    analyzing: t('analysis.stages.analyzing'),
    completed: t('analysis.stages.completed'),
  };

  return stageLabels[stage] || stage.replace(/_/g, ' ');
}

export default AnalysisProgress;
