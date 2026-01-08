namespace DeploymentRisk.Api.Models;

// data model -> shape of event -> name of webhook event, payload (body), dand date recieved (useful for logging)
public class WebhookEvent
{
    // string.empty binding to prevent NullReferenceException, it needs to have a string assigned
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
