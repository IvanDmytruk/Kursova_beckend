using Beckend.Repositories;
using Beckend.Services;
using Microsoft.OpenApi;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

//SERVICES 

// Controllers
builder.Services.AddControllers();

// MongoDB Configuration
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
if (string.IsNullOrEmpty(mongoConnectionString))
{
    throw new InvalidOperationException("MongoDB connection string is not configured");
}

builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase("CreateadTournament");
});

// Репозиторії
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<TournamentRepository>();
builder.Services.AddScoped<TeamRepository>();
builder.Services.AddScoped<MatchRepository>();
builder.Services.AddScoped<StatisticRepository>();
builder.Services.AddScoped<SportRepository>();

// Сервіси
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TournamentService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<StatisticService>();
builder.Services.AddScoped<SportService>();

//SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tournament Management API",
        Version = "v1",
        Description = "API for managing tournaments, teams, and users"
    });
});

//APP BUILD
var app = builder.Build();

// MIDDLEWARE 

app.UseHttpsRedirection();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tournament Management API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();