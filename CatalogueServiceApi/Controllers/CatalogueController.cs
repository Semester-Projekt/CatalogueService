// usings
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Model;
using Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Channels;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Controllers;

[ApiController] // api controller to handle api calls
[Route("[controller]")] // controller name set as default http endpoint name
public class CatalogueController : ControllerBase
{
    // creates 3 instances, 1 for a logger, one for a config, 1 for an instance of the userRepository.cs class
    private readonly ILogger<CatalogueController> _logger;
    private readonly IConfiguration _config;
    private CatalogueRepository _catalogueRepository;


    public CatalogueController(ILogger<CatalogueController> logger, IConfiguration config, CatalogueRepository catalogueRepository)
    {
        // initializes the controllers constructor with the 3 specified private objects
        _logger = logger;
        _config = config;
        _catalogueRepository = catalogueRepository;
        _logger.LogInformation($"Connecting to rabbitMQ on {_config["rabbithostname"]}");


    }

    //RabbitMQ start
    //  private object PublishNewArtifactMessage(Artifact newArtifact, object result)
        private object PublishNewArtifactMessage(object result)
    {
        // Configure RabbitMQ connection settings
        var factory = new ConnectionFactory()
        {
            HostName = _config["rabbithostname"],  // Replace with your RabbitMQ container name or hostname
     //       UserName = "guest",     // Replace with your RabbitMQ username
     //       Password = "guest"      // Replace with your RabbitMQ password
        };


        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            // Declare a queue
            channel.QueueDeclare(queue: "test-artifact-queue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            // Convert newArtifact to a JSON string
            var json = JsonSerializer.Serialize(result);

            // Publish the message to the queue
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(exchange: "", routingKey: "test-artifact-queue", basicProperties: null, body: body);
        }

        // Return the result object
        return result;
    }
    //RabbitMQ slut



    // VERSION_ENDEPUNKT
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        var assembly = typeof(Program).Assembly;


        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

        var versionInfo = new
        {
            InformationalVersion = informationalVersion,
            Description = description
        };

        return Ok(versionInfo);
    }






    //GET
    [HttpGet("getAllArtifacts"), DisableRequestSizeLimit] // getAllArtifacts endpoint to retreive all Artifacts in the collection
    public IActionResult GetAllArtifacts()
    {
        _logger.LogInformation("CatalogueService - getAllArtifacts function hit");

        var artifacts = _catalogueRepository.GetAllArtifacts().Result;

        _logger.LogInformation("CatalogueService - Total Artifacts: " + artifacts.Count());

        if (artifacts == null)
        {
            return BadRequest("CatalogueService - Artifact list is empty");
        }

        return Ok(artifacts);
    }

    [HttpGet("getArtifactById/{id}"), DisableRequestSizeLimit] // getArtifact endpoint to retreive the specified Artifact
    public async Task<IActionResult> GetArtifactById(int id)
    {
        _logger.LogInformation("CatalogueService - getArtifactById function hit");

        var artifact = await _catalogueRepository.GetArtifactById(id);

        if (artifact == null)
        {
            return BadRequest($"CatalogueService - Artifact with id {id} does NOT exist"); // checks validity of specified artifact
        }
        
        _logger.LogInformation("CatalogueService - Selected Artifact: " + artifact.ArtifactName);



        var filteredArtifact = new // filters the information returned by the function
        {
            artifact.ArtifactName,
            artifact.ArtifactDescription,
            ArtifactOwner = new
            {
                UserName = artifact.ArtifactOwner.UserName,
                UserEmail = artifact.ArtifactOwner.UserEmail,
                UserPhone = artifact.ArtifactOwner.UserPhone
            },
            artifact.ArtifactPicture
        };

        return Ok(filteredArtifact); // returns the filtered Artifact
    }

    [HttpGet("getAllCategories"), DisableRequestSizeLimit] // endpoint to retreive all categories
    public IActionResult GetAllCategories()
    {
        _logger.LogInformation("CatalogueService - getAllCategories function hit");

        var categories = _catalogueRepository.GetAllCategories().Result;

        _logger.LogInformation("CatalogueService - Total Categories: " + categories.Count());

        if (categories == null)
        {
            return BadRequest("CatalogueService - Category list is empty");
        }

        var filteredCategories = categories.Select(c => new // filters the information returned by the function
        {
            CategoryName = c.CategoryName,
            CategoryDescription = c.CategoryDescription
        });

        return Ok(filteredCategories); // returns the filtered list of categories
    }

    [HttpGet("getCategoryByCode/{categoryCode}"), DisableRequestSizeLimit] // endpoint retreive a specific Category and the related Artifacts
    public async Task<IActionResult> GetCategoryByCode(string categoryCode)
    {
        _logger.LogInformation("CatalogueService - getCategoryByCode function hit");

        var category = await _catalogueRepository.GetCategoryByCode(categoryCode); // retreives the specified category

        if (category == null)
        {
            return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryCode);
        }

        _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

        var artifacts = await _catalogueRepository.GetAllArtifacts(); // retreives allArtifacts
        var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryCode).ToList(); // creates a new list of Artifacts that all have the specified categoryCode
        category.CategoryArtifacts = categoryArtifacts; // populates the CategoryArtifacts attribute on Category.cs with the Artifacts that match the specified categoryCode

        var result = new // creates a new result, which filters and selects specific attributes to return from both Artifact.cs and Category.cs
        {
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription,
            Artifacts = category.CategoryArtifacts.Select(a => new
            {
                ArtifactName = a.ArtifactName,
                ArtifactDescription = a.ArtifactDescription,
                ArtifactOwner = new
                {
                    UserName = a.ArtifactOwner.UserName,
                    UserEmail = a.ArtifactOwner.UserEmail,
                    UserPhone = a.ArtifactOwner.UserPhone
                },
                Estimate = a.Estimate,
                ArtifactPicture = a.ArtifactPicture,
                Status = a.Status
            }).ToList()
        };

        return Ok(result); // returns the newly created result
    }

    // SAHARA STANDISERET GetCategory ENDEPUNKT
    [HttpGet("categories/{categoryId}")]
    public async Task<IActionResult> GetCategories(string categoryId)
    {
        _logger.LogInformation("CatalogueService - SAHARA - getCategories function hit");
        
        using (HttpClient client = new HttpClient())
        {
            //string auctionServiceUrl = "http://localhost:4000";
            //string auctionServiceUrl = "http://auction:80";
            string auctionServiceUrl = Environment.GetEnvironmentVariable("AUCTION_SERVICE_URL"); // retreives url to AuctionService from docker-compose.yml file
            string getAuctionEndpoint = "/auction/getAllAuctions";

            _logger.LogInformation(auctionServiceUrl + getAuctionEndpoint);

            HttpResponseMessage response = await client.GetAsync(auctionServiceUrl + getAuctionEndpoint); // makes http call to AuctionService
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve Auctions from AuctionService");
            }

            var auctionResponse = await response.Content.ReadFromJsonAsync<List<AuctionDTO>>(); // deserializes the response from the AuctionService endpoint

            var categoryName = _catalogueRepository.GetCategoryByCode(categoryId).Result.CategoryName; // specifies a categoryName for the result

            var category = await _catalogueRepository.GetCategoryByCode(categoryId); // specifies a Category for the result

            if (category == null)
            {
                return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryId);
            }

            _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

            var artifacts = await _catalogueRepository.GetAllArtifacts(); // retreives all Artifacts from _artifacts

            var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryId).ToList(); // creates a new list of Artifacts that all have the specified categoryId
            category.CategoryArtifacts = categoryArtifacts; // populates the CategoryArtifacts attribute on Category.cs with the Artifacts that match the specified categoryId

            var result = new // creates a new result, to be returned with filters for both AuctionDTO and Artifact
            {
                Artifacts = category.CategoryArtifacts.Select(a => new
                {
                    a.CategoryCode,
                    CategoryName = categoryName,
                    ItemDescription = a.ArtifactDescription,
                    AuctionDate = auctionResponse.Where(b => b.ArtifactID == a.ArtifactID).Select(c => c.AuctionEndDate)
                }).ToList()
            };

            return Ok(result);
        }
    }

    [HttpGet("getUserFromUserService/{id}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(int id)
    {
        _logger.LogInformation("CatalogueService - GetUser function hit");

        using (HttpClient client = new HttpClient())
        {
            //string userServiceUrl = "http://localhost:5006";
            //string userServiceUrl = "http://user:80";
            string userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL"); // retreives URL to UserService from docker-compose.yml file
            string getUserEndpoint = "/user/getUser/" + id;
            
            _logger.LogInformation(userServiceUrl + getUserEndpoint);

            HttpResponseMessage response = await client.GetAsync(userServiceUrl + getUserEndpoint); // calls the UserService endpoint
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve UserId from UserService");
            }

            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>(); // deserializes the response from UserService

            if (userResponse != null) // validates the result from the UserService endpoint call
            {
                _logger.LogInformation($"CatalogueService - MongId: {userResponse.MongoId}");
                _logger.LogInformation($"CatalogueService - UserName: {userResponse.UserName}");

                List<Artifact> usersArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(u => u.ArtifactOwner.UserName == userResponse.UserName).ToList(); // creates a list of ArtifactDTOs in which the ArtifactOwner matches with the specified UserName

                userResponse.UsersArtifacts = usersArtifacts.Where(a => a.Status != "Deleted").ToList(); // adds the matching artifacts to the UsersArtifacts attribute on the specified UserDTO
                
                var result = new // creates a result with filters on both the UserDTO and the List<Artifact> attribute on the specified UserDTO
                {
                    UserName = userResponse.UserName,
                    UserEmail = userResponse.UserEmail,
                    UserPhone = userResponse.UserPhone,
                    UsersArtifacts = userResponse.UsersArtifacts.Select(a => new
                    {
                        ArtifactName = a.ArtifactName,
                        ArtifactDescription = a.ArtifactDescription,
                        CategoryCode = a.CategoryCode,
                        Estimate = a.Estimate,
                        ArtifactPicture = a.ArtifactPicture,
                        Status = a.Status
                    }).ToList()
                };
                return Ok(userResponse);
            }
            else
            {
                return BadRequest("CatalogueService - Failed to retrieve User object");
            }
        }
    }







    //RabbitMQ på den her
    //POST
    [HttpPost("addNewArtifact/{userId}"), DisableRequestSizeLimit] // endpoint for adding a new Artifact to _artifacts
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact, int userId)
    {
        _logger.LogInformation("CatalogueService - addNewArtifact function hit");

        int latestID = _catalogueRepository.GetNextArtifactID(); // Gets latest ID in _artifacts + 1

        var category = await _catalogueRepository.GetCategoryByCode(artifact.CategoryCode); // retreives any Category that mathces with the categoryCode provided in the request body

        var userResponse = await GetUserFromUserService(userId); // retreives the UserDTO from GetUserFromUserService to later add as ArtifactOwner
      
        _logger.LogInformation("CatalogueService - ArtifactOwnerID: " + userId);

        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO artifactOwner)
        {
            _logger.LogInformation("CatalogueService - ArtifactOwnerMongo: " + artifactOwner.MongoId);
            _logger.LogInformation("CatalogueService - ArtifactOwnerName: " + artifactOwner.UserName);

            if (category == null)
            {
                return BadRequest("CatalogueService - Invalid Category code: " + artifact.CategoryCode);
            }

            var newArtifact = new Artifact // creates new Artifact object
            {
                ArtifactID = latestID,
                ArtifactName = artifact!.ArtifactName,
                ArtifactDescription = artifact.ArtifactDescription,
                CategoryCode = artifact.CategoryCode,
                ArtifactOwner = artifactOwner, // sets the ArtifactOwner of the new Artifact object as the retreived UserDTO
                Estimate = artifact.Estimate,
            };
            _logger.LogInformation("CatalogueService - new Artifact object made. ArtifactID: " + newArtifact.ArtifactID);


            if (newArtifact.ArtifactID == 0)
            {
                return BadRequest("CatalogueService - Invalid ID: " + newArtifact.ArtifactID);
            }
            else
            {
                _catalogueRepository.AddNewArtifact(newArtifact); // adds the new Artifact to _artifacts
            }
            _logger.LogInformation("CatalogueService - new Artifact object added to _artifacts");


            var result = new // creates a new result which is to be returned in case of succes on AddNewUser
            {
                Artifactname = artifact.ArtifactName,
                ArtifactDescription = artifact.ArtifactDescription,
                CategoryCode = artifact.CategoryCode,
                ArtifactOwner = new
                {
                    artifactOwner.UserName,
                    artifactOwner.UserEmail,
                    artifactOwner.UserPhone
                },
                Estimate = artifact.Estimate
            };

            // Publish the new artifact message to RabbitMQ
            //PublishNewArtifactMessage(newArtifact, result);
            PublishNewArtifactMessage(result);

            return Ok(result); // returns the created result object
        }
        else
        {
            return BadRequest("CatalogueService - Failed to retrieve User object");
        }
    }

    [HttpPost("addNewCategory"), DisableRequestSizeLimit] // endpoint for adding a new Category to _categories
    public IActionResult AddNewCategory([FromBody] Category? category)
    {
        _logger.LogInformation("CatalogueService - addNewCategory function hit");

        var newCategory = new Category // creates the new category
        {
            CategoryCode = category!.CategoryCode,
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription
        };
        _logger.LogInformation("CatalogueService - new Category object made. CategoryCode: " + newCategory.CategoryCode);

        var allCategories = _catalogueRepository.GetAllCategories().Result; // retreives all current categories in _categories


        // checks whether the CategoryCode and CategoryName provided in the request body already exist in _categories
        var existingCategory = new Category();

        for (int i = 0; i < allCategories.Count(); i++)
        {
            if (allCategories[i].CategoryCode == newCategory.CategoryCode && allCategories[i].CategoryName == newCategory.CategoryName)
            {
                existingCategory = allCategories[i];
            }
        }



        if (newCategory.CategoryCode == null)
        {
            return BadRequest("CatalogueService - Invalid Code: " + newCategory.CategoryCode);
        }
        else if (existingCategory.CategoryCode != null) // != here means that a Category with both the same CategoryCode AND CategoryName, already exists
        {
            _logger.LogInformation("CatalogueService - Existing CategoryCode: " + existingCategory.CategoryCode);
            return BadRequest("CatalogueService - Category already exists: " + existingCategory.CategoryCode);
        }
        else
        {
            _catalogueRepository.AddNewCategory(newCategory);
        }
        _logger.LogInformation("CatalogueService - new Category object added to _artifacts");

        return Ok(newCategory);
    }



    


    //PUT
    [HttpPut("updateArtifact/{id}"), DisableRequestSizeLimit] // endpoint for updating an Artifact in _artifacts
    public async Task<IActionResult> UpdateArtifact(int id, [FromBody] Artifact artifact)
    {
        _logger.LogInformation("CatalogueService - UpdateArtifact function hit");

        var updatedArtifact = _catalogueRepository.GetArtifactById(id); // retreives the specified Artifact

        if (updatedArtifact == null) // validates the specified Artifacts
        {
            return BadRequest("CatalogueService - Artifact does not exist");
        }
        _logger.LogInformation("CatalogueService - Artifact for update: " + updatedArtifact.Result.ArtifactName);

        await _catalogueRepository.UpdateArtifact(id, artifact!); // updates the Artifact with the matching ID with the new info from the request body

        var newUpdatedArtifact = _catalogueRepository.GetArtifactById(id);

        return Ok($"CatalogueService - Artifact, {updatedArtifact.Result.ArtifactName}, has been updated"); // returns the newUpdatedArtifact to see the updated info
    }

    [HttpPut("updateCategory/{categoryCode}"), DisableRequestSizeLimit] // updateCategory endpoint to update Category in _categories
    public async Task<IActionResult> UpdateCategory(string categoryCode, [FromBody] Category? category)
    {
        _logger.LogInformation("CatalogueService - UpdateCategory function hit");

        var updatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode); // retreives specified Category

        if (updatedCategory == null)
        {
            return BadRequest("CatalogueService - Category does not exist");
        }
        _logger.LogInformation("CatalogueService - Category for update: " + updatedCategory.Result.CategoryName);

        await _catalogueRepository.UpdateCategory(categoryCode, category!); // updates the specified Category with matching CategoryCode with new info from the request body

        var newUpdatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode); // creates a new variable containing the newly updated Category

        return Ok($"CatalogueService - CategoryDescription updated. New description for category {updatedCategory.Result.CategoryName}: {newUpdatedCategory.Result.CategoryDescription}");
    }

    [HttpPut("updatePicture/{artifactID}"), DisableRequestSizeLimit] // updatePicture endpoint to update the ArtifactPicture attribute on a specified Artifact
    public async Task<IActionResult> UpdatePicture(int artifactID)
    {
        _logger.LogInformation("CatalogueService - UpdatePicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID); // retreives the specified Artifact

        if (artifact == null)
        {
            return BadRequest("CatalogueService - Artifact not found");
        }

        var formFile = Request.Form.Files.FirstOrDefault();

        if (formFile == null || formFile.Length == 0)
        {
            return BadRequest("CatalogueService - No image file uploaded");
        }

        byte[] imageData;

        using (var memoryStream = new MemoryStream())
        {
            await formFile.CopyToAsync(memoryStream);
            imageData = memoryStream.ToArray();
        }

        artifact.ArtifactPicture = imageData;

        await _catalogueRepository.UpdatePicture(artifactID, formFile);

        return File(artifact.ArtifactPicture, "image/jpeg"); // Assuming the picture is in JPEG format
    }

    [HttpGet("getPicture/{artifactID}")] // getPicture endpoint to be used to check if ArtifactPicture was successfully updated
    public async Task<IActionResult> GetPicture(int artifactID)
    {
        _logger.LogInformation("CatalogueService - GetPicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        if (artifact == null)
        {
            return BadRequest("CatalogueService - Artifact not found");
        }

        if (artifact.ArtifactPicture == null)
        {
            return BadRequest("CatalogueService - No picture available for the artifact");
        }

        return File(artifact.ArtifactPicture, "image/jpeg"); // Assuming the picture is in JPEG format
    }

    [HttpPut("activateArtifact/{id}"), DisableRequestSizeLimit] // activateArtifact endpoint to change Status of Artifact to "Active" - used for putting artifact on auction
    public async Task<string> ActivateArtifact(int id)
    {
        _logger.LogInformation("CatalogueService - activateArtifact function hit");

        var activatedArtifact = await GetArtifactById(id); // retreives the specified Artifact

        _logger.LogInformation("CatalogueService - ID for deletion: " + activatedArtifact);

        if (activatedArtifact == null)
        {
            return "CatalogueService - ArtifactID is null";
        }
        else
        {
            await _catalogueRepository.ActivateArtifact(id); // Calls the PUT method in UserRepository.cs which updates the Status attribute of the Artifact to "Active"
        }
        
        return "CatalogueService - Artifact status changed to 'Active'";
    }







    //DELETE
    [HttpPut("deleteArtifact/{id}"), DisableRequestSizeLimit] // deleteArtifact endpoint to change Status of Artifact to "Deleted"
    public async Task<string> DeleteArtifact(int id)
    {
        _logger.LogInformation("CatalogueService - deleteArtifact function hit");

        var deletedArtifact = await GetArtifactById(id); // retreives the specified Artifact

        _logger.LogInformation("CatalogueService - ID for deletion: " + deletedArtifact);

        if (deletedArtifact == null)
        {
            return "CatalogueService - ArtifactID is null";
        }
        else
        {
            await _catalogueRepository.DeleteArtifact(id); // Calls the PUT method in UserRepository.cs which updates the Status attribute of the Artifact to "Deleted"
        }


        return "CatalogueService - Artifact status changed to 'Deleted'";
    }

    [HttpDelete("deleteCategory/{categoryCode}"), DisableRequestSizeLimit] // deleteCategory endpoint to delete a Category from _categories
    public async Task<IActionResult> DeleteCategory(string categoryCode)
    {
        _logger.LogInformation("CatalogueService - deleteCategory function hit");

        var deletedCategory = await GetCategoryByCode(categoryCode); // retreives the specified Category
        
        var categoryArtifacts = new List<Artifact>(); // initializes a new list of Artifacts intended to filter Artifacts with matching CategoryCode

        List<Artifact> allArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(a => a.Status != "Deleted").ToList(); // retreives all Artifacts whose status does NOT equal "Deleted"

        for (int i = 0; i < allArtifacts.Count(); i++) // loops through all Artifacts and adds any that match with the specified CategoryCode to the categoryArtifacts List
        {
            if (allArtifacts[i].CategoryCode == categoryCode)
            {
                categoryArtifacts.Add(allArtifacts[i]);
            }
        }
        _logger.LogInformation("CatalogueService - This category contains this many artifacts: " + categoryArtifacts.Count());

        if (deletedCategory == null)
        {
            return BadRequest("CatalogueService - CategoryCode is null");
        }
        else if (categoryArtifacts.Count() > 0) // checks whether the specified Category contains any Artifacts
        {
            _logger.LogInformation("CatalogueService - Cannot delete category containing Artifacts");
            return BadRequest($"CatalogueService - Cannot delete category containing Artifacts. There are still {categoryArtifacts.Count()} Artifacts in the category");
        }
        else
        {
            await _catalogueRepository.DeleteCategory(categoryCode);
            _logger.LogInformation($"CatalogueService - Category deleted");
        }


        return (IActionResult)Ok(GetAllCategories()).Value!;
    }
    
}
