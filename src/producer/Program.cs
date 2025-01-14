using System;
using System.IO;
using System.Text;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;


class Producer
{
    public static async Task SendChunksToQueue(string inputFile)
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

        using var connection = await factory.CreateConnectionAsync();
        
        
        using var channel = await connection.CreateChannelAsync();

        var properties = new BasicProperties
        {
            Persistent = true
        };


        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Fanout, durable: false, autoDelete: false, arguments: null); 
       
         
        try
        {
            const int chunkSize = 1024 * 1024 * 100;
            byte[] buffer = new byte[chunkSize];
            var stringBuilder = new StringBuilder();
            int lineCount = 0;
            var publishTasks = new List<Task>();

            using (var stream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
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
                                            routingKey: "task.DB",mandatory: true, basicProperties: properties,
                                            body: body);

                        await channel.BasicPublishAsync(exchange: exchangeName,
                                            routingKey: "task.XML", mandatory: true,basicProperties: properties,
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
                                            routingKey: "task.DB",mandatory: true,basicProperties: properties,
                                            body: body);
                    
                    await channel.BasicPublishAsync(exchange: exchangeName,
                                            routingKey: "task.XML", mandatory: true,basicProperties: properties,
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

    static async Task Main(string[] args)
    {

        string filePath = string.Empty;

        if (args.Length != 0)
        {
            filePath = args[0];
        }


        if (string.IsNullOrEmpty(filePath))
        {
            filePath = "../../../../../artifacts/file/dados_gerados.txt";
        }

        await SendChunksToQueue(filePath);
        Console.WriteLine("Todos os chunks foram enviados.");
    }
}


/*

class Program
{
    static async Task Main(string[] args)
    {
        string filePath = string.Empty;

        if (args.Length != 0)
        {
            filePath = args[0];
        }


        if (string.IsNullOrEmpty(filePath))
        {
            filePath = "../../../../../artifacts/file/dados_gerados.txt";
        }
        int chunkSize = 1024 * 1024 * 100; // 100MB per chunk
        int degreeOfParallelism = Environment.ProcessorCount; // Use all available cores
        degreeOfParallelism = 1; 
        await ProcessLargeFileAsync(filePath, chunkSize, degreeOfParallelism);

        Console.WriteLine("File processing completed.");
    }

    static async Task ProcessLargeFileAsync(string filePath, int chunkSize, int degreeOfParallelism)
    {
        long fileSize = new FileInfo(filePath).Length;

        // Divide the file into chunks
        var chunks = new ConcurrentQueue<(long Start, long End)>();
        for (long start = 0; start < fileSize; start += chunkSize)
        {
            long end = Math.Min(start + chunkSize - 1, fileSize - 1);
            chunks.Enqueue((start, end));
        }

         
            var factory = new ConnectionFactory 
            {   HostName = "localhost", 
                Port = 5672, 
                Password = "userpwd", 
                UserName = "user" ,
                AutomaticRecoveryEnabled = true,  
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),  
                RequestedHeartbeat = TimeSpan.FromSeconds(30) 
            };
             
             
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism };
        var cancellationToken = new CancellationToken();
        int count = 0;
        await Parallel.ForEachAsync(chunks, cancellationToken, async (chunk, ct) =>
        { 
            using var connection = await factory.CreateConnectionAsync();   
            using var channel = await connection.CreateChannelAsync();  
            await channel.QueueDeclareAsync(queue: $"file_chunks_{count}",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

            count++;
            ProcessChunk(filePath, chunk.Start, chunk.End, channel);
        });
    }

    static async void ProcessChunk(string filePath, long start, long end, IChannel channel)
    {
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            stream.Seek(start, SeekOrigin.Begin);

            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                StringBuilder buffer = new StringBuilder();

                // Adjust the start position to the beginning of a valid record
                if (start != 0)
                {
                    SkipToNextSeparator(reader, ';');
                }

                while (stream.Position <= end)
                {
                    char[] readBuffer = new char[4096];
                    int readCount = reader.Read(readBuffer, 0, readBuffer.Length);

                    if (readCount == 0)
                        break;

                    buffer.Append(readBuffer, 0, readCount);

                    // Process complete lines (up to `;` as a separator)
                    string[] records = buffer.ToString().Split(';');
                    for (int i = 0; i < records.Length - 1; i++)
                    {
                        await ProcessRecordAsync(records[i], channel);
                    }

                    // Keep the last, potentially incomplete record in the buffer
                    buffer.Clear();
                    buffer.Append(records[^1]);
                }

                // Process any remaining record in the buffer
                if (buffer.Length > 0)
                {
                    await ProcessRecordAsync(buffer.ToString(), channel);
                }
            }
        }
    }

    static void SkipToNextSeparator(StreamReader reader, char separator)
    {
        int currentChar;
        while ((currentChar = reader.Read()) != -1)
        {
            if ((char)currentChar == separator)
                break;
        }
    }

    static async Task ProcessRecordAsync(string record, IChannel channel)
    {
        // Split the record by commas and process the columns
        string[] columns = record.Split(',');
        Console.WriteLine($"Processed record: {string.Join("|", columns)}");
        
        var body = Encoding.UTF8.GetBytes(string.Join("|", columns));
        await channel.BasicPublishAsync(exchange: "",
                            routingKey: "file_chunks",
                            body: body);
    }



}
*/