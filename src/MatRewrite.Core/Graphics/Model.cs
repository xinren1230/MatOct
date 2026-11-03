// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Graphics;

/// <summary>RGB colour with components in [0,1].</summary>
public readonly record struct Rgb(double R, double G, double B)
{
    /// <summary>Current default colour order (set by the active <see cref="FigStyle"/>).</summary>
    public static Rgb[] DefaultOrder = FigStyle.OkabeIto;
    public static Rgb? FromChar(char c) => c switch
    {
        'r' => new Rgb(1, 0, 0), 'g' => new Rgb(0, 1, 0), 'b' => new Rgb(0, 0, 1), 'c' => new Rgb(0, 1, 1),
        'm' => new Rgb(1, 0, 1), 'y' => new Rgb(1, 1, 0), 'k' => new Rgb(0, 0, 0), 'w' => new Rgb(1, 1, 1),
        _ => null
    };
    public static Rgb? FromName(string s) => s switch
    {
        "red" => FromChar('r'), "green" => FromChar('g'), "blue" => FromChar('b'), "cyan" => FromChar('c'),
        "magenta" => FromChar('m'), "yellow" => FromChar('y'), "black" => FromChar('k'), "white" => FromChar('w'),
        _ => s.Length == 1 ? FromChar(s[0]) : null
    };
}

public enum LineStyle { None, Solid, Dashed, Dotted, DashDot }
public enum Marker { None, Circle, Plus, Star, Point, Cross, Square, Diamond, TriangleUp, TriangleDown, TriangleLeft, TriangleRight, Pentagram, Hexagram }

public abstract class GObject
{
    public double Handle { get; internal set; }
    public string DisplayName = "";
    /// <summary>Generic property bag: every property set by the user plus properties without a typed field.</summary>
    public readonly Dictionary<string, Runtime.Value> Props = new(StringComparer.OrdinalIgnoreCase);
    public bool Deleted;
    /// <summary>addlistener callbacks per property.</summary>
    public Dictionary<string, List<Runtime.Value>>? Listeners;
    public HashSet<string>? Firing;
}

/// <summary>The root object (handle 0).</summary>
public sealed class RootObject : GObject { public readonly List<GObject> Others = new(); }

/// <summary>Axes title / xlabel / ylabel / zlabel text object (string mapped to the axes field).</summary>
public sealed class LabelObject : GObject
{
    public Axes Axes = null!;
    public string Which = "title";
    public MatRewrite.Core.Runtime.Value? Raw;
    public string Text
    {
        get => Which switch { "title" => Axes.Title, "xlabel" => Axes.XLabel, "ylabel" => Axes.YLabel, _ => Axes.ZLabel };
        set { switch (Which) { case "title": Axes.Title = value; break; case "xlabel": Axes.XLabel = value; break; case "ylabel": Axes.YLabel = value; break; default: Axes.ZLabel = value; break; } }
    }
}

/// <summary>Object without a dedicated renderer (hggroup, uicontrol, legend, colorbar ...).</summary>
public sealed class GenericObject : Series
{
    public string Type = "hggroup";
    public GObject? Owner; // parent when it is not an axes (e.g. a figure)
}

/// <summary>patch: one or more filled polygons (columns of X/Y, or faces/vertices).</summary>
public sealed class PatchSeries : Series
{
    public List<(double[] x, double[] y)> Polys = new();
    public Rgb? FaceColor = new(0, 0, 0);
    public Rgb? EdgeColor = new(0, 0, 0);
    public double FaceAlpha = 1, LineWidth = 0.5;
}

public abstract class Series : GObject { public Axes Parent = null!; public Series? Group; }

public sealed class LineSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>();
    public double[]? Z; // plot3
    public Rgb Color;
    public LineStyle Style = LineStyle.Solid;
    public Marker Marker = Marker.None;
    public double LineWidth = FigStyle.Current.LineWidth, MarkerSize = FigStyle.Current.MarkerSize;
    public bool MarkerFilled;
    /// <summary>Marker edge colour (null = same as line colour, "auto").</summary>
    public Rgb? MarkerEdge;
    /// <summary>Marker face colour (null = "none" unless MarkerFilled, then line colour).</summary>
    public Rgb? MarkerFace;
    /// <summary>1-based indices of points that get a marker (null = all).</summary>
    public int[]? MarkerIndices;
}

public sealed class ScatterSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>();
    public double[] Sizes = { 36 };
    public Rgb Color;
    public double[]? CData; // per-point colour values mapped through the colormap
    public bool Filled;
    public Marker Marker = Marker.Circle;
}

public sealed class BarSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>();
    public double Width = 0.8;
    public Rgb Color;
    public int GroupIndex, GroupCount = 1;
    public bool Horizontal;
    // Octave-style bar object state (bar/barh); geometry is computed by __bar__'s algorithm.
    public List<BarSeries>? Members;
    public string Layout = "grouped";
    public double BaseValue;
    public bool ShowBaseline = true;
    public double[]? X1, X2, Y0, Y1;
    public PatchSeries? Patch;
    public GenericObject? Baseline;
}

public sealed class ImageSeries : Series
{
    public double[,] C = new double[0, 0]; // [row, col]
    public double X0 = 1, X1 = 1, Y0 = 1, Y1 = 1;
    public double CMin, CMax;
}

/// <summary>surf / mesh: a grid of quads. X, Y, Z, C are [row, col].</summary>
public sealed class SurfaceSeries : Series
{
    public double[,] X = new double[0, 0], Y = new double[0, 0], Z = new double[0, 0], C = new double[0, 0];
    public bool Mesh;                 // mesh: white faces, colormapped edges
    public string Shading = "faceted"; // faceted | flat | interp
    public Rgb? EdgeColor, FaceColor;
    public double FaceAlpha = 1, EdgeAlpha = 1;
    public string FaceLighting = "flat"; // none | flat | gouraud (only used when the axes has lights)
}

/// <summary>light / camlight: a directional light. Direction is given relative to the camera when CameraRelative.</summary>
public sealed class LightObject : Series
{
    public double Az, El;             // degrees (world or camera-relative)
    public bool CameraRelative;       // camlight
    public Rgb Color = new(1, 1, 1);
    public bool Visible = true;
}

/// <summary>Global registry of user-defined Nx3 colormaps (colormap(M)).</summary>
public static class CustomColormaps
{
    static readonly Dictionary<string, Rgb[]> maps = new();
    static int next;
    public static string Register(Rgb[] map)
    {
        lock (maps)
        {
            foreach (var kv in maps) if (kv.Value.SequenceEqual(map)) return kv.Key;
            var name = "custom" + (++next);
            maps[name] = map;
            return name;
        }
    }
    public static Rgb[]? Get(string name) { lock (maps) return maps.TryGetValue(name, out var m) ? m : null; }
    /// <summary>Colour for normalized t in [0,1] (MATLAB-style: index into N entries, no interpolation).</summary>
    public static Rgb Sample(Rgb[] map, double t)
    {
        if (map.Length == 0) return new Rgb(0, 0, 0);
        int k = (int)Math.Floor(Math.Clamp(t, 0, 1) * map.Length);
        return map[Math.Min(k, map.Length - 1)];
    }
}

/// <summary>contour / contourf on a rectilinear grid.</summary>
public sealed class ContourSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>(); // column / row coordinates
    public double[,] Z = new double[0, 0];
    public double[] Levels = Array.Empty<double>();
    public bool Filled;
    public Rgb? LineColor;
    public double LineWidth = 0.5;
    public bool Contour3;      // contour3: lines drawn at their level height
    public double? ZPlane;     // surfc / meshc: lines drawn on this z plane
    public List<(double level, double[] x, double[] y)> Lines = new();
}

public sealed class ErrorBarSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>(), L = Array.Empty<double>(), U = Array.Empty<double>();
    public Rgb Color;
    public LineStyle Style = LineStyle.Solid;
    public Marker Marker = Marker.None;
    public bool Horizontal;
}

public sealed class TextSeries : Series
{
    public double X, Y;
    public double? Z;
    public string Text = "";
    public Rgb Color = new(0, 0, 0);
    public double FontSize = FigStyle.Current.FontSize;
    public string HAlign = "left";
}

/// <summary>Figure-level annotation in normalized figure units (0..1, origin bottom-left).</summary>
public sealed class Annotation : GObject
{
    public string Kind = "line"; // line | arrow | doublearrow | textarrow | textbox | rectangle | ellipse
    public double[] X = { 0.3, 0.4 }, Y = { 0.3, 0.4 };
    public double[]? Position; // [x y w h] for textbox/rectangle/ellipse
    public string Text = "";
    public Rgb Color = new(0, 0, 0);
    public double FontSize = 10;
}

public sealed class Axes : GObject
{
    public Figure Figure = null!;
    public int Rows = 1, Cols = 1;
    public int[] Cells = { 1 }; // subplot cell indices (1-based, row-major)
    public string Title = "", XLabel = "", YLabel = "", ZLabel = "";
    public double[]? ZLim;
    /// <summary>[azimuth, elevation] in degrees; 2-D axes use [0 90].</summary>
    public double Az = 0, El = 90;
    public double Zoom = 1;
    public bool XLog, YLog;
    public bool Is3D => !(Az == 0 && El == 90);
    public string Lighting = "flat"; // none | flat | gouraud (lighting command)
    public readonly List<Series> Children = new();
    public LabelObject? TitleObj, XLabelObj, YLabelObj, ZLabelObj;
    public bool Hold, Grid = FigStyle.Current.Grid, MinorGrid, Box = FigStyle.Current.Box, Visible = true;
    /// <summary>Bold panel label drawn at the top-left corner of the axes cell (panellabel).</summary>
    public string? PanelLabel;
    public bool LegendVisible;
    /// <summary>Legend object (handle returned by legend()); carries FontSize/FontName/... props.</summary>
    public GenericObject? LegendObj, ColorbarObj;
    public string[]? LegendLabels;
    public string LegendLocation = "northeast";
    public double[]? XLim, YLim;
    public bool Equal, Tight, Square;
    public bool Colorbar;
    public bool YDirReverse;
    public double[]? CLim;
    public int ColorIndex;
    public Rgb NextColor() => Rgb.DefaultOrder[ColorIndex++ % Rgb.DefaultOrder.Length];
    public void Reset()
    {
        Children.Clear(); ColorIndex = 0; LegendVisible = false; LegendLabels = null; Colorbar = false;
        XLim = YLim = ZLim = null; Equal = Tight = Square = false; YDirReverse = false; CLim = null;
        Az = 0; El = 90; Zoom = 1; XLog = YLog = false; Lighting = "flat";
    }
}

public sealed class Figure : GObject
{
    public string Name = "";
    public readonly List<Axes> Axes = new();
    public Axes? CurrentAxes;
    public string Colormap = FigStyle.Current.Colormap;
    /// <summary>Physical width (pt) used while exporting; 0 = style width.</summary>
    public double ExportWidthPt;
    public bool Visible = true;
    public int Width = 560, Height = 420;
    public bool NumberTitle = true;
    public readonly List<Annotation> Annotations = new();
    public readonly List<GObject> Others = new(); // non-axes children (uicontrol, uimenu, legend ...)
    public string WindowTitle => NumberTitle ? (Name == "" ? $"Figure {Handle}" : $"Figure {Handle}: {Name}") : Name;
}

/// <summary>Front-end that displays figures (WPF windows, files, ...).</summary>
public interface IGraphicsBackend
{
    /// <summary>The figure was created or changed; (re)draw it.</summary>
    void Update(Figure f);
    void Close(Figure f);
    /// <summary>Render the figure to an image file (png/jpg/bmp/svg).</summary>
    void Export(Figure f, string path, int width, int height);
}
