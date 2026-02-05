import { useState, useEffect, useCallback, useRef } from "react";
import { useNotifications } from "../../contexts/NotificationsContext";
import "./Toast.css";

export type ToastType = "success" | "error" | "warning" | "info";

export interface Toast {
  id: string;
  message: string;
  type: ToastType;
  duration?: number;
}

interface ToastProps {
  toast: Toast;
  onRemove: (id: string) => void;
}

function ToastItem({ toast, onRemove }: ToastProps) {
  useEffect(() => {
    const timer = setTimeout(() => {
      onRemove(toast.id);
    }, toast.duration || 5000);

    return () => clearTimeout(timer);
  }, [toast.id, toast.duration, onRemove]);

  return (
    <div className={`toast toast-${toast.type}`} role="alert">
      <span className="toast-icon">
        {toast.type === "success" && "✓"}
        {toast.type === "error" && "✕"}
        {toast.type === "warning" && "⚠"}
        {toast.type === "info" && "ℹ"}
      </span>
      <span className="toast-message">{toast.message}</span>
      <button
        className="toast-close"
        onClick={() => onRemove(toast.id)}
        aria-label="Close"
      >
        ×
      </button>
    </div>
  );
}

export function ToastContainer({ toasts, onRemove }: { toasts: Toast[]; onRemove: (id: string) => void }) {
  return (
    <div className="toast-container" aria-live="polite">
      {toasts.map((toast) => (
        <ToastItem key={toast.id} toast={toast} onRemove={onRemove} />
      ))}
    </div>
  );
}

// Global toast container that listens to notification context
export function GlobalToastContainer() {
  const [visibleToasts, setVisibleToasts] = useState<Toast[]>([]);
  const { notifications } = useNotifications();
  const shownIds = useRef<Set<string>>(new Set());

  useEffect(() => {
    // Show toast for new notifications
    notifications.forEach((notification) => {
      if (!shownIds.current.has(notification.id)) {
        shownIds.current.add(notification.id);
        setVisibleToasts((prev) => [
          ...prev,
          {
            id: notification.id,
            message: notification.message,
            type: notification.type,
            duration: 5000,
          },
        ]);
      }
    });
  }, [notifications]);

  const removeToast = useCallback((id: string) => {
    setVisibleToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  return <ToastContainer toasts={visibleToasts} onRemove={removeToast} />;
}

// Hook for managing toasts - now uses notification context
export function useToast() {
  const { toast, addNotification } = useNotifications();

  return {
    toasts: [] as Toast[], // Legacy compatibility
    addToast: (message: string, type: ToastType = "info") => addNotification(message, type),
    removeToast: () => {}, // Handled by GlobalToastContainer
    success: toast.success,
    error: toast.error,
    warning: toast.warning,
    info: toast.info,
  };
}

// Standalone hook for components that might not be in NotificationProvider
export function useLocalToast() {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const addToast = useCallback((message: string, type: ToastType = "info", duration?: number) => {
    const id = Math.random().toString(36).substring(7);
    setToasts((prev) => [...prev, { id, message, type, duration }]);
    return id;
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const success = useCallback((message: string) => addToast(message, "success"), [addToast]);
  const error = useCallback((message: string) => addToast(message, "error"), [addToast]);
  const warning = useCallback((message: string) => addToast(message, "warning"), [addToast]);
  const info = useCallback((message: string) => addToast(message, "info"), [addToast]);

  return { toasts, addToast, removeToast, success, error, warning, info };
}
