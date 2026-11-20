using System.Net;

namespace MessageQueueNET.Tests;

public class OpenApiTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData("openApiUI", HttpStatusCode.OK)]
    [InlineData("swaggerUI", HttpStatusCode.OK)]
    public async Task OpenApiAndScalar_AreMappedOnlyWhenEnabled(string? setting, HttpStatusCode expected)
    {
        using var factory = new MessageQueueFactory(setting is null
            ? null
            : new Dictionary<string, string?> { [setting] = "true" });
        var client = factory.CreateClient();

        Assert.Equal(expected, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync("/scalar")).StatusCode);
    }

    [Fact]
    public async Task OpenApiUI_TakesPrecedenceOverLegacySwaggerUI()
    {
        using var factory = new MessageQueueFactory(new Dictionary<string, string?>
        {
            ["openApiUI"] = "false",
            ["swaggerUI"] = "true"
        });

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/openapi/v1.json")).StatusCode);
    }
}
