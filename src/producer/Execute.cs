using System;
using System.IO;
using System.Text;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public class Execute
{
    public string filePath { get; set; } = "../../../../../../../../../artifacts/file/dados_gerados.txt"; 
    public string operation { get; set; } = "2"; 


    [GlobalSetup]
    public async Task Run()
    {
 
        if (operation == "1")
            await SendChunksToQueue();
        else
            await AnalyzeFileAsync();

        Console.WriteLine("Todos os chunks foram enviados.");

    }

 
    public  async Task SendChunksToQueue()
    {
        Console.WriteLine("Operação com fila");
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

        using var connection = await factory.CreateConnectionAsync();


        using var channel = await connection.CreateChannelAsync();

        var properties = new BasicProperties
        {
            Persistent = true,
            DeliveryMode = DeliveryModes.Persistent
        };


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

                    Console.WriteLine("Enviado último chunk.");

                    Console.WriteLine($"Enviado chunk com {lineCount} linhas.");



                }
            }


            Console.WriteLine("Arquivo processado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro: {ex.Message}");
        }

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

