import { useMsal } from "@azure/msal-react";

interface IdTokenClaims {
  roles?: string[];
  [key: string]: unknown;
}

export function useRoles() {
  const { accounts } = useMsal();
  const account = accounts[0];

  const claims = account?.idTokenClaims as IdTokenClaims | undefined;
  const roles: string[] = claims?.roles || [];

  return {
    roles,
    isUser: roles.includes("Chivato.User") || roles.includes("Chivato.Admin"),
    isAdmin: roles.includes("Chivato.Admin"),
    hasRole: (role: string) => roles.includes(role),
  };
}
