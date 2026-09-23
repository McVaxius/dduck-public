using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

public sealed class State
{
    public static State Current = null!;
    public string Failure = "", CleanupFailure = "";
    public bool LogFailure, RetainDraw, NoModule, ProvidersBeforeLoad;
    public bool IntroductionOpen;
    public readonly Exception Original = new InvalidOperationException("construction fault");
    public int LoaderDisposals, Warnings, Windows, Draws, PrivateDraws, Opens;
    public readonly List<string> CommandsSeen = [];
    public TestInterface Interface { get; }
    public Commands Commands { get; }
    public Log Log { get; }
    public Textures Textures { get; } = new();
    public State() { Interface = new(this); Commands = new(this); Log = new(this); }
    public void Register(string stage) { if (Failure == stage) throw Original; }
    public void Remove(string stage) { if (CleanupFailure == stage) throw new InvalidOperationException("cleanup fault"); }
}
public sealed class Commands(State state) : ICommandManager
{
    public readonly Dictionary<string, Action<string, string>> Handlers = [];
    public readonly List<string> Removals = [];
    public bool AddHandler(string name, CommandInfo info)
    {
        if (Handlers.ContainsKey(name)) return false;
        Handlers.Add(name, info.Handler);
        return true;
    }
    public void RemoveHandler(string name) { Handlers.Remove(name); Removals.Add(name); state.Remove(name); }

}
public sealed class Log(State state) : IPluginLog
{
    public void Warning(Exception error, string message) { state.Warnings++; if (state.LogFailure) throw new Exception("logging fault"); }
}
public sealed class Textures : ITextureProvider { }
public sealed class TestInterface(State state) : IDalamudPluginInterface
{
    private readonly Dictionary<string, IProvider> providers = [];
    public Ui UiBuilder { get; } = new(state);
    public int ProviderCount => providers.Values.Count(provider => provider.Registered);
    private static string Label(string name) => name.Contains("Directory") ? "directory" : name.Contains("Validate") ? "validate" : "refresh";
    public Provider<T> GetIpcProvider<T>(string name)
    {
        if (!providers.TryGetValue(name, out var value)) providers[name] = value = new Provider<T>(state, Label(name));
        return (Provider<T>)value;
    }
    public Provider<TArg, T> GetIpcProvider<TArg, T>(string name)
    {
        if (!providers.TryGetValue(name, out var value)) providers[name] = value = new Provider<TArg, T>(state, Label(name));
        return (Provider<TArg, T>)value;
    }
    public string GetPluginConfigDirectory() => "synthetic-config";
}
public interface IProvider { bool Registered { get; } }
public sealed class Provider<T>(State state, string label) : IProvider
{
    public bool Registered { get; private set; }
    public Func<T>? Callback;
    public void RegisterFunc(Func<T> value) { Callback = value; Registered = true; state.Register(label); }
    public void UnregisterFunc() { Callback = null; Registered = false; state.Remove(label); }
}
public sealed class Provider<TArg, T>(State state, string label) : IProvider
{
    public bool Registered { get; private set; }
    public Func<TArg, T>? Callback;
    public void RegisterFunc(Func<TArg, T> value) { Callback = value; Registered = true; state.Register(label); }
    public void UnregisterFunc() { Callback = null; Registered = false; state.Remove(label); }
}
public sealed class Ui(State state)
{
    private Action? draw, main, config;
    public int Count => (draw?.GetInvocationList().Length ?? 0) + (main?.GetInvocationList().Length ?? 0) + (config?.GetInvocationList().Length ?? 0);
    public event Action Draw
    {
        add { draw += value; state.Register("draw"); }
        remove { if (state.RetainDraw) throw new Exception("event removal failed"); draw -= value; state.Remove("draw"); }
    }
    public event Action OpenMainUi { add { main += value; state.Register("main"); } remove { main -= value; state.Remove("main"); } }
    public event Action OpenConfigUi { add { config += value; state.Register("config"); } remove { config -= value; state.Remove("config"); } }
    public void DrawNow() => draw?.Invoke();
    public void MainNow() => main?.Invoke();
    public void ConfigNow() => config?.Invoke();
}
namespace Dalamud.Game.Command
{
    public sealed class CommandInfo(Action<string,string> handler) { public Action<string,string> Handler = handler; public string HelpMessage { get; set; } = ""; }
}
namespace Dalamud.IoC { [AttributeUsage(AttributeTargets.Property)] public sealed class PluginServiceAttribute : Attribute { } }
namespace Dalamud.Plugin.Services
{
    public interface ICommandManager { bool AddHandler(string name, CommandInfo info); void RemoveHandler(string name); }
    public interface IPluginLog { void Warning(Exception error, string message); }
    public interface ITextureProvider { }
}
namespace Dalamud.Plugin
{
    public interface IDalamudPlugin : IDisposable { }
    public interface IDalamudPluginInterface
    {
        Ui UiBuilder { get; }
        Provider<T> GetIpcProvider<T>(string name);
        Provider<TArg,T> GetIpcProvider<TArg,T>(string name);
        string GetPluginConfigDirectory();
    }
}
namespace Dalamud.Interface.Windowing
{
    public sealed class WindowSystem
    {
        public WindowSystem(string name) { }
        private readonly State state = State.Current;
        public void AddWindow(object window) => state.Windows++;
        public void RemoveAllWindows() { state.Windows = 0; state.Remove("windows"); }
        public void Draw() => state.Draws++;
    }
}
namespace DDuck.PublicShell
{
    internal sealed class IntroductionWindow
    {
        public IntroductionWindow(IDalamudPluginInterface pi, ITextureProvider textures, ModuleLoader loader, Action refresh) { }
        public bool IsOpen { get => State.Current.IntroductionOpen; set => State.Current.IntroductionOpen = value; }
    }
    internal sealed class ModuleLoader : IDisposable
    {
        private readonly State state = State.Current;
        public Module? Module { get; private set; }
        public bool Failed => false;
        public void Load(IDalamudPluginInterface pi) { state.ProvidersBeforeLoad = state.Interface.ProviderCount == 3; state.Register("load"); Module = state.NoModule ? null : new(state); }
        public void Dispose() { state.LoaderDisposals++; Module = null; state.Remove("loader"); }
    }
    internal sealed class Module(State state)
    {
        public void OpenMainWindow() => state.Opens++;
        public void OnCommand(string command, string arguments) => state.CommandsSeen.Add(command + " " + arguments);
        public void Draw() => state.PrivateDraws++;
    }
}
