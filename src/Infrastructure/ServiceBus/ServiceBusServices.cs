
using System.Text;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.ServiceBus;

public class ServiceBusServices : IServiceBusServices
{

    private readonly ILogger<ServiceBusServices> _logger;

    private IChannel? _channel;

    private readonly ServiceBusConnection _serviceBusConnection;

    public ServiceBusServices(ILogger<ServiceBusServices> logger, ServiceBusConnection serviceBusConnection)
    {
        _logger = logger;
        _serviceBusConnection = serviceBusConnection;
    }

    public async Task CreateChannel(string exchangeName)
    {
        _logger.LogInformation("CreateChannel");
        _channel = await _serviceBusConnection.CreateChannelAsync(exchangeName).ConfigureAwait(true);

    }


    public async Task SendMessagesAsync(string message, string exchangeName, string routingKey, CancellationToken stoppingToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            DeliveryMode = DeliveryModes.Persistent
        };

        byte[] body = Encoding.UTF8.GetBytes(message);
        if (_channel == null)
        {
            throw new InvalidOperationException("Channel is not created.");
        }

        await _channel.BasicPublishAsync(exchange: exchangeName,
                                         routingKey: routingKey, mandatory: true, basicProperties: properties,
                                         body: body, cancellationToken: stoppingToken).ConfigureAwait(true);


    }

    public IChannel GetChannel()
    {
        return _channel;
    }

    public async Task ReceiveMessagesAsync(IChannel channel, string queueName, string exchangeName, string routingKey, Action<string> onMessageReceived, CancellationToken stoppingToken)
    {
        if(channel.IsOpen)
        {
            _logger.LogInformation("Channel is open");
        }
        else
        {
            _logger.LogInformation("Channel is not open");
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            try
            {
                onMessageReceived(message);
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(true);
            }
            catch
            {
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, true, cancellationToken: stoppingToken).ConfigureAwait(true);
            }

            await Task.CompletedTask;

        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken).ConfigureAwait(true);

    }

 
}
