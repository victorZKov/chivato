import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Header } from "./Header";
import "./Layout.css";

interface LayoutProps {
  children: ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const { t } = useTranslation();

  return (
    <div className="layout">
      <Header />
      <main className="main-content">{children}</main>
      <footer className="footer">
        <span className="footer-text">Chivato v1.0.0</span>
        <span className="footer-separator">|</span>
        <a href="/docs" className="footer-link">
          {t("nav.documentation")}
        </a>
      </footer>
    </div>
  );
}
