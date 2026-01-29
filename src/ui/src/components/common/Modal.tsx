import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import "./Modal.css";

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  size?: "sm" | "md" | "lg";
  showCloseButton?: boolean;
  closeOnOverlay?: boolean;
  closeOnEscape?: boolean;
}

export function Modal({
  isOpen,
  onClose,
  title,
  children,
  size = "md",
  showCloseButton = true,
  closeOnOverlay = true,
  closeOnEscape = true,
}: ModalProps) {
  const modalRef = useRef<HTMLDivElement>(null);
  const { t } = useTranslation();

  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (closeOnEscape && e.key === "Escape") {
        onClose();
      }
    };

    if (isOpen) {
      document.addEventListener("keydown", handleEscape);
      document.body.style.overflow = "hidden";
    }

    return () => {
      document.removeEventListener("keydown", handleEscape);
      document.body.style.overflow = "";
    };
  }, [isOpen, onClose, closeOnEscape]);

  useEffect(() => {
    if (isOpen && modalRef.current) {
      modalRef.current.focus();
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleOverlayClick = (e: React.MouseEvent) => {
    if (closeOnOverlay && e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <div className="modal-overlay" onClick={handleOverlayClick} role="dialog" aria-modal="true">
      <div className={`modal modal-${size}`} ref={modalRef} tabIndex={-1}>
        {(title || showCloseButton) && (
          <div className="modal-header">
            {title && <h2 className="modal-title">{title}</h2>}
            {showCloseButton && (
              <button className="modal-close" onClick={onClose} aria-label={t("common.close")}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M18 6L6 18M6 6l12 12" />
                </svg>
              </button>
            )}
          </div>
        )}
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}

// Confirm Dialog
interface ConfirmDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: "danger" | "warning" | "info";
}

export function ConfirmDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText,
  cancelText,
  variant = "info",
}: ConfirmDialogProps) {
  const { t } = useTranslation();

  const handleConfirm = () => {
    onConfirm();
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title || t("modal.confirm.title")} size="sm" closeOnOverlay={false}>
      <div className="confirm-dialog">
        <p className="confirm-message">{message}</p>
        <div className="confirm-actions">
          <button className="btn btn-ghost" onClick={onClose}>
            {cancelText || t("common.cancel")}
          </button>
          <button className={`btn btn-${variant === "danger" ? "danger" : "primary"}`} onClick={handleConfirm}>
            {confirmText || t("common.confirm")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

// Alert Dialog
interface AlertDialogProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  message: string;
  variant?: "error" | "warning" | "success" | "info";
}

export function AlertDialog({ isOpen, onClose, title, message, variant = "info" }: AlertDialogProps) {
  const { t } = useTranslation();

  const icons = {
    error: "❌",
    warning: "⚠️",
    success: "✅",
    info: "ℹ️",
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title || t("modal.alert.title")} size="sm">
      <div className={`alert-dialog alert-${variant}`}>
        <div className="alert-icon">{icons[variant]}</div>
        <p className="alert-message">{message}</p>
        <div className="alert-actions">
          <button className="btn btn-primary" onClick={onClose}>
            {t("common.ok")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

// Prompt Dialog
interface PromptDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (value: string) => void;
  title?: string;
  message: string;
  placeholder?: string;
  defaultValue?: string;
  submitText?: string;
  cancelText?: string;
}

export function PromptDialog({
  isOpen,
  onClose,
  onSubmit,
  title,
  message,
  placeholder,
  defaultValue = "",
  submitText,
  cancelText,
}: PromptDialogProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.value = defaultValue;
    }
  }, [isOpen, defaultValue]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputRef.current) {
      onSubmit(inputRef.current.value);
      onClose();
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title || t("modal.prompt.title")} size="sm" closeOnOverlay={false}>
      <form className="prompt-dialog" onSubmit={handleSubmit}>
        <p className="prompt-message">{message}</p>
        <input ref={inputRef} type="text" className="input" placeholder={placeholder} defaultValue={defaultValue} />
        <div className="prompt-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            {cancelText || t("common.cancel")}
          </button>
          <button type="submit" className="btn btn-primary">
            {submitText || t("common.ok")}
          </button>
        </div>
      </form>
    </Modal>
  );
}
