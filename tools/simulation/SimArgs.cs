using System;
using System.Collections.Generic;
using System.Globalization;

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}

internal sealed class SimArgs
{
    private const string Save = "--save";
    private const string Seed = "--seed";
    private const string At = "--at";
    private const string Vitality = "--vitality";
    private const string Id = "--id";
    private const string Reason = "--reason";
    private const string To = "--to";
    private const string Days = "--days";
    private const string Start = "--start";
    private const string StepsPerDay = "--steps-per-day";
    private const string Selftest = "--selftest";
    private const string Replay = "--replay";
    private const string ProfileName = "--profile";

    private static readonly HashSet<string> KnownFlags = new(StringComparer.Ordinal)
    {
        Save, Seed, At, Vitality, Id, Reason, To, Days, Start, StepsPerDay, Selftest, Replay, ProfileName,
    };

    private static readonly HashSet<string> BooleanFlags = new(StringComparer.Ordinal) { Selftest, Replay };

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public SimArgs(string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new CliUsageException($"unexpected argument '{token}'");
            if (!KnownFlags.Contains(token))
                throw new CliUsageException($"unknown flag '{token}'");

            if (BooleanFlags.Contains(token))
            {
                _values[token] = "true";
                continue;
            }

            if (i + 1 >= tokens.Length)
                throw new CliUsageException($"flag '{token}' requires a value");
            string value = tokens[++i];
            if (value.StartsWith("--", StringComparison.Ordinal) && KnownFlags.Contains(value))
                throw new CliUsageException($"flag '{token}' requires a value");
            _values[token] = value;
        }
    }

    public bool Has(string flag) => _values.ContainsKey(flag);

    private string Require(string flag)
    {
        if (_values.TryGetValue(flag, out var value))
            return value;
        throw new CliUsageException($"missing required flag {flag}");
    }

    private string Optional(string flag) =>
        _values.TryGetValue(flag, out var value) ? value : string.Empty;

    public string SaveDir => Require(Save);

    public long Long(string flag)
    {
        string raw = Optional(flag);
        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        throw new CliUsageException($"{flag} expects an integer, got '{raw}'");
    }

    public long RequireLong(string flag) => Has(flag) ? Long(flag) : throw new CliUsageException($"missing required flag {flag} N");

    public int Int(string flag)
    {
        long value = Long(flag);
        if (value is < int.MinValue or > int.MaxValue)
            throw new CliUsageException($"{flag} is out of range: {value}");
        return checked((int)value);
    }

    public ulong ULong(string flag)
    {
        string raw = Optional(flag);
        if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        throw new CliUsageException($"{flag} expects an unsigned integer, got '{raw}'");
    }

    public DateTimeOffset Iso(string flag)
    {
        string raw = Optional(flag);
        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            return parsed;
        throw new CliUsageException($"{flag} expects an ISO-8601 instant, got '{raw}'");
    }

    public Guid Guid(string flag)
    {
        string raw = Optional(flag);
        if (System.Guid.TryParse(raw, out var parsed))
            return parsed;
        throw new CliUsageException($"{flag} expects a GUID, got '{raw}'");
    }

    public string Text(string flag) => Optional(flag);
}
