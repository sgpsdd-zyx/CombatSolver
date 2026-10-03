using System.Reflection;
using System.Runtime.Loader;
using SpireAdvisorMultiplayerFix;

if (args.Length != 3)
    throw new ArgumentException("Usage: <input.dll> <game-data-directory> <output.dll|--self-test>");

string inputPath = Path.GetFullPath(args[0]);
string gameDirectory = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(gameDirectory, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};

if (args[2] == "--self-test")
{
    // Delay loading game-dependent test types until dependency resolution is installed.
    Assembly.GetExecutingAssembly().GetType("SpireAdvisorMultiplayerFix.Checks", throwOnError: true)!
        .GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)!
        .Invoke(null, [inputPath, gameDirectory]);
    return;
}

string outputPath = Path.GetFullPath(args[2]);
if (outputPath == inputPath || File.Exists(outputPath))
    throw new IOException("The output must be a new file; the original mod is preserved.");

byte[] patched = Patcher.Create(inputPath, gameDirectory);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using (FileStream output = new(outputPath, FileMode.CreateNew, FileAccess.Write))
    output.Write(patched);
Console.WriteLine($"PATCHED version={Patcher.Version} methods=3 output={outputPath}");
