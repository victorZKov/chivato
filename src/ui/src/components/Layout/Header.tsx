import { useTranslation } from "react-i18next";
import { useAuth } from "../../hooks/useAuth";
import { useTheme } from "../../hooks/useTheme";
import type { Theme } from "../../hooks/useTheme";
import { useRoles } from "../../hooks/useRoles";
import { LanguageSelector } from "../common/LanguageSelector";
import "./Header.css";

const ThemeIcon = ({ theme }: { theme: Theme }) => {
  switch (theme) {
    case "light":
      return <span>☀️</span>;
    case "dark":
      return <span>🌙</span>;
    case "system":
      return <span>💻</span>;
  }
};

export function Header() {
  const { t } = useTranslation();
  const { isAuthenticated, user, login, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const { isAdmin } = useRoles();

  const cycleTheme = () => {
    const themes: Theme[] = ["light", "dark", "system"];
    const currentIndex = themes.indexOf(theme);
    const nextIndex = (currentIndex + 1) % themes.length;
    setTheme(themes[nextIndex]);
  };

  const getThemeLabel = () => {
    switch (theme) {
      case "light":
        return t("theme.light");
      case "dark":
        return t("theme.dark");
      case "system":
        return t("theme.system");
    }
  };

  return (
    <header className="header">
      <div className="header-brand">
        <span className="header-logo">🔥</span>
        <span className="header-title">{t("common.appName")}</span>
      </div>

      {isAuthenticated && (
        <nav className="header-nav">
          <a href="/" className="nav-link">
            {t("nav.dashboard")}
          </a>
          <a href="/pipelines" className="nav-link">
            {t("nav.pipelines")}
          </a>
          <a href="/drift" className="nav-link">
            {t("nav.driftHistory")}
          </a>
          <a href="/scans" className="nav-link">
            {t("nav.scans")}
          </a>
          {isAdmin && (
            <>
              <a href="/credentials" className="nav-link">
                {t("nav.credentials")}
              </a>
              <a href="/config" className="nav-link">
                {t("nav.configuration")}
              </a>
            </>
          )}
        </nav>
      )}

      <div className="header-actions">
        <LanguageSelector />

        <button
          className="btn btn-ghost theme-toggle"
          onClick={cycleTheme}
          title={`${t("theme.title")}: ${getThemeLabel()}`}
        >
          <ThemeIcon theme={theme} />
        </button>

        {isAuthenticated ? (
          <div className="user-menu">
            <span className="user-name">{user?.name}</span>
            {isAdmin && <span className="badge badge-info">{t("roles.admin")}</span>}
            <button className="btn btn-ghost" onClick={logout}>
              {t("auth.logout")}
            </button>
          </div>
        ) : (
          <button className="btn btn-primary" onClick={login}>
            {t("auth.login")}
          </button>
        )}
      </div>
    </header>
  );
}
