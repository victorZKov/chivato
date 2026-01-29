import { useState, useCallback } from "react";

interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: "danger" | "warning" | "info";
}

interface AlertOptions {
  title?: string;
  message: string;
  variant?: "error" | "warning" | "success" | "info";
}

interface PromptOptions {
  title?: string;
  message: string;
  placeholder?: string;
  defaultValue?: string;
  submitText?: string;
  cancelText?: string;
}

export interface ModalState {
  confirm: {
    isOpen: boolean;
    options: ConfirmOptions | null;
    resolve: ((value: boolean) => void) | null;
  };
  alert: {
    isOpen: boolean;
    options: AlertOptions | null;
  };
  prompt: {
    isOpen: boolean;
    options: PromptOptions | null;
    resolve: ((value: string | null) => void) | null;
  };
}

const initialState: ModalState = {
  confirm: { isOpen: false, options: null, resolve: null },
  alert: { isOpen: false, options: null },
  prompt: { isOpen: false, options: null, resolve: null },
};

export function useModal() {
  const [state, setState] = useState<ModalState>(initialState);

  const confirm = useCallback((options: ConfirmOptions): Promise<boolean> => {
    return new Promise((resolve) => {
      setState((prev) => ({
        ...prev,
        confirm: { isOpen: true, options, resolve },
      }));
    });
  }, []);

  const closeConfirm = useCallback((result: boolean) => {
    setState((prev) => {
      prev.confirm.resolve?.(result);
      return {
        ...prev,
        confirm: { isOpen: false, options: null, resolve: null },
      };
    });
  }, []);

  const alert = useCallback((options: AlertOptions): void => {
    setState((prev) => ({
      ...prev,
      alert: { isOpen: true, options },
    }));
  }, []);

  const closeAlert = useCallback(() => {
    setState((prev) => ({
      ...prev,
      alert: { isOpen: false, options: null },
    }));
  }, []);

  const prompt = useCallback((options: PromptOptions): Promise<string | null> => {
    return new Promise((resolve) => {
      setState((prev) => ({
        ...prev,
        prompt: { isOpen: true, options, resolve },
      }));
    });
  }, []);

  const closePrompt = useCallback((value: string | null) => {
    setState((prev) => {
      prev.prompt.resolve?.(value);
      return {
        ...prev,
        prompt: { isOpen: false, options: null, resolve: null },
      };
    });
  }, []);

  return {
    state,
    confirm,
    closeConfirm,
    alert,
    closeAlert,
    prompt,
    closePrompt,
  };
}
