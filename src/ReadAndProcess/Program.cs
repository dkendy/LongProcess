

using System.Xml.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using QueueConsumer;
using QueueProducer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
var serviceType = builder.Configuration.GetValue<string>("service")?.ToLowerInvariant();

switch (serviceType)
{
    case "api":
        Console.WriteLine("Starting in API mode...");
        builder.Services.AddControllers(); // Add API controllers
        builder.Services.AddOpenApi();
        break;

    case "consumer":
        Console.WriteLine("Starting consumer");
        builder.Services.AddHostedService<QueueConsumer.Process>();
        break;

    case "producer":
        Console.WriteLine("Starting producer");
        builder.Services.AddHostedService<QueueProducer.Send>();
        break;

    default:
        throw new InvalidOperationException($"Invalid service type: {serviceType}");
}



var app = builder.Build();


if (serviceType == "api")
{
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapControllers();



    app.MapGet("/service", async () =>
    {
        var client = new MongoClient("mongodb://mongodb:27017/root:mongopw@mongodb");
        var database = client.GetDatabase("dadosBancoCentro");
        var _collection = database.GetCollection<BsonDocument>("pessoas");

        var count = await _collection.CountDocumentsAsync(new BsonDocument());

        var pipeline = new[]
                    {
                new BsonDocument("$facet", new BsonDocument
                {
                    { "earliest", new BsonArray { new BsonDocument("$sort", new BsonDocument("receivedAt", 1)), new BsonDocument("$limit", 1) } },
                    { "latest", new BsonArray { new BsonDocument("$sort", new BsonDocument("receivedAt", -1)), new BsonDocument("$limit", 1) } }
                })
            };

        var results = await _collection.AggregateAsync<BsonDocument>(pipeline);
        var result = await results.FirstOrDefaultAsync();

        var earliest = result["earliest"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var latest = result["latest"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;


        return new { Earliest = earliest, Latest = latest, Total = count };

    })
    .WithName("GetCount");


}

app.Run();