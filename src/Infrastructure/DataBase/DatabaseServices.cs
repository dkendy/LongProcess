using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using MongoDB.Driver;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Infrastructure.Database;
public class DatabaseServices : IDatabaseServices
{
    private readonly IConfiguration _configuration;

    public DatabaseServices(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public MongoClient GetClient()
    {
        return new MongoClient(_configuration["ConnectionStrings:DefaultConnection"] ?? "conn");
    }
    public IMongoCollection<BsonDocument> GetDocument(MongoClient client, string databaseName, string collectionName)
    {
        IMongoDatabase database = client.GetDatabase(databaseName);
        return database.GetCollection<BsonDocument>(collectionName);
    }

}