using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace QueueConsumer
{
    public class Process : BackgroundService
    {
        private readonly ILogger<Process> _logger;

        public Process(ILogger<Process> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Consumer is starting.");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogInformation("Initial setup");
            var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 20, // Retry 5 times
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
                });


            var factory = new ConnectionFactory
            {
                HostName = "rabbitmq",
                Port = 5672,
                Password = "userpwd",
                UserName = "user",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            string exchangeName = "process_file";
            string queueName = $"task_huge_file";
            string routingKey = "task.DB";

            await retryPolicy.ExecuteAsync(async () =>
            {

                var connection = await factory.CreateConnectionAsync();

            });

            var connection = await factory.CreateConnectionAsync();
            
            using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct);
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);

            _logger.LogInformation("Consumer started.");
            // Configurar prefetchCount para limitar mensagens não confirmadas
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5000, global: true);
            var databaseName = "dadosBancoCentro";
            var collectionName = "pessoas";
            var client = new MongoClient("mongodb://mongodb:27017/root:mongopw@mongodb");
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                try
                {
                    _logger.LogInformation($" [x] Received {message.Split(',')[0]} - {message.Split(',')[1]}");

                    var document = new BsonDocument
                    {
                            { "id", message.Split(',')[0] },
                            { "name", message.Split(',')[1] },
                            { "receivedAt", DateTime.UtcNow }
                    };
                    collection.InsertOne(document);
                    channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    _logger.LogError(ex, $"Erro de processamento {ex.Message}.");
                    throw;
                }
                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName,
                                 autoAck: false,  // Manual acknowledgment
                                 consumer: consumer);
            _logger.LogInformation($"[Consumer - Waiting for messages...]");

            _logger.LogInformation("Consumer is running...");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation("Consumer stopped.");
        }

    }

}
































































