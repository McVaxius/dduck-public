using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using AethertekUI.Dalamud;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using DDuck.PublicShell;

namespace DDuck.PublicShell.Ui;

internal sealed class PublicAppearance : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly MaterialTextHost shapedText;
    private UiText text;
    private PublicFonts fonts;
    private MaterialTheme theme;
    private bool hindiAvailable;
    private MaterialOptions<string> Languages => new(UiText.Languages.Select(l => new MaterialOption<string>(l.Code, l.Code,
        l.Code == "hi" && !hindiAvailable ? "Hindi (unavailable)" : l.Name, l.Code == "hi" && !hindiAvailable)).ToArray());
    private string appliedLanguage = "";
    private uint appliedAccent;
    private Vector3 accentDraft;
    private int checkedGeneration = -1;
    private bool fontIssueLogged;
    private readonly MaterialWindowFold fontStatusFold = new();
    private readonly MaterialWindowDecorations fontStatusDecorations = new();
    private readonly MaterialWindowOpacity fontStatusOpacity = new();
    private readonly Action<ImGuiWindowPtr> prepareFontStatusDecorations;

    private void Apply()
    {
        var language = UiText.Languages.Any(l => l.Code == AppearancePreferences.Current.Language) ? AppearancePreferences.Current.Language : "en";
        if (language != appliedLanguage)
        {
            fonts?.Dispose();
            text?.Dispose();
            text = new(language, PushFont);
            fonts = new(pluginInterface.UiBuilder.FontAtlas, text.GlyphRanges(), language);
            appliedLanguage = language;
            checkedGeneration = -1;
            hindiAvailable = false;
            fontIssueLogged = false;
        }
        if (theme is null || appliedAccent != (AppearancePreferences.Current.AccentRgb & 0xFFFFFF))
        {
            appliedAccent = AppearancePreferences.Current.AccentRgb & 0xFFFFFF;
            theme = PublicPresentation.Theme(appliedAccent);
            var rgb = PublicPresentation.Rgb(appliedAccent);
            accentDraft = new(rgb.X, rgb.Y, rgb.Z);
        }
        theme.Density = AppearancePreferences.Current.Compact ? MaterialDensity.Compact : MaterialDensity.Standard;
    }

    internal void Draw(WindowSystem windows)
    {
        Apply();
        if (!windows.Windows.Any(window => window.IsOpen)) return;
        using var resources = text.Enter();
        using var shaping = shapedText.Push();
        if (fonts.Ready && checkedGeneration != fonts.Generation)
        {
            hindiAvailable = Enum.GetValues<UiFontRole>().All(role => shapedText.Renderer.TryCheckGlyphs(
                [UiText.Languages.First(l => l.Code == "hi").Name], PublicPresentation.AtlasHeight(role) * ImGuiHelpers.GlobalScale, out _));
            try
            {
                foreach (var role in Enum.GetValues<UiFontRole>())
                    shapedText.Renderer.CheckGlyphs(text.RequiredText, PublicPresentation.AtlasHeight(role) * ImGuiHelpers.GlobalScale);
                fonts.CheckGlyphs(text.RequiredText.Select(MaterialText.NativeGlyphText));
                checkedGeneration = fonts.Generation;
            }
            catch (Exception ex)
            {
                if (!fontIssueLogged) { Plugin.Log!.Error(ex, "[DDuck] Required UI glyph coverage failed."); fontIssueLogged = true; }
            }
        }
        if (!fonts.Ready || checkedGeneration != fonts.Generation)
        {
            if (!fontIssueLogged && fonts.LoadException is { } error) { Plugin.Log!.Error(error, "[DDuck] Required UI fonts failed to load."); fontIssueLogged = true; }
            ImGui.SetNextWindowSize(new Vector2(460 * ImGuiHelpers.GlobalScale, 0));
            using var fontTheme = MaterialTheme.Push(theme, ImGuiHelpers.GlobalScale, MaterialStyleMode.ColorsOnly);
            using var fontChrome = MaterialWindowChrome.Push();
            fontStatusFold.PreDraw("Deep Ducking##PublicFontStatus", null, null, false, prepareFontStatusDecorations);
            var visible = ImGui.Begin("Deep Ducking##PublicFontStatus", ImGuiWindowFlags.AlwaysAutoResize);
            try
            {
                if (visible)
                {
                    fontStatusDecorations.Paint();
                    var failed = fonts.LoadException is not null || fontIssueLogged;
                    MaterialText.TextWrapped(appliedLanguage == "hi"
                        ? failed ? "Hindi is unavailable. Use English to recover; your saved language is unchanged." : "Loading UI fonts..."
                        : UiText.T(failed ? "UI fonts failed to load. See the plugin log." : "Loading UI fonts..."));
                    if (appliedLanguage == "hi" && failed && ImGui.Button("Use English"))
                        AppearancePreferences.Save(AppearancePreferences.Current with { Language = "en" });
                }
            }
            finally
            {
                ImGui.End();
                fontStatusDecorations.Paint();
                fontStatusFold.PostDraw();
                ApplyWindowOpacity(fontStatusOpacity, "Deep Ducking##PublicFontStatus");
            }
            return;
        }
        using var palette = MaterialTheme.Push(theme, ImGuiHelpers.GlobalScale, MaterialStyleMode.ColorsOnly);
        using var style = new MaterialStyleScope();
        var s = ImGuiHelpers.GlobalScale;
        style.Style(ImGuiStyleVar.WindowPadding, new Vector2(AppearancePreferences.Current.Compact ? 12 : 20) * s);
        style.Style(ImGuiStyleVar.ItemSpacing, new Vector2(AppearancePreferences.Current.Compact ? 8 : 12, AppearancePreferences.Current.Compact ? 5 : 10) * s);
        style.Style(ImGuiStyleVar.FramePadding, new Vector2(AppearancePreferences.Current.Compact ? 10 : 14, AppearancePreferences.Current.Compact ? 4 : 7) * s);
        style.Style(ImGuiStyleVar.CellPadding, new Vector2(AppearancePreferences.Current.Compact ? 6 : 10, AppearancePreferences.Current.Compact ? 4 : 8) * s);
        style.Style(ImGuiStyleVar.FrameRounding, 4 * s);
        style.Style(ImGuiStyleVar.ChildRounding, 4 * s);
        using var body = fonts.Push(UiFontRole.Body);
        using var chrome = MaterialWindowChrome.Push();
        windows.Draw();
    }

    internal void DrawSelector(string id = "appearance")
    {
        var language = appliedLanguage;
        using var controls = MaterialControls.Push(PublicPresentation.Controls());
        var changed = MaterialAppearanceSelector.Draw(id, ref accentDraft, ref language, Languages,
            new(UiText.T("Color"), UiText.T("Language"), UiText.T("Teal"), UiText.T("Blue"), UiText.T("Pink"), UiText.T("Custom RGB")), 140);
        if (!changed.AccentChanged && !changed.LanguageChanged) return;
        var rgb = ((uint)Math.Clamp((int)MathF.Round(accentDraft.X * 255), 0, 255) << 16)
            | ((uint)Math.Clamp((int)MathF.Round(accentDraft.Y * 255), 0, 255) << 8) | (uint)Math.Clamp((int)MathF.Round(accentDraft.Z * 255), 0, 255);
        AppearancePreferences.Save(AppearancePreferences.Current with { Language = language, AccentRgb = rgb });
    }

    internal void DrawCompact(string id = "C")
    {
        var compact = AppearancePreferences.Current.Compact;
        if (UiGui.Checkbox(id, ref compact)) AppearancePreferences.Save(AppearancePreferences.Current with { Compact = compact });
        if (ImGui.IsItemHovered()) UiGui.SetTooltip("Compact mode");
    }

    internal PublicAppearance(IDalamudPluginInterface pluginInterface, ITextureProvider textures)
    {
        prepareFontStatusDecorations = fontStatusDecorations.Prepare;
        this.pluginInterface = pluginInterface;
        shapedText = new(textures);
        appliedLanguage = UiText.Languages.Any(l => l.Code == AppearancePreferences.Current.Language) ? AppearancePreferences.Current.Language : "en";
        text = new(appliedLanguage, PushFont);
        fonts = new(pluginInterface.UiBuilder.FontAtlas, text.GlyphRanges(), appliedLanguage);
        appliedAccent = AppearancePreferences.Current.AccentRgb & 0xFFFFFF;
        theme = PublicPresentation.Theme(appliedAccent);
        var rgb = PublicPresentation.Rgb(appliedAccent);
        accentDraft = new(rgb.X, rgb.Y, rgb.Z);
    }
    private IDisposable PushFont(UiFontRole role) => fonts.Push(role);

    public void Dispose() { shapedText.Dispose(); fonts?.Dispose(); text?.Dispose(); }
    internal void DrawLanguage(string id = "appearance")
    {
        var language = appliedLanguage;
        using var controls = MaterialControls.Push(PublicPresentation.Controls());
        if (!MaterialAppearanceSelector.DrawLanguage(id, ref language, Languages, 140)) return;
        AppearancePreferences.Save(AppearancePreferences.Current with { Language = language });
    }

    internal void ApplyWindowOpacity(MaterialWindowOpacity opacity, string name)
    {
        var config = AppearancePreferences.Current;
        opacity.Apply(name, Math.Clamp(config.UiWindowOpacityPercent, 10, 100) / 100f,
            config.UiTransparencyEnabled, config.UiAutoFade, Math.Clamp(config.UiFadedOpacityPercent, 10, 100) / 100f,
            float.IsFinite(config.UiUnfocusedDelaySeconds) ? Math.Max(0, config.UiUnfocusedDelaySeconds) : 10);
    }

    internal void DrawWindowAppearance()
    {
        UiGui.TextUnformatted("Window appearance");
        DrawSelector("settingsAppearance");
        DrawCompact("Compact mode");
        var config = AppearancePreferences.Current;
        var changed = false;
        var compactVisibleOnMainWindow = config.UiCompactVisibleOnMainWindow;
        if (UiGui.Checkbox(UiText.T("Compact visible on main window") + "###UiCompactVisibleOnMainWindowSettings", ref compactVisibleOnMainWindow))
        { changed = true; }
        var languageVisibleOnMainWindow = config.UiLanguageVisibleOnMainWindow;
        if (UiGui.Checkbox(UiText.T("Language visible on main window") + "###UiLanguageVisibleOnMainWindowSettings", ref languageVisibleOnMainWindow))
        { changed = true; }
        var transparencyEnabled = config.UiTransparencyEnabled;
        if (UiGui.Checkbox(UiText.T("Transparency") + "###UiTransparencyEnabledSettings", ref transparencyEnabled))
        { changed = true; }
        var autoFade = config.UiAutoFade;
        if (UiGui.Checkbox(UiText.T("Auto-fade when unfocused") + "###UiAutoFadeSettings", ref autoFade))
        { changed = true; }
        ImGui.BeginDisabled(!transparencyEnabled);
        var opacity = Math.Clamp(config.UiWindowOpacityPercent, 10, 100);
        ImGui.SetNextItemWidth(180 * MaterialTheme.Metrics.Scale);
        if (UiGui.SliderInt(UiText.T("Opacity (%)") + "###UiWindowOpacityPercentSettings", ref opacity, 10, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp))
        { changed = true; }
        var fadedOpacity = Math.Clamp(config.UiFadedOpacityPercent, 10, 100);
        ImGui.SetNextItemWidth(180 * MaterialTheme.Metrics.Scale);
        if (UiGui.SliderInt(UiText.T("Unfocused opacity (%)") + "###UiFadedOpacityPercentSettings", ref fadedOpacity, 10, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp))
        { changed = true; }
        ImGui.BeginDisabled(!autoFade);
        var delay = float.IsFinite(config.UiUnfocusedDelaySeconds) ? Math.Max(0, config.UiUnfocusedDelaySeconds) : 10;
        ImGui.SetNextItemWidth(180 * MaterialTheme.Metrics.Scale);
        if (UiGui.InputFloat(UiText.T("Unfocused delay (seconds)") + "###UiUnfocusedDelaySecondsSettings", ref delay))
        { delay = float.IsFinite(delay) ? Math.Max(0, delay) : 10; changed = true; }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        if (changed) AppearancePreferences.Save(config with { UiCompactVisibleOnMainWindow = compactVisibleOnMainWindow, UiLanguageVisibleOnMainWindow = languageVisibleOnMainWindow, UiTransparencyEnabled = transparencyEnabled, UiAutoFade = autoFade, UiWindowOpacityPercent = opacity, UiFadedOpacityPercent = fadedOpacity, UiUnfocusedDelaySeconds = delay });
    }

    internal void DrawTransparency()
    {
        var enabled = AppearancePreferences.Current.UiTransparencyEnabled;
        if (UiGui.Checkbox(UiText.T("Transparency") + "###UiTransparencyHeader", ref enabled)) AppearancePreferences.Save(AppearancePreferences.Current with { UiTransparencyEnabled = enabled });
    }

}
