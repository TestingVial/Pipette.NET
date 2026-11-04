using System.CommandLine;
using Pipette.Core;
using Pipette.Core.Diagnostics;

var inputsArgument = new Argument<string[]>("inputs")
{
    Description = "A .sln or .slnx file, or one or more .csproj files.",
    Arity = ArgumentArity.OneOrMore,
};

var outputOption = new Option<string>("--output", "-o")
{
    Description = "Path of the .vial file to write.",
    DefaultValueFactory = _ => PipetteRunOptions.DefaultOutputPath,
};

var strictOption = new Option<bool>("--strict")
{
    Description = "Exit with code 2 when any warning is reported.",
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Print informational notes in addition to warnings.",
};

var rootCommand = new RootCommand("Generates a .vial file from TestingVial.NET attributes.")
{
    inputsArgument,
    outputOption,
    strictOption,
    verboseOption,
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var options = new PipetteRunOptions(
        parseResult.GetValue(inputsArgument) ?? [],
        parseResult.GetValue(outputOption) ?? PipetteRunOptions.DefaultOutputPath,
        parseResult.GetValue(strictOption));
    var verbose = parseResult.GetValue(verboseOption);

    var diagnostics = new PipetteDiagnostics();
    var result = await new PipetteRunner(diagnostics).RunAsync(options, cancellationToken);

    var stderr = parseResult.InvocationConfiguration.Error;
    foreach (var message in diagnostics.Messages)
    {
        if (message.Severity == PipetteSeverity.Warning)
        {
            stderr.WriteLine($"warning: {message.Text}");
        }
        else if (verbose)
        {
            stderr.WriteLine($"note: {message.Text}");
        }
    }

    if (result.Error is not null)
    {
        stderr.WriteLine($"error: {result.Error}");
    }
    else if (result.Document is not null)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(
            $"Wrote {result.Document.Vials.Count} vial(s) to {result.OutputPath}");
    }

    if (result.ExitCode == PipetteExitCodes.StrictWarnings)
    {
        stderr.WriteLine("error: warnings were reported and --strict is set.");
    }

    return result.ExitCode;
});

return await rootCommand.Parse(args).InvokeAsync();
