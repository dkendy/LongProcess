using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure.Database;
public interface IDatabaseServices
{
    MongoClient GetClient();
    IMongoCollection<BsonDocument> GetDocument(MongoClient client, string databaseName, string collectionName);

}