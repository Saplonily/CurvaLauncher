﻿using CommunityToolkit.Mvvm.ComponentModel;
using CurvaLauncher.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Wpf.Ui.Appearance;

namespace CurvaLauncher;

public partial class AppConfig : ObservableObject
{
    [ObservableProperty]
    private int _launcherWidth = 800;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LauncherResultViewHeight))]
    private int _launcherResultViewCount = 7;

    [ObservableProperty]
    private int _queryResultIconSize = 64;

    [ObservableProperty]
    private bool _startsWithWindows = false;

    [ObservableProperty]
    private bool _keepLauncherWhenFocusLost = false;

    [ObservableProperty]
    private string _launcherHotkey = "Alt+Space";

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Auto;

    [ObservableProperty]
    private AppLanguage _language = AppLanguage.Auto;

    [ObservableProperty]
    private WindowStartupScreen _windowStartupScreen = WindowStartupScreen.PrimaryScreen;

    [ObservableProperty]
    private ObservableCollection<QueryHotkey> _customQueryHotkeys = new()
    {
        new QueryHotkey()
        {
            Hotkey = "Ctrl+Alt+Space",
            QueryText = ">"
        }
    };

    [ObservableProperty]
    private Dictionary<string, PluginConfig> _plugins = new();

    [JsonIgnore]
    public double LauncherResultViewHeight => LauncherResultViewCount * 57 + LauncherResultViewCount;



    public static IReadOnlyCollection<AppTheme> AvailableThemes { get; } = [AppTheme.Auto, AppTheme.Light, AppTheme.Dark];
    public static IReadOnlyCollection<AppLanguage> AvailableLanguages { get; } = Enum.GetValues<AppLanguage>();
    public static IReadOnlyCollection<WindowStartupScreen> AvailableWindowStartupScreens { get; } = Enum.GetValues<WindowStartupScreen>();


    public partial class PluginConfig : ObservableObject
    {
        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private float _weight;

        [ObservableProperty]
        JsonObject? _options;
    }
}
