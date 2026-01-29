import { useState, useEffect } from "react";

export type Theme = "light" | "dark" | "system";

const THEME_KEY = "chivato-theme";

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(() => {
    const stored = localStorage.getItem(THEME_KEY);
    return (stored as Theme) || "system";
  });

  const setTheme = (newTheme: Theme) => {
    setThemeState(newTheme);
    localStorage.setItem(THEME_KEY, newTheme);
  };

  useEffect(() => {
    const root = document.documentElement;

    const applyTheme = (resolvedTheme: "light" | "dark") => {
      root.setAttribute("data-theme", resolvedTheme);
    };

    if (theme === "system") {
      const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
      applyTheme(mediaQuery.matches ? "dark" : "light");

      const handler = (e: MediaQueryListEvent) => {
        applyTheme(e.matches ? "dark" : "light");
      };

      mediaQuery.addEventListener("change", handler);
      return () => mediaQuery.removeEventListener("change", handler);
    } else {
      applyTheme(theme);
    }
  }, [theme]);

  const resolvedTheme =
    theme === "system"
      ? window.matchMedia("(prefers-color-scheme: dark)").matches
        ? "dark"
        : "light"
      : theme;

  return {
    theme,
    setTheme,
    resolvedTheme,
    isDark: resolvedTheme === "dark",
  };
}
