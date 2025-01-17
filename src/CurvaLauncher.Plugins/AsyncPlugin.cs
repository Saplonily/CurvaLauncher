﻿using System.Diagnostics;

namespace CurvaLauncher.Plugins;

[DebuggerDisplay("{Name,nq}, Async")]
public abstract class AsyncPlugin : Plugin, IAsyncPlugin
{
    protected AsyncPlugin(CurvaLauncherContext context) : base(context)
    {
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task FinishAsync() => Task.CompletedTask;

    public abstract IAsyncEnumerable<IQueryResult> QueryAsync(string query);
}
