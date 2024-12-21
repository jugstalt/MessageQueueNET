namespace Aspire.Hosting.ApplicationModel;

public class MessageQueueResource(string name) 
    : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    public string ContainerName = $"{name}-{Convert.ToBase64String(Guid.NewGuid().ToByteArray()).ToLower().Replace("=","").Replace("+","").Replace("/","")}";
    public int ContainerHttpPort = 8080;

    private EndpointReference? _httpReference;

    public EndpointReference HttpEndpoint =>
        _httpReference ??= new(this, HttpEndpointName);
}
