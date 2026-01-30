import { createContext, useContext } from "react";
import type { ReactNode } from "react";
import { useModal } from "../hooks/useModal";
import { ConfirmDialog, AlertDialog, PromptDialog } from "../components/common/Modal";

interface ModalContextValue {
  confirm: (options: { title?: string; message: string; confirmText?: string; cancelText?: string; variant?: "danger" | "warning" | "info" }) => Promise<boolean>;
  alert: (options: { title?: string; message: string; variant?: "error" | "warning" | "success" | "info" }) => void;
  prompt: (options: { title?: string; message: string; placeholder?: string; defaultValue?: string; submitText?: string; cancelText?: string }) => Promise<string | null>;
}

const ModalContext = createContext<ModalContextValue | null>(null);

export function ModalProvider({ children }: { children: ReactNode }) {
  const { state, confirm, closeConfirm, alert, closeAlert, prompt, closePrompt } = useModal();

  return (
    <ModalContext.Provider value={{ confirm, alert, prompt }}>
      {children}

      {/* Confirm Dialog */}
      {state.confirm.options && (
        <ConfirmDialog
          isOpen={state.confirm.isOpen}
          onClose={() => closeConfirm(false)}
          onConfirm={() => closeConfirm(true)}
          title={state.confirm.options.title}
          message={state.confirm.options.message}
          confirmText={state.confirm.options.confirmText}
          cancelText={state.confirm.options.cancelText}
          variant={state.confirm.options.variant}
        />
      )}

      {/* Alert Dialog */}
      {state.alert.options && (
        <AlertDialog
          isOpen={state.alert.isOpen}
          onClose={closeAlert}
          title={state.alert.options.title}
          message={state.alert.options.message}
          variant={state.alert.options.variant}
        />
      )}

      {/* Prompt Dialog */}
      {state.prompt.options && (
        <PromptDialog
          isOpen={state.prompt.isOpen}
          onClose={() => closePrompt(null)}
          onSubmit={(value) => closePrompt(value)}
          title={state.prompt.options.title}
          message={state.prompt.options.message}
          placeholder={state.prompt.options.placeholder}
          defaultValue={state.prompt.options.defaultValue}
          submitText={state.prompt.options.submitText}
          cancelText={state.prompt.options.cancelText}
        />
      )}
    </ModalContext.Provider>
  );
}

export function useModalContext() {
  const context = useContext(ModalContext);
  if (!context) {
    throw new Error("useModalContext must be used within a ModalProvider");
  }
  return context;
}
