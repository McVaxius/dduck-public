using AethertekUI.Dalamud;
using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using AethertekUI;
using DDuck.PublicShell.Ui;

namespace DDuck.PublicShell;

internal sealed class IntroductionWindow : Window
{
    private readonly MaterialWindowMotion motion = new();
    private readonly AethertekUI.MaterialWindowOpacity windowOpacity = new();
    private const string DiscordUrl = "https://discord.gg/VsXqydsvpu";
    private const string SupportUrl = "https://ko-fi.com/mcvaxius";
    private static Vector4 Accent => MaterialTheme.Current.Colors.Primary;
    private readonly PublicAppearance appearance;
    private bool openAppearanceSection;

    internal void OpenSettings() { openAppearanceSection = true; IsOpen = true; }
    private readonly ISharedImmediateTexture icon;
    private readonly ModuleLoader loader;
    private readonly Action refresh;

    public IntroductionWindow(IDalamudPluginInterface pluginInterface, ITextureProvider textures, ModuleLoader loader, Action refresh, PublicAppearance appearance)
        : base($"Deep Ducking v{BuildInfo.Version}###DDuck.PublicShell.Introduction")
    {
        this.appearance = appearance;
        this.loader = loader;
        this.refresh = refresh;
        Size = new Vector2(1050, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.HorizontalScrollbar;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 380), MaximumSize = new Vector2(float.MaxValue) };
        icon = textures.GetFromFile(Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "icon.png"));
    }

    public override void PreDraw() => motion.Prepare(this, reducedMotion: false, roundedCorners: true);

    public override void PostDraw()
    {
        motion.Restore(this);
        appearance.ApplyWindowOpacity(windowOpacity, WindowName);
    }

    public override void Draw()
    {
        motion.DrawChrome();
        var scale = ImGuiHelpers.GlobalScale;
        var compact = PublicPresentation.Compact;
        UiGui.Title($"Deep Ducking v{BuildInfo.Version}", UiText.F("Deep Ducking v{0}", BuildInfo.Version));
        if (icon.TryGetWrap(out var texture, out _))
        { ImGui.Image(texture.Handle, new Vector2(compact ? 32 : 64) * scale); ImGui.SameLine(); }
        ImGui.BeginGroup();
        using (UiText.Font(compact ? UiFontRole.CompactTitle : UiFontRole.Title))
            UiGui.TextColored(Accent, "D E E P  D U C K I N G");
        if (!compact) UiGui.FTextDisabled($"By McVaxius  |  Version {BuildInfo.Version}");
        ImGui.EndGroup();
        if (AppearancePreferences.Current.UiLanguageVisibleOnMainWindow)
        { UiGui.SameLineIfFits(210 * scale); appearance.DrawLanguage(); }
        if (AppearancePreferences.Current.UiCompactVisibleOnMainWindow)
        { UiGui.SameLineIfFits(UiGui.CheckboxWidth("C")); appearance.DrawCompact(); }
        UiGui.SameLineIfFits(UiGui.CheckboxWidth("Transparency"));
        appearance.DrawTransparency();
        MaterialStatus.Badge(UiText.T("Public access host"), cornerRadius: 4);
        ImGui.Spacing();

        if (compact)
        {
            UiGui.TextWrapped("Deep Dungeon automation for FINAL FANTASY XIV through a separately supplied module.");
            DrawAccessButton();
            DrawSupportButtons();
            if (UiGui.CollapsingHeader("About and access installation")) DrawInformation();
        }
        else
        {
            Panel(() =>
            {
                Card("About", "Deep Dungeon automation", "Deep Ducking provides Deep Dungeon automation through a separately supplied module. This public plugin provides the introduction and loads authenticated DDuck modules.");
                ImGui.Separator();
                DrawAccessRow();
                DrawInstallation();
            });
            ImGui.Spacing();
            Panel(() => Card("Access", "Join the community", "Visit The Dumpster Fire community on Discord for plugin discussion and to arrange access with McVaxius. Support on Ko-fi is welcome; module access is granted privately."));
            ImGui.Spacing();
            Panel(() =>
            {
                using (UiText.Font(UiFontRole.PaneHeading)) UiGui.TextUnformatted("COMMUNITY AND SUPPORT");
                UiGui.TextDisabled("Get help, share feedback, or support the project.");
                DrawSupportButtons(24 * scale);
            });
        }
        ImGui.Spacing();
        if (openAppearanceSection) { ImGui.SetNextItemOpen(true); openAppearanceSection = false; }
        if (MaterialText.CollapsingHeader(UiText.T("Window appearance") + "###WindowAppearanceSection"))
            appearance.DrawWindowAppearance();
        if (loader.Failed)
        {
            var visibleRight = ImGuiP.GetCurrentWindow().InnerClipRect.Max.X;
            ImGui.PushTextWrapPos(visibleRight - ImGui.GetWindowPos().X + ImGui.GetScrollX() - 2 * scale);
            MaterialText.TextColored(new Vector4(1f, .65f, .25f, 1), UiText.T("Access could not initialize. Check /xllog for [Access] details."));
            ImGui.PopTextWrapPos();
        }
    }

    private void DrawInformation()
    {
        Card("About", "Deep Dungeon automation", "Deep Ducking provides Deep Dungeon automation through a separately supplied module. This public plugin provides the introduction and loads authenticated DDuck modules.");
        Card("Access", "Join the community", "Visit The Dumpster Fire community on Discord for plugin discussion and to arrange access with McVaxius. Support on Ko-fi is welcome; module access is granted privately.");
        UiGui.FTextDisabled($"By McVaxius  |  Version {BuildInfo.Version}");
        DrawInstallation();
    }

    private static void DrawInstallation()
    {
        UiGui.TextColored(Accent, "INSTALL YOUR ACCESS UPDATE");
        UiGui.TextWrapped("Keep this public plugin installed and enabled. Follow the installation instructions supplied with your DDuck access package. The package manager must support DDuck access updates.");
        UiGui.TextWrapped("After installing an update, click Check installed access or restart DDuck. Open /dduck or /dd to use the module.");
    }

    private void DrawAccessRow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - 24 * scale);
        var gap = 24 * scale;
        var buttonWidth = Math.Max(344 * scale,
            MathF.Ceiling(MaterialText.Measure(UiText.T("Check installed access")).X + 68 * scale));
        var beside = width >= buttonWidth + gap + 280 * scale;
        var informationWidth = beside ? width - buttonWidth - gap : width;
        var origin = ImGui.GetCursorPos();
        var textOrigin = ImGui.GetCursorScreenPos();
        var text = UiText.T("Install your access download, then check installed access.");
        var padding = 16 * scale;
        var iconSize = 28 * scale;
        var textWidth = Math.Max(1, informationWidth - padding * 2 - iconSize - 12 * scale);
        var height = Math.Max(68 * scale, MaterialText.Measure(text, false, textWidth).Y + padding * 2);
        var list = ImGui.GetWindowDrawList();
        list.AddRectFilled(textOrigin, textOrigin + new Vector2(informationWidth, height),
            MaterialCanvas.Color(MaterialTheme.Current.Colors.SurfaceContainerHigh), 6 * scale);
        MaterialIcons.Draw(MaterialIcon.Info, textOrigin + new Vector2(padding, (height - iconSize) * .5f),
            iconSize, MaterialTheme.Current.Colors.OnSurfaceVariant);
        MaterialText.AddText(list, ImGui.GetFont(), ImGui.GetFontSize(),
            textOrigin + new Vector2(padding + iconSize + 12 * scale, (height - MaterialText.Measure(text, false, textWidth).Y) * .5f),
            MaterialCanvas.Color(MaterialTheme.Current.Colors.OnSurface), text, textWidth);
        ImGui.Dummy(new Vector2(informationWidth, height));
        if (beside)
        {
            ImGui.SetCursorPos(origin + new Vector2(informationWidth + gap, (height - 68 * scale) * .5f));
            DrawAccessButton(width: buttonWidth);
            ImGui.SetCursorPos(origin + new Vector2(0, height + ImGui.GetStyle().ItemSpacing.Y));
        }
        else DrawAccessButton(24 * scale);
    }

    private void DrawAccessButton(float rightInset = 0, float? width = null)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, MaterialTheme.Current.Colors.Secondary);
        if (UiGui.Tile("Check installed access", "Check installed access", null, MaterialIcon.Search,
            new Vector2(width ?? Math.Max(1, ImGui.GetContentRegionAvail().X - rightInset), (PublicPresentation.Compact ? 42 : 68) * ImGuiHelpers.GlobalScale))) refresh();
        ImGui.PopStyleColor();
    }

    private static void DrawSupportButtons(float rightInset = 0)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - rightInset);
        var fit = UiGui.ButtonWidth("Open Discord") + UiGui.ButtonWidth("Support on Ko-fi") + 96 * scale <= width;
        var size = new Vector2(fit ? (width - ImGui.GetStyle().ItemSpacing.X) * .5f : width,
            (PublicPresentation.Compact ? 38 : 68) * scale);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.26f, .34f, .83f, 1));
        if (UiGui.Tile("Open Discord", "Open Discord", null, MaterialIcon.Discord, size)) Util.OpenLink(DiscordUrl);
        ImGui.PopStyleColor();
        if (fit) ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.95f, .32f, .44f, 1));
        if (UiGui.Tile("Support on Ko-fi", "Support on Ko-fi", null, MaterialIcon.KoFi, size)) Util.OpenLink(SupportUrl);
        ImGui.PopStyleColor();
    }

    private static void Card(string id, string title, string body)
    {
        ImGui.PushID(id);
        using (UiText.Font(PublicPresentation.Compact ? UiFontRole.CompactPaneHeading : UiFontRole.PaneHeading))
            UiGui.TextColored(Accent, title);
        UiGui.TextWrapped(body);
        ImGui.Spacing();
        ImGui.PopID();
    }

    private static void Panel(Action draw)
    {
        // Presentation only: no child/table scope is introduced around the original actions.
        var scale = ImGuiHelpers.GlobalScale; var padding = 24 * scale;
        var owner = ImGuiP.GetCurrentWindow();
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, Math.Min(ImGui.GetContentRegionAvail().X, owner.InnerClipRect.Max.X - origin.X));
        var savedWork = owner.WorkRect;
        var savedContent = owner.ContentRegionRect;
        var cursor = ImGui.GetCursorPos(); var list = ImGui.GetWindowDrawList();
        list.ChannelsSplit(2); list.ChannelsSetCurrent(1);
        ImGui.BeginGroup();
        ImGui.SetCursorPos(cursor + new Vector2(0, padding));
        ImGui.Indent(padding);
        var work = savedWork;
        work.Min.X = origin.X + padding;
        work.Max.X = origin.X + width - padding;
        owner.WorkRect = work;
        var content = savedContent;
        content.Min.X = work.Min.X;
        content.Max.X = work.Max.X;
        owner.ContentRegionRect = content;
        ImGui.PushTextWrapPos(cursor.X + width - padding);
        draw();
        ImGui.PopTextWrapPos();
        owner.WorkRect = savedWork;
        owner.ContentRegionRect = savedContent;
        ImGui.Dummy(new Vector2(Math.Max(1, width - padding * 2), padding));
        ImGui.Unindent(padding);
        ImGui.EndGroup();
        var bottom = Math.Max(origin.Y + padding * 2, ImGui.GetItemRectMax().Y);
        list.ChannelsSetCurrent(0);
        PublicPresentation.Surface(origin, new Vector2(origin.X + width, bottom));
        list.ChannelsMerge();
    }
}
