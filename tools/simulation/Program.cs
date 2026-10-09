using System;

try
{
    if (args.Length == 0)
        return Cli.Usage(null);

    return args[0] switch
    {
        "new" => Cli.New(args[1..]),
        "credit" => Cli.Credit(args[1..]),
        "advance" => Cli.Advance(args[1..]),
        "simulate" => Cli.Simulate(args[1..]),
        "walk" => Cli.Walk(args[1..]),
        "profile" => Cli.Profile(args[1..]),
        "longhaul" => Cli.LongHaul(args[1..]),
        "bench" => Bench.Run(args[1..]),
        "scenarios" => Scenarios.Run(args[1..]),
        "ack" => Cli.Ack(args[1..]),
        "glance" => Cli.Glance(args[1..]),
        "dump" => Cli.Dump(args[1..]),
        "validate" => Cli.Validate(args[1..]),
        _ => Cli.Usage($"unknown verb '{args[0]}'"),
    };
}
catch (CliUsageException ex)
{
    return Cli.Usage(ex.Message);
}
catch (CliScenarioException ex)
{
    // An EXPECTED verb failure, with its own documented exit code. It must not fall through
    // to the catch-all below: exit 4 is documented as "unexpected error", and reporting a
    // known scenario failure that way sends a reader looking for a bug in the tool (D-060).
    Console.Error.WriteLine("ERROR: " + ex.Message);
    return ex.ExitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine("ERROR: " + ex);
    return 4;
}
