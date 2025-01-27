using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace consumerxml;

public static class Program
{
    private static string XmlFilePath = "";

    public static async Task Main(string[] args)
    {


        if (args.Length != 0)
        {
            XmlFilePath = args[0];
        }


        if (string.IsNullOrEmpty(XmlFilePath))
        {
            XmlFilePath = "../../../../../artifacts/file/output.xml";
        }

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
        string queueName = $"task_huge_file_XML";
        string routingKey = "task.XML";
        IConnection connection = await factory.CreateConnectionAsync();

        using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);


        // Configurar prefetchCount para limitar mensagens não confirmadas
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5000, global: true);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            string message = Encoding.UTF8.GetString(body);
            try
            {
                Console.WriteLine($" [x] Received {message.Split(',')[0]} - {message.Split(',')[1]}");

                AppendToXmlFile(message);

                channel?.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false).AsTask();
            }
            catch (Exception ex)
            {
                channel.BasicNackAsync(ea.DeliveryTag, false, true).AsTask();
                Console.WriteLine($"[Consumer   ERROR: {ex.Message}");
                throw;
            }

            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queue: queueName,
                             autoAck: false,  // Manual acknowledgment
                             consumer: consumer);


    }


    private static readonly System.Threading.Lock FileLock = new();

    private static void AppendToXmlFile(string csvMessage)
    {
        lock (FileLock)
        {
            // Verifica se o arquivo existe; caso contrário, cria com o elemento raiz
            if (!File.Exists(XmlFilePath))
            {
                File.WriteAllText(XmlFilePath, "<Root>\n</Root>");
            }

            // Converte a mensagem CSV em elementos XML
            string[] fields = csvMessage.TrimEnd(';').Split(',');
            string xmlContent = $"<Record><Id>{fields[0]}</Id><Name>{fields[1]}</Name><Role>{fields[2]}</Role></Record>";

            // Lê o conteúdo atual do arquivo XML
            string currentXml = File.ReadAllText(XmlFilePath);

            // Insere o novo conteúdo antes da tag de fechamento </Root>
            string updatedXml = currentXml.Replace("</Root>", $"{xmlContent}\n</Root>");

            // Escreve o conteúdo atualizado no arquivo
            File.WriteAllText(XmlFilePath, updatedXml);
        }
    }

}

