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
using Microsoft.AspNetCore.Components.Web.Virtualization;


namespace Model
{
    public class CatalogueRepository
    {
        // Initializes the 2 collections in the Catalogue db
        private readonly IMongoCollection<Artifact> _artifacts;
        private readonly IMongoCollection<Category> _categories;


        public CatalogueRepository()
        {
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")!; // Mongo conn string env variable - retreived from docker-compose.yml
            var client = new MongoClient(connectionString); // Creates the mongo client
            var database = client.GetDatabase("Catalogue"); // Retreives db from mongo

            // Retreives collections from mongo
            _artifacts = database.GetCollection<Artifact>("Artifacts");
            _categories = database.GetCollection<Category>("Categories");
        }



        // GET
        public virtual async Task<List<Artifact>> GetAllArtifacts()
        {
            return await _artifacts.Aggregate().ToListAsync();
        }

        public virtual async Task<Artifact> GetArtifactById(int id)
        {
            // Create a filter to find the artifact with the specified ID
            var filter = Builders<Artifact>.Filter.Eq("ArtifactID", id);
            return await _artifacts.Find(filter).FirstOrDefaultAsync();
        }

        public virtual async Task<List<Category>> GetAllCategories()
        {
            return await _categories.Aggregate().ToListAsync();
        }

        public virtual async Task<Category> GetCategoryByCode(string code)
        {
            var filter = Builders<Category>.Filter.Eq("CategoryCode", code);
            return await _categories.Find(filter).FirstOrDefaultAsync();
        }




        

        // POST
        public virtual async Task AddNewArtifact(Artifact? artifact)
        {
            await Task.Run(() => _artifacts.InsertOne(artifact!));
        }

        public virtual void AddNewCategory(Category? category)
        {
            _categories.InsertOne(category!);
        }






        // PUT
        public virtual async Task UpdateArtifact(int id, Artifact? artifact)
        {
            // Create a filter to find the artifact with the specified ID
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);

            // Create an update to set the properties of the artifact
            var update = Builders<Artifact>.Update
                .Set(a => a.ArtifactName, artifact!.ArtifactName) // Update the artifact's name
                .Set(a => a.ArtifactDescription, artifact.ArtifactDescription) // Update the artifact's description
                .Set(a => a.CategoryCode, artifact.CategoryCode) // Update the artifact's category code
                .Set(a => a.Estimate, artifact.Estimate); // Update the artifact's estimate

            // Update the artifact in the artifacts collection
            await _artifacts.UpdateOneAsync(filter, update);
        }

        public virtual async Task UpdateCategory(string categoryCode, Category category)
        {
            // Create a filter to find the category with the specified category code
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);

            // Create an update to set the category description
            var update = Builders<Category>.Update
                .Set(a => a.CategoryDescription, category.CategoryDescription);

            // Update the category in the categories collection
            await _categories.UpdateOneAsync(filter, update);
        }

        public virtual async Task<bool> UpdatePicture(int artifactID, IFormFile imageFile)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, artifactID);

            // Find the artifact with the given ID
            var artifact = await _artifacts.Find(filter).ToListAsync();

            // Check if the artifact exists
            if (artifact == null || !artifact.Any())
            {
                // Handle scenario where the artifact is not found
                return false;
            }

            // Get the first found artifact
            var foundArtifact = artifact.First();

            // Convert the image file to a byte array and assign it to the artifact's picture
            using (var memoryStream = new MemoryStream())
            {
                await imageFile.CopyToAsync(memoryStream);
                foundArtifact.ArtifactPicture = memoryStream.ToArray();
            }

            // Replace the existing artifact with the updated one
            await _artifacts.ReplaceOneAsync(filter, foundArtifact);

            // Picture update successful
            return true;
        }
        
        public virtual async Task ActivateArtifact(int id)
        {
            // Create a filter to find the artifact with the specified ID
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);

            // Create an update to set the status of the artifact to "Active"
            var update = Builders<Artifact>.Update
                .Set(a => a.Status, "Active");

            // Update the artifact's status to "Active" in the artifacts collection
            await _artifacts.UpdateOneAsync(filter, update);
        }





        
        // DELETE
        public virtual async Task DeleteArtifact(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);
            var update = Builders<Artifact>.Update
                .Set(a => a.Status, "Deleted");

            await _artifacts.UpdateOneAsync(filter, update);

        }

        public virtual async Task DeleteCategory(string categoryCode)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            await _categories.DeleteOneAsync(filter);
        }


    }
}