using System.Diagnostics;
using System.Text.Json;

namespace PS2Manager.Core.SaveStates;

public sealed record NativeResult(bool Ok, int ExitCode, string? SourceFormat, string? SourceBuild, string? TargetFormat,
    string? Output, string? Error, IReadOnlyList<string> Report);

/// <summary>Converts one save state file into another release format.</summary>
public interface IStateConverter
{
    NativeResult Convert(string input, string output, ushort targetFormat);
}

/// <summary>
/// Runs pcsx2-stateconvert, the native converter built from pcsx2/SaveStateLegacy.cpp, and parses
/// its JSON output. All the format knowledge lives on the native side.
/// </summary>
public sealed class NativeConverter : IStateConverter
{
    public const string EnvironmentVariable = "SAVECONVERT_NATIVE";
    private static readonly string[] ExecutableNames = ["pcsx2-stateconvert.exe", "pcsx2-stateconvert-dev.exe"];

    public string ExecutablePath { get; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public NativeConverter(string executablePath)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Converter not found.", executablePath);
        ExecutablePath = Path.GetFullPath(executablePath);
    }

    /// <summary>
    /// Looks for the converter in: the explicit path, SAVECONVERT_NATIVE, the app's native folder
    /// (..\native when running from cli\), and the folder the .NET host unpacks a single-file build to.
    /// </summary>
    public static NativeConverter? Locate(string? explicitPath = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitPath)) candidates.Add(explicitPath);
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } fromEnvironment) candidates.Add(fromEnvironment);
        var folders = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "native"),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "native")),
        }.Concat(HostNativeFolders().Select(folder => Path.Combine(folder, "native")));
        foreach (var directory in folders)
            candidates.AddRange(ExecutableNames.Select(name => Path.Combine(directory, name)));
        var found = candidates.FirstOrDefault(File.Exists);
        return found is null ? null : new NativeConverter(found);
    }

    /// <summary>NATIVE_DLL_SEARCH_DIRECTORIES, which includes the extraction folder of a single-file build.</summary>
    private static IEnumerable<string> HostNativeFolders() =>
        (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public NativeResult Inspect(string input) => Run("inspect", Path.GetFullPath(input));

    public NativeResult Convert(string input, string output, ushort targetFormat) =>
        Run("convert", Path.GetFullPath(input), Path.GetFullPath(output), "--target", targetFormat.ToString("X4"));

    private NativeResult Run(params string[] arguments)
    {
        var start = new ProcessStartInfo(ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!,
        };
        start.ArgumentList.Add("--json");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start the converter.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            return new(false, -1, null, null, null, null, "The converter timed out.", []);
        }

        var line = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(x => x.StartsWith('{'));
        if (line is null)
            return new(false, process.ExitCode, null, null, null, null,
                $"The converter produced no result (exit {process.ExitCode}): {stderr.Result.Trim()}", []);

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        string? Text(string name) => root.TryGetProperty(name, out var value) ? value.GetString() : null;
        var report = root.TryGetProperty("report", out var lines)
            ? lines.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : [];
        return new(root.GetProperty("ok").GetBoolean(), process.ExitCode, Text("sourceFormat"), Text("sourceBuild"),
            Text("targetFormat"), Text("output"), Text("error"), report);
    }
}
