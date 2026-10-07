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
    private const string ReleaseRequiredMessage = "You are not on Dalamud Release";
    private readonly ModuleLoader loader = new();
    private readonly ReleaseDecision release;
    private readonly Window publicWindow;
    private readonly IntroductionWindow? introduction;
    private readonly Ui.PublicAppearance? appearance;
    private readonly List<Action> cleanup = [];
    private int disposed;
    private bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log, ITextureProvider textures)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        Log = log;
        release = ReleaseDecision.Capture(pluginInterface);
        try
        {
            if (release.Allowed)
            {
                var preferences = new Ui.PreferencePersistence(pluginInterface);
                AppearancePreferences.Initialize(preferences.Load, preferences.Save);
                appearance = new Ui.PublicAppearance(pluginInterface, textures);
                cleanup.Add(appearance.Dispose);
                introduction = new IntroductionWindow(pluginInterface, textures, loader, RefreshAccess, appearance);
                publicWindow = introduction;
            }
            else publicWindow = new ReleaseRequiredWindow();
            cleanup.Add(windows.RemoveAllWindows);
            windows.AddWindow(publicWindow);
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
            refresh.RegisterFunc(() => { RefreshAccess(); return release.Allowed && loader.Module != null && !loader.Failed; });
            if (!release.Allowed)
            {
                ReportReleaseDenial();
                publicWindow.IsOpen = true;
                return;
            }
            loader.Load(pluginInterface);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private bool ValidateAccess(byte[] bytes)
    {
        // Keep branch rejection outside the package-error catch so APM receives the explicit reason.
        if (!release.Allowed) throw new InvalidOperationException(ReleaseRequiredMessage);
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
        if (!release.Allowed) { publicWindow.IsOpen = true; return; }
        loader.Load(pluginInterface);
        if (release.Allowed && loader.Module is { } module) { publicWindow.IsOpen = false; module.OpenMainWindow(); }
    }

    private void Open()
    {
        if (IsDisposed) return;
        if (release.Allowed && loader.Module is { } module) module.OpenMainWindow();
        else publicWindow.IsOpen = true;
    }

    private void OnCommand(string command, string arguments)
    {
        if (IsDisposed) return;
        if (release.Allowed && loader.Module is { } module) module.OnCommand(command, arguments);
        else publicWindow.IsOpen = true;
    }

    private void OpenConfig()
    {
        if (IsDisposed) return;
        if (release.Allowed && loader.Module is { } module) module.OnCommand("/dduck", "config");
        else if (release.Allowed) introduction!.OpenSettings();
        else publicWindow.IsOpen = true;
    }

    private void Draw()
    {
        if (IsDisposed) return;
        if (!release.Allowed) { windows.Draw(); return; }
        appearance!.Draw(windows);
        loader.Module?.Draw();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        for (var index = cleanup.Count - 1; index >= 0; --index) Cleanup(cleanup[index]);
        cleanup.Clear();
        Cleanup(loader.Dispose);
    }

    private void ReportReleaseDenial()
    {
        try { Log?.Error("[Access] {Reason}. Track: {Track}. {Detail}", ReleaseRequiredMessage, release.Track, release.Detail); }
        catch { /* A logging failure must not prevent the error shell from opening. */ }
    }

    private sealed record ReleaseDecision(bool Allowed, string Track, string Detail)
    {
        internal static ReleaseDecision Capture(IDalamudPluginInterface pluginInterface)
        {
            try
            {
                var info = pluginInterface.GetDalamudVersion();
                var track = info.BetaTrack?.Trim();
                var allowed = string.Equals(track, "release", StringComparison.OrdinalIgnoreCase);
                var detail = $"Dalamud version: {info.Version}; ClientStructs Git hash: {info.GitHashClientStructs ?? "<unknown>"}.";
                if (string.IsNullOrWhiteSpace(track))
                    detail = "The runtime branch was not reported; release could not be confirmed. " + detail;
                return new ReleaseDecision(allowed, string.IsNullOrWhiteSpace(track) ? "<unknown>" : track, detail);
            }
            catch (Exception error)
            {
                return new ReleaseDecision(false, "<unknown>", $"Branch check failed: {error.GetType().Name}: {error.Message}");
            }
        }
    }

    private sealed class ReleaseRequiredWindow : Window
    {
        public ReleaseRequiredWindow() : base("Deep Ducking##DalamudReleaseRequired")
        {
            Size = new System.Numerics.Vector2(420, 100);
            SizeCondition = Dalamud.Bindings.ImGui.ImGuiCond.Appearing;
        }

        public override void Draw() => Dalamud.Bindings.ImGui.ImGui.TextUnformatted(ReleaseRequiredMessage);
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
