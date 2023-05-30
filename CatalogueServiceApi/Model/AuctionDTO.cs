using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class AuctionDTO
	{
        // This class works as a Data Transfer Object to receive data from the Auction class in AuctionService

        [BsonId] // Mongo id for a specific AuctionDTO
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("AuctionId")]
        public int AuctionId { get; set; }

        [BsonElement("AuctionEndDate")]
        public DateTime AuctionEndDate { get; set; }

        [BsonElement("AuctionId")]
        public int ArtifactID { get; set; }


        public AuctionDTO(int auctionId, DateTime auctionEndDate, int artifactID)
        {
            this.AuctionId = auctionId;
            this.AuctionEndDate = auctionEndDate;
            this.ArtifactID = artifactID;
        }


        public AuctionDTO()
		{

		}
	}
}

