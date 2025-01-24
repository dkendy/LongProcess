

using MongoDB.Bson;
using MongoDB.Driver; 

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

 
string serviceType = builder.Configuration.GetValue<string>("service")?.ToLowerInvariant();

switch (serviceType)
{
    case "api": 
        builder.Services.AddControllers(); // Add API controllers
        builder.Services.AddOpenApi();
        break;

    case "consumer": 
        builder.Services.AddHostedService<QueueConsumer.Process>();
        break;

    case "producer": 
        builder.Services.AddHostedService<QueueProducer.Send>();
        break;

    default:
        throw new InvalidOperationException($"Invalid service type: {serviceType}");
}



WebApplication app = builder.Build();


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
        IMongoDatabase database = client.GetDatabase("dadosBancoCentro");
        IMongoCollection<BsonDocument> _collection = database.GetCollection<BsonDocument>("pessoas");

        long count = await _collection.CountDocumentsAsync(new BsonDocument());

        BsonDocument[] pipeline = new[]
                    {
                new BsonDocument("$facet", new BsonDocument
                {
                    { "earliest", new BsonArray { new BsonDocument("$sort", new BsonDocument("receivedAt", 1)), new BsonDocument("$limit", 1) } },
                    { "latest", new BsonArray { new BsonDocument("$sort", new BsonDocument("receivedAt", -1)), new BsonDocument("$limit", 1) } }
                })
            };

        IAsyncCursor<BsonDocument> results = await _collection.AggregateAsync<BsonDocument>(pipeline);
        BsonDocument result = await results.FirstOrDefaultAsync();

        BsonDocument earliest = result["earliest"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        BsonDocument latest = result["latest"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;


        return new { Earliest = earliest, Latest = latest, Total = count };

    })
    .WithName("GetCount");


}

app.Run();