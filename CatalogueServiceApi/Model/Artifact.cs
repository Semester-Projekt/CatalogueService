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
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string? MongoId { get; set; }

        [BsonElement("ArtifactID")]
        public int ArtifactID { get; set; } //skal måske være en 'int'?

        [BsonElement("ArtifactName")]
        public string ArtifactName { get; set; }

        [BsonElement("ArtifactDescription")]
        public string ArtifactDescription { get; set; }

        [BsonElement("CategoryCode")]
        public string CategoryCode { get; set; }

        [BsonElement("ArtifactOwner")]
        public string ArtifactOwner { get; set; } // er dette rigtigt? skal evt laves om til string Name

        [BsonElement("Estimate")]
        public int Estimate { get; set; } = 0;

        [BsonElement("ArtifactPicture")]
        public string? ArtifactPicture { get; set; } = null; // hvordan uploader vi et billede?

        [BsonElement("Active")]
        public bool Active { get; set; } = false;


        public Artifact(int artifactID, string artifactName, string artifactDescription, int estimate, string categoryCode, string artifactOwner)
        {
            this.ArtifactID = artifactID;
            this.ArtifactName = artifactName;
            this.ArtifactDescription = artifactDescription;
            this.CategoryCode = categoryCode;
            this.ArtifactOwner = artifactOwner;
            this.Estimate = estimate;
        }


        public Artifact()
		{

		}
	}
}

