using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver.Linq;
using System.IO;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Model
{
    public class CatalogueRepository
    {
        // initializes the 2 collections in the Catalogue db
        private readonly IMongoCollection<Artifact> _artifacts;
        private readonly IMongoCollection<Category> _categories;


        public CatalogueRepository()
        {
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); // mongo conn string env variable - retreived from docker-compose.yml
            var client = new MongoClient(connectionString); // creates the mongo client
            var database = client.GetDatabase("Catalogue"); // retreives db from mongo

            // retreives collections from mongo
            _artifacts = database.GetCollection<Artifact>("Artifacts");
            _categories = database.GetCollection<Category>("Categories");
        }



        //GET
        public async Task<List<Artifact>> GetAllArtifacts()
        {
            return await _artifacts.Aggregate().ToListAsync();
        }

        public async Task<Artifact> GetArtifactById(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq("ArtifactID", id);
            return await _artifacts.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<Category>> GetAllCategories()
        {
            return await _categories.Aggregate().ToListAsync();
        }

        public async Task<Category> GetCategoryByCode(string code)
        {
            var filter = Builders<Category>.Filter.Eq("CategoryCode", code);
            return await _categories.Find(filter).FirstOrDefaultAsync();
        }

        public int GetNextArtifactID() // method for retreiving the highest+1 artifactId in the collection
        {
            var lastArtifact = _artifacts.AsQueryable().OrderByDescending(a => a.ArtifactID).FirstOrDefault(); // retreives allArtifactss and orders them by artifactId in descending order
            return (lastArtifact != null) ? lastArtifact.ArtifactID + 1 : 1; // adds 1 to the current highest auctionId
        }






        //POST
        public void AddNewArtifact(Artifact? artifact)
        {
            _artifacts.InsertOne(artifact!);
        }

        public void AddNewCategory(Category? category)
        {
            _categories.InsertOne(category!);
        }






        //PUT
        public async Task UpdateArtifact(int id, Artifact? artifact)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);
            var update = Builders<Artifact>.Update.
                Set(a => a.ArtifactName, artifact.ArtifactName).
                Set(a => a.ArtifactDescription, artifact.ArtifactDescription).
                Set(a => a.CategoryCode, artifact.CategoryCode).
                Set(a => a.Estimate, artifact.Estimate);

            await _artifacts.UpdateOneAsync(filter, update);
        }

        public async Task UpdateCategory(string categoryCode, Category category)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            var update = Builders<Category>.Update.
                Set(a => a.CategoryDescription, category.CategoryDescription);

            await _categories.UpdateOneAsync(filter, update);
        }

        public async Task<bool> UpdatePicture(int artifactID, IFormFile imageFile)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, artifactID);

            var artifact = await _artifacts.Find(filter).ToListAsync();

            if (artifact == null || !artifact.Any())
            {
                // Handle scenario where the artifact is not found
                return false;
            }

            var foundArtifact = artifact.First();

            using (var memoryStream = new MemoryStream())
            {
                await imageFile.CopyToAsync(memoryStream);
                foundArtifact.ArtifactPicture = memoryStream.ToArray();
            }

            await _artifacts.ReplaceOneAsync(filter, foundArtifact);
            return true;
        }

        public async Task ActivateArtifact(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);
            var update = Builders<Artifact>.Update
                .Set(a => a.Status, "Active");

            await _artifacts.UpdateOneAsync(filter, update);

        }






        //DELETE
        public async Task DeleteArtifact(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);
            var update = Builders<Artifact>.Update
                .Set(a => a.Status, "Deleted");

            await _artifacts.UpdateOneAsync(filter, update);

        }

        public async Task DeleteCategory(string categoryCode)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            await _categories.DeleteOneAsync(filter);
        }


    }
}