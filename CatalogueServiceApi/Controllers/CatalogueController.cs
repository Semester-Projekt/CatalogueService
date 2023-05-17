﻿using Microsoft.AspNetCore.Mvc;
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
    [Authorize]
    [HttpGet("getArtifactById/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetArtifactById(int id)
    {
        _logger.LogInformation("getArtifactById function hit");

        var artifact = await _catalogueRepository.GetArtifactById(id);

        _logger.LogInformation("Selected Artifact: " + artifact.ArtifactName);

        return Ok(artifact);

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

        var filteredCategories = categories.Select(c => new {
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
            Artifacts = category.CategoryArtifacts.Select(a => new {
                ArtifactName = a.ArtifactName,
                ArtifactDescription = a.ArtifactDescription,
                ArtifactOwner = a.ArtifactOwner,
                Estimate = a.Estimate
            }).ToList()
        };

        return Ok(result);
    }




    //POST
    [Authorize]
    [HttpPost("addNewArtifact"), DisableRequestSizeLimit]
    public async Task<IActionResult> AddNewArtifact([FromBody] Artifact? artifact)
    {
        _logger.LogInformation("addNewArtifact function hit");

        int latestID = _catalogueRepository.GetNextArtifactID(); // Gets latest ID in _artifacts + 1

        var category = await _catalogueRepository.GetCategoryByCode(artifact!.CategoryCode);

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
            ArtifactOwner = artifact.ArtifactOwner,
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


        return Ok(newArtifact);

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

        
        if (newCategory.CategoryCode == null)
        {
            return BadRequest("Invalid Code: " + newCategory.CategoryCode);
        }
        else
        {
            _catalogueRepository.AddNewCategory(newCategory);
        }
        _logger.LogInformation("new Category object added to _artifacts");


        return Ok(newCategory);

    }




    //DELETE
    /*
    [Authorize]
    [HttpDelete("deleteArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteArtifact(int id)
    {
        _logger.LogInformation("deleteArtifact function hit");

        
        var deletedArtifact = await GetArtifactById(id);
        _logger.LogInformation("ID for deletion: " + deletedArtifact);

        if (deletedArtifact == null)
        {
            return BadRequest("ArtifactID is null: ");
        }
        else
        {
            await _catalogueRepository.DeleteArtifact(id);
            _logger.LogInformation("Artifact deleted");
            return Ok(deletedArtifact);
        }
    }
    */

    [Authorize]
    [HttpPut("deleteArtifact/{id}"), DisableRequestSizeLimit]
    public async Task<string> DeleteArtifact(int id, [FromBody] Artifact artifact)
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
            await _catalogueRepository.DeleteArtifact(id, artifact);
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
        for (int i = 0; i < allArtifacts.Count; i++)
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




    //PUT
    [Authorize]
    [HttpPost("updateArtifact"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateArtifact(int id)
    {
        // not yet implemented
        return Ok();
    }

}
