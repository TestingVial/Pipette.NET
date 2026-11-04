using System.Diagnostics;

namespace Pipette.Core.Tests.EndToEnd;

internal sealed record ProcessResult(int ExitCode, string Output, string Error);

/// <summary>Runs the <c>dotnet</c> CLI as a child process.</summary>
internal static class DotnetProcess
{
    public static async Task<ProcessResult> RunAsync(
        IEnumerable<string> arguments, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // MSBuildLocator sets these in-process; a child dotnet must pick its own MSBuild.
        foreach (var variable in new[] { "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath" })
        {
            startInfo.Environment.Remove(variable);
        }

        var commandLine = $"dotnet {string.Join(' ', startInfo.ArgumentList)}";
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{commandLine}'.");
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{commandLine}' did not finish within {timeout}.");
        }

        return new ProcessResult(process.ExitCode, await output, await error);
    }
}
