using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;

class Program
{
    static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            Password = "userpwd",
            UserName = "user",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        string exchangeName = "process_file";
        string queueName = $"task_huge_file_{DateTime.Now.Ticks}";
        string routingKey = "task.DB";
        var connection = await factory.CreateConnectionAsync();

        using var channel = await connection.CreateChannelAsync();

 
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Fanout);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null); 
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);


        // Configurar prefetchCount para limitar mensagens não confirmadas
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: true);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            try
            {
                Console.WriteLine($" [x] Received {message.Split(',')[0]} - {message.Split(',')[1]}");

                var databaseName = "dadosBancoCentro";
                var collectionName = "pessoas";

                var client = new MongoClient("mongodb://localhost:27017/root:mongopw@localhost");
                var database = client.GetDatabase(databaseName);
                var collection = database.GetCollection<BsonDocument>(collectionName);

                var document = new BsonDocument
                {
                    { "id", message.Split(',')[0] },
                    { "name", message.Split(',')[1] },
                    { "receivedAt", DateTime.UtcNow }
                };

                collection.InsertOne(document);

                channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception)
            { 
                channel.BasicNackAsync(ea.DeliveryTag, false, true);
                throw;
            } 

            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue: queueName,
                             autoAck: false,  // Manual acknowledgment
                             consumer: consumer);

        Console.WriteLine($"[Consumer   Waiting for messages...");
        Console.ReadLine(); // Mantém o consumidor vivo
        Console.WriteLine("Press [enter] to exit.");
        Console.ReadLine();
    }

}

