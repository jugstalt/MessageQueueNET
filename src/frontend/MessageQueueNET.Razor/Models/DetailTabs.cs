namespace MessageQueueNET.Razor.Models;

internal static class DetailTabs
{
    public const string Overview = "overview";
    public const string Messages = "messages";
    public const string Unconfirmed = "unconfirmed";
    public const string Send = "send";
    public const string Settings = "settings";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Overview, Messages, Unconfirmed, Send, Settings };
}
