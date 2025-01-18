
using System.Text;
using RabbitMQ.Client;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace QueueProducer
{

    public class Send : BackgroundService
    {

        public string filePath { get; set; } = "/app/file/dados_gerados.txt";
        public string operation { get; set; } = "2";
        private readonly ILogger<Send> _logger;

        public Send(ILogger<Send> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var retryPolicy = Policy
              .Handle<RabbitMQ.Client.Exceptions.BrokerUnreachableException>()
              .WaitAndRetryAsync(
                  retryCount: 20, // Retry 5 times
                  sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                  onRetry: (exception, timeSpan, retryCount, context) =>
                  {
                      _logger.LogWarning($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to {exception.Message}");
                  });
 

            _logger.LogInformation($"Arquivo: {filePath}");

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

            await retryPolicy.ExecuteAsync(async () =>
            {
                string exchangeName = "process_file";

                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                var properties = new BasicProperties
                {
                    Persistent = true,
                    DeliveryMode = DeliveryModes.Persistent
                };

                _logger.LogInformation("Iniciando envio de mensagens");
                await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: false, autoDelete: false, arguments: null);


                try
                {
                    const int chunkSize = 1024 * 1024 * 100;
                    byte[] buffer = new byte[chunkSize];
                    var stringBuilder = new StringBuilder();
                    int lineCount = 0;
                    var publishTasks = new List<Task>();

                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        int bytesRead;
                        _logger.LogInformation("Lendo o arquivo");
                        while ((bytesRead = stream.Read(buffer, 0, chunkSize)) > 0)
                        {
                            string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            stringBuilder.Append(content);

                            string[] lines = stringBuilder.ToString().Split(';');
                            for (int i = 0; i < lines.Length - 1; i++)
                            {

                                var body = Encoding.UTF8.GetBytes(lines[i]);
                                await channel.BasicPublishAsync(exchange: exchangeName,
                                                    routingKey: "task.DB", mandatory: true, basicProperties: properties,
                                                    body: body);

                                await channel.BasicPublishAsync(exchange: exchangeName,
                                                    routingKey: "task.XML", mandatory: true, basicProperties: properties,
                                                    body: body);

                                lineCount++;

                            }

                            // Preservar a última linha parcial no buffer
                            stringBuilder.Clear();
                            stringBuilder.Append(lines[^1]);
                        }

                        // Processar o restante
                        if (stringBuilder.Length > 0)
                        {


                            var body = Encoding.UTF8.GetBytes(stringBuilder.ToString());
                            await channel.BasicPublishAsync(exchange: exchangeName,
                                                    routingKey: "task.DB", mandatory: true, basicProperties: properties,
                                                    body: body);

                            await channel.BasicPublishAsync(exchange: exchangeName,
                                                    routingKey: "task.XML", mandatory: true, basicProperties: properties,
                                                    body: body);

                            _logger.LogInformation($"Enviado chunk: {lineCount} linhas.");
                        }
                    }


                    _logger.LogInformation("Arquivo processado.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,$"Erro: {ex.Message}");
                    throw;
                }
            });
        
             _logger.LogInformation("Producer is running...");

            while (!stoppingToken.IsCancellationRequested)
            { 
                await Task.Delay(5000, stoppingToken);
            }
            _logger.LogInformation("Producer stopped.");
        
        }


        [Benchmark]
        public async Task AnalyzeFileAsync()
        {
            Console.WriteLine("Operação com analise de arquivo");
            try
            {
                const int chunkSize = 1024 * 1024 * 100;
                byte[] buffer = new byte[chunkSize];
                var stringBuilder = new StringBuilder();
                int lineCount = 0;

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, chunkSize)) > 0)
                    {
                        string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        stringBuilder.Append(content);

                        string[] lines = stringBuilder.ToString().Split(';');
                        for (int i = 0; i < lines.Length - 1; i++)
                        {
                            lineCount++;
                        }

                        // Preservar a última linha parcial no buffer
                        stringBuilder.Clear();
                        stringBuilder.Append(lines[^1]);
                    }

                    // Processar o restante
                    if (stringBuilder.Length > 0)
                    {
                        lineCount++;
                        Console.WriteLine("Enviado último chunk.");
                        Console.WriteLine($"Enviado chunk com {lineCount} linhas.");
                    }
                }


                Console.WriteLine($"Arquivo processado com {lineCount} linhas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
            }

        }
    }

}