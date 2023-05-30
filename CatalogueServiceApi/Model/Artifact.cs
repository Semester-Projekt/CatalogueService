using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace Model
{
	public class Artifact
	{
		[BsonId] // Mongo id for a specific Artifact
		[BsonRepresentation(BsonType.ObjectId)]
		public string? MongoId { get; set; }

        [BsonElement("ArtifactID")]
        public int ArtifactID { get; set; }

        [BsonElement("ArtifactName")]
        public string? ArtifactName { get; set; }

        [BsonElement("ArtifactDescription")]
        public string? ArtifactDescription { get; set; }

        [BsonElement("CategoryCode")]
        public string? CategoryCode { get; set; }
        
        [BsonElement("ArtifactOwner")]
        public UserDTO? ArtifactOwner { get; set; }

        [BsonElement("Estimate")]
        public int? Estimate { get; set; }

        [BsonElement("ArtifactPicture")]
        public byte[]? ArtifactPicture { get; set; } = null; // Represents the image data of the artifact as a nullable byte array.


        [BsonElement("Status")]
        public string? Status { get; set; } = "Pending";


        public Artifact(int artifactID, string artifactName, string artifactDescription, int estimate, string categoryCode)
        {
            this.ArtifactID = artifactID;
            this.ArtifactName = artifactName;
            this.ArtifactDescription = artifactDescription;
            this.CategoryCode = categoryCode;
            this.Estimate = estimate;
        }


        public Artifact()
		{

		}
	}
}

