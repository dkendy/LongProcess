
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

    private async Task<IChannel> CreateChannel(string exchangeName)
    {
        return await _serviceBusConnection.CreateChannelAsync(exchangeName).ConfigureAwait(true);
    }


    public async Task SendMessagesAsync(string message, string exchangeName, string routingKey, CancellationToken stoppingToken)
    {
        _channel ??= await CreateChannel(exchangeName);

        _channel.CallbackExceptionAsync += async (sender, args) =>
        {
            _logger.LogError("Exceção no canal: {Message}", args.Exception.Message);
            await Task.CompletedTask;
        };

        _logger.LogInformation(" Creating channel");
        var properties = new BasicProperties
        {
            Persistent = true,
            DeliveryMode = DeliveryModes.Persistent
        };

        byte[] body = Encoding.UTF8.GetBytes(message);
        await _channel.BasicPublishAsync(exchange: exchangeName,
                                                        routingKey: routingKey, mandatory: true, basicProperties: properties,
                                                        body: body, cancellationToken: stoppingToken).ConfigureAwait(true);


    }

    public async Task ReceiveMessagesAsync(string queueName, string exchangeName, string routingKey, Action<string> onMessageReceived, CancellationToken stoppingToken)
    {
        _channel ??= await CreateChannel(exchangeName);
        _logger.LogInformation(" Creating channel");

        await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, cancellationToken: stoppingToken).ConfigureAwait(true);
        await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken).ConfigureAwait(true);
        await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: stoppingToken).ConfigureAwait(true);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5000, global: true, cancellationToken: stoppingToken).ConfigureAwait(true);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            try
            {
                onMessageReceived(message);
                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken).ConfigureAwait(true);
            }
            catch
            {
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, true, cancellationToken: stoppingToken).ConfigureAwait(true);
            }

            await Task.CompletedTask;

        };

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer, cancellationToken: stoppingToken).ConfigureAwait(true);

    }

}
