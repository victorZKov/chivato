import { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../hooks/useAuth";
import { useTheme } from "../../hooks/useTheme";
import type { Theme } from "../../hooks/useTheme";
import { useRoles } from "../../hooks/useRoles";
import { NotificationBell } from "../common/NotificationBell";
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

const LanguageFlag = ({ lang }: { lang: string }) => {
  switch (lang) {
    case "es":
      return <span className="flag">🇪🇸</span>;
    case "en":
    default:
      return <span className="flag">🇬🇧</span>;
  }
};

export function Header() {
  const { t, i18n } = useTranslation();
  const { isAuthenticated, user, login, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const { isAdmin } = useRoles();
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

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

  const toggleLanguage = () => {
    const newLang = i18n.language === "en" ? "es" : "en";
    i18n.changeLanguage(newLang);
  };

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const getUserInitials = () => {
    if (!user?.name) return "?";
    const parts = user.name.split(" ");
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }
    return parts[0][0].toUpperCase();
  };

  return (
    <header className="header">
      <div className="header-left">
        <a href="/" className="header-brand">
          <span className="header-logo">🔥</span>
          <span className="header-title">{t("common.appName")}</span>
        </a>

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
          </nav>
        )}
      </div>

      <div className="header-right">
        {isAuthenticated ? (
          <>
            <NotificationBell />

            <div className="user-menu-container" ref={userMenuRef}>
              <button
                className="user-menu-trigger"
                onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
                aria-expanded={isUserMenuOpen}
                aria-haspopup="true"
              >
                <span className="user-avatar">{getUserInitials()}</span>
                <span className="user-name">{user?.name}</span>
                {isAdmin && <span className="badge badge-admin">{t("roles.admin")}</span>}
                <span className={`dropdown-arrow ${isUserMenuOpen ? "open" : ""}`}>▼</span>
              </button>

              {isUserMenuOpen && (
                <div className="user-dropdown">
                  <div className="dropdown-header">
                    <span className="dropdown-user-name">{user?.name}</span>
                    <span className="dropdown-user-email">{user?.email}</span>
                  </div>

                  <div className="dropdown-divider" />

                  {isAdmin && (
                    <>
                      <a href="/credentials" className="dropdown-item" onClick={() => setIsUserMenuOpen(false)}>
                        <span className="dropdown-icon">🔑</span>
                        {t("nav.credentials")}
                      </a>
                      <a href="/config" className="dropdown-item" onClick={() => setIsUserMenuOpen(false)}>
                        <span className="dropdown-icon">⚙️</span>
                        {t("nav.configuration")}
                      </a>
                      <div className="dropdown-divider" />
                    </>
                  )}

                  <button className="dropdown-item" onClick={toggleLanguage}>
                    <span className="dropdown-icon">
                      <LanguageFlag lang={i18n.language} />
                    </span>
                    {t("language.title")}
                    <span className="dropdown-value">{i18n.language.toUpperCase()}</span>
                  </button>

                  <button className="dropdown-item" onClick={cycleTheme}>
                    <span className="dropdown-icon">
                      <ThemeIcon theme={theme} />
                    </span>
                    {t("theme.title")}
                    <span className="dropdown-value">{getThemeLabel()}</span>
                  </button>

                  <div className="dropdown-divider" />

                  <button className="dropdown-item dropdown-item-danger" onClick={logout}>
                    <span className="dropdown-icon">🚪</span>
                    {t("auth.logout")}
                  </button>
                </div>
              )}
            </div>
          </>
        ) : (
          <button className="btn btn-primary" onClick={login}>
            {t("auth.login")}
          </button>
        )}
      </div>
    </header>
  );
}
