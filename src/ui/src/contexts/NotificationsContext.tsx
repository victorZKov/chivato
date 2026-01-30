import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import { useSignalR } from '../hooks/useSignalR';

// Event types from backend
export interface AnalysisProgressEvent {
  type: 'analysis_progress';
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  stage: string;
  progress: number;
  message: string;
  timestamp: string;
}

export interface AnalysisCompletedEvent {
  type: 'analysis_completed';
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  summary: {
    totalDrifts: number;
    critical: number;
    high: number;
    medium: number;
    low: number;
    durationSeconds: number;
  };
  timestamp: string;
}

export interface AnalysisFailedEvent {
  type: 'analysis_failed';
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  tenantId: string;
  error: string;
  timestamp: string;
}

export type NotificationEvent =
  | AnalysisProgressEvent
  | AnalysisCompletedEvent
  | AnalysisFailedEvent;

interface ActiveAnalysis {
  correlationId: string;
  pipelineId: string;
  pipelineName: string;
  stage: string;
  progress: number;
  message: string;
  startedAt: Date;
}

interface Notification {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  title: string;
  message: string;
  timestamp: Date;
  read: boolean;
  data?: NotificationEvent;
}

interface NotificationsContextValue {
  // Connection state
  isConnected: boolean;
  connectionError: string | null;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;

  // Active analyses
  activeAnalyses: Map<string, ActiveAnalysis>;

  // Notifications
  notifications: Notification[];
  unreadCount: number;
  markAsRead: (id: string) => void;
  markAllAsRead: () => void;
  clearNotifications: () => void;

  // Event subscriptions
  onProgress: (callback: (event: AnalysisProgressEvent) => void) => () => void;
  onCompleted: (callback: (event: AnalysisCompletedEvent) => void) => () => void;
  onFailed: (callback: (event: AnalysisFailedEvent) => void) => () => void;
}

const NotificationsContext = createContext<NotificationsContextValue | null>(null);

interface NotificationsProviderProps {
  children: ReactNode;
  tenantId?: string;
  autoConnect?: boolean;
}

export function NotificationsProvider({
  children,
  tenantId,
  autoConnect = false,
}: NotificationsProviderProps) {
  const { connection, isConnected, connect, disconnect, error, on, off } = useSignalR({
    tenantId,
    autoConnect,
  });

  const [activeAnalyses, setActiveAnalyses] = useState<Map<string, ActiveAnalysis>>(new Map());
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [progressCallbacks, setProgressCallbacks] = useState<Set<(event: AnalysisProgressEvent) => void>>(new Set());
  const [completedCallbacks, setCompletedCallbacks] = useState<Set<(event: AnalysisCompletedEvent) => void>>(new Set());
  const [failedCallbacks, setFailedCallbacks] = useState<Set<(event: AnalysisFailedEvent) => void>>(new Set());

  // Handle progress events
  useEffect(() => {
    if (!connection) return;

    const handleProgress = (event: AnalysisProgressEvent) => {
      // Update active analyses
      setActiveAnalyses((prev) => {
        const next = new Map(prev);
        next.set(event.correlationId, {
          correlationId: event.correlationId,
          pipelineId: event.pipelineId,
          pipelineName: event.pipelineName,
          stage: event.stage,
          progress: event.progress,
          message: event.message,
          startedAt: prev.get(event.correlationId)?.startedAt || new Date(),
        });
        return next;
      });

      // Notify subscribers
      progressCallbacks.forEach((cb) => cb(event));
    };

    on('analysisProgress', handleProgress);

    return () => {
      off('analysisProgress');
    };
  }, [connection, on, off, progressCallbacks]);

  // Handle completed events
  useEffect(() => {
    if (!connection) return;

    const handleCompleted = (event: AnalysisCompletedEvent) => {
      // Remove from active analyses
      setActiveAnalyses((prev) => {
        const next = new Map(prev);
        next.delete(event.correlationId);
        return next;
      });

      // Add notification
      const notification: Notification = {
        id: `completed-${event.correlationId}`,
        type: event.summary.totalDrifts > 0 ? 'warning' : 'success',
        title: 'Analysis Completed',
        message: event.summary.totalDrifts > 0
          ? `${event.pipelineName}: Found ${event.summary.totalDrifts} drift(s)`
          : `${event.pipelineName}: No drifts detected`,
        timestamp: new Date(event.timestamp),
        read: false,
        data: event,
      };
      setNotifications((prev) => [notification, ...prev].slice(0, 50)); // Keep last 50

      // Notify subscribers
      completedCallbacks.forEach((cb) => cb(event));
    };

    on('analysisCompleted', handleCompleted);

    return () => {
      off('analysisCompleted');
    };
  }, [connection, on, off, completedCallbacks]);

  // Handle failed events
  useEffect(() => {
    if (!connection) return;

    const handleFailed = (event: AnalysisFailedEvent) => {
      // Remove from active analyses
      setActiveAnalyses((prev) => {
        const next = new Map(prev);
        next.delete(event.correlationId);
        return next;
      });

      // Add notification
      const notification: Notification = {
        id: `failed-${event.correlationId}`,
        type: 'error',
        title: 'Analysis Failed',
        message: `${event.pipelineName}: ${event.error}`,
        timestamp: new Date(event.timestamp),
        read: false,
        data: event,
      };
      setNotifications((prev) => [notification, ...prev].slice(0, 50));

      // Notify subscribers
      failedCallbacks.forEach((cb) => cb(event));
    };

    on('analysisFailed', handleFailed);

    return () => {
      off('analysisFailed');
    };
  }, [connection, on, off, failedCallbacks]);

  const markAsRead = useCallback((id: string) => {
    setNotifications((prev) =>
      prev.map((n) => (n.id === id ? { ...n, read: true } : n))
    );
  }, []);

  const markAllAsRead = useCallback(() => {
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })));
  }, []);

  const clearNotifications = useCallback(() => {
    setNotifications([]);
  }, []);

  const onProgress = useCallback((callback: (event: AnalysisProgressEvent) => void) => {
    setProgressCallbacks((prev) => new Set(prev).add(callback));
    return () => {
      setProgressCallbacks((prev) => {
        const next = new Set(prev);
        next.delete(callback);
        return next;
      });
    };
  }, []);

  const onCompleted = useCallback((callback: (event: AnalysisCompletedEvent) => void) => {
    setCompletedCallbacks((prev) => new Set(prev).add(callback));
    return () => {
      setCompletedCallbacks((prev) => {
        const next = new Set(prev);
        next.delete(callback);
        return next;
      });
    };
  }, []);

  const onFailed = useCallback((callback: (event: AnalysisFailedEvent) => void) => {
    setFailedCallbacks((prev) => new Set(prev).add(callback));
    return () => {
      setFailedCallbacks((prev) => {
        const next = new Set(prev);
        next.delete(callback);
        return next;
      });
    };
  }, []);

  const unreadCount = notifications.filter((n) => !n.read).length;

  const value: NotificationsContextValue = {
    isConnected,
    connectionError: error,
    connect,
    disconnect,
    activeAnalyses,
    notifications,
    unreadCount,
    markAsRead,
    markAllAsRead,
    clearNotifications,
    onProgress,
    onCompleted,
    onFailed,
  };

  return (
    <NotificationsContext.Provider value={value}>
      {children}
    </NotificationsContext.Provider>
  );
}

export function useNotifications() {
  const context = useContext(NotificationsContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationsProvider');
  }
  return context;
}

export default NotificationsContext;
