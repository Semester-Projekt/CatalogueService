using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Controllers;
using Model;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client.Exceptions;
using Moq.Protected;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace CatalogueServiceApi.Test;

public class CatalogueControllerTests
{
    private ILogger<CatalogueController> _logger = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public void Setup()
    {
        // Mock ILogger for UserController
        _logger = new Mock<ILogger<CatalogueController>>().Object;

        // Mock IConfiguration using in-memory configuration values
        var myConfiguration = new Dictionary<string, string?>
    {
        {"Issuer", "megalangsuperdupertestSecret"},
        {"Secret", "megalangsuperdupertestIssuer"},
        {"AUCTION_SERVICE_URL", "http://localhost:4000" },
        {"AUTH_SERVICE_URL", "http://localhost:4000"},
        {"BID_SERVICE_URL", "http://localhost:4000"},
        {"USER_SERVICE_URL", "http://localhost:4000"}
    };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        // Print the configuration for debugging
        Console.WriteLine("Configuration values:");
        foreach (var config in _configuration.AsEnumerable())
        {
            Console.WriteLine($"{config.Key}: {config.Value}");
        }
    }






    // UNIT TEST AF GetCategoryByCode
    [Test]
    public async Task VALID_TestGetCategoryByCode()
    {
        // Arrange
        // Create test data
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var category2 = new Category("T2", "TestCategoryName2", "TestCategoryDescription2");
        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var user2 = new UserDTO(2, "TestUserName2", "TestUserPassword2", "TestUserEmail2", 22222222, "TestUserAddress2");
        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);
        var artifact2 = new Artifact(2, "ArtifactOneName2", "ArtifactOneDescription2", "TE", user2, 2000);

        // Create 2 lists for holding artifacts and categories
        var allCategories = new List<Category> { category1, category2 };
        var allArtifacts = new List<Artifact> { artifact1, artifact2 };

        var mockRepo = new Mock<CatalogueRepository>(); // Setup mockRepository

        // Setup mock behavior
        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts); // Returns the created artifacts list
        mockRepo.Setup(svc => svc.GetCategoryByCode(category1.CategoryCode!)).ReturnsAsync(category1);
        mockRepo.Setup(svc => svc.GetAllCategories()).ReturnsAsync(allCategories); // Returns the created categories list

        // Create the controller instance with the mock repository
        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object);

        // Act
        var result = await controller.GetCategoryByCode(category1.CategoryCode!); // Invoke the target method under test

        // Assert
        // Check the type of the result
        Assert.That(result, Is.TypeOf<OkObjectResult>()); // Checks whether the result is an instance of the OkObjectResult class.
        var okObjectResult = (OkObjectResult)result; // Casts the result object to the type OkObjectResult
        var resultValue = okObjectResult.Value; // <- Retrieves the value of the Value property from the okObjectResult object.
                                                // The value property contains the actual data returned by the action

        // Ensure the result value is not null
        Assert.That(resultValue, Is.Not.Null);

        // Extract the "Artifacts" property from the result value
        var resultProperties = resultValue.GetType().GetProperties();
        var artifactsProperty = resultProperties.FirstOrDefault(p => p.Name == "Artifacts");
        Assert.That(artifactsProperty, Is.Not.Null); // Asserts that the artifactProperty is not null and has been successfully extracted

        // Check the type and nullity of the extracted artifacts
        var artifacts = artifactsProperty.GetValue(resultValue);
        Assert.That(artifacts, Is.Not.Null);
        Assert.That(artifacts, Is.InstanceOf<IEnumerable<object>>()); // Validates whether the controller method returns the very specific object
    }




    /*
    // UNIT TEST AF SAHARA STANDADISERING ENDEPUNKT GetGategories
    [Test]
    public async Task VALID_TestSAHARAEndpoint()
    {
        // Arrange
        // Create test objects for category, user, and artifact
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var category2 = new Category("T2", "TestCategoryName2", "TestCategoryDescription2");
        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var user2 = new UserDTO(2, "TestUserName2", "TestUserPassword2", "TestUserEmail2", 22222222, "TestUserAddress2");
        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);
        var artifact2 = new Artifact(2, "ArtifactOneName2", "ArtifactOneDescription2", "T2", user2, 2000);
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IkRhbmllbCIsImV4cCI6MTY4ODExNDk0NCwiaXNzIjoibWVnYWxhbmdzdXBlcmR1cGVydGVzdElzc3VlciIsImF1ZCI6Imh0dHA6Ly9sb2NhbGhvc3QifQ.6t_GQOVA9f8LDsz - GkKDARhtNXJ52MZC2xSm2Z_XKSE";

        // Create collections of categories and artifacts
        var allCategories = new List<Category> { category1, category2 };
        var allArtifacts = new List<Artifact> { artifact1, artifact2 };

        // Create a mock repository and set up its behavior
        var mockRepo = new Mock<CatalogueRepository>();
        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts);
        mockRepo.Setup(svc => svc.GetCategoryByCode(category1.CategoryCode!)).ReturnsAsync(category1);
        mockRepo.Setup(svc => svc.GetAllCategories()).ReturnsAsync(allCategories);

        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object); // Create a controller instance with the mock repository

        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = "Bearer " + token;

        // Act
        var result = await controller.GetCategory(category1.CategoryCode!); // Call the endpoint method to get a specific category

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>()); // Verify that the result is of the expected type
    }
    */



    




    // UNIT TEST AF AddNewArtifact
    [Test]
    public async Task VALID_TestAddNewArtifact()
    {
        // Arrange
        // Create test objects for category, user, and existing artifact
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var category2 = new Category("T2", "TestCategoryName2", "TestCategoryDescription2");
        var allCategories = new List<Category> { category1, category2 };

        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var user2 = new UserDTO(2, "TestUserName2", "TestUserPassword2", "TestUserEmail2", 22222222, "TestUserAddress2");
        var allUsers = new List<UserDTO> { user1, user2 };

        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);
        var newArtifact = new Artifact(2, "ArtifactOneName2", "ArtifactOneDescription2", "T2", user2, 2000);
        var allArtifacts = new List<Artifact> { artifact1 };

        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IkRhbmllbCIsImV4cCI6MTY4ODExNDk0NCwiaXNzIjoibWVnYWxhbmdzdXBlcmR1cGVydGVzdElzc3VlciIsImF1ZCI6Imh0dHA6Ly9sb2NhbGhvc3QifQ.6t_GQOVA9f8LDsz - GkKDARhtNXJ52MZC2xSm2Z_XKSE";
        
        // Create a mock repository and set up its behavior
        var mockRepo = new Mock<CatalogueRepository>();
        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts);
        mockRepo.Setup(svc => svc.GetCategoryByCode(newArtifact.CategoryCode!)).ReturnsAsync(category1);
        mockRepo.Setup(svc => svc.AddNewArtifact(It.IsAny<Artifact>())).Returns(Task.FromResult<Artifact?>(newArtifact));

        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object); // Create a controller instance with the mock repository

        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = "Bearer " + token;

        // Act
        var result = await controller.AddNewArtifact(newArtifact, user2.UserName); // Call the endpoint method to add a new artifact

        // Assert
        // Verify that the result is of the expected type and contains a new artifact
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That((result as OkObjectResult)?.Value, Is.TypeOf<Artifact>());
    }







    // UNIT TEST AF DeleteCategory
    [Test]
    public async Task VALID_TestDeleteCategory()
    {
        // Arrange
        // Create test objects for categories, users, and artifacts
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var category2 = new Category("T2", "TestCategoryName2", "TestCategoryDescription2");
        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var user2 = new UserDTO(2, "TestUserName2", "TestUserPassword2", "TestUserEmail2", 22222222, "TestUserAddress2");
        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);
        var artifact2 = new Artifact(2, "ArtifactOneName2", "ArtifactOneDescription2", "TE", user2, 2000);

        // Create collections of categories and artifacts
        var allCategories = new List<Category> { category1, category2 };
        var allArtifacts = new List<Artifact> { artifact1, artifact2 };

        // Create a mock repository and set up its behavior
        var mockRepo = new Mock<CatalogueRepository>();
        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts);
        mockRepo.Setup(svc => svc.GetCategoryByCode(category1.CategoryCode!)).ReturnsAsync(category1);
        mockRepo.Setup(svc => svc.GetAllCategories()).ReturnsAsync(allCategories);

        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object); // Create a controller instance with the mock repository

        // Act
        var result = await controller.DeleteCategory(category2.CategoryCode!); // Call the endpoint method to delete a category

        // Assert
        // Verify that the result is of the expected type and contains a list of categories
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(((result as OkObjectResult)?.Value as IEnumerable<Category>)!.ToList(), Is.TypeOf<List<Category>>());
    }


    [Test]
    public async Task INVALID_TestDeleteCategory_CategoryContainsArtifacts()
    {
        // Arrange
        // Create test objects for categories, users, and artifacts
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var category2 = new Category("T2", "TestCategoryName2", "TestCategoryDescription2");
        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var user2 = new UserDTO(2, "TestUserName2", "TestUserPassword2", "TestUserEmail2", 22222222, "TestUserAddress2");
        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);
        var artifact2 = new Artifact(2, "ArtifactOneName2", "ArtifactOneDescription2", "T2", user2, 2000);

        // Create collections of categories and artifacts
        var allCategories = new List<Category> { category1, category2 };
        var allArtifacts = new List<Artifact> { artifact1, artifact2 };

        // Create a mock repository and set up its behavior
        var mockRepo = new Mock<CatalogueRepository>();
        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts);
        mockRepo.Setup(svc => svc.GetCategoryByCode(category1.CategoryCode!)).ReturnsAsync(category1);
        mockRepo.Setup(svc => svc.GetAllCategories()).ReturnsAsync(allCategories);

        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object); // Create a controller instance with the mock repository

        // Act
        var result = await controller.DeleteCategory(category2.CategoryCode!); // Call the endpoint method to delete a category that contains artifacts

        // Assert
        // Verify that the result is of the expected type and contains a validation error message
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That((result as BadRequestObjectResult)?.Value, Is.TypeOf<string>());
    } 
}