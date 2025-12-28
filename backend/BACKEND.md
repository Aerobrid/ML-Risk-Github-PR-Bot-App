# Deployment Risk Platform - Backend

The backend is a .NET 10 Web API responsible for handling GitHub App authentication, processing webhooks, managing ML models, and performing risk scoring.

## Project Structure

The main project is located in `DeploymentRisk.Api/`.

- `Program.cs`: Application entry point and service configuration.
- `Controllers/`: API endpoints (Auth, Dashboard, Webhooks, Risk, MLModels).
- `Services/`: Business logic (GitHub integration, Risk Assessment, ML Client).
- `Data/`: EF Core DbContext and entities.
- `Scoring/`: Risk scoring logic (Rule-based, ML-based).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or latest available preview/RC if 10 is not yet released, otherwise .NET 9/8 as appropriate for the project target).
- SQL Server (LocalDB, Docker, or separate instance).

## Configuration

The backend uses `DotNetEnv` to load configuration from a `.env` file in the root of the repository, as well as `appsettings.json`.

Create `DeploymentRisk.Api/appsettings.json` and follow this format:

```
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "GitHub": {
    "AppId": your-github-app-id,
    "PrivateKeyPath": "your/path/to/github-app.pem",
    "WebhookSecret": "your-webhook-secret-here",
    "ClientId": "your-github-oauth-app-client-Id",
    "ClientSecret": "your-github-oauth-app-client-secret",
    "InstallationId": github-app-installation-id
  },
  "Jwt": {
    "SecretKey": "your-super-secret-jwt-key-min-32-chars-long",
    "Issuer": "DeploymentRiskPlatform",
    "Audience": "DeploymentRiskPlatform"
  },
  "MLService": {
    "BaseUrl": "http://localhost:8000"
  },
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": null
  },
  "RiskScoring": {
    "Enabled": {
      "RuleBased": true,
      "MLModel": true,
      "SecurityScan": true,
      "BugDetection": false
    },
    "Weights": {
      "RuleBased": 0.5,
      "MLModel": 0.5,
      "SecurityScan": 0.0,
      "BugDetection": 0.0
    }
  },
  "LLM": {
    "Enabled": true,
    "Provider": "OpenAI",
    "Model": "gpt-4o-mini",
    "ApiKey": "",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "FallbackToBasic": true
  }
}
```

### Environment Variables

Create a `.env` file in the repository root (or set these environment variables):

- `GitHub__AppId`: Your GitHub App ID.
- `GitHub__InstallationId`: The installation ID of the App.
- `GitHub__PrivateKeyPath`: Path to the GitHub App private key (`.pem`).
- `ML__ServiceUrl`: URL of the ML Service (default: `http://localhost:8000`).
- `Database__ConnectionString`: SQL Server connection string.

### Secrets

1.  Place your GitHub App private key (`.pem`) in the `secrets/` directory at the repository root.
2.  Set `GitHub__PrivateKeyPath` to point to it (e.g., `./secrets/github-app.pem`).

> **Note:** Never commit `.env` or `.pem` files to version control.

## Running Locally

1.  Navigate to the API directory:
    ```powershell
    cd DeploymentRisk.Api
    ```

2.  Restore dependencies and build:
    ```powershell
    dotnet build
    ```

3.  Run the application:
    ```powershell
    dotnet run 
    ```

The API will be available at `http://localhost:5000`.
Swagger documentation is available at `http://localhost:5000/swagger`.

## Database

The project uses Entity Framework Core.

- If a connection string is provided, it attempts to connect to SQL Server.
- `EnsureCreated()` is used to automatically create the database schema during development startup if it doesn't exist.

## Troubleshooting

-   **Private key errors:** Ensure the path in `GitHub__PrivateKeyPath` is correct and relative to the execution directory (or absolute). Verify the file format. I had to switch .pem key scanner/viewers because of this. You might have to as well if you get errors trying to reader key header/footer.
-   **Swagger errors:** If multipart uploads fail in Swagger, try using the ML service directly or check file permissions.
