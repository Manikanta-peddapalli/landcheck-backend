using System.Text;
using LandCheck.API.Data;
using LandCheck.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LandCheck API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }},
        Array.Empty<string>()
    }});
});

// ── PostgreSQL ─────────────────────────────────────────────
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    connectionString = $"Host={uri.Host};" +
                       $"Port={(uri.Port > 0 ? uri.Port : 5432)};" +
                       $"Database={uri.AbsolutePath.TrimStart('/')};" +
                       $"Username={Uri.UnescapeDataString(userInfo[0])};" +
                       $"Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : "")};" +
                       $"SSL Mode=Require;Trust Server Certificate=true;";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No connection string found.");
}

builder.Services.AddDbContext<LandCheckDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── JWT ────────────────────────────────────────────────────
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret not configured!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LandCheck",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LandCheckUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("FarmerOrAbove", p => p.RequireRole("Farmer","Bank","Lawyer","RealEstateAgent","NRI","Government"));
    options.AddPolicy("BankOrAbove",   p => p.RequireRole("Bank","Lawyer","Government"));
    options.AddPolicy("GovernmentOnly",p => p.RequireRole("Government"));
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILandRecordService, LandRecordService>();
builder.Services.AddScoped<IRiskAnalysisService, RiskAnalysisService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173", "https://landcheck-frontend.vercel.app")
              .AllowAnyMethod().AllowAnyHeader().AllowCredentials());
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LandCheck API v1"));
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Create tables using raw SQL if they don't exist ────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LandCheckDbContext>();
    try
    {
        Console.WriteLine("Creating database tables...");

        // Create Users table
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""FullName"" VARCHAR(200) NOT NULL,
                ""Email"" VARCHAR(256) NOT NULL UNIQUE,
                ""Phone"" VARCHAR(20),
                ""PasswordHash"" TEXT NOT NULL,
                ""Role"" INTEGER NOT NULL DEFAULT 0,
                ""Organisation"" VARCHAR(200),
                ""AadhaarRef"" VARCHAR(50),
                ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                ""IsVerified"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                ""LastLoginAt"" TIMESTAMP NOT NULL DEFAULT NOW()
            );");

        // Create RefreshTokens table
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""AppUserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                ""Token"" VARCHAR(256) NOT NULL,
                ""ExpiresAt"" TIMESTAMP NOT NULL,
                ""IsRevoked"" BOOLEAN NOT NULL DEFAULT FALSE,
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                ""CreatedByIp"" VARCHAR(50)
            );");

        // Create LandRecords table
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""LandRecords"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""SurveyNumber"" VARCHAR(50) NOT NULL UNIQUE,
                ""Village"" VARCHAR(100) NOT NULL,
                ""Mandal"" VARCHAR(100) NOT NULL,
                ""District"" VARCHAR(100) NOT NULL,
                ""State"" VARCHAR(100) NOT NULL DEFAULT 'Andhra Pradesh',
                ""ExtentAcres"" DECIMAL(10,4) NOT NULL DEFAULT 0,
                ""LandType"" VARCHAR(100),
                ""CurrentOwner"" VARCHAR(200) NOT NULL,
                ""Status"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                ""UserId"" INTEGER
            );");

        Console.WriteLine("✅ Database tables created successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Table creation note: {ex.Message}");
    }
}

app.Run();
