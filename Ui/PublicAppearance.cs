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
    private MaterialUiSoundPlayer? uiSounds;
    private bool appliedSoundsMuted = true;
    private readonly Dictionary<Window, MaterialAppearanceTitleBar> titleBars = new();
    private readonly Dictionary<Window, TitleBarButton> soundButtons = new();
    private bool hindiAvailable;
    private MaterialOptions<string> Languages => new(UiText.Languages.Select(l => new MaterialOption<string>(l.Code, l.Code,
        l.Code == "hi" && !hindiAvailable ? "Hindi (unavailable)" : l.Name, l.Code == "hi" && !hindiAvailable)).ToArray());
    private string appliedLanguage = "";
    private uint appliedAccent;
    private Vector3 accentDraft;
    private MaterialColorSettings colorDraft = new();
    private MaterialColorSettings? appliedSavedColors;
    private MaterialColorSettings? appliedColors;
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
        var savedColors = AppearancePreferences.Current.UiColors ?? new MaterialColorSettings();
        if (savedColors != appliedSavedColors)
        {
            colorDraft = savedColors;
            appliedSavedColors = savedColors;
        }
        if (theme is null || appliedAccent != (AppearancePreferences.Current.AccentRgb & 0xFFFFFF) || colorDraft != appliedColors)
        {
            appliedAccent = AppearancePreferences.Current.AccentRgb & 0xFFFFFF;
            appliedColors = colorDraft;
            theme = PublicPresentation.Theme(appliedAccent, colorDraft);
            theme.Feedback = PlaySound;
            if (!theme.IsLcars) uiSounds?.Stop();
            var rgb = PublicPresentation.Rgb(appliedAccent);
            accentDraft = new(rgb.X, rgb.Y, rgb.Z);
        }
        theme.Density = AppearancePreferences.Current.Compact ? MaterialDensity.Compact : MaterialDensity.Standard;
        if (appliedSoundsMuted != AppearancePreferences.Current.UiLcarsSoundsMuted)
        {
            appliedSoundsMuted = AppearancePreferences.Current.UiLcarsSoundsMuted;
            if (appliedSoundsMuted) uiSounds?.Stop();
        }
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
            bool visible;
            using (MaterialSkin.BackdropStyle())
                visible = ImGui.Begin("Deep Ducking##PublicFontStatus", ImGuiWindowFlags.AlwaysAutoResize);
            try
            {
                if (visible)
                {
                    MaterialSkin.Backdrop();
                    fontStatusDecorations.Paint();
                    if (theme.IsLcars) MaterialLcars.Header("Deep Ducking");
                    var failed = fonts.LoadException is not null || fontIssueLogged;
                    MaterialText.TextWrapped(appliedLanguage == "hi"
                        ? failed ? "Hindi is unavailable. Use English to recover; your saved language is unchanged." : "Loading UI fonts..."
                        : UiText.T(failed ? "UI fonts failed to load. See the plugin log." : "Loading UI fonts..."));
                    if (appliedLanguage == "hi" && failed && UiGui.Button("Use English", display: "Use English"))
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
        if (theme.IsLcars)
        {
            style.Color(ImGuiCol.CheckMark, MaterialLcars.ControlBlue);
            style.Color(ImGuiCol.SliderGrab, MaterialLcars.ControlBlue);
            style.Color(ImGuiCol.SliderGrabActive, MaterialLcars.ControlBlue);
        }
        using var body = fonts.Push(UiFontRole.Body);
        using var chrome = MaterialWindowChrome.Push();
        PrepareTitleBars();
        using (MaterialSkin.BackdropStyle()) windows.Draw();
    }

    internal MaterialAppearanceTitleBar RegisterTitleBar(Window owner)
    {
        var actions = MaterialAppearanceTitleBar.Register(owner,
            () => new(UiText.T("Language"), UiText.T("Compact mode"), UiText.T("Transparency"), UiText.T("Color")),
            () => DrawLanguage("titlebar-language"),
            () => AppearancePreferences.Save(AppearancePreferences.Current with { Compact = !AppearancePreferences.Current.Compact }),
            DrawWindowAppearance, DrawTitleColour,
            () => new(AppearancePreferences.Current.UiLanguageVisibleOnMainWindow, AppearancePreferences.Current.UiCompactVisibleOnMainWindow,
                AppearancePreferences.Current.UiTransparencyVisibleOnMainWindow, true));
        titleBars.Add(owner, actions);
        return actions;
    }

    private void PrepareTitleBars()
    {
        foreach (var (owner, actions) in titleBars)
        {
            actions.SetLcarsIcons(theme.IsLcars);
            actions.RefreshVisibility();
            if (!soundButtons.TryGetValue(owner, out var speaker))
            {
                speaker = new()
                {
                    Priority = (int)Math.Max(int.MinValue + 1L, owner.TitleBarButtons.Select(static button => (long)button.Priority).DefaultIfEmpty(0).Min() - 1),
                    IconOffset = new(2, 1),
                    Click = button => { if (button == ImGuiMouseButton.Left && LcarsActive) SetSoundsMuted(!AppearancePreferences.Current.UiLcarsSoundsMuted); },
                    ShowTooltip = () => MaterialText.SetTooltip(UiText.T(AppearancePreferences.Current.UiLcarsSoundsMuted ? "UI sounds muted" : "UI sounds on")),
                };
                soundButtons.Add(owner, speaker);
            }
            speaker.Icon = AppearancePreferences.Current.UiLcarsSoundsMuted ? Dalamud.Interface.FontAwesomeIcon.VolumeMute : Dalamud.Interface.FontAwesomeIcon.VolumeUp;
            if (theme.IsLcars && !owner.TitleBarButtons.Contains(speaker)) owner.TitleBarButtons.Add(speaker);
            else if (!theme.IsLcars) owner.TitleBarButtons.Remove(speaker);
        }
    }

    private void DrawTitleColour() => DrawSelector("titlebar-colour", showLanguage: false);

    internal void DrawSelector(string id = "appearance", bool showLanguage = true)
    {
        using var controls = MaterialControls.Push(PublicPresentation.Controls());
        if (MaterialAppearanceSelector.DrawColors(id, ref colorDraft, ref accentDraft,
            new(UiText.T("Color"), UiText.T("Language"), UiText.T("Teal"), UiText.T("Blue"), UiText.T("Pink"), UiText.T("Custom RGB")),
            new(UiText.T("Auto"), UiText.T("Picker"), UiText.T("Default"), UiText.T("Background, controls and text follow your theme."),
                UiText.T("Theme colour"), UiText.T("Choose each colour independently."), UiText.T("Background"), UiText.T("Controls"),
                UiText.T("Text"), UiText.T("Aa  Preview text"), UiText.T("Control"),
                UiText.T("Switch to Picker to set each colour yourself."), UiText.T("Switch back to Auto to use the automatic theme."))
            {
                Lcars = UiText.T("LCARS"), LcarsHelp = UiText.T("LCARS panels, bars and controls"),
                Glassmorphism = UiText.T("Glassmorphism"), Spatial = UiText.T("Spatial"), Cyberpunk = UiText.T("Cyberpunk"),
                LcarsBar1 = UiText.T("LCARS bar 1"), LcarsBar2 = UiText.T("LCARS bar 2"), LcarsBar3 = UiText.T("LCARS bar 3"),
                Reset = UiText.T("Appearance reset"), ResetColors = UiText.T("Reset colours"),
                Animation = UiText.T("Animation"), AnimationSpeed = UiText.T("Animation speed (0.1–10x; default 1x)"),
                WaveColors = UiText.T("Wave colours"), GlassWave1 = UiText.T("Wave colour 1"),
                GlassWave2 = UiText.T("Wave colour 2"), GlassWave3 = UiText.T("Wave colour 3"),
                ResetWaveColors = UiText.T("Reset wave colours"),
            }, new MaterialColorSettings(), MaterialColorSettings.Rgb(PublicPresentation.ReferenceAccent),
            AppearancePreferences.Current.Compact ? 298 : 316))
        {
            AppearancePreferences.Save(AppearancePreferences.Current with { UiColors = colorDraft, AccentRgb = MaterialColorSettings.ToRgb(accentDraft) });
            if (!LcarsActive) uiSounds?.Stop();
        }
        if (showLanguage) DrawLanguage(id);
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
        colorDraft = AppearancePreferences.Current.UiColors ?? new();
        appliedSavedColors = appliedColors = colorDraft;
        theme = PublicPresentation.Theme(appliedAccent, colorDraft);
        theme.Feedback = PlaySound;
        var rgb = PublicPresentation.Rgb(appliedAccent);
        accentDraft = new(rgb.X, rgb.Y, rgb.Z);
    }
    private IDisposable PushFont(UiFontRole role) => fonts.Push(role);

    private bool LcarsActive => AppearancePreferences.Current.UiColors?.Mode == MaterialColorMode.Lcars;

    private void PlaySound(MaterialUiSoundCue cue)
    {
        if (LcarsActive && !AppearancePreferences.Current.UiLcarsSoundsMuted)
            (uiSounds ??= new MaterialUiSoundPlayer()).TryPlay(cue);
    }

    private void SetSoundsMuted(bool muted)
    {
        AppearancePreferences.Save(AppearancePreferences.Current with { UiLcarsSoundsMuted = muted });
        if (muted) uiSounds?.Stop();
    }

    private void DrawLcarsSounds()
    {
        MaterialSettings.Section(UiText.T("LCARS UI sounds"));
        ImGui.BeginDisabled(!LcarsActive);
        try
        {
            var enabled = !AppearancePreferences.Current.UiLcarsSoundsMuted;
            if (UiGui.Checkbox("UI sounds", ref enabled)) SetSoundsMuted(!enabled);
            UiGui.TextWrapped("Custom UI tones. The titlebar speaker toggles the same setting.");
            ImGui.BeginDisabled(AppearancePreferences.Current.UiLcarsSoundsMuted);
            try
            {
                var current = MaterialTheme.Current;
                var feedback = current.Feedback;
                bool preview;
                try { current.Feedback = null; preview = UiGui.Button("Preview sound"); }
                finally { current.Feedback = feedback; }
                MaterialLcars.Feedback(preview, MaterialUiSoundCue.Preview);
            }
            finally { ImGui.EndDisabled(); }
            if (uiSounds?.Error is not null) UiGui.TextWrapped("Audio output is unavailable.");
        }
        finally { ImGui.EndDisabled(); }
        if (!LcarsActive) UiGui.TextDisabled("Select LCARS to use UI sounds.");
        else if (AppearancePreferences.Current.UiLcarsSoundsMuted) UiGui.TextDisabled("Turn on UI sounds to preview.");
    }

    public void Dispose() { uiSounds?.Dispose(); shapedText.Dispose(); fonts?.Dispose(); text?.Dispose(); }
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
        var config = AppearancePreferences.Current;
        var changed = false;
        var compactVisibleOnMainWindow = config.UiCompactVisibleOnMainWindow;
        var transparencyVisibleOnMainWindow = config.UiTransparencyVisibleOnMainWindow;
        var languageVisibleOnMainWindow = config.UiLanguageVisibleOnMainWindow;
        var transparencyEnabled = config.UiTransparencyEnabled;
        var autoFade = config.UiAutoFade;
        var opacity = Math.Clamp(config.UiWindowOpacityPercent, 10, 100);
        var fadedOpacity = Math.Clamp(config.UiFadedOpacityPercent, 10, 100);
        var delay = float.IsFinite(config.UiUnfocusedDelaySeconds) ? Math.Max(0, config.UiUnfocusedDelaySeconds) : 10;
        var scale = MaterialTheme.Metrics.Scale;
        var padding = config.Compact ? 12 : 20;
        float minimum;
        using (MaterialControls.Push(PublicPresentation.Controls()))
        {
            var fieldLabelWidth = new[] { "Background", "Controls", "Text", "LCARS bar 1", "LCARS bar 2", "LCARS bar 3",
                "Wave colour 1", "Wave colour 2", "Wave colour 3" }.Max(label => MaterialText.Measure(UiText.T(label)).X);
            var editorWidth = MathF.Ceiling(Math.Max(100 * scale, ImGui.CalcTextSize("#FFFFFF").X + 2 * ImGui.GetStyle().FramePadding.X)
                + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);
            var resetWidth = MaterialText.Measure(UiText.T("Appearance reset")).X + 2 * ImGui.GetStyle().FramePadding.X;
            minimum = Math.Max((config.Compact ? 298 : 316) * scale,
                fieldLabelWidth + editorWidth + resetWidth + 3 * ImGui.GetStyle().ItemSpacing.X);
            minimum = Math.Max(minimum, new[] { "Animation speed (0.1–10x; default 1x)", "Reset colours", "Reset wave colours",
                "Theme & language", "Main window controls", "Window opacity", "When unfocused", "Unfocused delay (seconds)",
                "Unfocused opacity (%)" }.Max(label => MaterialText.Measure(UiText.T(label)).X) + 2 * ImGui.GetStyle().FramePadding.X);
            var metrics = MaterialControls.Metrics;
            var languageWidth = MaterialText.Measure(Languages.LabelFor(appliedLanguage, "Select...")).X
                + metrics.Height + 3 * metrics.Gap + Math.Min(metrics.IconSize, metrics.Height);
            minimum = Math.Max(minimum, languageWidth);
        }
        minimum = Math.Max(minimum, new[] { "Compact mode", "Compact visible on main window", "Transparency visible on main window",
            "Language visible on main window", "Auto-fade when unfocused" }.Max(label => UiGui.CheckboxWidth(label))) + 2 * padding * scale;

        if ((ImGuiP.GetCurrentWindow().Flags & ImGuiWindowFlags.AlwaysAutoResize) != 0)
        {
            // Reserve the full padded card width so native popup auto-fit retains both insets after settling.
            var cursor = ImGui.GetCursorPos();
            ImGui.Dummy(new Vector2(MathF.Ceiling(minimum), 0));
            ImGui.SetCursorPos(cursor);
        }
        MaterialSettings.Columns("public-appearance-groups", minimum, () =>
        {
            PublicPresentation.Panel(() =>
            {
                MaterialSkin.Heading(UiText.T("Theme & language"));
                DrawSelector("settingsAppearance");
                DrawLcarsSounds();
            }, padding);
            ImGui.Spacing();
            PublicPresentation.Panel(() =>
            {
                MaterialSkin.Heading(UiText.T("Main window controls"), 1);
                DrawCompact("Compact mode");
                changed |= UiGui.Checkbox(UiText.T("Compact visible on main window") + "###UiCompactVisibleOnMainWindowSettings", ref compactVisibleOnMainWindow);
                changed |= UiGui.Checkbox(UiText.T("Transparency visible on main window") + "###UiTransparencyVisibleOnMainWindowSettings", ref transparencyVisibleOnMainWindow);
                changed |= UiGui.Checkbox(UiText.T("Language visible on main window") + "###UiLanguageVisibleOnMainWindowSettings", ref languageVisibleOnMainWindow);
            }, padding);
        }, () =>
        {
            PublicPresentation.Panel(() =>
            {
                MaterialSkin.Heading(UiText.T("Window opacity"), 1);
                changed |= UiGui.Checkbox(UiText.T("Transparency") + "###UiTransparencyEnabledSettings", ref transparencyEnabled);
                ImGui.BeginDisabled(!transparencyEnabled);
                try
                {
                    ImGui.SetNextItemWidth(180 * scale);
                    changed |= UiGui.SliderInt(UiText.T("Opacity (%)") + "###UiWindowOpacityPercentSettings", ref opacity, 10, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp);
                }
                finally { ImGui.EndDisabled(); }
            }, padding);
            ImGui.Spacing();
            PublicPresentation.Panel(() =>
            {
                MaterialSkin.Heading(UiText.T("When unfocused"), 1);
                changed |= UiGui.Checkbox(UiText.T("Auto-fade when unfocused") + "###UiAutoFadeSettings", ref autoFade);
                ImGui.BeginDisabled(!transparencyEnabled);
                try
                {
                    ImGui.SetNextItemWidth(180 * scale);
                    changed |= UiGui.SliderInt(UiText.T("Unfocused opacity (%)") + "###UiFadedOpacityPercentSettings", ref fadedOpacity, 10, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp);
                    ImGui.BeginDisabled(!autoFade);
                    try
                    {
                        ImGui.SetNextItemWidth(180 * scale);
                        if (UiGui.InputFloat(UiText.T("Unfocused delay (seconds)") + "###UiUnfocusedDelaySecondsSettings", ref delay))
                        { delay = float.IsFinite(delay) ? Math.Max(0, delay) : 10; changed = true; }
                    }
                    finally { ImGui.EndDisabled(); }
                }
                finally { ImGui.EndDisabled(); }
            }, padding);
        }, divider: false);
        if (changed) AppearancePreferences.Save(AppearancePreferences.Current with { UiCompactVisibleOnMainWindow = compactVisibleOnMainWindow, UiTransparencyVisibleOnMainWindow = transparencyVisibleOnMainWindow, UiLanguageVisibleOnMainWindow = languageVisibleOnMainWindow, UiTransparencyEnabled = transparencyEnabled, UiAutoFade = autoFade, UiWindowOpacityPercent = opacity, UiFadedOpacityPercent = fadedOpacity, UiUnfocusedDelaySeconds = delay });
    }

    internal void DrawTransparency()
    {
        var enabled = AppearancePreferences.Current.UiTransparencyEnabled;
        if (UiGui.Checkbox(UiText.T("Transparency") + "###UiTransparencyHeader", ref enabled)) AppearancePreferences.Save(AppearancePreferences.Current with { UiTransparencyEnabled = enabled });
    }

}
