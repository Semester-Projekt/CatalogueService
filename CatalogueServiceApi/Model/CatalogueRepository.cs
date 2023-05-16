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
        private readonly IMongoCollection<User> _user;
        private readonly IMongoCollection<Category> _category;

        

        public CatalogueRepository()
        {
            string connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING"); // mongo conn string miljøvariabel
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("Auctionhouse");
            _artifact = database.GetCollection<Artifact>("Artifact");
            _user = database.GetCollection<User>("User");
            _category = database.GetCollection<Category>("Category");
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
            await _artifact.DeleteOneAsync(filter);
        }

        public async Task DeleteCategory(string categoryCode)
        {
            var filter = Builders<Category>.Filter.Eq(a => a.CategoryCode, categoryCode);
            await _category.DeleteOneAsync(filter);
        }




        //PUT
        public void UpdateArtifact(int id)
        {
            //Not yet implemented
        }

        public void UpdateCategory(string categoryCode)
        {
            //Not yet implemented
        }


    }
}
