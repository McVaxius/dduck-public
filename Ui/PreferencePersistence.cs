using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Plugin;

namespace DDuck.PublicShell.Ui;

internal sealed class PreferencePersistence(IDalamudPluginInterface pluginInterface)
{
    private JsonObject Read() => File.Exists(pluginInterface.ConfigFile.FullName)
        ? JsonNode.Parse(File.ReadAllText(pluginInterface.ConfigFile.FullName), documentOptions: new()
            { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
            ?? throw new InvalidDataException("DDuck configuration must be an object.")
        : new JsonObject();

    internal AppearanceValues Load()
    {
        var data = Read();
        return new(data["UiLanguage"]?.GetValue<string>() ?? "en", data["UiAccentRgb"]?.GetValue<uint>() ?? PublicPresentation.ReferenceAccent,
            data["UiCompact"]?.GetValue<bool>() ?? false)
        {
            UiCompactVisibleOnMainWindow = data["UiCompactVisibleOnMainWindow"]?.GetValue<bool>() ?? true,
            UiLanguageVisibleOnMainWindow = data["UiLanguageVisibleOnMainWindow"]?.GetValue<bool>() ?? true,
            UiTransparencyEnabled = data["UiTransparencyEnabled"]?.GetValue<bool>() ?? true,
            UiWindowOpacityPercent = data["UiWindowOpacityPercent"]?.GetValue<int>() ?? 100,
            UiAutoFade = data["UiAutoFade"]?.GetValue<bool>() ?? true,
            UiFadedOpacityPercent = data["UiFadedOpacityPercent"]?.GetValue<int>() ?? 50,
            UiUnfocusedDelaySeconds = data["UiUnfocusedDelaySeconds"]?.GetValue<float>() ?? 10,
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void Save(AppearanceValues values)
    {
#if LOCAL_DEV_BUILD
        var config = global::DDuck.Configuration.Load(pluginInterface);
        config.UiLanguage = values.Language;
        config.UiAccentRgb = values.AccentRgb;
        config.UiCompact = values.Compact;
        config.UiCompactVisibleOnMainWindow = values.UiCompactVisibleOnMainWindow;
        config.UiLanguageVisibleOnMainWindow = values.UiLanguageVisibleOnMainWindow;
        config.UiTransparencyEnabled = values.UiTransparencyEnabled;
        config.UiWindowOpacityPercent = values.UiWindowOpacityPercent;
        config.UiAutoFade = values.UiAutoFade;
        config.UiFadedOpacityPercent = values.UiFadedOpacityPercent;
        config.UiUnfocusedDelaySeconds = values.UiUnfocusedDelaySeconds;
        config.Save();
#else
        var data = Read();
        data["UiLanguage"] = values.Language;
        data["UiAccentRgb"] = values.AccentRgb;
        data["UiCompact"] = values.Compact;
        data["UiCompactVisibleOnMainWindow"] = values.UiCompactVisibleOnMainWindow;
        data["UiLanguageVisibleOnMainWindow"] = values.UiLanguageVisibleOnMainWindow;
        data["UiTransparencyEnabled"] = values.UiTransparencyEnabled;
        data["UiWindowOpacityPercent"] = values.UiWindowOpacityPercent;
        data["UiAutoFade"] = values.UiAutoFade;
        data["UiFadedOpacityPercent"] = values.UiFadedOpacityPercent;
        data["UiUnfocusedDelaySeconds"] = values.UiUnfocusedDelaySeconds;
        // Preserve the shared root identity and private fields without a private serialization dependency.
        var path = pluginInterface.ConfigFile.FullName;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false), leaveOpen: true))
            { writer.Write(data.ToJsonString(new() { WriteIndented = true })); writer.Flush(); output.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
#endif
    }
}
