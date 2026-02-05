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
  status?: string;
  driftCount?: number;
  overallRisk?: string;
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

export interface Notification {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  title?: string;
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
  addNotification: (message: string, type: Notification['type'], title?: string) => string;
  removeNotification: (id: string) => void;
  markAsRead: (id: string) => void;
  markAllAsRead: () => void;
  clearNotifications: () => void;

  // Toast helpers
  toast: {
    success: (message: string) => string;
    error: (message: string) => string;
    warning: (message: string) => string;
    info: (message: string) => string;
  };

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

      // Format error message - truncate if too long and clean up
      let errorMessage = event.error || 'Unknown error';
      // If error contains "Pipeline failed with result:", extract just that part
      if (errorMessage.includes('Pipeline failed with result:')) {
        const match = errorMessage.match(/Pipeline failed with result: (\w+)/);
        if (match) {
          errorMessage = `Pipeline failed with result: ${match[1]}. Check the pipeline in Azure DevOps for details.`;
        }
      }
      // Truncate very long messages
      if (errorMessage.length > 200) {
        errorMessage = errorMessage.substring(0, 200) + '...';
      }

      // Add notification
      const notification: Notification = {
        id: `failed-${event.correlationId}`,
        type: 'error',
        title: 'Analysis Failed',
        message: event.pipelineName ? `${event.pipelineName}: ${errorMessage}` : errorMessage,
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

  const addNotification = useCallback((message: string, type: Notification['type'], title?: string) => {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const notification: Notification = {
      id,
      type,
      title,
      message,
      timestamp: new Date(),
      read: false,
    };
    setNotifications((prev) => [notification, ...prev].slice(0, 50));
    return id;
  }, []);

  const removeNotification = useCallback((id: string) => {
    setNotifications((prev) => prev.filter((n) => n.id !== id));
  }, []);

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

  const toast = {
    success: useCallback((message: string) => addNotification(message, 'success'), [addNotification]),
    error: useCallback((message: string) => addNotification(message, 'error'), [addNotification]),
    warning: useCallback((message: string) => addNotification(message, 'warning'), [addNotification]),
    info: useCallback((message: string) => addNotification(message, 'info'), [addNotification]),
  };

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
    addNotification,
    removeNotification,
    markAsRead,
    markAllAsRead,
    clearNotifications,
    toast,
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
