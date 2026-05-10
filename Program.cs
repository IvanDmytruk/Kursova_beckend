using Beckend.JWT;
using Beckend.Mappings;
using Beckend.Repositories;
using Beckend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Driver;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var url = $"http://0.0.0.0:{port}";
// CORS 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVercel", policy =>
    {
        policy.WithOrigins(
                "https://kursova-frontend.vercel.app",
                "http://localhost:5500"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(5036);
        options.ListenLocalhost(7171, listenOptions =>
        {
            listenOptions.UseHttps();
        });
    });
}
builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddControllers();
// MongoDB Connection
var mongoConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("MongoDB");
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
// JWT Settings
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["JwtSettings:SecretKey"];
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["JwtSettings:Issuer"];
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["JwtSettings:Audience"];
var jwtExpiryMinutes = int.Parse(Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_EXPIRY_MINUTES")
    ?? builder.Configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60");
var jwtRefreshExpiryDays = int.Parse(Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_DAYS")
    ?? builder.Configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7");
// Реєстрація JwtSettings
builder.Services.Configure<JwtSettings>(options =>
{
    options.SecretKey = jwtSecretKey;
    options.Issuer = jwtIssuer;
    options.Audience = jwtAudience;
    options.AccessTokenExpiryMinutes = jwtExpiryMinutes;
    options.RefreshTokenExpiryDays = jwtRefreshExpiryDays;
});
builder.Services.AddScoped<TokenService>();
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
builder.Services.AddScoped<SearchService>();
// Swagger
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
// Аутентифікація JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey ?? ""))
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseCors("AllowVercel");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tournament Management API v1");
        c.RoutePrefix = "swagger";
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        Console.WriteLine($"STACK: {ex.StackTrace}");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync($"Internal error: {ex.Message}");
    }
});
app.Run(url);