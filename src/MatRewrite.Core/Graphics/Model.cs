// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Graphics;

/// <summary>RGB colour with components in [0,1].</summary>
public readonly record struct Rgb(double R, double G, double B)
{
    public static readonly Rgb[] DefaultOrder =
    {
        new(0, 0.447, 0.741), new(0.850, 0.325, 0.098), new(0.929, 0.694, 0.125), new(0.494, 0.184, 0.556),
        new(0.466, 0.674, 0.188), new(0.301, 0.745, 0.933), new(0.635, 0.078, 0.184)
    };
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
}

public abstract class Series : GObject { public Axes Parent = null!; }

public sealed class LineSeries : Series
{
    public double[] X = Array.Empty<double>(), Y = Array.Empty<double>();
    public Rgb Color;
    public LineStyle Style = LineStyle.Solid;
    public Marker Marker = Marker.None;
    public double LineWidth = 0.5, MarkerSize = 6;
    public bool MarkerFilled;
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
}

public sealed class ImageSeries : Series
{
    public double[,] C = new double[0, 0]; // [row, col]
    public double X0 = 1, X1 = 1, Y0 = 1, Y1 = 1;
    public double CMin, CMax;
}

public sealed class Axes : GObject
{
    public Figure Figure = null!;
    public int Rows = 1, Cols = 1;
    public int[] Cells = { 1 }; // subplot cell indices (1-based, row-major)
    public string Title = "", XLabel = "", YLabel = "";
    public readonly List<Series> Children = new();
    public bool Hold, Grid, MinorGrid, Box = true, Visible = true;
    public bool LegendVisible;
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
        XLim = YLim = null; Equal = Tight = Square = false; YDirReverse = false; CLim = null;
    }
}

public sealed class Figure : GObject
{
    public string Name = "";
    public readonly List<Axes> Axes = new();
    public Axes? CurrentAxes;
    public string Colormap = "viridis";
    public bool Visible = true;
    public int Width = 560, Height = 420;
    public bool NumberTitle = true;
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
