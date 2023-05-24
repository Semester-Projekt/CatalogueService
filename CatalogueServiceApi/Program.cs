using System.Text;
using Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using RabbitMQ.Client;
using System.Text.Json;
using Controllers;


/*
//RabbitMQ START
//Create connection (LOCALHOST I DET HER TILFÆLDE)
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

//Vælg hvilken queue det skal sendes til i rabbitmq management (queue: "NAVN")
channel.QueueDeclare(queue: "projekttest2",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);


var newArtifact = new Artifact();

//var message = JsonSerializer.SerializeToUtf8Bytes(newTaxaBooking); (newTaxaBooking skal laves om til den
// metode vi vil have sendt ind i rabbitmq
var message = JsonSerializer.SerializeToUtf8Bytes(newArtifact);
//Selve beskeden den sender ind i rabbitmq management
//var message = "projekttest1";
//var body = Encoding.UTF8.GetBytes(message);

//Routingkey = den queue lavet i QueueDeclare
channel.BasicPublish(exchange: string.Empty,
                     routingKey: "projekttest2",
                     basicProperties: null,
                     body: message);
Console.WriteLine($" [x] Sent {message}");

//Bekræfter at besked er sendt
Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();
Console.WriteLine("Sendt til rabbitmq");
//RabbitMQ SLUT

*/

//Indlæs NLog.config-konfigurationsfil
var logger =
NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try // try/catch/finally fra m10.01 opgave b step 4
{

    var builder = WebApplication.CreateBuilder(args);


    string mySecret = Environment.GetEnvironmentVariable("Secret") ?? "none";
    string myIssuer = Environment.GetEnvironmentVariable("Issuer") ?? "none";
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
            IssuerSigningKey =
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mySecret))
        };
    });

    // Add services to the container.
    builder.Services.AddSingleton<CatalogueRepository>();

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //Brug NLog som logger fremadrettet
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
    logger.Error(ex, "Stopped program because of exception");
    throw;
}

finally
{
    NLog.LogManager.Shutdown();
}