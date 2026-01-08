using DeploymentRisk.Api.Models;

// class is organized under `Services` section of backend 
// reminder to freshen up on C# for future me reading this
namespace DeploymentRisk.Api.Services;

// service class whose job is to make a request to our ML microservice and catch a response
public class MlClient
{
    // readonly -> assigned only once in constructor (similar to final in Java)
    // managed by ASP.NET DI and used to make http requests
    private readonly HttpClient _http;

    // injected HttpClient
    public MlClient(HttpClient http)
    {
        _http = http;
    }

    // if (RiskRequest is a success) ? return RiskResponse : null 
    public async Task<RiskResponse?> PredictAsync(RiskRequest request)
    {
        // ! change if endpoint differs in future
        var response = await _http.PostAsJsonAsync(
            "http://localhost:8000/predict",
            request
        );

        // throws an exception if status code is not a 2xx
        response.EnsureSuccessStatusCode();

        // deserialize json back to RiskResponse to return
        return await response.Content.ReadFromJsonAsync<RiskResponse>();
    }
}
