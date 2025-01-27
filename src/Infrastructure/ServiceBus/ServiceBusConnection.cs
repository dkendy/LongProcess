using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Threading.Tasks;

namespace Infrastructure.ServiceBus;
public class ServiceBusConnection : IDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    private readonly ILogger<ServiceBusConnection> _logger;
    private readonly IConfiguration _configuration;

    public ServiceBusConnection(IConfiguration configuration, ILogger<ServiceBusConnection> logger)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IChannel> CreateChannelAsync(string exchangeName)
    {
        if (_connection == null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["ServiceBus:HostName"] ?? "rabbitmq",
                Port = 5672,
                Password = _configuration["ServiceBus:Password"] ?? "userpwd",
                UserName = _configuration["ServiceBus:UserName"] ?? "pwd",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            AsyncRetryPolicy retryPolicy = Policy
          .Handle<RabbitMQ.Client.Exceptions.BrokerUnreachableException>()
          .WaitAndRetryAsync(
              retryCount: 20,
              sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
              onRetry: (exception, timeSpan, retryCount, context) =>
              {
                  _logger.LogWarning("Retry {RetryCount} after {TotalSeconds} seconds due to {Message}", retryCount, timeSpan.TotalSeconds, exception.Message);
              });

            await retryPolicy.ExecuteAsync(async () =>
            {

                _connection = await factory.CreateConnectionAsync();

                _connection.ConnectionShutdownAsync += async (sender, args) =>
                {
                    _logger.LogWarning("Conexão encerrada - connection: {Mensagem}", args.ReplyText);
                    await Task.CompletedTask;
                };

            });
        }

        if (_connection == null)
        {
            throw new InvalidOperationException("Connection is not established.");
        }

        _channel = await _connection.CreateChannelAsync();

        _logger.LogInformation("Conexão com o RabbitMQ estabelecida.");
        await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: false, autoDelete: false, arguments: null);
        return _channel;
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }

}
