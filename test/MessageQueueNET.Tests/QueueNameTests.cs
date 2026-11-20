using MessageQueueNET.Extensions;

namespace MessageQueueNET.Tests;

public class QueueNameTests
{
    [Theory]
    [InlineData("queue")]
    [InlineData("appname.main.in")]
    [InlineData("a-b_c.1")]
    [InlineData("0")]
    public void ValidNames(string name) => Assert.True(name.IsValidQueueName());

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a..b")]
    [InlineData("a.")]
    [InlineData(".a")]
    [InlineData("_x")]
    [InlineData("-x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("Queue")]
    public void InvalidNames(string name) => Assert.False(name.IsValidQueueName());

    [Fact]
    public void NameLengthIsLimited()
    {
        Assert.True(new string('a', StringExtensions.MaxQueueNameLength).IsValidQueueName());
        Assert.False(new string('a', StringExtensions.MaxQueueNameLength + 1).IsValidQueueName());
    }
}
