using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

// Check released artifacts, not assemblies recompiled against game stubs. Test
// runners can carry NuGet dependencies that are absent from a player's runtime.
var supplied = new HashSet<string>(StringComparer.Ordinal) {
    "mscorlib", "System", "System.Core", "BepInEx", "0Harmony",
    "assembly_valheim", "assembly_utils", "assembly_guiutils",
    "UnityEngine", "UnityEngine.CoreModule", "UnityEngine.UI",
    "UnityEngine.UIModule", "UnityEngine.TextRenderingModule", "Unity.TextMeshPro"
};

if (args.Length == 0)
{
    Console.Error.WriteLine("Pass one or more built Varia DLLs or release ZIPs to inspect.");
    return 1;
}

int failures = 0;
foreach (string path in args)
{
    try
    {
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            using var manifestStream = archive.GetEntry("manifest.json")!.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            string name = manifest.RootElement.GetProperty("name").GetString()!;
            var version = Version.Parse(manifest.RootElement.GetProperty("version_number").GetString()!);
            string[] expected = ["LICENSE", "README.md", "icon.png", "manifest.json", name + ".dll"];
            Require(archive.Entries.Select(entry => entry.FullName).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)), "Unexpected package contents");
            using var dll = new MemoryStream();
            using (var source = archive.GetEntry(name + ".dll")!.Open()) source.CopyTo(dll);
            dll.Position = 0;
            CheckAssembly(dll, name, new Version(version.Major, version.Minor, version.Build, 0));
        }
        else
        {
            using var dll = File.OpenRead(path);
            CheckAssembly(dll);
        }
        Console.WriteLine("PASS " + path);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("FAIL " + path + ": " + error.Message);
        failures++;
    }
}
Console.WriteLine($"{args.Length - failures}/{args.Length} artifact checks passed.");
return failures == 0 ? 0 : 1;

void CheckAssembly(Stream stream, string? expectedName = null, Version? expectedVersion = null)
{
    using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
    var metadata = pe.GetMetadataReader();
    var assembly = metadata.GetAssemblyDefinition();
    Require(metadata.GetString(assembly.Name).StartsWith("Varia", StringComparison.Ordinal), "Not a Varia assembly");
    if (expectedName != null) Require(metadata.GetString(assembly.Name) == expectedName, "DLL/manifest name mismatch");
    if (expectedVersion != null) Require(assembly.Version == expectedVersion, "DLL/manifest version mismatch");
    string[] missing = metadata.AssemblyReferences
        .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
        .Where(name => !supplied.Contains(name)).ToArray();
    Require(missing.Length == 0, "Unshipped runtime dependencies: " + string.Join(", ", missing));
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}
