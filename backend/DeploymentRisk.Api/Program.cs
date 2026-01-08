using System.IO;
// for loadin in env vars
using DotNetEnv;
// import other app code/functionalities into program
using DeploymentRisk.Api.Data;
using DeploymentRisk.Api.Repositories;
using DeploymentRisk.Api.Services;
using DeploymentRisk.Api.Services.Scoring;
using Microsoft.EntityFrameworkCore;

// Load .env (if present) so environment variables override appsettings.json
// this block looks for .env in the current directory + repo root
try
{
    var candidates = new[] { ".env", Path.Combine("..", ".env"), Path.Combine("..","..", ".env"), Path.Combine("..","..","..", ".env") };
    foreach (var candidate in candidates)
    {
        var full = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), candidate));
        if (File.Exists(full))
        {
            Env.Load(full);
            Console.WriteLine($"Loaded .env from {full}");
            break;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($".env load skipped: {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// Controllers and API
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Allow reading request body for webhooks
        options.SuppressModelStateInvalidFilter = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:80")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Database configuration (optional)
var connectionString = builder.Configuration.GetValue<string>("Database:ConnectionString");

/*
 If a connection string is present we register EF Core with SQL Server
 and wire up the SQL-backed repository implementations.
 Otherwise fall back to lightweight in-memory/no-op repositories used
 for development or when persistence is not required 
*/
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<RiskDbContext>(options =>
        options.UseSqlServer(connectionString));
    builder.Services.AddScoped<IRiskRepository, SqlServerRiskRepository>();
    builder.Services.AddScoped<IConfigRepository, SqlServerConfigRepository>();

    Console.WriteLine("✅ Database configured - using SQL Server for persistence");
}
else
{
    builder.Services.AddSingleton<IRiskRepository, NoOpRiskRepository>();
    builder.Services.AddSingleton<IConfigRepository, InMemoryConfigRepository>();

    Console.WriteLine("ℹ️  Database not configured - using GitHub comment-only mode");
}

// GitHub and Risk Assessment services
builder.Services.AddScoped<GitHubClientService>();
builder.Services.AddScoped<GitHubCodeScanningService>();
builder.Services.AddScoped<RiskAssessmentService>();
builder.Services.AddScoped<LlmReviewService>();
builder.Services.AddScoped<WebhookHandler>();

// Risk scorers
builder.Services.AddScoped<IRiskScorer, RuleBasedScorer>();
builder.Services.AddScoped<IRiskScorer, MLModelScorer>();
builder.Services.AddScoped<IRiskScorer, CodeScanningScorer>();

// ML Client and HTTP client for LLM APIs
builder.Services.AddHttpClient<MlClient>();
builder.Services.AddHttpClient(); // For LlmReviewService
builder.Services.AddSingleton<RiskLogStore>();

// Background webhook processor
builder.Services.AddSingleton<WebhookProcessor>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WebhookProcessor>());

var app = builder.Build();

// Run migrations if database is configured
if (!string.IsNullOrEmpty(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
    try
    {
        // Apply any pending EF Core migrations to the target database
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ Database migrations applied");
    }
    catch (Exception ex)
    {
        // Migrations can fail for many reasons (network, permissions, connectionstring without actual DB instance, etc.)
        Console.WriteLine($"⚠️  Database migration failed: {ex.Message}");
        try
        {
            // EnsureCreated fallback will create the schema directly if missing
            // It does NOT run migrations and is not suitable for complex updates
            // TODO: Work on this logic more
            var created = await db.Database.EnsureCreatedAsync();
            Console.WriteLine(created ? "✅ Database created (EnsureCreated)" : "ℹ️  Database already exists (EnsureCreated)");
        }
        catch (Exception inner)
        {
            // If EnsureCreated also fails, surface the error so operator can act
            Console.WriteLine($"❌ Failed to create database with EnsureCreated: {inner.Message}");
        }
    }
}

// swagger ui for dev work
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Disable HTTPS redirection for webhooks (ngrok handles HTTPS if running locally)
// TODO: if NOT running locally, work on this
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.MapControllers();

Console.WriteLine("🚀 Deployment Risk API started");
app.Run();
