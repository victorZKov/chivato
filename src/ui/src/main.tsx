import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { MsalProvider } from "@azure/msal-react";
import { PublicClientApplication, EventType } from "@azure/msal-browser";
import { msalConfig } from "./auth/authConfig";
import { ModalProvider } from "./contexts/ModalContext";
import App from "./App";
import "./i18n";
import "./styles/globals.css";

const msalInstance = new PublicClientApplication(msalConfig);

// Set default account if available
const accounts = msalInstance.getAllAccounts();
if (accounts.length > 0) {
  msalInstance.setActiveAccount(accounts[0]);
}

// Handle redirect response
msalInstance.addEventCallback((event) => {
  if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
    const payload = event.payload as { account: { username: string } };
    const account = payload.account;
    msalInstance.setActiveAccount(account as ReturnType<typeof msalInstance.getAllAccounts>[0]);
  }
});

// Set default theme
const savedTheme = localStorage.getItem("chivato-theme") || "system";
if (savedTheme === "system") {
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  document.documentElement.setAttribute("data-theme", prefersDark ? "dark" : "light");
} else {
  document.documentElement.setAttribute("data-theme", savedTheme);
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <MsalProvider instance={msalInstance}>
      <ModalProvider>
        <App />
      </ModalProvider>
    </MsalProvider>
  </StrictMode>
);
