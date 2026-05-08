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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// JWT
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<TokenService>();

// Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5036);
    options.ListenLocalhost(7171, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddControllers();

// MongoDB
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB");
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
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<TokenService>();

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

//АУТЕНТИФІКАЦІЯ JWT 
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings?.Issuer,
            ValidAudience = jwtSettings?.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? ""))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("AllowAll");

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

app.Run();