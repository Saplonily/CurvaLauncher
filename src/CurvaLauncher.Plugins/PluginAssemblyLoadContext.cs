using System.Diagnostics;
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
        => Lookup(assemblyName);

    private Assembly? Lookup(AssemblyName assemblyName)
    {
        if (librariesLookup is null)
            return null;

        string dllName;
        string pdbName;

        string? cultureName = assemblyName.CultureName;
        if (string.IsNullOrEmpty(cultureName))
        {
            dllName = $"{assemblyName.Name}.dll";
            pdbName = $"{assemblyName.Name}.pdb";
        }
        else
        {
            dllName = $"{cultureName}/{assemblyName.Name}.dll";
            pdbName = $"{cultureName}/{assemblyName.Name}.pdb";
        }
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
