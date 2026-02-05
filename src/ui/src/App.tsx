import { useState, useEffect } from "react";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { Layout } from "./components/Layout";
import { Dashboard } from "./components/Dashboard";
import { Billing } from "./components/Billing";
import { Pipelines } from "./components/Pipelines";
import { PipelineDetail } from "./components/Pipelines/PipelineDetail";
import { Configuration } from "./components/Configuration";
import { DriftHistory } from "./components/DriftHistory/DriftHistory";
import { ScanLogs } from "./components/ScanLogs/ScanLogs";
import { Credentials } from "./components/Credentials/Credentials";
import { NotificationsProvider } from "./contexts/NotificationsContext";
import { ModalProvider } from "./contexts/ModalContext";
import { GlobalToastContainer } from "./components/common/Toast";
import { useAuth } from "./hooks/useAuth";
import "./App.css";

function LoginPage() {
  const { login } = useAuth();

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-header">
          <span className="login-logo">🔥</span>
          <h1>Chivato</h1>
          <p className="text-muted">Azure Infrastructure Drift Detector</p>
        </div>

        <div className="login-body">
          <p>
            Monitorea el drift de tu infraestructura Azure comparando los
            pipelines de ADO con el estado real de los recursos.
          </p>

          <button className="btn btn-primary btn-large" onClick={login}>
            Iniciar Sesión con Microsoft
          </button>
        </div>

        <div className="login-footer">
          <p className="text-muted">
            Requiere cuenta de Microsoft con rol asignado
          </p>
        </div>
      </div>
    </div>
  );
}

function Router() {
  const [path, setPath] = useState(window.location.pathname);

  useEffect(() => {
    const handleLocationChange = () => {
      setPath(window.location.pathname);
    };

    window.addEventListener("popstate", handleLocationChange);

    // Handle link clicks
    const handleClick = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      const anchor = target.closest("a");
      if (
        anchor &&
        anchor.href &&
        anchor.href.startsWith(window.location.origin) &&
        !anchor.hasAttribute("target")
      ) {
        e.preventDefault();
        window.history.pushState({}, "", anchor.href);
        setPath(new URL(anchor.href).pathname);
      }
    };

    document.addEventListener("click", handleClick);

    return () => {
      window.removeEventListener("popstate", handleLocationChange);
      document.removeEventListener("click", handleClick);
    };
  }, []);

  // Route to component
  if (path.startsWith("/pipelines/") && path !== "/pipelines/") {
    const pipelineId = path.split("/pipelines/")[1];
    return <PipelineDetail key={pipelineId} id={pipelineId} />;
  }

  switch (path) {
    case "/billing":
      return <Billing />;
    case "/pipelines":
      return <Pipelines />;
    case "/config":
      return <Configuration />;
    case "/drift":
      return <DriftHistory />;
    case "/scans":
      return <ScanLogs />;
    case "/credentials":
      return <Credentials />;
    default:
      return <Dashboard />;
  }
}

function AuthenticatedApp() {
  const { accounts } = useMsal();
  // Use dev tenant ID in development, otherwise use the authenticated user's tenant
  const isDev = import.meta.env.DEV || window.location.hostname === 'localhost';
  const tenantId = isDev 
    ? 'dev-tenant-00000000-0000-0000-0000-000000000000'
    : accounts[0]?.tenantId;

  return (
    <NotificationsProvider tenantId={tenantId} autoConnect={true}>
      <ModalProvider>
        <Layout>
          <Router />
        </Layout>
        <GlobalToastContainer />
      </ModalProvider>
    </NotificationsProvider>
  );
}

function App() {
  return (
    <>
      <AuthenticatedTemplate>
        <AuthenticatedApp />
      </AuthenticatedTemplate>

      <UnauthenticatedTemplate>
        <LoginPage />
      </UnauthenticatedTemplate>
    </>
  );
}

export default App;
