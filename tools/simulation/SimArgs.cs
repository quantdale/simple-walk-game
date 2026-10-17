using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}

/// <summary>
/// An EXPECTED failure of a verb: the scenario or benchmark could not be driven, and the
/// reason is known and reportable. Carries its own documented exit code so it does not
/// surface as exit 4 "unexpected error", which is reserved for a genuine defect in the
/// tool itself (D-060).
/// </summary>
internal sealed class CliScenarioException : Exception
{
    /// <summary>Exit code: 2 = domain/validation failure, 3 = save missing/unreadable.</summary>
    public int ExitCode { get; }

    public CliScenarioException(int exitCode, string message) : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Maps a boot failure onto the documented exit codes.</summary>
    public static CliScenarioException FromBoot(string what, WalkGame.Application.StartStatus status)
    {
        int code = status == WalkGame.Application.StartStatus.StateInvalid
            ? 2
            : 3;
        return new CliScenarioException(code, what + " failed: " + status);
    }
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
    private const string Shape = "--shape";
    private const string Iterations = "--iterations";
    private const string Cadence = "--cadence";
    private const string Noise = "--noise";

    private static readonly HashSet<string> KnownFlags = new(StringComparer.Ordinal)
    {
        Save, Seed, At, Vitality, Id, Reason, To, Days, Start, StepsPerDay, Selftest, Replay, ProfileName, Shape, Iterations, Cadence, Noise,
    };

    private static readonly HashSet<string> BooleanFlags = new(StringComparer.Ordinal) { Selftest, Replay, Cadence, Noise };

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

    /// <summary>
    /// Validates a user-supplied save directory whose contents are only READ: the PARENT
    /// must already exist, so a mistyped path is reported as a usage error (exit 1) rather
    /// than as "no save found" (exit 3) — a path that cannot exist and a path with no save
    /// in it are different mistakes and deserve different messages (D-060).
    ///
    /// Deliberately NOT applied to `new`, which legitimately creates the directory, nor to
    /// `scenarios`/`bench`, which create their own work subdirectories.
    /// </summary>
    public string RequireReadableSaveDir()
    {
        string dir = SaveDir;
        // GetDirectoryName is nullable only for a root path, which GetFullPath never
        // returns here; the empty check is kept so the message degrades gracefully.
        string? parent = Path.GetDirectoryName(Path.GetFullPath(dir));
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            throw new CliUsageException(
                $"--save parent directory does not exist: '{parent ?? dir}'. "
                + "Create it first, or use 'new' to create a save.");
        return dir;
    }

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
