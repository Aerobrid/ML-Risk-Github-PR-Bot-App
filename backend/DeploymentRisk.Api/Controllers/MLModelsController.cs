using Microsoft.AspNetCore.Mvc;

namespace DeploymentRisk.Api.Controllers;

[ApiController]
[Route("api/ml")]
public class MLModelsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<MLModelsController> _logger;
    private readonly IWebHostEnvironment _environment;
    private static List<MLModelInfo> _models = new();

    private readonly IHttpClientFactory _httpClientFactory;

    public MLModelsController(
        IConfiguration config,
        ILogger<MLModelsController> logger,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try 
        {
            var client = _httpClientFactory.CreateClient();
            // TODO: use config for URL
            var response = await client.GetAsync("http://localhost:8000/models");
            if (response.IsSuccessStatusCode)
            {
                 var svcModels = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();

                 var combined = new List<MLModelInfo>();
                 if (svcModels != null)
                 {
                     foreach (var m in svcModels)
                     {
                         var name = m.ContainsKey("name") ? m["name"]?.ToString() ?? string.Empty : string.Empty;
                         var active = m.ContainsKey("active") && bool.TryParse(m["active"]?.ToString(), out var a) ? a : false;
                         var purpose = m.ContainsKey("purpose") ? m["purpose"]?.ToString() ?? string.Empty : string.Empty;

                         var local = _models.FirstOrDefault(x => x.Endpoint == name);

                         combined.Add(new MLModelInfo
                         {
                             Id = local?.Id ?? name,
                             Name = local?.Name ?? name,
                             Endpoint = name,
                             Type = local?.Type ?? (purpose.Contains("Risk") ? "risk" : "analysis"),
                             Enabled = local?.Enabled ?? active,
                             UploadedAt = local?.UploadedAt
                         });
                     }
                 }

                 // Also include any registered models that the ML service did not report
                 foreach (var local in _models)
                 {
                     if (!combined.Any(c => c.Endpoint == local.Endpoint))
                         combined.Add(local);
                 }

                 return Ok(combined);
            }
            return StatusCode((int)response.StatusCode, "Failed to fetch models from ML service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models");
            return StatusCode(500, "Error connecting to ML service");
        }
    }

    [HttpPost("models/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadModel([FromForm] UploadModelRequest request)
    {
        try
        {
            var file = request.File;
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            var client = _httpClientFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream();
            using var streamContent = new StreamContent(fileStream);
            
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.FileName);
            
            if (!string.IsNullOrEmpty(request.Name))
            {
                content.Add(new StringContent(request.Name), "name");
            }

            var response = await client.PostAsync("http://localhost:8000/models/upload", content);

                if (response.IsSuccessStatusCode)
                {
                    // Expecting JSON like { message: ..., filename: "..." }
                    var resultJson = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

                    string filename;
                    if (resultJson != null && resultJson.TryGetValue("filename", out var fnameObj) && fnameObj != null)
                    {
                        filename = fnameObj.ToString() ?? file.FileName;
                    }
                    else
                    {
                        filename = file.FileName;
                    }

                    // Register the uploaded model locally so frontend can manage it
                    var modelInfo = new MLModelInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = request.Name ?? file.FileName,
                        Endpoint = filename,
                        Type = request.Type ?? "ml-model",
                        Enabled = true,
                        UploadedAt = DateTime.UtcNow,
                    };

                    _models.Add(modelInfo);

                    return Ok(new { message = "Uploaded", filename, id = modelInfo.Id });
                }

            return StatusCode((int)response.StatusCode, "ML Service rejected upload");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading model");
            return StatusCode(500, new { message = "Failed to upload model" });
        }
    }

    public class UploadModelRequest
    {
        public IFormFile? File { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
    }

    [HttpPost("models/{modelId}/endpoint")]
    public IActionResult UpdateModelEndpoint(string modelId, [FromBody] UpdateEndpointRequest request)
    {
        var model = _models.FirstOrDefault(m => m.Id == modelId);

        if (model == null)
        {
            return NotFound(new { message = "Model not found" });
        }

        model.Endpoint = request.Endpoint;

        _logger.LogInformation("Updated model endpoint: {ModelId} -> {Endpoint}", modelId, request.Endpoint);

        return Ok(new { message = "Endpoint updated successfully" });
    }

    [HttpDelete("models/{modelId}")]
    public IActionResult DeleteModel(string modelId)
    {
        var model = _models.FirstOrDefault(m => m.Id == modelId);

        if (model == null)
        {
            // If not found in registry, treat modelId as filename and attempt remote delete
            try
            {
                var client = _httpClientFactory.CreateClient();
                var deleteResp = client.DeleteAsync($"http://localhost:8000/models/{modelId}").GetAwaiter().GetResult();
                if (deleteResp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Deleted model file on ML service by filename: {File}", modelId);
                    return NoContent();
                }
                return NotFound(new { message = "Model not found" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error attempting remote delete for {File}", modelId);
                return StatusCode(500, new { message = "Error deleting model" });
            }
        }

        // If the endpoint is a filename stored in the ML service, request deletion there too
        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(model.Endpoint))
            {
                // Try to delete from ML service (best-effort)
                var deleteResp = client.DeleteAsync($"http://localhost:8000/models/{model.Endpoint}").GetAwaiter().GetResult();
                if (!deleteResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ML service delete returned {Status} for {File}", deleteResp.StatusCode, model.Endpoint);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ML service delete for {File}", model.Endpoint);
        }

        _models.Remove(model);

        _logger.LogInformation("Deleted model: {ModelId}", modelId);

        return NoContent();
    }

    [HttpPatch("models/{modelId}/toggle")]
    public IActionResult ToggleModel(string modelId)
    {
        var model = _models.FirstOrDefault(m => m.Id == modelId);

        if (model == null)
        {
            return NotFound(new { message = "Model not found" });
        }

        model.Enabled = !model.Enabled;

        _logger.LogInformation("Toggled model: {ModelId} -> {Enabled}", modelId, model.Enabled);

        return Ok(model);
    }
}

public class MLModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? Version { get; set; }
}

public record UpdateEndpointRequest(string Endpoint);
