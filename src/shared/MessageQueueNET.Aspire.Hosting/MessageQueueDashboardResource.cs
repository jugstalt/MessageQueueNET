namespace Aspire.Hosting.ApplicationModel;

public class MessageQueueDashboardResource(string name)
    : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    public int ContainerHttpPort = 8080;

    private EndpointReference? _httpReference;

    public EndpointReference HttpEndpoint =>
        _httpReference ??= new(this, HttpEndpointName);
}
