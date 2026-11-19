using MudBlazor;

namespace MessageQueueNET.Razor.Components;

internal static class DialogDefaults
{
    public static DialogOptions Options => new()
    {
        MaxWidth = MaxWidth.Medium,
        CloseButton = true,
        CloseOnEscapeKey = true
    };
}
