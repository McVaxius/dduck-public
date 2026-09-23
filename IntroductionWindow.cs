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

namespace DDuck.PublicShell;

internal sealed class IntroductionWindow : Window
{
    private const string DiscordUrl = "https://discord.gg/VsXqydsvpu";
    private const string SupportUrl = "https://ko-fi.com/mcvaxius";
    private static readonly Vector4 Accent = new(0.65f, 0.57f, 1f, 1f);
    private readonly ISharedImmediateTexture icon;
    private readonly ModuleLoader loader;
    private readonly Action refresh;

    public IntroductionWindow(IDalamudPluginInterface pluginInterface, ITextureProvider textures, ModuleLoader loader, Action refresh)
        : base($"Deep Ducking v{BuildInfo.Version}###DDuck.PublicShell.Introduction")
    {
        this.loader = loader;
        this.refresh = refresh;
        Size = new Vector2(620, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 380), MaximumSize = new Vector2(float.MaxValue) };
        icon = textures.GetFromFile(Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "icon.png"));
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(12, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * scale);
        try
        {
            if (icon.TryGetWrap(out var texture, out _))
            {
                ImGui.Image(texture.Handle, new Vector2(64) * scale);
                ImGui.SameLine();
            }
            ImGui.BeginGroup();
            ImGui.TextColored(Accent, "D E E P  D U C K I N G");
            ImGui.TextUnformatted("By McVaxius");
            ImGui.TextUnformatted($"Public plugin v{BuildInfo.Version}");
            ImGui.EndGroup();
            ImGui.Separator();
            Card("About", "Deep Dungeon automation", "Deep Ducking provides Deep Dungeon automation through a separately supplied module. This public plugin provides the introduction and loads authenticated DDuck modules.");
            Card("Access", "Join the community", "Visit The Dumpster Fire community on Discord for plugin discussion and to arrange access with McVaxius. Support on Ko-fi is welcome; module access is granted privately.");
            ImGui.TextColored(Accent, "COMMUNITY AND SUPPORT");
            if (ImGui.Button("Open Discord")) Util.OpenLink(DiscordUrl);
            ImGui.SameLine();
            if (ImGui.Button("Support on Ko-fi")) Util.OpenLink(SupportUrl);
            ImGui.Separator();
            ImGui.TextColored(Accent, "INSTALL YOUR ACCESS UPDATE");
            ImGui.TextWrapped("Keep this public plugin installed and enabled. Follow the installation instructions supplied with your DDuck access package. The package manager must support DDuck access updates.");
            ImGui.TextWrapped("After installing an update, click Check installed access or restart DDuck. Open /dduck or /dd to use the module.");
            if (loader.Failed) ImGui.TextColored(new Vector4(1f, 0.65f, 0.25f, 1f), "Access could not initialize. Check /xllog for [Access] details.");
            if (ImGui.Button("Check installed access")) refresh();
        }
        finally { ImGui.PopStyleVar(3); }
    }

    private static void Card(string id, string title, string body)
    {
        ImGui.PushID(id);
        ImGui.TextColored(Accent, title);
        ImGui.TextWrapped(body);
        ImGui.Spacing();
        ImGui.PopID();
    }
}
