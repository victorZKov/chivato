import type { Configuration, IPublicClientApplication } from "@azure/msal-browser";
import { LogLevel, PublicClientApplication } from "@azure/msal-browser";

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID || "",
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID || "common"}`,
    redirectUri: import.meta.env.VITE_REDIRECT_URI || window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            break;
          case LogLevel.Warning:
            console.warn(message);
            break;
          case LogLevel.Info:
            console.info(message);
            break;
          case LogLevel.Verbose:
            console.debug(message);
            break;
        }
      },
      logLevel: LogLevel.Warning,
    },
  },
};

export const loginRequest = {
  scopes: ["User.Read", "openid", "profile", "email"],
};

// Note: Using the same scopes as login since the API doesn't validate tokens yet
// To enable API token validation, configure the Application ID URI in Azure AD
// and set up JWT authentication in the API
export const apiRequest = {
  scopes: ["User.Read", "openid", "profile", "email"],
};

// Create singleton MSAL instance
let msalInstance: IPublicClientApplication | null = null;
let initializationPromise: Promise<void> | null = null;

export function getMsalInstance(): IPublicClientApplication {
  if (!msalInstance) {
    msalInstance = new PublicClientApplication(msalConfig);
  }
  return msalInstance;
}

export async function ensureMsalInitialized(): Promise<IPublicClientApplication> {
  const instance = getMsalInstance();
  if (!initializationPromise) {
    initializationPromise = (instance as PublicClientApplication).initialize();
  }
  await initializationPromise;
  return instance;
}

// Legacy export for backward compatibility - DO NOT USE in new code
export { msalInstance };
