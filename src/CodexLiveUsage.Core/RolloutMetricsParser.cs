using System.Text.Json;

namespace CodexLiveUsage.Core;

public static class RolloutMetricsParser
{
    private const string TokenCountMarker = "\"token_count\"";

    public static MetricsSnapshot? Parse(IEnumerable<string> lines)
    {
        var events = new List<ParsedTokenEvent>();

        foreach (var line in lines)
        {
            if (TryParseTokenEvent(line, out var tokenEvent))
            {
                events.Add(tokenEvent);
            }
        }

        if (events.Count == 0)
        {
            return null;
        }

        var latest = events[^1];
        var contextUsed = latest.LastTotal ?? latest.SessionTotal;
        var contextLimit = latest.ContextLimit;
        var lastInput = latest.LastInput;
        var cachedInput = latest.CachedInput;

        return new MetricsSnapshot(
            ContextUsed: contextUsed,
            ContextLimit: contextLimit,
            ContextPercent: Ratio(contextUsed, contextLimit),
            CacheHitPercent: Ratio(cachedInput, lastInput),
            LastInput: lastInput,
            LastOutput: latest.LastOutput,
            LastTotal: latest.LastTotal,
            OutputTokensPerSecond: CalculateOutputSpeed(events),
            SessionTotal: latest.SessionTotal);
    }

    private static bool TryParseTokenEvent(string? line, out ParsedTokenEvent tokenEvent)
    {
        tokenEvent = default!;
        if (string.IsNullOrWhiteSpace(line) || !line.Contains(TokenCountMarker, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var rootType) ||
                rootType.GetString() != "event_msg" ||
                !root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("type", out var payloadType) ||
                payloadType.GetString() != "token_count" ||
                !payload.TryGetProperty("info", out var info) ||
                info.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            info.TryGetProperty("last_token_usage", out var lastUsage);
            info.TryGetProperty("total_token_usage", out var totalUsage);

            var parsed = new ParsedTokenEvent
            {
                LastInput = ReadNumber(lastUsage, "input_tokens"),
                CachedInput = ReadNumber(lastUsage, "cached_input_tokens"),
                LastOutput = ReadNumber(lastUsage, "output_tokens"),
                LastTotal = ReadNumber(lastUsage, "total_tokens"),
                SessionInput = ReadNumber(totalUsage, "input_tokens"),
                SessionOutput = ReadNumber(totalUsage, "output_tokens"),
                SessionTotal = ReadNumber(totalUsage, "total_tokens"),
                ContextLimit = PositiveNumber(info, "model_context_window"),
            };

            if (parsed.LastTotal is null &&
                parsed.SessionTotal is null &&
                parsed.LastInput is null &&
                parsed.LastOutput is null)
            {
                return false;
            }

            if (root.TryGetProperty("timestamp", out var timestampValue) &&
                DateTimeOffset.TryParse(timestampValue.GetString(), out var timestamp))
            {
                parsed = parsed with { Timestamp = timestamp };
            }

            tokenEvent = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static double? CalculateOutputSpeed(IReadOnlyList<ParsedTokenEvent> events)
    {
        var timed = events
            .Where(item => item.Timestamp is not null && item.SessionOutput is not null)
            .ToArray();

        if (timed.Length < 2)
        {
            return null;
        }

        var previous = timed[^2];
        var latest = timed[^1];
        var seconds = (latest.Timestamp!.Value - previous.Timestamp!.Value).TotalSeconds;
        var outputDelta = latest.SessionOutput!.Value - previous.SessionOutput!.Value;
        if (seconds <= 0 || outputDelta <= 0)
        {
            return null;
        }

        return Math.Round(outputDelta / seconds, 1, MidpointRounding.AwayFromZero);
    }

    private static double? ReadNumber(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number) ||
            number < 0)
        {
            return null;
        }

        return number;
    }

    private static double? PositiveNumber(JsonElement parent, string propertyName)
    {
        var value = ReadNumber(parent, propertyName);
        return value > 0 ? value : null;
    }

    private static double? Ratio(double? numerator, double? denominator)
    {
        if (numerator is null || denominator is null || denominator <= 0)
        {
            return null;
        }

        return Math.Round(
            Math.Clamp(numerator.Value / denominator.Value * 100, 0, 100),
            1,
            MidpointRounding.AwayFromZero);
    }

    private sealed record ParsedTokenEvent
    {
        public double? LastInput { get; init; }
        public double? CachedInput { get; init; }
        public double? LastOutput { get; init; }
        public double? LastTotal { get; init; }
        public double? SessionInput { get; init; }
        public double? SessionOutput { get; init; }
        public double? SessionTotal { get; init; }
        public double? ContextLimit { get; init; }
        public DateTimeOffset? Timestamp { get; init; }
    }
}
