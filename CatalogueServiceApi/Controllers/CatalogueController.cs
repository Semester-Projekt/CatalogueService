using Microsoft.AspNetCore.Mvc;
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

    }

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
    [HttpGet("getArtifactById/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetArtifactById(int id)
    {
        _logger.LogInformation("getArtifactById function hit");

        var artifact = await _catalogueRepository.GetArtifactById(id);

        _logger.LogInformation("Selected Artifact: " + artifact.ArtifactName);


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

        // evt noget filtrering på hvad andre brugere ser af data?
    }

    [Authorize]
    [HttpGet("getAllCategories"), DisableRequestSizeLimit]
    public IActionResult GetAllCategories()
    {
        _logger.LogInformation("getAllCategories function hit");

        var categories = _catalogueRepository.GetAllCategories().Result;

        _logger.LogInformation("Total Categories: " + categories.Count());

        if (categories == null)
        {
            return BadRequest("Category list is empty");
        }

        var filteredCategories = categories.Select(c => new
        {
            CategoryName = c.CategoryName,
            CategoryDescription = c.CategoryDescription
        });

        return Ok(filteredCategories);
    }

    [Authorize]
    [HttpGet("getCategoryByCode/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetCategoryByCode(string categoryCode)
    {
        _logger.LogInformation("getCategoryByCode function hit");

        var category = await _catalogueRepository.GetCategoryByCode(categoryCode);

        if (category == null)
        {
            return BadRequest("Invalid category does not exist: " + categoryCode);
        }

        _logger.LogInformation("Selected category: " + category.CategoryName);

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

    [Authorize]
    [HttpGet("getUserFromUserService/{id}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(int id)
    {
        _logger.LogInformation("CatalogueService - GetUser function hit");

        using (HttpClient client = new HttpClient())
        {
            string userServiceUrl = "http://localhost:5030";
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
                _logger.LogInformation($"MongId: {userResponse.MongoId}");
                _logger.LogInformation($"UserName: {userResponse.UserName}");

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
                return BadRequest("Failed to retrieve User object");
            }
        }
    }








    //POST
    [Authorize]
    [HttpPost("addNewArtifact/{userId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact, int userId)
    {
        _logger.LogInformation("addNewArtifact function hit");

        int latestID = _catalogueRepository.GetNextArtifactID(); // Gets latest ID in _artifacts + 1

        var category = await _catalogueRepository.GetCategoryByCode(artifact.CategoryCode);

        var userResponse = await GetUserFromUserService(userId); // Use await to get the User object

        _logger.LogInformation("ArtifactOwnerID: " + userId);

        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO artifactOwner)
        {
            _logger.LogInformation("ArtifactOwnerMongo: " + artifactOwner.MongoId);
            _logger.LogInformation("ArtifactOwnerName: " + artifactOwner.UserName);

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
            _logger.LogInformation("new Artifact object made. ArtifactID: " + newArtifact.ArtifactID);


            if (newArtifact.ArtifactID == 0)
            {
                return BadRequest("Invalid ID: " + newArtifact.ArtifactID);
            }
            else
            {
                _catalogueRepository.AddNewArtifact(newArtifact);
            }
            _logger.LogInformation("new Artifact object added to _artifacts");


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



            return Ok(result);
        }
        else
        {
            return BadRequest("Failed to retrieve User object");
        }
    }


    [Authorize]
    [HttpPost("addNewCategory"), DisableRequestSizeLimit]
    public IActionResult AddNewCategory([FromBody] Category? category)
    {
        _logger.LogInformation("addNewCategory function hit");

        var newCategory = new Category
        {
            CategoryCode = category!.CategoryCode,
            CategoryName = category.CategoryName,
            CategoryDescription = category.CategoryDescription
        };
        _logger.LogInformation("new Category object made. CategoryCode: " + newCategory.CategoryCode);

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
            return BadRequest("Invalid Code: " + newCategory.CategoryCode);
        }
        else if (existingCategory.CategoryCode != null)
        {
            _logger.LogInformation("Existing CategoryCode: " + existingCategory.CategoryCode);
            return BadRequest("Category already exists: " + existingCategory.CategoryCode);
        }
        else
        {
            _catalogueRepository.AddNewCategory(newCategory);
        }
        _logger.LogInformation("new Category object added to _artifacts");

        return Ok(newCategory);
    }






    //PUT
    [Authorize]
    [HttpPut("updateArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateArtifact(int id, [FromBody] Artifact artifact)
    {
        _logger.LogInformation("UpdateArtifact function hit");

        var updatedArtifact = _catalogueRepository.GetArtifactById(id);

        if (updatedArtifact == null)
        {
            return BadRequest("Artifact does not exist");
        }
        _logger.LogInformation("Artifact for update: " + updatedArtifact.Result.ArtifactName);

        await _catalogueRepository.UpdateArtifact(id, artifact!);

        var newUpdatedArtifact = _catalogueRepository.GetArtifactById(id);

        return Ok($"Artifact, {updatedArtifact.Result.ArtifactName}, has been updated");
    }

    [Authorize]
    [HttpPut("updateCategory/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateCategory(string categoryCode, [FromBody] Category? category)
    {
        _logger.LogInformation("UpdateCategory function hit");

        var updatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode);

        if (updatedCategory == null)
        {
            return BadRequest("Category does not exist");
        }
        _logger.LogInformation("Category for update: " + updatedCategory.Result.CategoryName);

        await _catalogueRepository.UpdateCategory(categoryCode, category!);

        var newUpdatedCategory = _catalogueRepository.GetCategoryByCode(categoryCode);

        return Ok($"CategoryDescription updated. New description for category {updatedCategory.Result.CategoryName}: {newUpdatedCategory.Result.CategoryDescription}");
    }

    [Authorize]
    [HttpPut("updatePicture/{artifactID}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdatePicture(int artifactID)
    {
        _logger.LogInformation("UpdatePicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        if (artifact == null)
        {
            return BadRequest("Artifact not found");
        }

        var formFile = Request.Form.Files.FirstOrDefault();

        if (formFile == null || formFile.Length == 0)
        {
            return BadRequest("No image file uploaded");
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

    [Authorize]
    [HttpGet("getPicture/{artifactID}")]
    public async Task<IActionResult> GetPicture(int artifactID)
    {
        _logger.LogInformation("GetPicture function hit");

        var artifact = await _catalogueRepository.GetArtifactById(artifactID);

        if (artifact == null)
        {
            return BadRequest("Artifact not found");
        }

        if (artifact.ArtifactPicture == null)
        {
            return BadRequest("No picture available for the artifact");
        }

        return File(artifact.ArtifactPicture, "image/jpeg"); // Assuming the picture is in JPEG format
    }







    //DELETE
    [Authorize]
    [HttpPut("deleteArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<string> DeleteArtifact(int id)
    {
        _logger.LogInformation("deleteArtifact function hit");

        var deletedArtifact = await GetArtifactById(id);
        _logger.LogInformation("ID for deletion: " + deletedArtifact);

        if (deletedArtifact == null)
        {
            return "ArtifactID is null";
        }
        else
        {
            await _catalogueRepository.DeleteArtifact(id);
        }


        return "Artifact status changed to 'Deleted'";
    }

    [Authorize]
    [HttpDelete("deleteCategory/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteCategory(string categoryCode)
    {
        _logger.LogInformation("deleteCategory function hit");

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
        _logger.LogInformation("This category contains this many artifacts: " + categoryArtifacts.Count());
        //sikrer at man ikke sletter en kategori der inderholder Artifacts

        if (deletedCategory == null)
        {
            return BadRequest("CategoryCode is null");
        }
        else if (categoryArtifacts.Count() > 0)
        {
            _logger.LogInformation("Cannot delete category containing Artifacts");
            return BadRequest($"Cannot delete category containing Artifacts. There are still {categoryArtifacts.Count()} Artifacts in the category");
        }
        else
        {
            await _catalogueRepository.DeleteCategory(categoryCode);
            _logger.LogInformation($"Category deleted");
        }


        return (IActionResult)Ok(GetAllCategories()).Value!;
    }
    
}
