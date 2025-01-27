using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Infrastructure.ServiceBus;
using System.Data;
using Infrastructure.Database;

namespace consumer;

public class Process : BackgroundService
{
    private readonly ILogger<Process> _logger;
    private readonly IServiceBusServices _serviceBusServices;
    private readonly IDatabaseServices _databaseServices;

    public Process(ILogger<Process> logger, IServiceBusServices serviceBusServices, IDatabaseServices databaseServices)
    {
        _logger = logger;
        _serviceBusServices = serviceBusServices;
        _databaseServices = databaseServices;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consumer is starting.");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        string exchangeName = "process_file";
        string queueName = $"task_huge_file";
        string routingKey = "task.DB";

        await _serviceBusServices.ReceiveMessagesAsync(queueName: queueName, exchangeName: exchangeName, routingKey: routingKey,
         async onMessageReceived =>
        {

            try
            {
                using MongoClient client = _databaseServices.GetClient();

                _logger.LogInformation("[x] Received {Id} - {Name} **** ", onMessageReceived.Split(',')[0], onMessageReceived.Split(',')[1]);

                var document = new BsonDocument
                {
                            { "id", onMessageReceived.Split(',')[0] },
                            { "name", onMessageReceived.Split(',')[1] },
                            { "receivedAt", DateTime.UtcNow }
                };
                IMongoCollection<BsonDocument> collection = _databaseServices.GetDocument(client, "Pessoas", "BancoCentral2");
                await collection.InsertOneAsync(document);

                _logger.LogInformation("Insert ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro de processamento {Message}.", ex.Message);
                throw new Exception($"Error processing message: {onMessageReceived}", ex);
            }
        }, stoppingToken);

    }

}


































































