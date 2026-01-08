namespace DeploymentRisk.Api.Models.Entities;

// database entity
// represents a single webhook event stored in the DB
public class WebhookEventEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    // if false it still needs to be handled by the WebhookProcessor
    public bool Processed { get; set; }
    public string? ErrorMessage { get; set; }
}
