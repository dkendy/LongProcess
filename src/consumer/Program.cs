using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; 
using Polly;
using Polly.Retry; 

namespace QueueConsumer;
 
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
            AsyncRetryPolicy retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 20, // Retry 5 times
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {Count} after {TotalSeconds} seconds due to {Message}", retryCount,timeSpan.TotalSeconds,exception.Message);
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

            await retryPolicy.ExecuteAsync(
                async () =>  await factory.CreateConnectionAsync()
             );

            IConnection connection = await factory.CreateConnectionAsync(cancellationToken:stoppingToken);
            
            using IChannel channel = await connection.CreateChannelAsync(cancellationToken:stoppingToken);
            await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct,cancellationToken:stoppingToken);
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null,cancellationToken:stoppingToken);
            await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey,cancellationToken:stoppingToken);

            _logger.LogInformation("Consumer started.");
            // Configurar prefetchCount para limitar mensagens não confirmadas
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5000, global: true,cancellationToken:stoppingToken);
            string databaseName = "dadosBancoCentro";
            string collectionName = "pessoas";
            using var client = new MongoClient("mongodb://mongodb:27017/root:mongopw@mongodb");
            IMongoDatabase database = client.GetDatabase(databaseName);
            IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(collectionName);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                string message = Encoding.UTF8.GetString(body);
                try
                {
                    _logger.LogInformation("[x] Received {Id} - {Name}", message.Split(',')[0], message.Split(',')[1]);

                    var document = new BsonDocument
                    {
                            { "id", message.Split(',')[0] },
                            { "name", message.Split(',')[1] },
                            { "receivedAt", DateTime.UtcNow }
                    };
                    await collection.InsertOneAsync(document);
                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro de processamento {Message}.", ex.Message);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken: stoppingToken);
                    throw new Exception($"Erro de processamento na mensagem: {message}", ex);
                }
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName,
                                 autoAck: false,  // Manual acknowledgment
                                 consumer: consumer,
                                  cancellationToken: stoppingToken);

            _logger.LogInformation("[Consumer - Waiting for messages...]");

            _logger.LogInformation("Consumer is running...");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation("Consumer stopped.");
        }

    }


































































