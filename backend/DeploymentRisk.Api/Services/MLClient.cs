using System.Net.Http.Json;
using DeploymentRisk.Api.Models;

namespace DeploymentRisk.Api.Services;

public class MlClient
{
    private readonly HttpClient _http;

    public MlClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<RiskResponse?> PredictAsync(RiskRequest request)
    {
        var response = await _http.PostAsJsonAsync(
            "http://localhost:8000/predict",
            request
        );

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RiskResponse>();
    }
}
