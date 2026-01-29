# Configuracion de Microsoft Entra ID - Chivato

## Resumen

Chivato usa Microsoft Entra ID (antes Azure AD) para autenticacion de usuarios.
El flujo es **Authorization Code + PKCE**, el estandar recomendado para SPAs.

## Arquitectura de Autenticacion

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Usuario       │     │   Chivato UI    │     │  Entra ID       │
│   (Browser)     │     │   (React SPA)   │     │  (Identity)     │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         │  1. Accede a /        │                       │
         │──────────────────────▶│                       │
         │                       │                       │
         │                       │ 2. No hay token,      │
         │                       │    redirect a login   │
         │◀──────────────────────│                       │
         │                       │                       │
         │  3. Login en Microsoft│                       │
         │──────────────────────────────────────────────▶│
         │                       │                       │
         │  4. Usuario autentica │                       │
         │◀──────────────────────────────────────────────│
         │     (auth code)       │                       │
         │                       │                       │
         │  5. Redirect con code │                       │
         │──────────────────────▶│                       │
         │                       │                       │
         │                       │ 6. Intercambia code   │
         │                       │    por tokens (PKCE)  │
         │                       │──────────────────────▶│
         │                       │                       │
         │                       │ 7. Access + ID tokens │
         │                       │◀──────────────────────│
         │                       │                       │
         │  8. UI cargada        │                       │
         │◀──────────────────────│                       │
         │                       │                       │
         │                       │ 9. API calls con      │
         │                       │    Bearer token       │
         │                       │──────────────────────▶│
         │                       │    (Azure Functions)  │
```

## Paso 1: Crear App Registration

### En Azure Portal

1. Ir a **Microsoft Entra ID** > **App registrations** > **New registration**

2. Configurar:
   - **Name**: `Chivato`
   - **Supported account types**: "Accounts in this organizational directory only"
   - **Redirect URI**:
     - Platform: `Single-page application (SPA)`
     - URI: `http://localhost:5173` (desarrollo)

3. Guardar el **Application (client) ID** y **Directory (tenant) ID**

### Configuracion post-creacion

```
App Registration: Chivato
│
├── Overview
│   ├── Application (client) ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
│   └── Directory (tenant) ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
│
├── Authentication
│   ├── Platform configurations
│   │   └── Single-page application
│   │       ├── Redirect URIs:
│   │       │   ├── http://localhost:5173 (dev)
│   │       │   ├── http://localhost:5173/auth/callback (dev)
│   │       │   ├── https://chivato.azurewebsites.net (prod)
│   │       │   └── https://chivato.azurewebsites.net/auth/callback (prod)
│   │       │
│   │       └── Implicit grant and hybrid flows:
│   │           ├── Access tokens: [ ] (NO - usamos PKCE)
│   │           └── ID tokens: [ ] (NO - usamos PKCE)
│   │
│   └── Advanced settings
│       └── Allow public client flows: No
│
├── Certificates & secrets
│   └── (No se necesita para SPA con PKCE)
│
├── API permissions
│   ├── Microsoft Graph
│   │   └── User.Read (Delegated) ✓ Granted
│   │
│   └── (Opcional) Azure Service Management
│       └── user_impersonation (si necesitamos On-Behalf-Of)
│
└── App roles (ver seccion siguiente)
```

## Paso 2: Crear App Roles

En **App registrations** > **Chivato** > **App roles** > **Create app role**:

### Rol: Chivato.User

| Campo | Valor |
|-------|-------|
| Display name | Chivato User |
| Allowed member types | Users/Groups |
| Value | Chivato.User |
| Description | Puede ver dashboard y drift records |
| Enable this app role | Yes |

### Rol: Chivato.Admin

| Campo | Valor |
|-------|-------|
| Display name | Chivato Admin |
| Allowed member types | Users/Groups |
| Value | Chivato.Admin |
| Description | Puede configurar conexiones, pipelines y settings |
| Enable this app role | Yes |

## Paso 3: Asignar Usuarios a Roles

En **Enterprise applications** > **Chivato** > **Users and groups**:

1. Click **Add user/group**
2. Seleccionar usuario o grupo
3. Seleccionar rol (Chivato.User o Chivato.Admin)
4. **Assign**

## Paso 4: Configuracion en React (MSAL)

### Instalar dependencias

```bash
npm install @azure/msal-browser @azure/msal-react
```

### Archivo de configuracion: `src/auth/authConfig.ts`

```typescript
import { Configuration, LogLevel } from "@azure/msal-browser";

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID}`,
    redirectUri: import.meta.env.VITE_REDIRECT_URI || window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: "sessionStorage", // Mas seguro que localStorage
    storeAuthStateInCookie: false,
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
        }
      },
    },
  },
};

export const loginRequest = {
  scopes: ["User.Read", "openid", "profile", "email"],
};

// Scopes para llamar a nuestra API (Azure Functions)
export const apiRequest = {
  scopes: [`api://${import.meta.env.VITE_ENTRA_CLIENT_ID}/access_as_user`],
};
```

### Variables de entorno: `.env`

```env
VITE_ENTRA_CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
VITE_ENTRA_TENANT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
VITE_REDIRECT_URI=http://localhost:5173
VITE_API_URL=http://localhost:7071/api
```

### Provider en `main.tsx`

```typescript
import { MsalProvider } from "@azure/msal-react";
import { PublicClientApplication } from "@azure/msal-browser";
import { msalConfig } from "./auth/authConfig";

const msalInstance = new PublicClientApplication(msalConfig);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <MsalProvider instance={msalInstance}>
      <App />
    </MsalProvider>
  </React.StrictMode>
);
```

### Hook para verificar roles

```typescript
// src/hooks/useRoles.ts
import { useMsal } from "@azure/msal-react";

export function useRoles() {
  const { accounts } = useMsal();
  const account = accounts[0];

  const roles: string[] = account?.idTokenClaims?.roles || [];

  return {
    isUser: roles.includes("Chivato.User") || roles.includes("Chivato.Admin"),
    isAdmin: roles.includes("Chivato.Admin"),
    roles,
  };
}
```

## Paso 5: Validacion en Azure Functions

### Configuracion en `host.json` (Easy Auth)

Si usas Azure Functions con Easy Auth habilitado, la validacion es automatica.

### Validacion manual del token

```csharp
// Services/TokenValidationService.cs
public class TokenValidationService
{
    private readonly IConfiguration _config;

    public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"https://login.microsoftonline.com/{_config["EntraId:TenantId"]}/v2.0/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());

        var config = await configManager.GetConfigurationAsync();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://login.microsoftonline.com/{_config["EntraId:TenantId"]}/v2.0",
            ValidateAudience = true,
            ValidAudience = _config["EntraId:ClientId"],
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, validationParameters, out _);
    }

    public bool HasRole(ClaimsPrincipal principal, string role)
    {
        return principal.Claims
            .Where(c => c.Type == "roles")
            .Any(c => c.Value == role);
    }
}
```

### Atributo para proteger funciones

```csharp
// Attributes/RequireRoleAttribute.cs
[AttributeUsage(AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute
{
    public string[] Roles { get; }

    public RequireRoleAttribute(params string[] roles)
    {
        Roles = roles;
    }
}

// Middleware o filtro que valida el rol antes de ejecutar la funcion
```

## Flujo Completo de Tokens

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         TOKEN LIFECYCLE                                  │
└─────────────────────────────────────────────────────────────────────────┘

1. Usuario hace login
   │
   ▼
2. MSAL obtiene tokens de Entra ID
   ├── ID Token (informacion del usuario, roles)
   ├── Access Token (para APIs)
   └── Refresh Token (para renovar)
   │
   ▼
3. Tokens almacenados en sessionStorage (MSAL los gestiona)
   │
   ▼
4. Llamada a API
   │
   ├── MSAL adjunta Access Token automaticamente
   │   Authorization: Bearer eyJ0eXAi...
   │
   ▼
5. Azure Function recibe request
   │
   ├── Valida token (firma, issuer, audience, expiry)
   ├── Extrae claims (user id, email, roles)
   └── Verifica rol requerido
   │
   ▼
6. Si token expira (1h default)
   │
   ├── MSAL usa Refresh Token silenciosamente
   └── Obtiene nuevo Access Token sin interaccion del usuario
   │
   ▼
7. Si Refresh Token expira (24h-90 dias segun config)
   │
   └── Usuario debe hacer login de nuevo
```

## Checklist de Implementacion

- [ ] Crear App Registration en Azure Portal
- [ ] Configurar Redirect URIs (dev y prod)
- [ ] Crear App Roles (User y Admin)
- [ ] Asignar usuarios a roles en Enterprise Applications
- [ ] Configurar MSAL en React
- [ ] Implementar componentes protegidos por rol
- [ ] Validar tokens en Azure Functions
- [ ] Configurar variables de entorno
- [ ] Probar flujo completo en desarrollo
- [ ] Añadir URIs de produccion antes del deploy
