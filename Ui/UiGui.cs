using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DDuck.PublicShell.Ui;

// Native widgets receive their original English labels/IDs. Only their visible label is painted in the selected locale.
// This also preserves English-derived helper IDs and existing saved window identities.
internal static class UiGui
{
    internal static float ButtonWidth(string label) => MaterialText.Measure(UiText.T(label.Split("##", 2)[0])).X + 2 * ImGui.GetStyle().FramePadding.X;
    private static bool ToolbarSwitch(string label) => label is "Enabled" or "DTR ON" or "Krangle";
    internal static float CheckboxWidth(string label) => ToolbarSwitch(label)
        ? MaterialText.Measure(UiText.T(label)).X + 75 * MaterialTheme.Metrics.Scale
        : ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + MaterialText.Measure(UiText.T(label.Split("##", 2)[0])).X;
    internal static void SameLineIfFits(float width)
    {
        var edge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width <= edge) ImGui.SameLine();
    }
    internal static bool BeginCombo(string label, string preview, bool translatePreview = true)
    {
        var text = translatePreview ? UiText.T(preview) : preview;
        BeginField(label, Math.Max(EditorWidth, MaterialText.Measure(text).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight()));
        using var height = MaterialText.PushLineHeight(text);
        bool open;
        try { open = MaterialText.BeginCombo("", text); }
        catch { ImGui.PopID(); throw; }
        if (!open) ImGui.PopID();
        return open;
    }
    internal static void EndCombo() { ImGui.EndCombo(); ImGui.PopID(); }
    internal static bool BeginTabItem(string label, ref bool open, ImGuiTabItemFlags flags)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label));
        var style = ImGui.GetStyle(); var delta = MaterialText.Measure(UiText.T(label)).X - MaterialText.Measure(label).X;
        var padding = new Vector2(48 * MaterialTheme.Metrics.Scale + Math.Max(0, delta * .5f), (PublicPresentation.Compact ? 10 : 14) * MaterialTheme.Metrics.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        ImGui.PushStyleColor(ImGuiCol.TabActive, MaterialTheme.Current.Colors.Primary);
        var selected = ImGui.BeginTabItem(label, ref open, flags); ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        var color = selected ? MaterialTheme.Current.Colors.Primary : style.Colors[(int)(ImGui.IsItemHovered() ? ImGuiCol.TabHovered : ImGuiCol.Tab)];
        Label(label, ImGui.GetItemRectMin() + padding, color,
            selected ? MaterialTheme.Current.Colors.OnPrimary : style.Colors[(int)ImGuiCol.Text], repaint: true);
        return selected;
    }
    internal static bool Combo(string label, ref int index, string zeroSeparated)
    {
        var options = zeroSeparated.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return Combo(label, ref index, options, options.Length);
    }
    internal static bool DragFloat(string label, ref float value, float speed, float min, float max, string format)
    {
        BeginField(label, EditorWidth);
        var changed = ImGui.DragFloat("", ref value, speed, min, max, format);
        if (!ImGui.IsItemActive())
        {
            var rectMin = ImGui.GetItemRectMin();
            var rectMax = ImGui.GetItemRectMax();
            var style = ImGui.GetStyle(); var dl = ImGui.GetWindowDrawList();
            var background = style.Colors[(int)(ImGui.IsItemHovered() ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg)];
            var translated = value.ToString("0.0", UiText.Current.Culture);
            dl.PushClipRect(rectMin, rectMax, true);
            dl.AddRectFilled(rectMin + new Vector2(3, 1), rectMax - new Vector2(3, 1), MaterialCanvas.Color(background));
            MaterialText.AddText(dl, rectMin + (rectMax - rectMin - MaterialText.Measure(translated)) * .5f, MaterialCanvas.Color(style.Colors[(int)ImGuiCol.Text]), translated);
            dl.PopClipRect();
        }
        ImGui.PopID(); return changed;
    }
    internal static bool ColorEdit3(string label, ref Vector3 value)
    {
        BeginField(label, EditorWidth * 3 + ImGui.GetStyle().ItemInnerSpacing.X * 3 + ImGui.GetFrameHeight());
        try { return ImGui.ColorEdit3("", ref value); } finally { ImGui.PopID(); }
    }
    internal static bool Selectable(string raw, bool selected, string? display = null)
    {
        var translated = display ?? UiText.T(raw.Split("##", 2)[0]);
        using var height = MaterialText.PushLineHeight(translated);
        if (MaterialText.RequiresShaping(translated))
            return MaterialText.Selectable(raw, selected, size: new Vector2(Math.Max(ImGui.GetContentRegionAvail().X, MaterialText.Measure(translated).X), MaterialText.Measure(translated).Y), display: translated);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Selectable(raw, selected, ImGuiSelectableFlags.None, new Vector2(Math.Max(ImGui.GetContentRegionAvail().X, MaterialText.Measure(translated).X), MaterialText.RequiresShaping(translated) ? MaterialText.Measure(translated).Y : 0));
        ImGui.PopStyleColor();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax(); var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true); MaterialText.AddText(dl, min, MaterialCanvas.Color(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), translated); dl.PopClipRect();
        return clicked;
    }
    private static float EditorWidth => Math.Max(80 * MaterialTheme.Metrics.Scale,
        MaterialText.Measure("-0000.000").X + ImGui.GetStyle().FramePadding.X * 2);
    private static void BeginField(string label, float minimum)
    {
        var requested = ImGui.CalcItemWidth();
        var visible = label.Split("##", 2)[0];
        if (visible.Length != 0) MaterialText.Text(UiText.T(visible));
        ImGui.SetNextItemWidth(MaterialLayout.FitNextItemWidth(requested, MathF.Ceiling(minimum)));
        ImGuiP.PushOverrideID(ImGui.GetID(label));
    }
    internal static void TextDisabled(string text)
    {
        ImGui.PushTextWrapPos(0); MaterialText.TextDisabled(UiText.T(text)); ImGui.PopTextWrapPos();
    }
    internal static bool InputText(string label,ref string value,int length,ImGuiInputTextFlags flags=ImGuiInputTextFlags.None)
    {
        BeginField(label, EditorWidth);
        using var height = MaterialText.PushLineHeight(value);
        try { return MaterialShapedInput.SingleLine("", string.Empty, ref value, length, flags); } finally { ImGui.PopID(); }
    }
    internal static bool RadioButton(string label,bool active)
    {
        var gap = ImGui.GetStyle().ItemInnerSpacing;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(Math.Max(0, gap.X + MaterialText.Measure(UiText.T(label)).X - MaterialText.Measure(label).X), gap.Y));
        using var height = MaterialText.PushLineHeight(UiText.T(label));
        var changed=ImGui.RadioButton(label,active);
        ImGui.PopStyleVar();
        Label(label,ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+ImGui.GetStyle().ItemInnerSpacing.X,(ImGui.GetFrameHeight()-MaterialText.Measure(UiText.T(label)).Y)*.5f),MaterialTheme.Current.Colors.Background,MaterialTheme.Current.Colors.OnSurface);
        return changed;
    }
    internal static bool Combo(string label,ref int index,string[] options,int count,string[]? displays=null)
    {
        var preview = index >= 0 && index < count ? displays?[index] ?? UiText.T(options[index]) : string.Empty;
        if (!BeginCombo(label, preview, false)) return false;
        var changed = false;
        for (var option = 0; option < count; option++)
        {
            // Native Combo uses an index scope plus the original option label for each item.
            ImGui.PushID(option);
            var selected = index == option;
            if (Selectable(options[option], selected, displays?[option])) { index = option; changed = true; }
            if (selected) ImGui.SetItemDefaultFocus();
            ImGui.PopID();
        }
        EndCombo();
        return changed;
    }
    internal static void TextUnformatted(string text) => MaterialText.Text(UiText.T(text));
    internal static void TextWrapped(string text) => MaterialText.TextWrapped(UiText.T(text));
    internal static void Text(string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.Text(UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void TextColored(Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0);
        try { MaterialText.TextColored(color, UiText.T(text)); }
        finally { ImGui.PopTextWrapPos(); }
    }
    internal static void SetTooltip(string text) => MaterialText.SetTooltip(UiText.T(text));
    private static void Label(string original,Vector2 position,Vector4 background,Vector4 foreground,Vector2? clip=null,string? display=null,ImDrawListPtr? drawList=null,bool repaint=false,Vector2? clipMin=null)
    {
        var visible=original.Split("##",2)[0];
        var translated=display ?? UiText.T(visible);
        using var height = MaterialText.PushLineHeight(translated);
        if(translated==visible && !repaint) return;
        var dl=drawList ?? ImGui.GetWindowDrawList();
        var width=Math.Max(MaterialText.Measure(visible).X,MaterialText.Measure(translated).X);
        if(clip is { } max) dl.PushClipRect(clipMin ?? position,max,true);
        dl.AddRectFilled(position,position+new Vector2(width,Math.Max(ImGui.GetTextLineHeight(),MaterialText.Measure(translated).Y)),ImGui.ColorConvertFloat4ToU32(background));
        foreground.W*=ImGui.GetStyle().Alpha;
        try { MaterialText.AddText(dl, position,ImGui.ColorConvertFloat4ToU32(foreground),translated); }
        finally { if(clip.HasValue) dl.PopClipRect(); }
    }
    internal static bool Button(string label,string? display=null)
    {
        var translated=display ?? UiText.T(label.Split("##",2)[0]);
        var toolbar = MaterialControls.Context != MaterialControlContext.Dense && ImGui.GetStyle().FramePadding.Y > 0;
        using var height = MaterialText.PushLineHeight(translated);
        using var controls = toolbar ? MaterialControls.Push(MaterialControlContext.Toolbar) : default;
        var buttonHeight = toolbar
            ? MaterialControlMetrics.Measure(MaterialTheme.Metrics, Math.Max(ImGui.GetTextLineHeight(), MaterialText.Measure(translated).Y), MaterialControlContext.Toolbar).Height
            : ImGui.GetFrameHeight();
        var width=MaterialText.Measure(translated).X+2*ImGui.GetStyle().FramePadding.X;
        width = MaterialLayout.FitNextItemWidth(width, MathF.Ceiling(width));
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var clicked=ImGui.Button(label,new Vector2(width,buttonHeight));
        ImGui.PopStyleColor();
        var min=ImGui.GetItemRectMin(); var max=ImGui.GetItemRectMax();
        foreground.W*=ImGui.GetStyle().Alpha;
        ImGui.GetWindowDrawList().PushClipRect(min,max,true);
        MaterialText.AddText(ImGui.GetWindowDrawList(), min+(max-min-MaterialText.Measure(translated))*.5f,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        ImGui.GetWindowDrawList().PopClipRect();
        return clicked;
    }
    internal static bool Button(string label, Vector2 pixels, string? display = null)
    {
        var translated = display ?? UiText.T(label.Split("##", 2)[0]);
        var toolbar = pixels.Y <= 0 && MaterialControls.Context != MaterialControlContext.Dense && ImGui.GetStyle().FramePadding.Y > 0;
        using var height = MaterialText.PushLineHeight(translated);
        using var controls = toolbar ? MaterialControls.Push(MaterialControlContext.Toolbar) : default;
        var buttonHeight = toolbar
            ? MaterialControlMetrics.Measure(MaterialTheme.Metrics, Math.Max(ImGui.GetTextLineHeight(), MaterialText.Measure(translated).Y), MaterialControlContext.Toolbar).Height
            : ImGui.GetFrameHeight();
        pixels.X = MaterialLayout.FitNextItemWidth(pixels.X, MathF.Ceiling(MaterialText.Measure(translated).X + ImGui.GetStyle().FramePadding.X * 2));
        var color = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Button(label, new Vector2(pixels.X, Math.Max(pixels.Y, buttonHeight)));
        ImGui.PopStyleColor();
        color.W *= ImGui.GetStyle().Alpha;
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true);
        MaterialText.AddText(dl, min + (max - min - MaterialText.Measure(translated)) * .5f, MaterialCanvas.Color(color), translated);
        dl.PopClipRect();
        return clicked;
    }
    internal static bool SmallButton(string label,string? display=null)
    {
        // Native small buttons use the same ID and behavior with zero vertical padding.
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(ImGui.GetStyle().FramePadding.X,0));
        var clicked=Button(label,display);
        ImGui.PopStyleVar();
        return clicked;
    }
    internal static bool Checkbox(string label,ref bool value, string? display = null)
    {
        if (ToolbarSwitch(label)) return DrawToolbarSwitch(label, ref value);
        var visible=label.Split("##",2)[0];
        var translated=display ?? UiText.T(visible);
        using var height = MaterialText.PushLineHeight(translated);
        var foreground=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var gap=ImGui.GetStyle().ItemInnerSpacing;
        // Native Checkbox sizes its hit area from the original label. Adjust that size for the
        // translated ink while keeping the native widget and its original ID.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing,new Vector2(Math.Max(0,gap.X+MaterialText.Measure(translated).X-MaterialText.Measure(visible).X),gap.Y));
        ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Zero);
        var changed=ImGui.Checkbox(label,ref value);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        var p=ImGui.GetItemRectMin()+new Vector2(ImGui.GetFrameHeight()+gap.X,(ImGui.GetFrameHeight()-MaterialText.Measure(translated).Y)*.5f);
        foreground.W*=ImGui.GetStyle().Alpha;
        MaterialText.AddText(ImGui.GetWindowDrawList(), p,ImGui.ColorConvertFloat4ToU32(foreground),translated);
        return changed;
    }
    private static bool DrawToolbarSwitch(string label, ref bool value)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label));
        var scale = MaterialTheme.Metrics.Scale; var style = ImGui.GetStyle();
        var width = CheckboxWidth(label);
        // Keep the original native Checkbox ID, keyboard navigation and hit area.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(Math.Max(0, width - ImGui.GetFrameHeight() - MaterialText.Measure(label).X), style.ItemInnerSpacing.Y));
        var changed = ImGui.Checkbox(label, ref value);
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        var colors = MaterialTheme.Current.Colors; var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, MaterialCanvas.Color(ImGui.IsItemHovered() ? colors.SurfaceContainerHighest : colors.SurfaceContainer), 4 * scale);
        dl.AddRect(min, max, MaterialCanvas.Color(ImGui.IsItemFocused() ? colors.Primary : colors.OutlineVariant), 4 * scale);
        var text = UiText.T(label);
        MaterialText.AddText(dl, new Vector2(min.X + 10 * scale, min.Y + (max.Y - min.Y - MaterialText.Measure(text).Y) * .5f), MaterialCanvas.Color(colors.OnSurface), text);
        var trackMin = new Vector2(max.X - 51 * scale, min.Y + (max.Y - min.Y - 22 * scale) * .5f);
        var trackMax = trackMin + new Vector2(41, 22) * scale;
        dl.AddRectFilled(trackMin, trackMax, MaterialCanvas.Color(value ? colors.Primary : colors.SurfaceVariant), 11 * scale);
        dl.AddCircleFilled(new Vector2(value ? trackMax.X - 11 * scale : trackMin.X + 11 * scale, trackMin.Y + 11 * scale), 8.5f * scale, MaterialCanvas.Color(colors.OnSurface), 20);
        return changed;
    }
    internal static void Title(string original,string translated)
        => TitleWithButtons(original, translated, null);

    internal static void ReserveTitleSpace(Window owner, string visible, float minimumWidth)
    {
        var style = ImGui.GetStyle();
        var fontSize = ImGui.GetFontSize();
        var collapse = (owner.Flags & (ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.Modal)) == 0
            && style.WindowMenuButtonPosition != ImGuiDir.None;
        var controls = AdditionalTitleButtonWidth(owner, fontSize)
            + ((owner.ShowCloseButton ? 1 : 0) + (collapse ? 1 : 0)) * (fontSize + style.ItemInnerSpacing.X);
        var required = (MaterialText.Measure(visible).X + controls + style.FramePadding.X * 2 + style.ItemInnerSpacing.X)
            / ImGui.GetIO().FontGlobalScale;
        var bounds = owner.SizeConstraints ?? new WindowSizeConstraints();
        bounds.MinimumSize = new(Math.Max(minimumWidth, required), bounds.MinimumSize.Y);
        owner.SizeConstraints = bounds;
    }

    private static float AdditionalTitleButtonWidth(Window? owner, float fontSize)
    {
        if (owner is null) return 0;
        var count = owner.TitleBarButtons.Count(button => !owner.IsClickthrough || button.AvailableClickthrough);
        if (owner.AllowPinning || owner.AllowClickthrough || owner.AllowBackgroundBlur) count++;
        return count * (fontSize + ImGui.GetStyle().ItemInnerSpacing.X);
    }

    internal static void TitleWithButtons(string original,string translated, Window? owner)
    {
        var s=ImGui.GetStyle(); var size=ImGui.GetFontSize();var height=ImGui.GetFrameHeight();
        var flags=ImGuiP.GetCurrentWindow().Flags;
        var collapseOnLeft=(flags & (ImGuiWindowFlags.NoCollapse|ImGuiWindowFlags.Modal))==0 && s.WindowMenuButtonPosition==ImGuiDir.Left;
        var position=ImGui.GetWindowPos()+new Vector2(s.FramePadding.X+(collapseOnLeft?size+s.ItemInnerSpacing.X:0),s.FramePadding.Y);
        var originalWidth=MaterialText.Measure(original).X;
        using var font=UiText.Font(UiFontRole.Body);
        var translatedWidth=MaterialText.Measure(translated).X*size/ImGui.GetFontSize();
        var dl=ImGui.GetWindowDrawList();
        var reserved = owner is null ? height * 1.5f : s.FramePadding.X * 2
            + (owner.ShowCloseButton ? size : 0) + AdditionalTitleButtonWidth(owner, size);
        if (owner is not null && (flags & ImGuiWindowFlags.NoCollapse) == 0 && s.WindowMenuButtonPosition == ImGuiDir.Right)
            reserved += size + s.ItemInnerSpacing.X;
        dl.PushClipRect(position,ImGui.GetWindowPos()+new Vector2(Math.Max(0, ImGui.GetWindowSize().X - reserved),height),false);
        var bg=s.Colors[(int)(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)?ImGuiCol.TitleBgActive:ImGuiCol.TitleBg)];
        dl.AddRectFilled(position,position+new Vector2(Math.Max(originalWidth,translatedWidth),height-s.FramePadding.Y),ImGui.ColorConvertFloat4ToU32(bg));
        MaterialText.AddText(dl, ImGui.GetFont(),size,position,ImGui.ColorConvertFloat4ToU32(s.Colors[(int)ImGuiCol.Text]),translated);
        dl.PopClipRect();
    }

    internal static Vector2 CalcTextSize(string text) => MaterialText.Measure(UiText.T(text.Split("##", 2)[0]));
    internal static bool BeginCombo(string label, string preview, ImGuiComboFlags flags)
    {
        var text = UiText.T(preview);
        BeginField(label, Math.Max(EditorWidth, MaterialText.Measure(text).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetFrameHeight()));
        using var height = MaterialText.PushLineHeight(text);
        bool open;
        try { open = MaterialText.BeginCombo("", text, flags); }
        catch { ImGui.PopID(); throw; }
        if (!open) ImGui.PopID();
        return open;
    }
    internal static bool Selectable(string raw, bool selected, ImGuiSelectableFlags flags, Vector2 size)
    {
        var text = UiText.T(raw.Split("##", 2)[0]);
        using var height = MaterialText.PushLineHeight(text);
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Selectable(raw, selected, flags, new Vector2(Math.Max(size.X, MaterialText.Measure(text).X), MaterialText.RequiresShaping(text) ? Math.Max(size.Y, MaterialText.Measure(text).Y) : size.Y));
        ImGui.PopStyleColor();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax(); var list = ImGui.GetWindowDrawList();
        list.PushClipRect(min, max, true);
        MaterialText.AddText(list, min + new Vector2(8 * MaterialTheme.Metrics.Scale, (max.Y - min.Y - MaterialText.Measure(text).Y) * .5f), MaterialCanvas.Color(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), text);
        list.PopClipRect(); return clicked;
    }
    internal static bool InputInt(string label, ref int value, int step = 0, int stepFast = 0)
    {
        BeginField(label, EditorWidth + (step > 0 ? 2 * (ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X) : 0));
        try { return ImGui.InputInt("", ref value, step, stepFast); } finally { ImGui.PopID(); }
    }
    internal static bool SliderInt(string label, ref int value, int min, int max, string format = "%d", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
    { BeginField(label, EditorWidth); try { return ImGui.SliderInt("", ref value, min, max, format, flags); } finally { ImGui.PopID(); } }
    internal static bool InputFloat(string label, ref float value)
    { BeginField(label, EditorWidth); try { return ImGui.InputFloat("", ref value); } finally { ImGui.PopID(); } }
    internal static bool SliderFloat(string label, ref float value, float min, float max, string format = "%.3f")
    { BeginField(label, EditorWidth); try { return ImGui.SliderFloat("", ref value, min, max, format); } finally { ImGui.PopID(); } }
    internal static bool InputTextMultiline(string label, ref string value, int length, Vector2 size)
    {
        var visible = label.Split("##", 2)[0];
        if (visible.Length != 0) MaterialText.Text(UiText.T(visible));
        size.X = MaterialLayout.FitNextItemWidth(size.X, MathF.Ceiling(EditorWidth));
        ImGuiP.PushOverrideID(ImGui.GetID(label));
        using var height = MaterialText.PushLineHeight(value);
        try { return MaterialShapedInput.Multiline("", ref value, length, size); } finally { ImGui.PopID(); }
    }
    internal static bool InputTextWithHint(string id, string hint, ref string value, int length)
    {
        var text = UiText.T(hint);
        BeginField(id, Math.Max(EditorWidth, MaterialText.Measure(text).X + ImGui.GetStyle().FramePadding.X * 2));
        using var height = MaterialText.PushLineHeight(value, text);
        try { return MaterialShapedInput.SingleLine("", text, ref value, length); } finally { ImGui.PopID(); }
    }
    internal static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        var display = UiText.T(label.Split("##",2)[0]);
        if (MaterialText.RequiresShaping(display)) return MaterialText.TreeNode(label, flags | ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.NoAutoOpenOnLog, display);
        var open = ImGui.CollapsingHeader(label, flags); TreeLabel(label); return open;
    }
    internal static bool TreeNode(string label)
    { var display = UiText.T(label.Split("##",2)[0]); if (MaterialText.RequiresShaping(display)) return MaterialText.TreeNode(label, display: display); var open = ImGui.TreeNode(label); TreeLabel(label); return open; }
    private static void TreeLabel(string label, string? display = null)
    {
        var position = ImGui.GetItemRectMin() + new Vector2(ImGui.GetTreeNodeToLabelSpacing(), ImGui.GetStyle().FramePadding.Y);
        var style = ImGui.GetStyle();
        var bg = style.Colors[(int)(ImGui.IsItemHovered() ? ImGuiCol.HeaderHovered : ImGuiCol.Header)];
        Label(label, position, bg, style.Colors[(int)ImGuiCol.Text], clip: ImGui.GetItemRectMax(), display: display);
    }
    internal static bool BeginTabItem(string label, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
    {
        using var height = MaterialText.PushLineHeight(UiText.T(label));
        var style = ImGui.GetStyle();
        var display = UiText.T(label);
        var shaped = MaterialText.RequiresShaping(display);
        var delta = shaped ? 0 : Math.Max(0, MaterialText.Measure(display).X - MaterialText.Measure(label).X);
        var padding = style.FramePadding + new Vector2(delta * .5f, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        if (shaped && ((int)ImGui.GetCurrentContext().NextItemData.Flags & 1) == 0)
            ImGui.SetNextItemWidth(Math.Max(ImGui.CalcTextSize(label, true).X, MaterialText.Measure(display).X) + 2 * padding.X);
        var open = ImGui.BeginTabItem(label, flags); ImGui.PopStyleVar();
        var bg = style.Colors[(int)(open ? ImGuiCol.TabActive : ImGui.IsItemHovered() ? ImGuiCol.TabHovered : ImGuiCol.Tab)];
        try { Label(label, ImGui.GetItemRectMin() + new Vector2(padding.X, (ImGui.GetItemRectSize().Y - MaterialText.Measure(UiText.T(label)).Y) * .5f), bg, style.Colors[(int)ImGuiCol.Text], clip: ImGui.GetItemRectMax(), clipMin: shaped ? ImGui.GetItemRectMin() : null); }
        catch { if (open) ImGui.EndTabItem(); throw; }
        return open;
    }
    internal static void TableSetupColumn(string label, ImGuiTableColumnFlags flags = ImGuiTableColumnFlags.None, float width = 0)
        => ImGui.TableSetupColumn(label, flags, width);
    internal static void TableHeader(string label)
    {
        var display = UiText.T(label.Split("##",2)[0]);
        if (MaterialText.RequiresShaping(display)) { MaterialText.TableHeader(label, display); return; }
        ImGui.TableHeader(label);
        Label(label, ImGui.GetItemRectMin() + new Vector2(ImGui.GetStyle().CellPadding.X, 0),
            MaterialTheme.Current.Colors.SurfaceContainerHigh, ImGui.GetStyle().Colors[(int)ImGuiCol.Text], clip: ImGui.GetItemRectMax());
    }
    internal static void TableHeadersRow()
    {
        var height = Enumerable.Range(0, ImGui.TableGetColumnCount()).Select(column => UiText.T(ImGui.TableGetColumnName(column))).Where(MaterialText.RequiresShaping).Select(caption => MaterialText.Measure(caption).Y + 2 * ImGui.GetStyle().CellPadding.Y).DefaultIfEmpty(0).Max();
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, height);
        for (var column = 0; column < ImGui.TableGetColumnCount(); ++column)
        { ImGui.TableSetColumnIndex(column); TableHeader(ImGui.TableGetColumnName(column)); }
    }
    internal static void FTextUnformatted(FormattableString text) => MaterialText.Text(UiText.Interpolated(text));
    internal static void FTextWrapped(FormattableString text) => MaterialText.TextWrapped(UiText.Interpolated(text));
    internal static void FTextDisabled(FormattableString text) => MaterialText.TextDisabled(UiText.Interpolated(text));
    internal static void FTextColored(Vector4 color, FormattableString text) => MaterialText.TextColored(color, UiText.Interpolated(text));
    internal static void FSetTooltip(FormattableString text) => MaterialText.SetTooltip(UiText.Interpolated(text));
    internal static bool FButton(FormattableString label, Vector2? size = null, string? display = null) => Button(label.ToString(), size ?? Vector2.Zero, display ?? UiText.Interpolated(label).Split("##", 2)[0]);
    internal static bool FSmallButton(FormattableString label)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0));
        var clicked = FButton(label); ImGui.PopStyleVar(); return clicked;
    }
    internal static bool FCheckbox(FormattableString label, ref bool value)
    { return Checkbox(label.ToString(), ref value, UiText.Interpolated(label).Split("##", 2)[0]); }
    internal static bool FCollapsingHeader(FormattableString label)
    { var raw = label.ToString(); var display = UiText.Interpolated(label).Split("##",2)[0]; if (MaterialText.RequiresShaping(display)) return MaterialText.TreeNode(raw, ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.NoAutoOpenOnLog, display); var open = ImGui.CollapsingHeader(raw); TreeLabel(raw, UiText.Interpolated(label).Split("##", 2)[0]); return open; }
    internal static bool FTreeNode(FormattableString label)
    { var raw = label.ToString(); var display = UiText.Interpolated(label).Split("##",2)[0]; if (MaterialText.RequiresShaping(display)) return MaterialText.TreeNode(raw, display: display); var open = ImGui.TreeNode(raw); TreeLabel(raw, UiText.Interpolated(label).Split("##", 2)[0]); return open; }

    internal static bool Tile(string raw, string title, string? description, MaterialIcon icon, Vector2 pixels, bool centered = false)
    {
        var scale = MaterialTheme.Metrics.Scale;
        title = UiText.T(title); description = description is null ? null : UiText.T(description);
        var measuredTitle = MaterialText.Measure(title);
        var measuredDetail = description is null ? Vector2.Zero : MaterialText.Measure(description);
        pixels.X = MaterialLayout.FitNextItemWidth(pixels.X, MathF.Ceiling(Math.Max(measuredTitle.X, measuredDetail.X) + 68 * scale));
        pixels.Y = Math.Max(pixels.Y, measuredTitle.Y + measuredDetail.Y + (description is null ? 8 : 28) * scale);
        var foreground = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
        var clicked = ImGui.Button(raw, pixels);
        ImGui.PopStyleColor();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax(); var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, true);
        var iconSize = Math.Min(28 * scale, Math.Max(0, (max.X - min.X) * .18f));
        var groupWidth = Math.Max(measuredTitle.X, measuredDetail.X) + iconSize + 12 * scale;
        var iconLeft = centered ? min.X + Math.Max(12 * scale, (max.X - min.X - groupWidth) * .5f) : min.X + 16 * scale;
        var left = iconLeft + iconSize + 12 * scale;
        var titleSize = measuredTitle;
        var detailSize = measuredDetail;
        var top = min.Y + Math.Max(4 * scale, (max.Y - min.Y - titleSize.Y - detailSize.Y - (description is null ? 0 : 4 * scale)) * .5f);
        foreground.W *= ImGui.GetStyle().Alpha;
        MaterialIcons.Draw(icon, new Vector2(iconLeft, min.Y + (max.Y - min.Y - iconSize) * .5f), iconSize, foreground);
        MaterialText.AddText(dl, ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(left, top), MaterialCanvas.Color(foreground), title);
        if (description is not null) MaterialText.AddText(dl, ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(left, top + titleSize.Y + 4 * scale), MaterialCanvas.Color(foreground), description);
        if (ImGui.IsItemFocused()) dl.AddRect(min, max, MaterialCanvas.Color(MaterialTheme.Current.Colors.Primary), 4 * scale, ImDrawFlags.None, 2 * scale);
        dl.PopClipRect(); return clicked;
    }
}
