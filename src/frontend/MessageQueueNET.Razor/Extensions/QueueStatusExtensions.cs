using MessageQueueNET.Client;
using MessageQueueNET.Razor.Models;
using System.Globalization;

namespace MessageQueueNET.Razor.Extensions;

static internal class QueueStatusExtensions
{
    // A queue with items counts as "idle" if no client is dequeuing and nothing changed for this long
    public static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(1);

    public static int DequeuingClientsCount(this QueueInfoModel queue)
        => queue.DequeuingClients?.Count ?? 0;

    public static int? MaxUnconfirmedTotal(this QueueInfoModel queue)
        => queue.MaxUnconfirmedItems is int max && max > 0
            ? queue.MaxUnconfirmedItemsStrategy == MaxUnconfirmedItemsStrategy.PerClient
                ? max * Math.Max(1, queue.DequeuingClientsCount())
                : max
            : null;

    public static QueueStatus GetStatus(this QueueInfoModel queue)
    {
        if (queue.SuspendEnqueue || queue.SuspendDequeue)
        {
            return QueueStatus.Error;
        }

        if (queue.MaxUnconfirmedTotal() is int max && (queue.UnconfirmedItems ?? 0) >= max)
        {
            return QueueStatus.Warn;
        }

        if (queue.QueueLength > 0
            && queue.DequeuingClientsCount() == 0
            && DateTime.UtcNow - queue.LastModifiedUTC.AsUtc() > IdleThreshold)
        {
            return QueueStatus.Idle;
        }

        return queue.QueueLength == 0 ? QueueStatus.Empty : QueueStatus.Ok;
    }

    public static bool IsProblem(this QueueStatus status) => status <= QueueStatus.Idle;

    public static bool IsProblem(this QueueInfoModel queue) => queue.GetStatus().IsProblem();

    public static string CssClass(this QueueStatus status) => status.ToString().ToLowerInvariant();

    public static string DisplayText(this QueueStatus status) => status switch
    {
        QueueStatus.Error => "Suspended",
        QueueStatus.Warn => "Unconfirmed at limit",
        QueueStatus.Idle => "Items without consumer",
        QueueStatus.Ok => "Active",
        _ => "Empty"
    };

    public static string Description(this QueueStatus status) => status switch
    {
        QueueStatus.Error => "Enqueue and/or dequeue is suspended",
        QueueStatus.Warn => "Max. unconfirmed items reached – nothing more is delivered",
        QueueStatus.Idle => $"Items present, no dequeuing client and unchanged for > {IdleThreshold.TotalMinutes:0} min",
        QueueStatus.Ok => "Items are being processed",
        _ => "No items"
    };

    public static string UnconfirmedText(this QueueInfoModel queue)
    {
        if (queue.UnconfirmedItems is not int unconfirmed)
        {
            return "–";
        }

        if (queue.MaxUnconfirmedItems is int max && max > 0)
        {
            int clients = queue.DequeuingClientsCount();
            return queue.MaxUnconfirmedItemsStrategy == MaxUnconfirmedItemsStrategy.PerClient && clients > 0
                ? $"{unconfirmed}/{max}×{clients}"
                : $"{unconfirmed}/{max}";
        }

        return unconfirmed.ToString();
    }

    public static string GroupName(this QueueInfoModel queue)
        => queue.QueueName.LastIndexOf('.') is int pos && pos > 0
            ? queue.QueueName[..pos]
            : "";

    public static string ShortName(this QueueInfoModel queue)
        => queue.QueueName.LastIndexOf('.') is int pos && pos > 0
            ? queue.QueueName[(pos + 1)..]
            : queue.QueueName;

    #region Formatting

    public static DateTime AsUtc(this DateTime dateTime)
        => dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();

    public static string ToAgoText(this DateTime utc)
    {
        if (utc == default)
        {
            return "–";
        }

        var seconds = Math.Max(0, (DateTime.UtcNow - utc.AsUtc()).TotalSeconds);

        return seconds switch
        {
            < 60 => $"{seconds:0} s ago",
            < 3600 => $"{seconds / 60:0} min ago",
            < 172800 => $"{seconds / 3600:0} h ago",
            _ => $"{seconds / 86400:0} d ago"
        };
    }

    public static string ToLocalText(this DateTime utc)
        => utc == default ? "–" : utc.AsUtc().ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

    public static string ToSecondsText(this int seconds, string zeroText = "∞")
        => seconds switch
        {
            <= 0 => zeroText,
            < 120 => $"{seconds} s",
            < 7200 => $"{seconds / 60.0:0.#} min",
            < 172800 => $"{seconds / 3600.0:0.#} h",
            _ => $"{seconds / 86400.0:0.#} d"
        };

    public static string ToNumberText(this int value) => value.ToString("#,0", CultureInfo.CurrentCulture);

    #endregion
}
