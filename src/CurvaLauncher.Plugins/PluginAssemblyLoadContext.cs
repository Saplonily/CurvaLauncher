using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace CurvaLauncher.Plugins;

public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, MemoryStream>? librariesLookup;

    public PluginAssemblyLoadContext(string name, Dictionary<string, MemoryStream>? librariesLookup)
        : base(name)
    {
        this.librariesLookup = librariesLookup;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (librariesLookup is null)
            return null;

        string dllName = $"{assemblyName.Name}.dll";
        string pdbName = $"{assemblyName.Name}.pdb";
        librariesLookup.Remove(dllName, out MemoryStream? dllStream);
        librariesLookup.Remove(pdbName, out MemoryStream? pdbStream);

        if (dllStream is null)
            return null;

        Assembly assembly = LoadFromStream(dllStream, pdbStream);
        dllStream.Dispose();
        pdbStream?.Dispose();
        return assembly;
    }
}
