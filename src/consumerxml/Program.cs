using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text; 

class Program
{
    private static string XmlFilePath = "";

    static async Task Main(string[] args)
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
        var connection = await factory.CreateConnectionAsync();

        using var channel = await connection.CreateChannelAsync();
        
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null); 
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);


        // Configurar prefetchCount para limitar mensagens não confirmadas
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5000, global: true);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            try
            {
                Console.WriteLine($" [x] Received {message.Split(',')[0]} - {message.Split(',')[1]}");

                AppendToXmlFile(message);

                channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            { 
                channel.BasicNackAsync(ea.DeliveryTag, false, true);
                Console.WriteLine($"[Consumer   ERROR: {ex.Message}");
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

    
    private static readonly object FileLock = new object();

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
            var fields = csvMessage.TrimEnd(';').Split(',');
            string xmlContent = $"<Record><Id>{fields[0]}</Id><Name>{fields[1]}</Name><Role>{fields[2]}</Role></Record>";

            // Lê o conteúdo atual do arquivo XML
            var currentXml = File.ReadAllText(XmlFilePath);

            // Insere o novo conteúdo antes da tag de fechamento </Root>
            var updatedXml = currentXml.Replace("</Root>", $"{xmlContent}\n</Root>");

            // Escreve o conteúdo atualizado no arquivo
            File.WriteAllText(XmlFilePath, updatedXml);
            Console.WriteLine($"[Consumer - Successfully appended to XML file.");
        }
    }

}

