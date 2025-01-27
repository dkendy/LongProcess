
using System.Text;
using RabbitMQ.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Infrastructure.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace producer;

public class Send : BackgroundService
{
    public readonly IServiceBusServices _serviceBusServices;
    public readonly IConfiguration _configuration;
    public Send(IServiceBusServices serviceBusServices, ILogger<Send> logger, IConfiguration configuration)
    {
        _serviceBusServices = serviceBusServices;
        _logger = logger;
        _configuration = configuration;
        FilePath = _configuration["local"] ?? "C:\\Users\\Public\\Documents\\hugefile.csv";
    }
    public string FilePath { get; set; } = "C:\\Users\\Public\\Documents\\hugefile.csv";
    public string Operation { get; set; } = "2";
    private readonly ILogger<Send> _logger;



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        try
        {
            const int chunkSize = 1024 * 1024 * 100;
            byte[] buffer = new byte[chunkSize];
            var stringBuilder = new StringBuilder();
            int lineCount = 0;

            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead;
                _logger.LogInformation("Lendo o arquivo");
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, chunkSize), stoppingToken)) > 0)
                {
                    string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    stringBuilder.Append(content);

                    string[] lines = stringBuilder.ToString().Split(';');
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        _logger.LogInformation("[x] Sending {Id} - {Name}", lines[i].Split(',')[0], lines[i].Split(',')[1]);
                        await _serviceBusServices.SendMessagesAsync(message: lines[i], exchangeName: "process_file", routingKey: "task.DB", stoppingToken);
                        lineCount++;
                    }

                    // Preservar a última linha parcial no buffer
                    stringBuilder.Clear();
                    stringBuilder.Append(lines[^1]);
                }

                // Processar o restante
                if (stringBuilder.Length > 0)
                {
                    await _serviceBusServices.SendMessagesAsync(message: stringBuilder.ToString(), exchangeName: "exchangeName", routingKey: "task.DB", stoppingToken);
                    _logger.LogInformation("Enviado chunk: {LineCount} linhas.", lineCount);
                }
            }


            _logger.LogInformation("Arquivo processado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar o arquivo {FilePath}: {Message}", FilePath, ex.Message);
            throw new Exception($"Erro ao processar o arquivo {FilePath}", ex);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
        _logger.LogInformation("Producer stopped.");

    }


}

