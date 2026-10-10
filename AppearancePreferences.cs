namespace DDuck.PublicShell;

// Consumer-owned preferences shared with an authenticated module; the module ABI remains unchanged.
public readonly record struct AppearanceValues(string Language, uint AccentRgb, bool Compact)
{
    public bool UiCompactVisibleOnMainWindow { get; init; }
    public bool UiTransparencyVisibleOnMainWindow { get; init; }
    public bool UiLanguageVisibleOnMainWindow { get; init; } = true;
    public bool UiTransparencyEnabled { get; init; } = true;
    public int UiWindowOpacityPercent { get; init; } = 100;
    public bool UiAutoFade { get; init; } = true;
    public int UiFadedOpacityPercent { get; init; } = 50;
    public float UiUnfocusedDelaySeconds { get; init; } = 10;
}

public static class AppearancePreferences
{
    private static Func<AppearanceValues>? read;
    private static Action<AppearanceValues>? save;
    private static AppearanceValues values = new("en", 0xA92CF1, true);

    public static AppearanceValues Current => read?.Invoke() ?? values;
    public static void Save(AppearanceValues next)
    {
        if (save is null) throw new InvalidOperationException("DDuck appearance storage is unavailable.");
        save(next);
        values = next;
    }

    internal static void Initialize(Func<AppearanceValues> reader, Action<AppearanceValues> writer)
    { read = null; save = writer; values = reader(); }

    public static IDisposable Bind(Func<AppearanceValues> reader, Action<AppearanceValues> writer)
    {
        var previousRead = read;
        var previousSave = save;
        read = reader; save = writer;
        return new Binding(() =>
        {
            if (read != reader) return;
            values = reader();
            read = previousRead; save = previousSave;
        });
    }

    private sealed class Binding(Action restore) : IDisposable
    {
        private Action? restore = restore;
        public void Dispose() => Interlocked.Exchange(ref restore, null)?.Invoke();
    }
}
