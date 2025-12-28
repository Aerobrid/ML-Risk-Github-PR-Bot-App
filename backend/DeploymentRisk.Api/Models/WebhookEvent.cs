namespace DeploymentRisk.Api.Models;

public class WebhookEvent
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
