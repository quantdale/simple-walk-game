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
        "ack" => Cli.Ack(args[1..]),
        "dump" => Cli.Dump(args[1..]),
        "validate" => Cli.Validate(args[1..]),
        _ => Cli.Usage($"unknown verb '{args[0]}'"),
    };
}
catch (CliUsageException ex)
{
    return Cli.Usage(ex.Message);
}
catch (Exception ex)
{
    Console.Error.WriteLine("ERROR: " + ex);
    return 4;
}
