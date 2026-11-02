// MatRewrite - GPL-3.0-or-later
// Phase 13.1: figstyle / panellabel / exportgraphics (publication figures).
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static void RegisterFigStyle()
        {
            Reg("groot", (it, a, n) => Out(Num(0)));
            // figstyle            -> current style name
            // figstyle('nature')  / figstyle('science','Columns',2) / figstyle('default') / figstyle('classic')
            // figstyle(..., 'FontSize',7, 'FontName','Arial', 'Width',89, 'Units','mm', 'LineWidth',1)
            // figstyle('list')    -> cell array of preset names
            RegG("figstyle", (it, a, n) =>
            {
                if (a.Length == 0) return Out(Str(FigStyle.Current.Name));
                var name = S(a[0], "figstyle").ToLowerInvariant();
                if (name == "list") return Out(new CellValue(1, FigStyle.Presets.Length, FigStyle.Presets.Select(p => (Value)Str(p)).ToArray()));
                int cols = 1; double? width = null; string units = "mm";
                var extra = new List<(string, Value)>();
                for (int k = 1; k + 1 < a.Length; k += 2)
                {
                    var key = S(a[k], "figstyle").ToLowerInvariant();
                    switch (key)
                    {
                        case "columns": cols = (int)D(a[k + 1], "figstyle"); break;
                        case "width": width = D(a[k + 1], "figstyle"); break;
                        case "units": units = S(a[k + 1], "figstyle"); break;
                        default: extra.Add((key, a[k + 1])); break;
                    }
                }
                var old = FigStyle.Current;
                var st = FigStyle.Preset(name, cols);
                if (width is double wv) st.WidthPt = FigStyle.ToPt(wv, units);
                FigStyle.Current = st;
                foreach (var (key, v) in extra)
                {
                    var mapped = key switch
                    {
                        "fontsize" => "defaultaxesfontsize", "fontname" => "defaultaxesfontname", "linewidth" => "defaultlinelinewidth",
                        "axeslinewidth" => "defaultaxeslinewidth", "markersize" => "defaultlinemarkersize", "tickdir" => "defaultaxestickdir",
                        "box" => "defaultaxesbox", "grid" => "defaultaxesxgrid", "colororder" => "defaultaxescolororder",
                        _ => throw new OctaveException($"figstyle: unknown option '{key}'")
                    };
                    FigStyle.ApplyRootDefault(mapped, v);
                }
                Restyle(it, old, FigStyle.Current);
                return n > 0 ? Out(Str(st.Name)) : Array.Empty<Value>();
            });

            // panellabel           -> label every axes of gcf a, b, c ... (creation order)
            // panellabel('b')      -> label gca;  panellabel(ax,'b');  panellabel('A') keeps the case given
            // panellabel('off')    -> remove labels from gcf
            RegG("panellabel", (it, a, n) =>
            {
                var f = it.Graphics.Gcf();
                if (a.Length == 0 || (a.Length == 1 && IsString(a[0]) && S(a[0], "panellabel") is "auto"))
                {
                    int i = 0;
                    foreach (var ax in f.Axes.Where(x => x.Visible || x.Title != "")) ax.PanelLabel = ((char)('a' + i++)).ToString();
                }
                else if (a.Length == 1 && IsString(a[0]) && S(a[0], "panellabel") == "off")
                    foreach (var ax in f.Axes) ax.PanelLabel = null;
                else
                {
                    Axes ax = it.Graphics.Gca();
                    var rest = a;
                    if (a[0] is MatrixValue hm && !hm.IsChar && hm.Numel == 1) { ax = it.Graphics.Lookup(hm.Re[0]) as Axes ?? throw new OctaveException("panellabel: invalid axes handle"); rest = a[1..]; }
                    if (rest.Length < 1) throw new OctaveException("panellabel: missing label");
                    ax.PanelLabel = S(rest[0], "panellabel");
                    f = ax.Figure;
                }
                it.Graphics.Touch(f);
                return Array.Empty<Value>();
            });

            // exportgraphics(obj, file, 'Resolution',600, 'Width',89, 'Height',60, 'Units','mm', 'Columns',1|2, 'ContentType','vector')
            RegG("exportgraphics", (it, a, n) =>
            {
                if (a.Length < 2) throw new OctaveException("Invalid call to exportgraphics");
                var o = a[0] is MatrixValue hm && !hm.IsChar && hm.Numel == 1 ? it.Graphics.Lookup(hm.Re[0]) : null;
                var f = o switch { Figure ff => ff, Axes ax => ax.Figure, _ => throw new OctaveException("exportgraphics: first argument must be a figure or axes handle") };
                var file = S(a[1], "exportgraphics");
                double dpi = 300, w = 0, h = 0; string units = "centimeters"; int cols = 0;
                for (int k = 2; k + 1 < a.Length; k += 2)
                {
                    var key = S(a[k], "exportgraphics").ToLowerInvariant();
                    switch (key)
                    {
                        case "resolution": dpi = D(a[k + 1], "exportgraphics"); if (dpi <= 0) throw new OctaveException("exportgraphics: Resolution must be positive"); break;
                        case "width": w = D(a[k + 1], "exportgraphics"); break;
                        case "height": h = D(a[k + 1], "exportgraphics"); break;
                        case "units": units = S(a[k + 1], "exportgraphics"); break;
                        case "columns": cols = (int)D(a[k + 1], "exportgraphics"); break;
                        case "contenttype" or "backgroundcolor" or "colorspace" or "append" or "padding": break;
                        default: throw new OctaveException($"exportgraphics: unknown option '{key}'");
                    }
                }
                if (!Path.HasExtension(file)) file += ".png";
                double wpt = w > 0 ? FigStyle.ToPt(w, units) : 0, hpt = h > 0 ? FigStyle.ToPt(h, units) : 0;
                if (wpt == 0 && cols > 0)
                {
                    var basis = FigStyle.Current.Name.StartsWith("science") ? "science" : "nature";
                    wpt = FigStyle.Preset(basis, cols).WidthPt;
                }
                ExportPhys(it, f, file, wpt, hpt, dpi);
                return Array.Empty<Value>();
            });
        }

        /// <summary>Carry existing figures over to a new style: box/grid, default line widths/markers and default-order colours.</summary>
        static void Restyle(Interpreter it, FigStyle old, FigStyle st)
        {
            static bool Same(Rgb a, Rgb b) => Math.Abs(a.R - b.R) < 1e-3 && Math.Abs(a.G - b.G) < 1e-3 && Math.Abs(a.B - b.B) < 1e-3;
            Rgb Map(Rgb c)
            {
                for (int i = 0; i < old.ColorOrder.Length; i++) if (Same(c, old.ColorOrder[i])) return st.ColorOrder[i % st.ColorOrder.Length];
                return c;
            }
            foreach (var f in it.Graphics.Figures.Values)
            {
                if (f.Colormap == old.Colormap) f.Colormap = st.Colormap;
                foreach (var ax in f.Axes)
                {
                    if (ax.Box == old.Box) ax.Box = st.Box;
                    if (ax.Grid == old.Grid) ax.Grid = st.Grid;
                    foreach (var s in ax.Children)
                        switch (s)
                        {
                            case LineSeries l:
                                if (Math.Abs(l.LineWidth - old.LineWidth) < 1e-9) l.LineWidth = st.LineWidth;
                                if (Math.Abs(l.MarkerSize - old.MarkerSize) < 1e-9) l.MarkerSize = st.MarkerSize;
                                l.Color = Map(l.Color); break;
                            case BarSeries b: b.Color = Map(b.Color); break;
                            case ScatterSeries p: p.Color = Map(p.Color); break;
                            case ErrorBarSeries e: e.Color = Map(e.Color); break;
                            case TextSeries t: if (Math.Abs(t.FontSize - old.FontSize) < 1e-9) t.FontSize = st.FontSize; break;
                        }
                }
                it.Graphics.Touch(f);
            }
        }
    }
}
