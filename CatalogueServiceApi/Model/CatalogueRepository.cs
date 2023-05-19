using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Model
{
    public class CatalogueRepository
    {
        private readonly IMongoCollection<Artifact> _artifact;
        private readonly IMongoCollection<Category> _category;


        public CatalogueRepository()
        {
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); // mongo conn string miljøvariabel
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("Catalogue");
            _artifact = database.GetCollection<Artifact>("Artifacts");
            _category = database.GetCollection<Category>("Categories");
        }
        
        

        //GET
        public async Task<List<Artifact>> GetAllArtifacts()
        {
            return await _artifact.Aggregate().ToListAsync();
        }

        public async Task<Artifact> GetArtifactById(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq("ArtifactID", id);
            return await _artifact.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<Category>> GetAllCategories()
        {
            return await _category.Aggregate().ToListAsync();
        }

        public async Task<Category> GetCategoryByCode(string code)
        {
            var filter = Builders<Category>.Filter.Eq("CategoryCode", code);
            return await _category.Find(filter).FirstOrDefaultAsync();
        }
        
        public int GetNextArtifactID()
        {
            var lastArtifact = _artifact.AsQueryable().OrderByDescending(a => a.ArtifactID).FirstOrDefault();
            return (lastArtifact != null) ? lastArtifact.ArtifactID + 1 : 1;
        }




        //POST
        public void AddNewArtifact(Artifact? artifact)
        {
            _artifact.InsertOne(artifact!);
        }

        public void AddNewCategory(Category? category)
        {
            _category.InsertOne(category!);
        }




        //DELETE
        public async Task DeleteArtifact(int id)
        {
            var filter = Builders<Artifact>.Filter.Eq(a => a.ArtifactID, id);
            var update = Builders<Artifact>.Update
                .Set(a => a.Status, "Deleted");

            await _artifact.UpdateOneAsync(filter, update);
            
        }




        public async Task DeleteCategory(string categoryCode)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            await _category.DeleteOneAsync(filter);
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

            await _artifact.UpdateOneAsync(filter, update);
        }

        public async Task UpdateCategory(string categoryCode, Category category)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            var update = Builders<Category>.Update.
                Set(a => a.CategoryDescription, category.CategoryDescription);

            await _category.UpdateOneAsync(filter, update);
        }


    }
}
