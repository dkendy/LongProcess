
using RabbitMQ.Client;

namespace Infrastructure.ServiceBus;

public interface IServiceBusServices
{
    Task SendMessagesAsync(string message, string exchangeName, string routingKey, CancellationToken stoppingToken);
    Task ReceiveMessagesAsync(string queueName, string exchangeName, string routingKey, Action<string> onMessageReceived, CancellationToken stoppingToken);
}
