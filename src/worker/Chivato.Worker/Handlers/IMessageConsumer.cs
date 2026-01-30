namespace Chivato.Worker.Handlers;

/// <summary>
/// Abstraction for message consumption from different queue providers
/// </summary>
public interface IMessageConsumer : IHostedService, IAsyncDisposable
{
}
