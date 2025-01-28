
using RabbitMQ.Client;

namespace Infrastructure.ServiceBus;

public interface IServiceBusServices
{
    IChannel GetChannel();
    Task CreateChannel(string exchangeName);
    Task SendMessagesAsync(string message, string exchangeName, string routingKey, CancellationToken stoppingToken);
    Task ReceiveMessagesAsync(IChannel channel, string queueName, string exchangeName, string routingKey, Action<string> onMessageReceived, CancellationToken stoppingToken);
}
