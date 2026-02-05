import type { TFunction } from "i18next";

export function formatTimeAgo(date: string, t: TFunction): string {
  const now = new Date();
  const past = new Date(date);
  const diffMs = now.getTime() - past.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 60) return t("time.minutesAgo", { count: diffMins });
  if (diffHours < 24) return t("time.hoursAgo", { count: diffHours });
  if (diffDays < 7) return t("time.daysAgo", { count: diffDays });
  return t("time.weeksAgo", { count: Math.floor(diffDays / 7) });
}
