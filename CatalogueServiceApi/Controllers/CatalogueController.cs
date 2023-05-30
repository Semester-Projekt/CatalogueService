// Usings
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
using System.Net.Http;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;


namespace Controllers;

[ApiController] // Api controller to handle api calls
[Route("[controller]")] // Controller name set as default http endpoint name
public class CatalogueController : ControllerBase
{
    // Creates 3 instances, 1 for a logger, one for a config, 1 for an instance of the userRepository.cs class
    private readonly ILogger<CatalogueController> _logger;
    private readonly IConfiguration _config;
    private CatalogueRepository _catalogueRepository;

    public CatalogueController(ILogger<CatalogueController> logger, IConfiguration config, CatalogueRepository catalogueRepository)
    {
        // Initializes the controllers constructor with the 3 specified private objects
        _logger = logger;
        _config = config;
        _catalogueRepository = catalogueRepository;

        _logger.LogInformation($"Connecting to rabbitMQ on {_config["rabbithostname"]}");

        // Logger host information
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddr = ips.First().MapToIPv4().ToString();
        _logger.LogInformation(1, $"UserService - Auth service responding from {_ipaddr}");
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
    public async Task<Dictionary<string, string>> GetVersion()
    {
        // Create a dictionary to hold the version properties
        var properties = new Dictionary<string, string>();

        // Get the assembly information of the program
        var assembly = typeof(Program).Assembly;

        // Add the service name to the properties dictionary
        properties.Add("service", "Catalogue");

        // Retrieve the product version from the assembly and add it to the properties dictionary
        var ver = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
        properties.Add("version", ver!);

        try
        {
            // Get the host name of the current machine
            var hostName = System.Net.Dns.GetHostName();

            // Get the IP addresses associated with the host name
            var ips = await System.Net.Dns.GetHostAddressesAsync(hostName);

            // Retrieve the first IPv4 address and add it to the properties dictionary
            var ipa = ips.First().MapToIPv4().ToString();
            properties.Add("hosted-at-address", ipa);
        }
        catch (Exception ex)
        {
            // Log and handle any exceptions that occurred during IP address retrieval
            _logger.LogError(ex.Message);

            // Add a default message to the properties dictionary if IP address resolution failed
            properties.Add("hosted-at-address", "Could not resolve IP address");
        }

        // Return the populated properties dictionary
        return properties;
    }






    //GET
    [HttpGet("getAllArtifacts"), DisableRequestSizeLimit] // GetAllArtifacts endpoint to retreive all Artifacts in the collection
    public IActionResult GetAllArtifacts()
    {
        _logger.LogInformation("CatalogueService - getAllArtifacts function hit");

        var artifacts = _catalogueRepository.GetAllArtifacts().Result; // Calls the method from the UserRepository

        _logger.LogInformation("CatalogueService - Total Artifacts: " + artifacts.Count());

        if (artifacts == null)
        {
            return BadRequest("CatalogueService - Artifact list is empty");
        }

        return Ok(artifacts);
    }

    [HttpGet("getArtifactById/{id}"), DisableRequestSizeLimit] // GetArtifact endpoint to retreive the specified Artifact
    public async Task<IActionResult> GetArtifactById(int id)
    {
        _logger.LogInformation("CatalogueService - getArtifactById function hit");

        var artifact = await _catalogueRepository.GetArtifactById(id);

        if (artifact == null)
        {
            return BadRequest($"CatalogueService - Artifact with id {id} does NOT exist"); // Checks validity of specified artifact
        }

        _logger.LogInformation("CatalogueService - Selected Artifact: " + artifact.ArtifactName);



        var filteredArtifact = new // Filters the information returned by the function
        {
            artifact.ArtifactName,
            artifact.ArtifactDescription,
            ArtifactOwner = new
            {
                UserName = artifact.ArtifactOwner!.UserName,
                UserEmail = artifact.ArtifactOwner.UserEmail,
                UserPhone = artifact.ArtifactOwner.UserPhone
            },
            artifact.ArtifactPicture
        };

        return Ok(filteredArtifact); // Returns the filtered Artifact
    }

    [HttpGet("getAllCategories"), DisableRequestSizeLimit] // Endpoint to retreive all categories
    public IActionResult GetAllCategories()
    {
        _logger.LogInformation("CatalogueService - getAllCategories function hit");

        var categories = _catalogueRepository.GetAllCategories().Result;

        _logger.LogInformation("CatalogueService - Total Categories: " + categories.Count());

        if (categories == null)
        {
            return BadRequest("CatalogueService - Category list is empty");
        }

        var filteredCategories = categories.Select(c => new Category
        {
            CategoryName = c.CategoryName,
            CategoryDescription = c.CategoryDescription
        });

        return Ok(filteredCategories); // Returns the filtered list of categories
    }

    [HttpGet("getCategoryByCode/{categoryCode}"), DisableRequestSizeLimit] // Endpoint retreive a specific Category and the related Artifacts
    public async Task<IActionResult> GetCategoryByCode(string categoryCode)
    {
        _logger.LogInformation("CatalogueService - getCategoryByCode function hit");

        var category = await _catalogueRepository.GetCategoryByCode(categoryCode); // Retreives the specified category

        if (category == null)
        {
            return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryCode);
        }

        _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

        var artifacts = await _catalogueRepository.GetAllArtifacts(); // Retreives allArtifacts
        var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryCode).ToList(); // Creates a new list of Artifacts that all have the specified categoryCode
        category.CategoryArtifacts = categoryArtifacts; // Populates the CategoryArtifacts attribute on Category.cs with the Artifacts that match the specified categoryCode

        var result = new // Creates a new result, which filters and selects specific attributes to return from both Artifact.cs and Category.cs
        {
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription,
            Artifacts = category.CategoryArtifacts.Select(a => new
            {
                ArtifactName = a.ArtifactName,
                ArtifactDescription = a.ArtifactDescription,
                ArtifactOwner = new
                {
                    UserName = a.ArtifactOwner!.UserName,
                    UserEmail = a.ArtifactOwner.UserEmail,
                    UserPhone = a.ArtifactOwner.UserPhone
                },
                Estimate = a.Estimate,
                ArtifactPicture = a.ArtifactPicture,
                Status = a.Status
            }).ToList()
        };

        return Ok(result); // Returns the newly created result
    }











    // SAHARA STANDISERET GetCategory ENDEPUNKT
    [HttpGet("categories/{categoryId}")]
    public async Task<IActionResult> GetCategories(string categoryId)
    {
        //()





        //()


        _logger.LogInformation("CatalogueService - SAHARA - getCategories function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            string auctionServiceUrl = Environment.GetEnvironmentVariable("AUCTION_SERVICE_URL")!; // Retreives url to AuctionService from docker-compose.yml file
            string getAuctionEndpoint = "/auction/getAllAuctions";

            _logger.LogInformation(auctionServiceUrl + getAuctionEndpoint);

            HttpResponseMessage response = await _httpClient.GetAsync(auctionServiceUrl + getAuctionEndpoint); // Makes http call to AuctionService
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve Auctions from AuctionService");
            }
            
            var auctionResponse = await response.Content.ReadFromJsonAsync<List<AuctionDTO>>(); // Deserializes the response from the AuctionService endpoint

            var categoryName = _catalogueRepository.GetCategoryByCode(categoryId).Result.CategoryName; // Specifies a categoryName for the result

            var category = await _catalogueRepository.GetCategoryByCode(categoryId); // Specifies a Category for the result

            if (category == null)
            {
                return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryId);
            }

            _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

            var artifacts = await _catalogueRepository.GetAllArtifacts(); // Retreives all Artifacts from _artifacts

            var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryId).ToList(); // Creates a new list of Artifacts that all have the specified categoryId
            category.CategoryArtifacts = categoryArtifacts; // Populates the CategoryArtifacts attribute on Category.cs with the Artifacts that match the specified categoryId

            var result = new // Creates a new result, to be returned with filters for both AuctionDTO and Artifact
            {
                Artifacts = category.CategoryArtifacts.Select(a => new
                {
                    a.CategoryCode,
                    CategoryName = categoryName,
                    ItemDescription = a.ArtifactDescription,
                    AuctionDate = auctionResponse!.Where(b => b.ArtifactID == a.ArtifactID).Select(c => c.AuctionEndDate)
                }).ToList()
            };

            return Ok(result);
        }
    }

    [HttpGet("getauctions/")]
    public async Task<IActionResult> GetAuctions()
    {
        _logger.LogInformation("CatalogueService - SAHARA - getAuctions function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            string auctionServiceUrl = Environment.GetEnvironmentVariable("AUCTION_SERVICE_URL")!; // Retreives url to AuctionService from docker-compose.yml file
            string getAuctionEndpoint = "/auction/getAllAuctions";

            _logger.LogInformation(auctionServiceUrl + getAuctionEndpoint);
            
            HttpResponseMessage response = await _httpClient.GetAsync(auctionServiceUrl + getAuctionEndpoint); // Makes http call to AuctionService
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve Auctions from AuctionService");
            }

            var auctionResponse = await response.Content.ReadFromJsonAsync<List<AuctionDTO>>(); // Deserializes the response from the AuctionService endpoint

            if (auctionResponse != null)
            {
                var allAuctions = auctionResponse.ToList();

                return Ok(allAuctions);
            }
            else
            {
                return BadRequest("Failed to retreive allAuctions");
            }
        }
    }















    [HttpGet("getUserFromUserService/{id}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(int id)
    {
        _logger.LogInformation("CatalogueService - GetUser function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            string userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")!; // Retreives URL to UserService from docker-compose.yml file
            string getUserEndpoint = "/user/getUser/" + id;

            _logger.LogInformation($"CatalogueService - {userServiceUrl + getUserEndpoint}");

            HttpResponseMessage response = await _httpClient.GetAsync(userServiceUrl + getUserEndpoint); // Calls the UserService endpoint
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve UserId from UserService");
            }

            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>(); // Deserializes the response from UserService

            if (userResponse != null) // Validates the result from the UserService endpoint call
            {
                _logger.LogInformation($"CatalogueService.GetUser - MongId: {userResponse.MongoId}");
                _logger.LogInformation($"CatalogueService.GetUser - UserName: {userResponse.UserName}");

                List<Artifact> usersArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(u => u.ArtifactOwner!.UserName == userResponse.UserName).ToList(); // creates a list of ArtifactDTOs in which the ArtifactOwner matches with the specified UserName

                userResponse.UsersArtifacts = usersArtifacts.Where(a => a.Status != "Deleted").ToList(); // Adds the matching artifacts to the UsersArtifacts attribute on the specified UserDTO

                var result = new // Creates a result with filters on both the UserDTO and the List<Artifact> attribute on the specified UserDTO
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
    [HttpPost("addNewArtifact/{userId}"), DisableRequestSizeLimit] // Endpoint for adding a new Artifact to _artifacts
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact, int? userId)
    {
        //()
        _logger.LogInformation("CatalogueService - addNewArtifact function hit");

        // Retreive the latest artifactID by retreiving all artifacts and then using .Max to see the highest in the list
        var allArtifacts = await _catalogueRepository.GetAllArtifacts();
        int? latestID = allArtifacts.DefaultIfEmpty().Max(a => a == null ? 0 : a.ArtifactID) + 1;

        var category = await _catalogueRepository.GetCategoryByCode(artifact!.CategoryCode!); // Retreives any Category that mathces with the categoryCode provided in the request body

        var userResponse = await GetUserFromUserService((int)userId); // Retreives the UserDTO from GetUserFromUserService to later add as ArtifactOwner

        ObjectResult objectResult = (ObjectResult)userResponse.Result;

        UserDTO artifactOwner = (UserDTO)objectResult.Value;


        _logger.LogInformation("CatalogueService - artifactOwner.UserName: " + artifactOwner.UserName);

        if (category == null)
        {
            return BadRequest("CatalogueService - Invalid Category code: " + artifact.CategoryCode);
        }

        var newArtifact = new Artifact // Creates new Artifact object
        {
            ArtifactID = (int)latestID,
            ArtifactName = artifact!.ArtifactName,
            ArtifactDescription = artifact.ArtifactDescription,
            CategoryCode = artifact.CategoryCode,
            ArtifactOwner = artifactOwner, // Sets the ArtifactOwner of the new Artifact object as the retreived UserDTO
            Estimate = artifact.Estimate,
        };

        if (newArtifact.ArtifactID == 0)
        {
            return BadRequest("CatalogueService - Invalid ID: " + newArtifact.ArtifactID);
        }
        else
        {
            await _catalogueRepository.AddNewArtifact(newArtifact); // Adds the new Artifact to _artifacts
        }
        _logger.LogInformation("CatalogueService - new Artifact object added to _artifacts");


        return Ok(newArtifact); // Returns the created result object




        //()



        /*
        _logger.LogInformation("CatalogueService - addNewArtifact function hit");

        // Retreive the latest artifactID by retreiving all artifacts and then using .Max to see the highest in the list
        var allArtifacts = await _catalogueRepository.GetAllArtifacts();
        int? latestID = allArtifacts.DefaultIfEmpty().Max(a => a == null ? 0 : a.ArtifactID) + 1;

        var category = await _catalogueRepository.GetCategoryByCode(artifact!.CategoryCode!); // Retreives any Category that mathces with the categoryCode provided in the request body

        var userResponse = await GetUserFromUserService(userId); // Retreives the UserDTO from GetUserFromUserService to later add as ArtifactOwner

        
        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO artifactOwner)
        {
            _logger.LogInformation("CatalogueService - ArtifactOwnerMongo: " + artifactOwner.MongoId);
            _logger.LogInformation("CatalogueService - ArtifactOwnerName: " + artifactOwner.UserName);

            if (category == null)
            {
                return BadRequest("CatalogueService - Invalid Category code: " + artifact.CategoryCode);
            }

            var newArtifact = new Artifact // Creates new Artifact object
            {
                ArtifactID = (int)latestID,
                ArtifactName = artifact!.ArtifactName,
                ArtifactDescription = artifact.ArtifactDescription,
                CategoryCode = artifact.CategoryCode,
                ArtifactOwner = artifactOwner, // Sets the ArtifactOwner of the new Artifact object as the retreived UserDTO
                Estimate = artifact.Estimate,
            };
            _logger.LogInformation("CatalogueService - new Artifact object made. ArtifactID: " + newArtifact.ArtifactID);


            if (newArtifact.ArtifactID == 0)
            {
                return BadRequest("CatalogueService - Invalid ID: " + newArtifact.ArtifactID);
            }
            else
            {
                await _catalogueRepository.AddNewArtifact(newArtifact); // Adds the new Artifact to _artifacts
            }
            _logger.LogInformation("CatalogueService - new Artifact object added to _artifacts");


            var result = new // Creates a new result which is to be returned in case of succes on AddNewUser
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

            return Ok(result); // Returns the created result object
        }
        else
        {
            return BadRequest("CatalogueService - Failed to retrieve User object");
        }
        */
    }

    [HttpPost("addNewCategory"), DisableRequestSizeLimit] // Endpoint for adding a new Category to _categories
    public IActionResult AddNewCategory([FromBody] Category? category)
    {
        _logger.LogInformation("CatalogueService - addNewCategory function hit");

        var newCategory = new Category // Creates the new category
        {
            CategoryCode = category!.CategoryCode,
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription
        };
        _logger.LogInformation("CatalogueService - new Category object made. CategoryCode: " + newCategory.CategoryCode);

        var allCategories = _catalogueRepository.GetAllCategories().Result; // Retreives all current categories in _categories


        // Checks whether the CategoryCode and CategoryName provided in the request body already exist in _categories
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
        else if (existingCategory.CategoryCode != null) // != Here means that a Category with both the same CategoryCode AND CategoryName, already exists
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
    [HttpPut("updateArtifact/{id}"), DisableRequestSizeLimit] // Endpoint for updating an Artifact in _artifacts
    public async Task<IActionResult> UpdateArtifact(int id, [FromBody] Artifact artifact)
    {
        _logger.LogInformation("CatalogueService - UpdateArtifact function hit");

        var updatedArtifact = _catalogueRepository.GetArtifactById(id); // Retreives the specified Artifact

        if (updatedArtifact == null) // Validates the specified Artifacts
        {
            return BadRequest("CatalogueService - Artifact does not exist");
        }
        _logger.LogInformation("CatalogueService - Artifact for update: " + updatedArtifact.Result.ArtifactName);

        await _catalogueRepository.UpdateArtifact(id, artifact!); // Updates the Artifact with the matching ID with the new info from the request body

        var newUpdatedArtifact = _catalogueRepository.GetArtifactById(id);

        return Ok($"CatalogueService - Artifact, {updatedArtifact.Result.ArtifactName}, has been updated"); // Returns the newUpdatedArtifact to see the updated info
    }

    [HttpPut("updateCategory/{categoryCode}"), DisableRequestSizeLimit] // UpdateCategory endpoint to update Category in _categories
    public async Task<IActionResult> UpdateCategory(string categoryCode, [FromBody] Category? category)
    {
        _logger.LogInformation("CatalogueService - UpdateCategory function hit");

        var updatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode); // Retreives specified Category

        if (updatedCategory == null)
        {
            return BadRequest("CatalogueService - Category does not exist");
        }
        _logger.LogInformation("CatalogueService - Category for update: " + updatedCategory.Result.CategoryName);

        await _catalogueRepository.UpdateCategory(categoryCode, category!); // Updates the specified Category with matching CategoryCode with new info from the request body

        var newUpdatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode); // Creates a new variable containing the newly updated Category

        return Ok($"CatalogueService - CategoryDescription updated. New description for category {updatedCategory.Result.CategoryName}: {newUpdatedCategory.Result.CategoryDescription}");
    }

    [HttpPut("updatePicture/{artifactID}"), DisableRequestSizeLimit] // UpdatePicture endpoint to update the ArtifactPicture attribute on a specified Artifact
    public async Task<IActionResult> UpdatePicture(int artifactID)
    {
        _logger.LogInformation("CatalogueService - UpdatePicture function hit");

        // Retrieve the specified Artifact by its ID
        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        // Check if the artifact exists
        if (artifact == null)
        {
            return BadRequest("CatalogueService - Artifact not found");
        }

        // Retrieve the image file from the request
        var formFile = Request.Form.Files.FirstOrDefault();

        // Check if an image file was uploaded
        if (formFile == null || formFile.Length == 0)
        {
            return BadRequest("CatalogueService - No image file uploaded");
        }

        byte[] imageData;

        // Convert the image file to a byte array
        using (var memoryStream = new MemoryStream())
        {
            await formFile.CopyToAsync(memoryStream);
            imageData = memoryStream.ToArray();
        }

        // Assign the image data to the ArtifactPicture attribute of the artifact
        artifact.ArtifactPicture = imageData;

        // Update the picture in the repository
        await _catalogueRepository.UpdatePicture(artifactID, formFile);

        // Return the updated picture as a file response assuming it's in JPEG format
        return File(artifact.ArtifactPicture, "image/jpeg");
    }


    [HttpGet("getPicture/{artifactID}")] // GetPicture endpoint to be used to check if ArtifactPicture was successfully updated
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

    [HttpPut("activateArtifact/{id}"), DisableRequestSizeLimit] // ActivateArtifact endpoint to change Status of Artifact to "Active" - used for putting artifact on auction
    public async Task<string> ActivateArtifact(int id)
    {
        _logger.LogInformation("CatalogueService - activateArtifact function hit");

        var activatedArtifact = await GetArtifactById(id); // Retreives the specified Artifact

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
    [HttpPut("deleteArtifact/{id}"), DisableRequestSizeLimit] // DeleteArtifact endpoint to change Status of Artifact to "Deleted"
    public async Task<string> DeleteArtifact(int id)
    {
        _logger.LogInformation("CatalogueService - deleteArtifact function hit");

        var deletedArtifact = await GetArtifactById(id); // Retreives the specified Artifact

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

    [HttpDelete("deleteCategory/{categoryCode}"), DisableRequestSizeLimit] // DeleteCategory endpoint to delete a Category from _categories
    public async Task<IActionResult> DeleteCategory(string categoryCode)
    {
        _logger.LogInformation("CatalogueService - deleteCategory function hit");

        var deletedCategory = await GetCategoryByCode(categoryCode); // Retreives the specified Category

        var categoryArtifacts = new List<Artifact>(); // Initializes a new list of Artifacts intended to filter Artifacts with matching CategoryCode

        List<Artifact> allArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(a => a.Status != "Deleted").ToList(); // retreives all Artifacts whose status does NOT equal "Deleted"

        for (int i = 0; i < allArtifacts.Count(); i++) // Loops through all Artifacts and adds any that match with the specified CategoryCode to the categoryArtifacts List
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
        else if (categoryArtifacts.Count() > 0) // Checks whether the specified Category contains any Artifacts
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