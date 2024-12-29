using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CurvaLauncher.Models;
using CurvaLauncher.Plugins;
using CurvaLauncher.PluginInteraction;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.IO.Compression;
using IOPath = System.IO.Path;
using System.Runtime.Loader;

namespace CurvaLauncher.Services;

public partial class PluginService
{
    private readonly PathService _pathService;
    private readonly ConfigService _configService;

    public string Path { get; set; } = "Plugins";

    public ObservableCollection<CurvaLauncherPluginInstance> PluginInstances { get; } = new();

    public PluginService(
        PathService pathService,
        ConfigService configService)
    {
        _pathService = pathService;
        _configService = configService;
    }


    private DirectoryInfo EnsurePluginDirectory()
    {
        DirectoryInfo dir = new(_pathService.GetPath(Path));

        if (!dir.Exists)
            dir.Create();

        return dir;
    }

    private void CoreLoadPlugins(out List<CurvaLauncherPluginInstance> plugins)
    {
        plugins = new List<CurvaLauncherPluginInstance>();

        var dir = EnsurePluginDirectory();
        var pluginFiles = dir.EnumerateFiles("*.dll").Concat(dir.EnumerateFiles("*.zip"));

        AppConfig config = _configService.Config;

        foreach (FileInfo file in pluginFiles)
            if (CoreLoadPlugin(config, file, out CurvaLauncherPluginInstance? plugin))
            {
                plugins.Add(plugin);
            }
    }

    private bool CoreLoadPlugin(AppConfig config, FileInfo file, [NotNullWhen(true)] out CurvaLauncherPluginInstance? pluginInstance)
    {
        pluginInstance = null;

        try
        {
            Assembly? assembly = null;
            if (file.Extension is ".zip")
                assembly = CoreLoadPluginAssemblyFromZip(file);
            else if (file.Extension is ".dll")
                assembly = CoreLoadPluginAssemblyFromDll(file);
            else
                return false;

            Type? pluginType = assembly.GetCustomAttribute<PluginTypeAttribute>()?.PluginType;

            static bool IsPluginType(Type type)
                => type.IsAssignableTo(typeof(ISyncPlugin)) || type.IsAssignableTo(typeof(IAsyncPlugin));

            if (pluginType is not null)
            {
                if (!IsPluginType(pluginType))
                    return false;
            }
            else
            {
                pluginType = assembly.ExportedTypes.FirstOrDefault(IsPluginType);

                if (pluginType is null)
                    return false;
            }

            if (!CurvaLauncherPluginInstance.TryCreate(pluginType, out pluginInstance))
                return false;

            var typeName = pluginType.FullName!;

            if (config.Plugins.TryGetValue(typeName, out var pluginConfig))
            {
                if (pluginConfig.Options != null)
                {
                    var props = pluginInstance.Plugin.GetType().GetProperties()
                            .Where(p => p.GetCustomAttribute<PluginOptionAttribute>() is not null
                                || p.GetCustomAttribute<PluginI18nOptionAttribute>() is not null);

                    foreach (var property in props)
                    {
                        if (pluginConfig.Options.TryGetPropertyValue(property.Name, out var value))
                        {
                            var type = property.PropertyType;
                            var val = JsonSerializer.Deserialize(value, type);
                            property.SetValue(pluginInstance.Plugin, val);
                        }
                    }
                }

                pluginInstance.IsEnabled = pluginConfig.IsEnabled;
                pluginInstance.Weight = pluginConfig.Weight;
            }
            else
            {
                pluginInstance.IsEnabled = true;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Plugin load failed, {ex}");
            return false;
        }
    }

    private Assembly CoreLoadPluginAssemblyFromZip(FileInfo zipFile)
    {
        using ZipArchive zipArchive = new(zipFile.OpenRead());
        var entries = zipArchive.Entries;
        var rootFiles = entries.Where(e => !e.FullName.Contains('/'));
        ZipArchiveEntry dllEntry = rootFiles.Single(e => e.Name.EndsWith(".dll"));
        ZipArchiveEntry? pdbEntry = rootFiles.Single(e => e.FullName == $"{dllEntry.Name[..^4]}.pdb");

        bool hasLibraries = zipArchive.Entries.Any(e => e.FullName.StartsWith("Libraries/"));

        IEnumerable<ZipArchiveEntry>? libraryEntries = null;
        if (hasLibraries)
            libraryEntries = entries.Where(e =>
                IOPath.GetDirectoryName(e.FullName) is "Libraries" &&
                e.FullName[^1] is not '/' // bandzip 在打包 zip 时会将目录本身也写成一个 entry
            );

        MemoryStream dllStream = new(checked((int)dllEntry.Length));
        MemoryStream pdbStream = new(checked((int)pdbEntry.Length));
        Dictionary<string, MemoryStream>? libraryStreams = null;

        dllEntry.Open().CopyTo(dllStream);
        dllStream.Seek(0, SeekOrigin.Begin);

        pdbEntry?.Open().CopyTo(pdbStream);
        pdbStream?.Seek(0, SeekOrigin.Begin);

        if (hasLibraries)
            libraryStreams = new(libraryEntries!.Select(e =>
            {
                string name = e.Name;
                MemoryStream stream = new(checked((int)e.Length));
                e.Open().CopyTo(stream);
                stream.Seek(0, SeekOrigin.Begin);
                return new KeyValuePair<string, MemoryStream>(e.Name, stream);
            }));

        PluginAssemblyLoadContext alc = new(zipFile.Name, libraryStreams);
        return alc.LoadFromStream(dllStream, pdbStream);
    }

    private Assembly CoreLoadPluginAssemblyFromDll(FileInfo dllFile)
    {
        PluginAssemblyLoadContext alc = new(dllFile.Name, null);
        return alc.LoadFromAssemblyPath(dllFile.FullName);
    }

    private void MoveCommandPluginsToTheBeginning(IList<CurvaLauncherPluginInstance> plugins)
    {
        int indexStart = 0;
        for (int i = 1; i < plugins.Count; i++)
        {
            if (plugins[i].Plugin is CommandPlugin)
            {
                (plugins[indexStart], plugins[i]) = (plugins[i], plugins[indexStart]);
                indexStart++;
            }
        }
    }

    [RelayCommand]
    public void LoadAllPlugins()
    {
        CoreLoadPlugins(out var plugins);
        MoveCommandPluginsToTheBeginning(plugins);

        PluginInstances.Clear();
        foreach (var plugin in plugins)
            PluginInstances.Add(plugin);
    }

    [RelayCommand]
    public async Task ReloadAllPlugins()
    {
        foreach (var plugin in PluginInstances.Where(ins => ins.IsEnabled))
        {
            if (plugin.Plugin is ISyncPlugin syncPlugin)
                syncPlugin.Initialize();
            else if (plugin.Plugin is IAsyncPlugin asyncPlugin)
                await asyncPlugin.InitializeAsync();
        }
    }

    [RelayCommand]
    public async Task FinishAllPlugins()
    {
        foreach (var plugin in PluginInstances.Where(ins => ins.IsEnabled))
        {
            if (plugin.Plugin is ISyncPlugin syncPlugin)
                syncPlugin.Finish();
            else if (plugin.Plugin is IAsyncPlugin asyncPlugin)
                await asyncPlugin.FinishAsync();
        }
    }
}