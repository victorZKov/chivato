import { useMsal, useIsAuthenticated } from "@azure/msal-react";
import { loginRequest } from "../auth/authConfig";

export function useAuth() {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const account = accounts[0];

  const login = async () => {
    try {
      await instance.loginRedirect(loginRequest);
    } catch (error) {
      console.error("Login failed:", error);
    }
  };

  const logout = async () => {
    try {
      await instance.logoutRedirect({
        postLogoutRedirectUri: window.location.origin,
      });
    } catch (error) {
      console.error("Logout failed:", error);
    }
  };

  const getAccessToken = async () => {
    if (!account) return null;
    try {
      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account,
      });
      return response.accessToken;
    } catch (error) {
      console.error("Token acquisition failed:", error);
      return null;
    }
  };

  return {
    isAuthenticated,
    account,
    user: account
      ? {
          name: account.name || "Usuario",
          email: account.username,
        }
      : null,
    login,
    logout,
    getAccessToken,
  };
}
