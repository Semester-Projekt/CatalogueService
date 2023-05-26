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

[ApiController]
[Route("[controller]")]
public class CatalogueController : ControllerBase
{
    private readonly ILogger<CatalogueController> _logger;
    private readonly IConfiguration _config;
    private CatalogueRepository _catalogueRepository;


    public CatalogueController(ILogger<CatalogueController> logger, IConfiguration config, CatalogueRepository catalogueRepository)
    {
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



    //VERSION_ENDEPUNKT
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
    [HttpGet("getAllArtifacts"), DisableRequestSizeLimit]
    public IActionResult GetAllArtifacts()
    {
        _logger.LogInformation("CatalogueController - getAllArtifacts function hit");

        var artifacts = _catalogueRepository.GetAllArtifacts().Result;

        _logger.LogInformation("CatalogueController - Total Artifacts: " + artifacts.Count());

        if (artifacts == null)
        {
            return BadRequest("catalogueService - Artifact list is empty");
        }

        return Ok(artifacts);
    }

    [HttpGet("getArtifactById/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetArtifactById(int id)
    {
        _logger.LogInformation("catalogueService - getArtifactById function hit");

        var artifact = await _catalogueRepository.GetArtifactById(id);

        _logger.LogInformation("catalogueService - Selected Artifact: " + artifact.ArtifactName);


        var filteredArtifact = new
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

        return Ok(filteredArtifact);
    }

    [HttpGet("getAllCategories"), DisableRequestSizeLimit]
    public IActionResult GetAllCategories()
    {
        _logger.LogInformation("catalogueService - getAllCategories function hit");

        var categories = _catalogueRepository.GetAllCategories().Result;

        _logger.LogInformation("catalogueService - Total Categories: " + categories.Count());

        if (categories == null)
        {
            return BadRequest("catalogueService - Category list is empty");
        }

        var filteredCategories = categories.Select(c => new
        {
            CategoryName = c.CategoryName,
            CategoryDescription = c.CategoryDescription
        });

        return Ok(filteredCategories);
    }

    [HttpGet("getCategoryByCode/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetCategoryByCode(string categoryCode)
    {
        _logger.LogInformation("catalogueService - getCategoryByCode function hit");

        var category = await _catalogueRepository.GetCategoryByCode(categoryCode);

        if (category == null)
        {
            return BadRequest("catalogueService - Invalid category does not exist: " + categoryCode);
        }

        _logger.LogInformation("catalogueService - Selected category: " + category.CategoryName);

        var artifacts = await _catalogueRepository.GetAllArtifacts();
        var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryCode).ToList();
        category.CategoryArtifacts = categoryArtifacts;

        var result = new
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

        return Ok(result);
    }

    // SAHARA STANDISERET GetCategory ENDEPUNKT
    [HttpGet("categories/{categoryId}")]
    public async Task<IActionResult> GetCategories(string categoryId)
    {
        _logger.LogInformation("catalogueService - SAHARA - getCategories function hit");
        
        using (HttpClient client = new HttpClient())
        {
            //string auctionServiceUrl = "http://localhost:4000";
            //string auctionServiceUrl = "http://auction:80";
            string auctionServiceUrl = Environment.GetEnvironmentVariable("AUCTION_SERVICE_URL");
            string getAuctionEndpoint = "/auction/getAllAuctions/";

            _logger.LogInformation(auctionServiceUrl + getAuctionEndpoint);

            HttpResponseMessage response = await client.GetAsync($"catalogueService - {auctionServiceUrl + getAuctionEndpoint}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "catalogueService - Failed to retrieve Auctions from AuctionService");
            }

            var auctionResponse = await response.Content.ReadFromJsonAsync<List<AuctionDTO>>();

            var categoryName = _catalogueRepository.GetCategoryByCode(categoryId).Result.CategoryName;
            
            var category = await _catalogueRepository.GetCategoryByCode(categoryId);

            if (category == null)
            {
                return BadRequest("catalogueService - Invalid category does not exist: " + categoryId);
            }

            _logger.LogInformation("catalogueService - Selected category: " + category.CategoryName);

            var artifacts = await _catalogueRepository.GetAllArtifacts();
            var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryId).ToList();
            category.CategoryArtifacts = categoryArtifacts;
            
            var result = new
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
            string userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL");
            string getUserEndpoint = "/user/getUser/" + id;
            
            _logger.LogInformation(userServiceUrl + getUserEndpoint);

            HttpResponseMessage response = await client.GetAsync(userServiceUrl + getUserEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to retrieve UserId from UserService");
            }
            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>();

            if (userResponse != null)
            {
                _logger.LogInformation($"catalogueService - MongId: {userResponse.MongoId}");
                _logger.LogInformation($"catalogueService - UserName: {userResponse.UserName}");

                List<Artifact> usersArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(u => u.ArtifactOwner.UserName == userResponse.UserName).ToList();

                userResponse.UsersArtifacts = usersArtifacts.Where(a => a.Status != "Deleted").ToList();
                
                var result = new
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
                return BadRequest("catalogueService - Failed to retrieve User object");
            }
        }
    }







    //RabbitMQ på den her
    //POST
    [HttpPost("addNewArtifact/{userId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact, int userId)
    {
        _logger.LogInformation("catalogueService - addNewArtifact function hit");

        int latestID = _catalogueRepository.GetNextArtifactID(); // Gets latest ID in _artifacts + 1

        var category = await _catalogueRepository.GetCategoryByCode(artifact.CategoryCode);

        var userResponse = await GetUserFromUserService(userId); // Use await to get the User object
      
        _logger.LogInformation("catalogueService - ArtifactOwnerID: " + userId);

        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO artifactOwner)
        {
            _logger.LogInformation("catalogueService - ArtifactOwnerMongo: " + artifactOwner.MongoId);
            _logger.LogInformation("catalogueService - ArtifactOwnerName: " + artifactOwner.UserName);

            if (category == null)
            {
                return BadRequest("Invalid category code: " + artifact.CategoryCode);
            }

            var newArtifact = new Artifact
            {
                ArtifactID = latestID,
                ArtifactName = artifact!.ArtifactName,
                ArtifactDescription = artifact.ArtifactDescription,
                CategoryCode = artifact.CategoryCode,
                ArtifactOwner = artifactOwner, // Use the UserName property of the User object
                Estimate = artifact.Estimate,
            };
            _logger.LogInformation("catalogueService - new Artifact object made. ArtifactID: " + newArtifact.ArtifactID);


            if (newArtifact.ArtifactID == 0)
            {
                return BadRequest("catalogueService - Invalid ID: " + newArtifact.ArtifactID);
            }
            else
            {
                _catalogueRepository.AddNewArtifact(newArtifact);
            }
            _logger.LogInformation("catalogueService - new Artifact object added to _artifacts");


            var result = new
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

            return Ok(result);
        }
        else
        {
            return BadRequest("catalogueService - Failed to retrieve User object");
        }
    }

    [HttpPost("addNewCategory"), DisableRequestSizeLimit]
    public IActionResult AddNewCategory([FromBody] Category? category)
    {
        _logger.LogInformation("catalogueService - addNewCategory function hit");

        var newCategory = new Category
        {
            CategoryCode = category!.CategoryCode,
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription
        };
        _logger.LogInformation("catalogueService - new Category object made. CategoryCode: " + newCategory.CategoryCode);

        var allCategories = _catalogueRepository.GetAllCategories().Result;

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
            return BadRequest("catalogueService - Invalid Code: " + newCategory.CategoryCode);
        }
        else if (existingCategory.CategoryCode != null)
        {
            _logger.LogInformation("catalogueService - Existing CategoryCode: " + existingCategory.CategoryCode);
            return BadRequest("catalogueService - Category already exists: " + existingCategory.CategoryCode);
        }
        else
        {
            _catalogueRepository.AddNewCategory(newCategory);
        }
        _logger.LogInformation("catalogueService - new Category object added to _artifacts");

        return Ok(newCategory);
    }






    //PUT
    [HttpPut("updateArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateArtifact(int id, [FromBody] Artifact artifact)
    {
        _logger.LogInformation("catalogueService - UpdateArtifact function hit");

        var updatedArtifact = _catalogueRepository.GetArtifactById(id);

        if (updatedArtifact == null)
        {
            return BadRequest("catalogueService - Artifact does not exist");
        }
        _logger.LogInformation("catalogueService - Artifact for update: " + updatedArtifact.Result.ArtifactName);

        await _catalogueRepository.UpdateArtifact(id, artifact!);

        var newUpdatedArtifact = _catalogueRepository.GetArtifactById(id);

        return Ok($"catalogueService - Artifact, {updatedArtifact.Result.ArtifactName}, has been updated");
    }

    [HttpPut("updateCategory/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateCategory(string categoryCode, [FromBody] Category? category)
    {
        _logger.LogInformation("catalogueService - UpdateCategory function hit");

        var updatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode);

        if (updatedCategory == null)
        {
            return BadRequest("catalogueService - Category does not exist");
        }
        _logger.LogInformation("catalogueService - Category for update: " + updatedCategory.Result.CategoryName);

        await _catalogueRepository.UpdateCategory(categoryCode, category!);

        var newUpdatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode);

        return Ok($"catalogueService - CategoryDescription updated. New description for category {updatedCategory.Result.CategoryName}: {newUpdatedCategory.Result.CategoryDescription}");
    }

    [HttpPut("updatePicture/{artifactID}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdatePicture(int artifactID)
    {
        _logger.LogInformation("catalogueService - UpdatePicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        if (artifact == null)
        {
            return BadRequest("catalogueService - Artifact not found");
        }

        var formFile = Request.Form.Files.FirstOrDefault();

        if (formFile == null || formFile.Length == 0)
        {
            return BadRequest("catalogueService - No image file uploaded");
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

    [HttpGet("getPicture/{artifactID}")]
    public async Task<IActionResult> GetPicture(int artifactID)
    {
        _logger.LogInformation("catalogueService - GetPicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        if (artifact == null)
        {
            return BadRequest("catalogueService - Artifact not found");
        }

        if (artifact.ArtifactPicture == null)
        {
            return BadRequest("catalogueService - No picture available for the artifact");
        }

        return File(artifact.ArtifactPicture, "image/jpeg"); // Assuming the picture is in JPEG format
    }







    //DELETE
    [HttpPut("deleteArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<string> DeleteArtifact(int id)
    {
        _logger.LogInformation("catalogueService - deleteArtifact function hit");

        var deletedArtifact = await GetArtifactById(id);
        _logger.LogInformation("catalogueService - ID for deletion: " + deletedArtifact);

        if (deletedArtifact == null)
        {
            return "catalogueService - ArtifactID is null";
        }
        else
        {
            await _catalogueRepository.DeleteArtifact(id);
        }


        return "catalogueService - Artifact status changed to 'Deleted'";
    }

    [HttpDelete("deleteCategory/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteCategory(string categoryCode)
    {
        _logger.LogInformation("catalogueService - deleteCategory function hit");

        var deletedCategory = await GetCategoryByCode(categoryCode);

        //henter alle artifacts med categoryCode til deletion:
        var categoryArtifacts = new List<Artifact>();
        List<Artifact> allArtifacts = _catalogueRepository.GetAllArtifacts().Result;
        for (int i = 0; i < allArtifacts.Count(); i++)
        {
            if (allArtifacts[i].CategoryCode == categoryCode)
            {
                categoryArtifacts.Add(allArtifacts[i]);
            }
        }
        _logger.LogInformation("catalogueService - This category contains this many artifacts: " + categoryArtifacts.Count());
        //sikrer at man ikke sletter en kategori der inderholder Artifacts

        if (deletedCategory == null)
        {
            return BadRequest("catalogueService - CategoryCode is null");
        }
        else if (categoryArtifacts.Count() > 0)
        {
            _logger.LogInformation("catalogueService - Cannot delete category containing Artifacts");
            return BadRequest($"catalogueService - Cannot delete category containing Artifacts. There are still {categoryArtifacts.Count()} Artifacts in the category");
        }
        else
        {
            await _catalogueRepository.DeleteCategory(categoryCode);
            _logger.LogInformation($"catalogueService - Category deleted");
        }


        return (IActionResult)Ok(GetAllCategories()).Value!;
    }
    
}
