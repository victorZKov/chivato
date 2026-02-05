interface MetricCardProps {
  title: string;
  value: string | number;
  severity: "critical" | "high" | "medium" | "low" | "ok";
  icon: string;
  loading?: boolean;
}

export function MetricCard({ title, value, severity, icon, loading }: MetricCardProps) {
  return (
    <div className={`metric-card metric-${severity}`}>
      <span className="metric-icon">{icon}</span>
      <div className="metric-content">
        <span className="metric-value">{loading ? "..." : value}</span>
        <span className="metric-title">{title}</span>
      </div>
    </div>
  );
}
