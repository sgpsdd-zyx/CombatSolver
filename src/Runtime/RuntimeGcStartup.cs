using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace CombatSolver;

internal static class RuntimeGcStartup
{
    internal static string Status { get; private set; } = "Unchanged";

    internal static void Prepare(bool enabled)
    {
        // Freeze actual startup state before touching the file for the next run.
        _ = RuntimeGcProfile.Current;
        if (!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(RuntimeGcProfile.EnvironmentVariable))
            || (DisplayServer.GetName() == "headless"
                && System.Environment.GetEnvironmentVariable("COMBATSOLVER_TEST_AUTO_GC_CONFIG") != "1"))
        {
            Status = "Skipped";
            return;
        }
        string assemblyPath = typeof(NGame).Assembly.Location;
        string executablePath = OS.GetExecutablePath();
        string? path = RuntimeGcStartupConfig.ResolvePath(
            assemblyPath, executablePath, OperatingSystem.IsMacOS());
        if (path is null)
        {
            Status = "Failed";
            Entry.Logger.Warn("[CombatSolver] RUNTIME_GC_STARTUP failed: game startup path is unavailable or outside the game installation.");
            return;
        }
        try
        {
            Status = RuntimeGcStartupConfig.Apply(path, enabled);
            Entry.Logger.Info($"[CombatSolver] RUNTIME_GC_STARTUP status={Status} next_launch_enabled={enabled}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Status = "Failed";
            Entry.Logger.Warn($"[CombatSolver] RUNTIME_GC_STARTUP failed: {ex.Message}");
        }
    }
}
