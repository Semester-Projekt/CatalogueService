using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Model
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("CategoryCode")]
        public string CategoryCode { get; set; } //skal måske være en 'int'?

        [BsonElement("CategoryName")]
        public string CategoryName { get; set; }

        [BsonElement("CategoryDescription")]
        public string CategoryDescription { get; set; }
        
        public List<Artifact>? CategoryArtifacts { get; internal set; }

        public Category()
        {

        }

        public Category(string categoryCode, string categoryName, string categoryDescription)
        {
            this.CategoryCode = categoryCode;
            this.CategoryName = categoryName;
            this.CategoryDescription = categoryDescription;
        }
    }
}

