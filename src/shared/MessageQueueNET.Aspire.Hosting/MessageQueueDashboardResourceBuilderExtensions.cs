using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

static public class MessageQueueDashboardResourceBuilderExtensions
{
    static public MessageQueueDashboardResourceBuilder AddDashboardForMessageQueueNET(
            this IDistributedApplicationBuilder builder,
            string name,
            int? httpPort = null,
            int? httpsPort = null,
            string? imageTag = null
        )
    {
        var resource = new MessageQueueDashboardResource(name);
        var resourceBuilder = builder.AddResource(resource)
                    .WithImage(MessageQueueDashboardContainerImageTags.Image)
                    .WithImageRegistry(MessageQueueDashboardContainerImageTags.Registry)
                    .WithImageTag(imageTag ?? MessageQueueDashboardContainerImageTags.Tag)
                    .WithHttpEndpoint(
                          targetPort: resource.ContainerHttpPort,
                          port: httpPort,
                          name: MessageQueueResource.HttpEndpointName);

        return new MessageQueueDashboardResourceBuilder(resourceBuilder);
    }

    static public MessageQueueDashboardResourceBuilder ConnectToMessageQueue(
        this MessageQueueDashboardResourceBuilder builder,
        IResourceBuilder<MessageQueueResource> messageQueue,
        string name,
        string filter = "*")
    {
        builder.ResourceBuilder.WithEnvironment(e =>
        {
            e.EnvironmentVariables.Add($"DASHBOARD__QUEUES__{builder.QueueIndex}__NAME", name);
            e.EnvironmentVariables.Add(
                $"DASHBOARD__QUEUES__{builder.QueueIndex}__URL",
                $"http://{messageQueue.Resource.ContainerName}:{messageQueue.Resource.ContainerHttpPort}"
                );
            e.EnvironmentVariables.Add($"DASHBOARD__QUEUES__{builder.QueueIndex}__FILTER", filter);

            builder.QueueIndex++;
        });

        if (!builder.ConnectedMessageQueues.Contains(messageQueue))
        {
            builder.ConnectedMessageQueues.Add(messageQueue);
            builder.ResourceBuilder.WaitFor(messageQueue);
        }

        return builder;
    }

    static public MessageQueueDashboardResourceBuilder WithMaxPollingSeconds(
        this MessageQueueDashboardResourceBuilder builder,
        int maxPollingSeconds = 10)
    {
        builder.ResourceBuilder.WithEnvironment(e =>
        {
            e.EnvironmentVariables.Add($"DASHBOARD__MAXREQUESTPOLLINGSECONDS", maxPollingSeconds.ToString());
        });

        return builder;
    }

    static public IResourceBuilder<MessageQueueDashboardResource> Build(
        this MessageQueueDashboardResourceBuilder builder) => builder.ResourceBuilder;
}

public class MessageQueueDashboardResourceBuilder(
        IResourceBuilder<MessageQueueDashboardResource> resourceBuilder
    )
{
    internal IResourceBuilder<MessageQueueDashboardResource> ResourceBuilder { get; } = resourceBuilder;

    internal List<IResourceBuilder<MessageQueueResource>> ConnectedMessageQueues = new();

    internal int QueueIndex = 0;

}

internal static class MessageQueueDashboardContainerImageTags
{
    internal const string Registry = "docker.io";
    internal const string Image = "gstalt/messagequeue_net_dashboard";
    internal const string Tag = "latest";
}