// MatRewrite - GPL-3.0-or-later
// Phase 13.1: publication figure styles (figstyle('nature'|'science'|'default'|'classic')).
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Graphics;

/// <summary>
/// Global figure style. All sizes are in typographic points at the *physical* figure width
/// (<see cref="WidthPt"/>); renderers scale points to pixels with px-per-pt = pixel width / WidthPt,
/// so on-screen figures and exports at any DPI keep the same proportions as the printed figure.
/// </summary>
public sealed class FigStyle
{
    public string Name = "default";
    public string FontName = "Arial";
    public double FontSize = 8, LabelSize = 9, TitleSize = 9, LegendSize = 8, PanelLabelSize = 10;
    public bool TitleBold = false;
    public double AxesLineWidth = 0.75, LineWidth = 1.25, MarkerSize = 5, TickLength = 3;
    public string TickDir = "out";
    public bool Box = false, Grid = false, LegendBox = false;
    /// <summary>Physical figure width in points (1 in = 72 pt).</summary>
    public double WidthPt = MmToPt(120);
    public string Colormap = "parula";
    public Rgb[] ColorOrder = OkabeIto;

    public static double MmToPt(double mm) => mm / 25.4 * 72;
    public static double ToPt(double v, string units) => units.ToLowerInvariant() switch
    {
        "mm" or "millimeters" or "millimetres" => MmToPt(v),
        "cm" or "centimeters" or "centimetres" => MmToPt(v * 10),
        "in" or "inch" or "inches" => v * 72,
        "pt" or "points" => v,
        "px" or "pixels" => v * 0.75,
        _ => throw new OctaveException($"unknown units '{units}'")
    };

    /// <summary>Okabe–Ito colour-blind-safe palette (Nature Methods "Points of view", 2011).</summary>
    public static readonly Rgb[] OkabeIto =
    {
        C(0, 114, 178), C(213, 94, 0), C(0, 158, 115), C(230, 159, 0), C(86, 180, 233), C(204, 121, 167), C(240, 228, 66), C(0, 0, 0)
    };
    public static readonly Rgb[] MatlabOrder =
    {
        new(0, 0.447, 0.741), new(0.850, 0.325, 0.098), new(0.929, 0.694, 0.125), new(0.494, 0.184, 0.556),
        new(0.466, 0.674, 0.188), new(0.301, 0.745, 0.933), new(0.635, 0.078, 0.184)
    };
    static Rgb C(int r, int g, int b) => new(r / 255.0, g / 255.0, b / 255.0);

    static FigStyle current = Preset("default");
    public static FigStyle Current
    {
        get => current;
        set { current = value; Rgb.DefaultOrder = value.ColorOrder; }
    }

    public static readonly string[] Presets = { "default", "nature", "nature2", "science", "science2", "classic" };

    public static FigStyle Preset(string name, int columns = 1)
    {
        name = name.ToLowerInvariant();
        if (name == "nature" && columns == 2) name = "nature2";
        if (name == "science" && columns == 2) name = "science2";
        var s = new FigStyle { Name = name };
        switch (name)
        {
            case "default":
                break;
            case "nature":   // Nature: 89 mm single column, 5-7 pt text, Helvetica/Arial
            case "nature2":  // 183 mm double column
                s.WidthPt = MmToPt(name == "nature" ? 89 : 183);
                s.FontSize = 7; s.LabelSize = 8; s.TitleSize = 8; s.LegendSize = 7; s.PanelLabelSize = 8;
                s.AxesLineWidth = 0.6; s.LineWidth = 1.0; s.MarkerSize = 4; s.TickLength = 2.5;
                break;
            case "science":  // Science: 5.7 cm single column, 12.1 cm double
            case "science2":
                s.WidthPt = MmToPt(name == "science" ? 57 : 121);
                s.FontSize = 7; s.LabelSize = 8; s.TitleSize = 8; s.LegendSize = 7; s.PanelLabelSize = 9;
                s.AxesLineWidth = 0.5; s.LineWidth = 1.0; s.MarkerSize = 4; s.TickLength = 2.5;
                break;
            case "classic": // the pre-0.13.1 MATLAB-like look
                s.WidthPt = 560 * 0.75; s.FontName = "Helvetica";
                s.FontSize = 10; s.LabelSize = 11; s.TitleSize = 11; s.LegendSize = 9; s.TitleBold = true;
                s.AxesLineWidth = 0.5; s.LineWidth = 0.5; s.MarkerSize = 6; s.TickLength = 4; s.TickDir = "in";
                s.Box = true; s.LegendBox = true; s.ColorOrder = MatlabOrder; s.Colormap = "parula";
                break;
            default:
                throw new OctaveException($"figstyle: unknown style '{name}' (use {string.Join(", ", Presets)})");
        }
        return s;
    }

    public FigStyle Clone() { var c = (FigStyle)MemberwiseClone(); c.ColorOrder = (Rgb[])ColorOrder.Clone(); return c; }

    /// <summary>Pixels per point when a figure that is <paramref name="pixelWidth"/> px wide shows this style's physical width.</summary>
    public double PxPerPt(double pixelWidth) => Math.Max(0.5, pixelWidth / WidthPt);

    /// <summary>On-screen scale: fixed relative to MATLAB's default 560 px figure so text does not grow when the window is resized.</summary>
    public double ScreenPxPerPt => Math.Max(1.0, 560 / WidthPt);

    /// <summary>Keep the style in sync with set(groot,'DefaultXxx',v). Returns true when the key is style-relevant.</summary>
    public static bool ApplyRootDefault(string key, Value v)
    {
        key = key.ToLowerInvariant();
        double? n = v is MatrixValue m && !m.IsChar && m.Numel >= 1 ? m.Re[0] : null;
        string? s = v is MatrixValue sm && sm.IsChar ? sm.AsString() : null;
        var st = Current;
        switch (key)
        {
            case "defaultaxesfontsize" when n is double fs:
                double r = fs / st.FontSize;
                st.FontSize = fs; st.LabelSize *= r; st.TitleSize *= r; st.LegendSize *= r; return true;
            case "defaulttextfontsize" when n is double ts: st.LabelSize = ts; st.TitleSize = ts; return true;
            case "defaultlegendfontsize" when n is double ls: st.LegendSize = ls; return true;
            case "defaultaxesfontname" or "defaulttextfontname" when s != null: st.FontName = s; return true;
            case "defaultaxeslinewidth" when n is double alw: st.AxesLineWidth = alw; return true;
            case "defaultlinelinewidth" when n is double llw: st.LineWidth = llw; return true;
            case "defaultlinemarkersize" when n is double ms: st.MarkerSize = ms; return true;
            case "defaultaxestickdir" when s != null: st.TickDir = s.ToLowerInvariant(); return true;
            case "defaultaxesbox" when s != null: st.Box = s == "on"; return true;
            case "defaultaxesxgrid" or "defaultaxesygrid" when s != null: st.Grid = s == "on"; return true;
            case "defaultlegendbox" when s != null: st.LegendBox = s == "on"; return true;
            case "defaultaxestitlefontweight" when s != null: st.TitleBold = s == "bold"; return true;
            case "defaultaxescolororder" when v is MatrixValue co && !co.IsChar && co.C == 3 && co.R > 0:
                st.ColorOrder = Enumerable.Range(0, co.R).Select(i => new Rgb(co.Re[i], co.Re[i + co.R], co.Re[i + 2 * co.R])).ToArray();
                Rgb.DefaultOrder = st.ColorOrder; return true;
        }
        return false;
    }

    /// <summary>Value of a root default derived from the style (null when not style-controlled).</summary>
    public static Value? RootDefault(string key)
    {
        var st = Current;
        return key.ToLowerInvariant() switch
        {
            "defaultaxesfontsize" => MatrixValue.Scalar(st.FontSize),
            "defaulttextfontsize" => MatrixValue.Scalar(st.LabelSize),
            "defaultlegendfontsize" => MatrixValue.Scalar(st.LegendSize),
            "defaultaxesfontname" or "defaulttextfontname" => MatrixValue.Str(st.FontName),
            "defaultaxeslinewidth" => MatrixValue.Scalar(st.AxesLineWidth),
            "defaultlinelinewidth" => MatrixValue.Scalar(st.LineWidth),
            "defaultlinemarkersize" => MatrixValue.Scalar(st.MarkerSize),
            "defaultaxestickdir" => MatrixValue.Str(st.TickDir),
            "defaultaxesbox" => MatrixValue.Str(st.Box ? "on" : "off"),
            "defaultaxesxgrid" or "defaultaxesygrid" => MatrixValue.Str(st.Grid ? "on" : "off"),
            "defaultlegendbox" => MatrixValue.Str(st.LegendBox ? "on" : "off"),
            _ => null
        };
    }

    /// <summary>MATLAB-like parula colormap as an 11-entry anchor table (interpolated by consumers).</summary>
    public static readonly double[] ParulaLut =
    {
        0.2422, 0.1504, 0.6603, 0.2803, 0.2782, 0.9221, 0.2440, 0.4358, 0.9988, 0.1540, 0.5902, 0.9218,
        0.0297, 0.7082, 0.8163, 0.1938, 0.7758, 0.6251, 0.5044, 0.7993, 0.3480, 0.8634, 0.7406, 0.1596,
        0.9892, 0.8136, 0.1885, 0.9610, 0.8906, 0.1537, 0.9763, 0.9831, 0.0538
    };
    /// <summary>Sample <see cref="ParulaLut"/> at t in [0,1].</summary>
    public static Rgb Parula(double t)
    {
        int p = ParulaLut.Length / 3; double x = Math.Clamp(double.IsFinite(t) ? t : 0, 0, 1) * (p - 1);
        int k = Math.Min((int)x, p - 2); double f = x - k;
        double L(int c) => ParulaLut[k * 3 + c] * (1 - f) + ParulaLut[(k + 1) * 3 + c] * f;
        return new Rgb(L(0), L(1), L(2));
    }
}
