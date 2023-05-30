using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text;
using Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using RabbitMQ.Client;
using System.Text.Json;
using Controllers;

// Initialize NLog logger
var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try
{
    // Create a new WebApplication instance
    var builder = WebApplication.CreateBuilder(args);

    // Retrieve secret and issuer from environment variables or use default values
    string mySecret = Environment.GetEnvironmentVariable("Secret") ?? "none";
    string myIssuer = Environment.GetEnvironmentVariable("Issuer") ?? "none";

    // Configure JWT bearer authentication with provided options
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = myIssuer,
                ValidAudience = "http://localhost",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mySecret))
            };
        });

    // Add CatalogueRepository as a singleton service
    builder.Services.AddSingleton<CatalogueRepository>();

    // Add controllers, Swagger, and API explorer services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Use NLog as the logger
    builder.Host.UseNLog();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseHttpsRedirection();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    // Log and handle any exceptions that occurred during program execution
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    // Shutdown NLog logger
    NLog.LogManager.Shutdown();
}
