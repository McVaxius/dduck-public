using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Security.Cryptography;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DDuck.PublicShell;

public sealed class Plugin : IDalamudPlugin
{
    public static IPluginLog? Log { get; private set; }
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("DDuck.Information");
    private readonly ModuleLoader loader = new();
    private readonly IntroductionWindow introduction;
    private readonly List<Action> cleanup = [];
    private int disposed;
    private bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log, ITextureProvider textures)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        Log = log;
        introduction = new IntroductionWindow(pluginInterface, textures, loader, RefreshAccess);
        try
        {
            cleanup.Add(windows.RemoveAllWindows);
            windows.AddWindow(introduction);
            foreach (var command in new[] { "/dduck", "/dd" })
            {
                if (!commands.AddHandler(command, new CommandInfo(OnCommand) { HelpMessage = "Open Deep Ducking. /dd help opens the command guide (full module required). /dduck is an alias." }))
                    throw new InvalidOperationException($"The {command} command is already registered.");
                cleanup.Add(() => commands.RemoveHandler(command));
            }
            cleanup.Add(() => pluginInterface.UiBuilder.Draw -= Draw);
            pluginInterface.UiBuilder.Draw += Draw;
            cleanup.Add(() => pluginInterface.UiBuilder.OpenMainUi -= Open);
            pluginInterface.UiBuilder.OpenMainUi += Open;
            cleanup.Add(() => pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig);
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
            var directory = pluginInterface.GetIpcProvider<string>("DDuck.Access.Directory.v1");
            cleanup.Add(directory.UnregisterFunc);
            directory.RegisterFunc(() => Path.Combine(pluginInterface.GetPluginConfigDirectory(), "tasks"));
            var validate = pluginInterface.GetIpcProvider<byte[], bool>("DDuck.Access.Validate.v1");
            cleanup.Add(validate.UnregisterFunc);
            validate.RegisterFunc(ValidateAccess);
            var refresh = pluginInterface.GetIpcProvider<bool>("DDuck.Access.Refresh.v1");
            cleanup.Add(refresh.UnregisterFunc);
            refresh.RegisterFunc(() => { RefreshAccess(); return loader.Module != null && !loader.Failed; });
            loader.Load(pluginInterface);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static bool ValidateAccess(byte[] bytes)
    {
        try
        {
            var plaintext = ModulePackage.VerifyAndDecrypt(bytes, TrustAnchor.PublicKey, Version.Parse(BuildInfo.Version));
            CryptographicOperations.ZeroMemory(plaintext);
            return true;
        }
        catch { return false; }
    }

    private void RefreshAccess()
    {
        if (IsDisposed) return;
        loader.Load(pluginInterface);
        if (loader.Module is { } module) { introduction.IsOpen = false; module.OpenMainWindow(); }
    }

    private void Open()
    {
        if (IsDisposed) return;
        if (loader.Module is { } module) module.OpenMainWindow();
        else introduction.IsOpen = true;
    }

    private void OnCommand(string command, string arguments)
    {
        if (IsDisposed) return;
        if (loader.Module is { } module) module.OnCommand(command, arguments);
        else introduction.IsOpen = true;
    }

    private void OpenConfig()
    {
        if (IsDisposed) return;
        if (loader.Module is { } module) module.OnCommand("/dduck", "config");
        else introduction.IsOpen = true;
    }

    private void Draw() { if (IsDisposed) return; windows.Draw(); loader.Module?.Draw(); }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        for (var index = cleanup.Count - 1; index >= 0; --index) Cleanup(cleanup[index]);
        cleanup.Clear();
        Cleanup(loader.Dispose);
    }

    private static void Cleanup(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            try { Log?.Warning(error, "DDuck public host cleanup failed."); }
            catch { /* Logging cannot interrupt cleanup or hide the construction error. */ }
        }
    }
}
