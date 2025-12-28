namespace DeploymentRisk.Api.Models.Entities;

public class WebhookEventEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool Processed { get; set; }
    public string? ErrorMessage { get; set; }
}
