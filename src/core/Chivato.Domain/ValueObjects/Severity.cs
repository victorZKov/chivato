namespace Chivato.Domain.ValueObjects;

/// <summary>
/// Severity level for drift detection
/// </summary>
public enum Severity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public static class SeverityExtensions
{
    public static string ToDisplayString(this Severity severity) => severity switch
    {
        Severity.Low => "Low",
        Severity.Medium => "Medium",
        Severity.High => "High",
        Severity.Critical => "Critical",
        _ => "Unknown"
    };

    public static Severity FromString(string value) => value.ToLowerInvariant() switch
    {
        "low" => Severity.Low,
        "medium" => Severity.Medium,
        "high" => Severity.High,
        "critical" => Severity.Critical,
        _ => Severity.Low
    };
}
