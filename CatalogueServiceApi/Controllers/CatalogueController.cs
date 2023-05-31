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
using System.Net.Http.Headers;

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

        var artifacts = _catalogueRepository.GetAllArtifacts().Result; // Retreives a list of all artifacts

        _logger.LogInformation("CatalogueService - Total Artifacts: " + artifacts.Count());

        if (artifacts == null) // Validates the list of artifacts
        {
            return BadRequest("CatalogueService - Artifact list is empty");
        }

        return Ok(artifacts); // Returns the full list of artifacts
    }

    [Authorize]
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

        return Ok(artifact); // Returns the filtered Artifact
    }

    [Authorize]
    [HttpGet("getAllCategories"), DisableRequestSizeLimit] // Endpoint to retreive all categories
    public IActionResult GetAllCategories()
    {
        _logger.LogInformation("CatalogueService - getAllCategories function hit");

        var categories = _catalogueRepository.GetAllCategories().Result; // Retreives all categories

        _logger.LogInformation("CatalogueService - Total Categories: " + categories.Count());

        if (categories == null) // Validates the list of categories
        {
            return BadRequest("CatalogueService - Category list is empty");
        }

        // Filters out unnecessary attributes from each Category object in the categories list
        var result = new
        {
            Categories = categories.Select(a => new
            {
                a.CategoryName,
                a.CategoryDescription
            })
        };
        
        return Ok(result); // Returns the filtered list of categories
    }

    [Authorize]
    [HttpGet("getCategoryByCode/{categoryCode}"), DisableRequestSizeLimit] // Endpoint retreive a specific Category and the related Artifacts
    public async Task<IActionResult> GetCategoryByCode(string categoryCode)
    {
        _logger.LogInformation("CatalogueService - getCategoryByCode function hit");

        var category = await _catalogueRepository.GetCategoryByCode(categoryCode); // Retreives the specified Category

        if (category == null) // Validates specified Category
        {
            return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryCode); // Return BadRequest in case of invalid CategoryCode
        }

        _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

        var artifacts = await _catalogueRepository.GetAllArtifacts(); // Retreives allArtifacts
        var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryCode && a.Status != "Deleted").ToList(); // Creates a new list of Artifacts that all have the specified categoryCode
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


    [Authorize]
    [HttpGet("getauctions")]
    public virtual async Task<ActionResult<List<AuctionDTO>>> GetAuctionsFromAuctionService()
    {
        _logger.LogInformation("CatalogueService - SAHARA - getAuctions function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            // Retrieve the AuctionService URL and endpoint from environment variables
            string auctionServiceUrl = Environment.GetEnvironmentVariable("AUCTION_SERVICE_URL")!;
            string getAuctionEndpoint = "/auction/getAllAuctions";

            _logger.LogInformation(auctionServiceUrl + getAuctionEndpoint);

            // Retrieve the current user's token from the request
            var tokenValue = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("CatalogueService - token first default: " + tokenValue);
            var token = tokenValue?.Replace("Bearer ", "");
            _logger.LogInformation("CatalogueService - token w/o bearer: " + token);

            // Create a new HttpRequestMessage to include the token
            var request = new HttpRequestMessage(HttpMethod.Get, auctionServiceUrl + getAuctionEndpoint);
            //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Send the request to the AuctionService API to retrieve all auctions
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            // Check if the response is successful; if not, return an appropriate status code and error message
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "CatalogueService - Failed to retrieve Auctions from AuctionService");
            }

            var auctionResponse = await response.Content.ReadFromJsonAsync<List<AuctionDTO>>(); // Deserialize the response content into a List<AuctionDTO> object
            
            // Check if the deserialization was successful
            if (auctionResponse != null)
            {
                return Ok(auctionResponse); // Return the list of auctions
            }
            else
            {
                return BadRequest("Failed to retrieve allAuctions"); // Return a bad request status and error message if the auctions couldn't be retrieved
            }
        }
    }
    
    // SAHARA STANDISERET GetCategory ENDEPUNKT
    [HttpGet("categories/{categoryId}")]
    public async Task<IActionResult> GetCategory(string categoryId)
    {
        _logger.LogInformation("CatalogueService - SAHARA - getCategories function hit");
        
        var auctionResponse = await GetAuctionsFromAuctionService(); // Retrieve all auctions
        ObjectResult objectResult = (ObjectResult)auctionResponse.Result!; // Extract the ObjectResult from the auctionResponse
        List<AuctionDTO> auctions = (List<AuctionDTO>)objectResult.Value!; // Retrieve the list of auctions from the ObjectResult
        
        //_logger.LogInformation("CatalogueService - auctionresponse: " + auctionResponse.Value.ToList().FirstOrDefault());
        _logger.LogInformation("CatalogueService - objectresult" + objectResult.Value);
        _logger.LogInformation("CatalogueService - ");
        _logger.LogInformation("CatalogueService - ");

        //List<AuctionDTO> auctions = auctionResponse.Value;

        var categoryName = _catalogueRepository.GetCategoryByCode(categoryId).Result.CategoryName; // Retrieve the category name based on the category ID

        var category = await _catalogueRepository.GetCategoryByCode(categoryId); // Retrieve the category object based on the category ID

        // Check if the category exists
        if (category == null)
        {
            return BadRequest("CatalogueService - Invalid, Category does not exist: " + categoryId); // Return a BadRequest if the category does not exist
        }

        _logger.LogInformation("CatalogueService - Selected category: " + category.CategoryName);

        var artifacts = await _catalogueRepository.GetAllArtifacts(); // Retrieve all artifacts

        var categoryArtifacts = artifacts.Where(a => a.CategoryCode == categoryId && a.Status == "Active").ToList(); // Filter artifacts based on the category ID
        category.CategoryArtifacts = categoryArtifacts;

        // Prepare the result object with required artifact information
        var result = new
        {
            Artifacts = category.CategoryArtifacts.Select(a => new
            {
                a.CategoryCode,
                CategoryName = categoryName,
                ItemDescription = a.ArtifactDescription,
                AuctionDate = auctions.Where(b => b.ArtifactID == a.ArtifactID).Select(c => c.AuctionEndDate).FirstOrDefault(),
                AuctionId = auctions.Where(b => b.ArtifactID == a.ArtifactID).Select(c => c.ArtifactID).FirstOrDefault()
            }).ToList()
        };

        return Ok(result); // Return the result object containing artifact information
    }


    
    [Authorize]
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
    [Authorize]
    [HttpPost("addNewArtifact/{userId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact, int? userId)
    {
        _logger.LogInformation("CatalogueService - addNewArtifact function hit");

        // Get all existing artifacts and determine the latest ID
        var allArtifacts = await _catalogueRepository.GetAllArtifacts();
        int? latestID = allArtifacts.DefaultIfEmpty().Max(a => a == null ? 0 : a.ArtifactID) + 1;

        var category = await _catalogueRepository.GetCategoryByCode(artifact!.CategoryCode!); // Get the category of the artifact based on the provided category code

        var userResponse = await GetUserFromUserService((int)userId!); // Retrieve the user information from the user service
        ObjectResult objectResult = (ObjectResult)userResponse.Result!; // Extract the result from the user response as an ObjectResult
        UserDTO artifactOwner = (UserDTO)objectResult.Value!; // Get the UserDTO object from the value of the ObjectResult

        _logger.LogInformation("CatalogueService - artifactOwner.UserName: " + artifactOwner.UserName);

        if (artifactOwner.UserId == null)
        {
            return BadRequest("User not found");
        }

        // Check if the category is valid
        if (category == null)
        {
            return BadRequest("CatalogueService - Invalid Category code: " + artifact.CategoryCode);
        }

        // Create a new artifact object with the provided data
        var newArtifact = new Artifact
        {
            ArtifactID = (int)latestID!,
            ArtifactName = artifact!.ArtifactName,
            ArtifactDescription = artifact.ArtifactDescription,
            CategoryCode = artifact.CategoryCode,
            ArtifactOwner = artifactOwner,
            Estimate = artifact.Estimate,
        };

        // Check if the new artifact has a valid ID
        if (newArtifact.ArtifactID == 0)
        {
            return BadRequest("CatalogueService - Invalid ID: " + newArtifact.ArtifactID);
        }
        else
        {
            category.CategoryArtifacts!.Add(newArtifact);
            await _catalogueRepository.AddNewArtifact(newArtifact); // Add the new artifact to the repository
        }

        _logger.LogInformation("CatalogueService - new Artifact object added to _artifacts");

        var result = new
        {
            Status = "Artifact successfully added",
            ArtifactName = artifact.ArtifactName,
            ArtifactDescription = artifact.ArtifactDescription,
            Estimate = artifact.Estimate
        };


        return Ok(result); // Return the newly created artifact as the response
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
    [Authorize]
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


    [Authorize]
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

        var formFile = Request.Form.Files.FirstOrDefault(); // Retrieve the image file from the request

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

        artifact.ArtifactPicture = imageData; // Assign the image data to the ArtifactPicture attribute of the artifact

        await _catalogueRepository.UpdatePicture(artifactID, formFile); // Update the picture in the repository

        return File(artifact.ArtifactPicture, "image/jpeg"); // Return the updated picture as a file response assuming it's in JPEG format
    }

    [HttpGet("getPicture/{artifactID}")] // GetPicture endpoint to be used to check if ArtifactPicture was successfully updated
    public async Task<IActionResult> GetPicture(int artifactID)
    {
        _logger.LogInformation("CatalogueService - GetPicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID); // Retreives the specified artifact

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

        var allArtifacts = await _catalogueRepository.GetAllArtifacts();

        var activatedArtifact = await _catalogueRepository.GetArtifactById(id); // Retreives the specified Artifact

        _logger.LogInformation("CatalogueService - ID for activation: " + activatedArtifact);

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
    [Authorize]
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

        // Retreives all Artifacts whose status does NOT equal "Deleted"
        List<Artifact> allArtifacts = _catalogueRepository.GetAllArtifacts().Result.Where(a => a.Status != "Deleted").ToList();

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