using System;
using System.Collections.Concurrent;
﻿using System.Text.RegularExpressions;

namespace MessageQueueNET.Extensions;

public static class StringExtensions
{
    public const int MaxQueueNameLength = 128;

    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex MultipleWildcards = new Regex(@"\*{2,}", RegexOptions.Compiled);

    // patterns come from requests => the cache is limited
    private const int MaxCachedRegexes = 1000;
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    // Must start with [a-z0-9]; a '.' must be followed by a non-dot character
    // => no ".", "..", leading/trailing dots or consecutive dots (prevents path traversal)
    private static readonly Regex QueueNameRegex = new Regex(
        @"^[a-z0-9](?:[a-z0-9_-]|\.(?=[a-z0-9_-]))*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsPattern(this string pattern)
        => string.IsNullOrEmpty(pattern) ? false : pattern.Contains("*") || pattern.Contains(',');

    public static string ToRegexPattern(this string pattern)
        // Replace '*' with '.*' to convert it into a Regex pattern
        => $"^{Regex.Escape(MultipleWildcards.Replace(pattern, "*")).Replace(@"\*", ".*")}$";

    public static bool FitsPattern(this string aString, string pattern)
        => aString.FitsRegexPattern(pattern.ToRegexPattern());

    public static bool FitsRegexPattern(this string aString, string regexPattern)
    {
        try
        {
            if (RegexCache.Count >= MaxCachedRegexes)
            {
                RegexCache.Clear();
            }

            var regex = RegexCache.GetOrAdd(regexPattern, p => new Regex(p, RegexOptions.CultureInvariant, RegexMatchTimeout));

            return regex.IsMatch(aString);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool IsValidQueueName(this string? queueName) =>
        !string.IsNullOrEmpty(queueName)
        && queueName.Length <= MaxQueueNameLength
        && QueueNameRegex.IsMatch(queueName);
}
