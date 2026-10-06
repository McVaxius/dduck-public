using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using AethertekUI;
using Dalamud.Bindings.ImGui;

namespace DDuck.PublicShell.Ui;

internal enum UiFontRole { Body, BodyStrong, Title, PaneHeading, CompactTitle, CompactPaneHeading }

internal static class PublicPresentation
{
    // Approved regular 1505x1045: main (21,20)-(1484,1025), inset30/40;
    // header170, cards291/199/212, gap23; access row74 and support buttons68.
    // Compact composite1536x1024: introduction (1074,674)-(1497,900),
    // inset15, title26, body12-16, access42, support38, gaps8/10.
    // Actual author/version and native instructions replace sample reference text.
    internal const uint ReferenceAccent = 0xA92CF1;
    internal static readonly float[] FontSizes = [16, 16, 46, 28, 26, 22];
    internal static readonly string[] FontFiles = ["segoeui.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf", "seguisb.ttf"];
    internal static float AtlasHeight(UiFontRole role) => FontSizes[(int)role] * 4 / 3;
    internal static bool Compact => MaterialTheme.Current.Density == MaterialDensity.Compact;
    internal static float HeaderHeight => Compact ? 68 : 82;
    internal static float Gap => Compact ? 10 : 15;
    internal static float ControlHeight => Compact ? 32 : 40;
    internal static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);

    internal static MaterialTheme Theme(uint accent)
    {
        accent &= 0xFFFFFF;
        var selected = Rgb(accent);
        var reference = Rgb(ReferenceAccent);
        var seed = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(selected.X, selected.Y, selected.Z)));
        var original = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(reference.X, reference.Y, reference.Z)));
        var hue = seed.Y < .001f ? 0 : seed.Z - original.Z;
        var chroma = seed.Y < .001f ? 0 : seed.Y / original.Y;
        Vector4 Relative(uint rgb)
        {
            var color = Rgb(rgb);
            if (accent == ReferenceAccent) return color;
            var lch = MaterialColor.LabToLch(MaterialColor.SrgbToOklab(new(color.X, color.Y, color.Z)));
            return new(MaterialColor.GamutMap(lch.X, lch.Y * chroma, lch.Z + hue), 1);
        }
        var background = Relative(0x13171E);
        var foreground = Relative(0xF6F1FF);
        var primary = Relative(ReferenceAccent);
        var palette = new OklchPaletteGenerator().Generate(new(selected.X, selected.Y, selected.Z));
        var colors = new MaterialColorScheme(palette)
        {
            Background = background, OnBackground = foreground,
            Surface = Relative(0x1A1E26), OnSurface = foreground,
            SurfaceContainerLowest = Relative(0x11151B), SurfaceContainerLow = Relative(0x1A1E26),
            SurfaceContainer = Relative(0x1E232E), SurfaceContainerHigh = Relative(0x252B38), SurfaceContainerHighest = Relative(0x252B38),
            SurfaceVariant = Relative(0x303447), OnSurfaceVariant = Relative(0xB9C9E0),
            Outline = Relative(0x758195), OutlineVariant = Relative(0x343D49),
            Primary = primary, OnPrimary = MaterialColor.Contrast(primary, background) >= MaterialColor.Contrast(primary, foreground) ? background : foreground,
            PrimaryContainer = Relative(0x292038), OnPrimaryContainer = foreground,
            Secondary = Relative(0x5D3DD1), OnSecondary = background, SecondaryContainer = Relative(0x303447), OnSecondaryContainer = foreground,
            Tertiary = Relative(0xAC77ED), OnTertiary = background, TertiaryContainer = Relative(0x302B40), OnTertiaryContainer = foreground,
            InverseSurface = foreground, InverseOnSurface = background, InversePrimary = Relative(0x7641B0),
        };
        return new(colors, MaterialDensity.Standard) { SurfaceOpacity = 1 };
    }

    internal static MaterialControlMetrics Controls(float height = 0)
    {
        if (height <= 0) height = ControlHeight;
        var s = MaterialTheme.Metrics.Scale;
        return new() { Height = height * s, Padding = new(12 * s, Math.Max(0, (height * s - ImGui.GetTextLineHeight()) * .5f)),
            Gap = 8 * s, IconSize = 22 * s, Rounding = 4 * s, ItemSpacing = new(10 * s, 6 * s), CellPadding = new(12 * s, 6 * s) };
    }

    internal static void Surface(Vector2 min, Vector2 max)
    {
        var c = MaterialTheme.Current.Colors;
        MaterialCanvas.Surface(min, max, c.SurfaceContainerHigh, c.Surface, 4 * MaterialTheme.Metrics.Scale);
        ImGui.GetWindowDrawList().AddRect(min, max, MaterialCanvas.Color(c.OutlineVariant), 4 * MaterialTheme.Metrics.Scale);
    }

    internal static void Brand(Vector2 origin, float size)
    {
        var dl = ImGui.GetWindowDrawList(); var ink = MaterialCanvas.Color(MaterialTheme.Current.Colors.Primary);
        dl.AddCircleFilled(origin + new Vector2(.30f, .20f) * size, size * .18f, ink, 24);
        dl.AddCircleFilled(origin + new Vector2(.74f, .32f) * size, size * .14f, ink, 24);
        dl.AddRectFilled(origin + new Vector2(.02f, .48f) * size, origin + new Vector2(.58f, .92f) * size, ink, size * .22f, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(origin + new Vector2(.52f, .62f) * size, origin + new Vector2(.98f, .92f) * size, ink, size * .18f, ImDrawFlags.RoundCornersTop);
    }
}
