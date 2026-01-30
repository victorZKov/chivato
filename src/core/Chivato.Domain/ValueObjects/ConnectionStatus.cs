namespace Chivato.Domain.ValueObjects;

/// <summary>
/// Status of a connection (Azure, ADO, etc.)
/// </summary>
public enum ConnectionStatus
{
    Unknown = 0,
    Connected = 1,
    Disconnected = 2,
    Error = 3,
    Expired = 4
}

public static class ConnectionStatusExtensions
{
    public static string ToDisplayString(this ConnectionStatus status) => status switch
    {
        ConnectionStatus.Unknown => "Unknown",
        ConnectionStatus.Connected => "Connected",
        ConnectionStatus.Disconnected => "Disconnected",
        ConnectionStatus.Error => "Error",
        ConnectionStatus.Expired => "Expired",
        _ => "Unknown"
    };

    public static bool IsHealthy(this ConnectionStatus status) =>
        status == ConnectionStatus.Connected;

    public static bool NeedsAttention(this ConnectionStatus status) =>
        status == ConnectionStatus.Error || status == ConnectionStatus.Expired || status == ConnectionStatus.Disconnected;
}
