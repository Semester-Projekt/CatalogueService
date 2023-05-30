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


    // UNIT TEST AF SAHARA STANDADISERING ENDEPUNKT GetGategories






    // UNIT TEST AF AddNewArtifact
    [Test]
    public async Task VALID_TestAddNewArtifact()
    {
        // Arrange
        var category1 = new Category("TE", "TestCategoryName", "TestCategoryDescription");
        var user1 = new UserDTO(1, "TestUserName", "TestUserPassword", "TestUserEmail", 11111111, "TestUserAddress");
        var artifact1 = new Artifact(1, "ArtifactOneName", "ArtifactOneDescription", "TE", user1, 1000);

        var allCategories = new List<Category> { category1 };
        var allArtifacts = new List<Artifact> { artifact1 };

        var newArtifact = new Artifact(2, "NewArtifactName", "NewArtifactDescription", "TE", user1, 2000);

        // Mocks the CatalogueRepository and defines the desired behavior
        var mockRepo = new Mock<CatalogueRepository>();

        mockRepo.Setup(svc => svc.GetAllArtifacts()).ReturnsAsync(allArtifacts); // Returns the existing artifacts list
        mockRepo.Setup(svc => svc.GetCategoryByCode(newArtifact.CategoryCode)).ReturnsAsync(category1); // Returns the existing artifacts list
        mockRepo.Setup(svc => svc.AddNewArtifact(It.IsAny<Artifact>())).Returns(Task.FromResult<Artifact?>(newArtifact));

        // Initializes the controller with the necessary values from the CatalogueController constructor
        var controller = new CatalogueController(_logger, _configuration, mockRepo.Object);

        // Act
        Console.WriteLine("test - user id: " + user1.UserId);
        Console.WriteLine("test - newartifact owner user id: " + newArtifact.ArtifactOwner.UserId);
        var result = await controller.AddNewArtifact(newArtifact, newArtifact.ArtifactOwner.UserId); // Passes any integer value for the userId parameter

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That((result as OkObjectResult)?.Value, Is.TypeOf<Artifact>());
    }





}