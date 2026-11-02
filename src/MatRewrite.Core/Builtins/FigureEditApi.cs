// MatRewrite - GPL-3.0-or-later
// Figure editing API used by the GUI (property inspector, .fig JSON, Generate Code) and by
// the savefig / openfig / hgsave / hgload / makemcode builtins.
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static class FigureEditApi
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        /// <summary>set(o, prop, value) as if typed in the command window (listeners fire, model updated).</summary>
        public static void Set(Interpreter it, GObject o, string prop, Value v) => BuiltinLibrary.UiSetProps(it, o, new Value[] { MatrixValue.Str(prop), v });
        public static Value Get(Interpreter it, GObject o, string prop) => BuiltinLibrary.UiGetAny(it, o, prop);
        public static IEnumerable<string> PropertyNames(Interpreter it, GObject o) => BuiltinLibrary.UiPropNames(it, o);
        public static string TypeOf(GObject o) => BuiltinLibrary.UiTypeOf(o);
        public static GenericObject Legend(Interpreter it, Axes a) => BuiltinLibrary.LegendOf(it, a);

        public static string LineStyleStr(LineStyle s) => s switch { LineStyle.Solid => "-", LineStyle.Dashed => "--", LineStyle.Dotted => ":", LineStyle.DashDot => "-.", _ => "none" };
        public static string MarkerStr(Marker m) => m switch
        {
            Marker.Circle => "o", Marker.Plus => "+", Marker.Star => "*", Marker.Point => ".", Marker.Cross => "x", Marker.Square => "s",
            Marker.Diamond => "d", Marker.TriangleUp => "^", Marker.TriangleDown => "v", Marker.TriangleLeft => "<", Marker.TriangleRight => ">",
            Marker.Pentagram => "p", Marker.Hexagram => "h", _ => "none"
        };

        // ------------------------------------------------------------------ value <-> text (inspector)
        /// <summary>Text shown in a property editor for a value.</summary>
        public static string ToText(Value v)
        {
            switch (v)
            {
                case MatrixValue m when m.IsChar: return m.AsString();
                case MatrixValue m:
                    if (m.Numel == 0) return "[]";
                    var parts = m.Re.Select(d => Fmt(d));
                    if (m.R > 1 && m.C > 1) return "[" + string.Join("; ", Enumerable.Range(0, m.R).Select(r => string.Join(" ", Enumerable.Range(0, m.C).Select(c => Fmt(m.Re[c * m.R + r]))))) + "]";
                    return m.Numel == 1 ? Fmt(m.Re[0]) : "[" + string.Join(" ", parts) + "]";
                case CellValue c: return "{" + string.Join("|", c.Data.Select(e => e is MatrixValue mm && mm.IsChar ? mm.AsString() : ToText(e))) + "}";
                default: return "<" + v.GetType().Name.Replace("Value", "").ToLowerInvariant() + ">";
            }
        }

        /// <summary>Parse inspector text back into a value of the same kind as <paramref name="like"/>.</summary>
        public static Value FromText(string s, Value? like)
        {
            s = s.Trim();
            if (like is CellValue || (s.StartsWith("{") && s.EndsWith("}")))
            {
                var body = s.StartsWith("{") && s.EndsWith("}") ? s[1..^1] : s;
                var items = body.Length == 0 ? Array.Empty<string>() : body.Split('|');
                return new CellValue(1, items.Length, items.Select(x => (Value)MatrixValue.Str(x)).ToArray());
            }
            if (like is MatrixValue lm && lm.IsChar) return MatrixValue.Str(s);
            var nums = ParseNumbers(s);
            if (nums != null) return nums;
            return MatrixValue.Str(s);
        }

        public static MatrixValue? ParseNumbers(string s)
        {
            s = s.Trim();
            if (s == "[]") return new MatrixValue(0, 0);
            if (s.StartsWith("[") && s.EndsWith("]")) s = s[1..^1];
            var rows = s.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var data = new List<double[]>();
            foreach (var r in rows)
            {
                var toks = r.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var vals = new double[toks.Length];
                for (int i = 0; i < toks.Length; i++)
                {
                    var t = toks[i].ToLowerInvariant();
                    if (t == "inf") vals[i] = double.PositiveInfinity; else if (t == "-inf") vals[i] = double.NegativeInfinity;
                    else if (t == "nan") vals[i] = double.NaN; else if (t == "pi") vals[i] = Math.PI;
                    else if (!double.TryParse(t, NumberStyles.Float, IC, out vals[i])) return null;
                }
                data.Add(vals);
            }
            if (data.Count == 0) return null;
            int cols = data[0].Length;
            if (data.Any(d => d.Length != cols)) return null;
            var m = new MatrixValue(data.Count, cols);
            for (int r = 0; r < data.Count; r++) for (int c = 0; c < cols; c++) m.Re[c * data.Count + r] = data[r][c];
            return m;
        }

        static string Fmt(double d) => double.IsNaN(d) ? "NaN" : double.IsPositiveInfinity(d) ? "Inf" : double.IsNegativeInfinity(d) ? "-Inf" : d.ToString("G15", IC);

        // ------------------------------------------------------------------ figure -> JSON
        static readonly string[] AxesStyleProps = { "fontname", "fontsize", "fontweight", "fontangle", "tickdir", "ticklength", "linewidth", "xcolor", "ycolor", "color",
            "xtick", "ytick", "xticklabel", "yticklabel", "xticklabelrotation", "xminorgrid", "yminorgrid", "xgrid", "ygrid", "gridcolor", "gridlinestyle", "gridalpha",
            "xminortick", "yminortick", "titlefontsizemultiplier", "labelfontsizemultiplier", "layer" };
        static readonly string[] TextStyleProps = { "fontname", "fontsize", "fontweight", "fontangle", "color", "interpreter", "horizontalalignment", "rotation" };
        static readonly string[] LegendStyleProps = { "fontname", "fontsize", "fontweight", "fontangle", "textcolor", "box", "orientation", "color", "edgecolor", "numcolumns" };
        static readonly string[] LineExtraProps = { "visible", "tag", "userdata_text" };

        static JsonNode? ValJson(Value v) => v switch
        {
            MatrixValue m when m.IsChar => JsonValue.Create(m.AsString()),
            MatrixValue m when m.Im == null => new JsonObject { ["rows"] = m.R, ["cols"] = m.C, ["data"] = Arr(m.Re) },
            CellValue c when c.Data.All(e => e is MatrixValue mm && mm.IsChar) => new JsonObject { ["cell"] = new JsonArray(c.Data.Select(e => (JsonNode?)JsonValue.Create(((MatrixValue)e).AsString())).ToArray()) },
            _ => null
        };

        static Value? JsonVal(JsonNode? n)
        {
            if (n == null) return null;
            if (n is JsonValue jv && jv.TryGetValue<string>(out var s)) return MatrixValue.Str(s);
            if (n is JsonObject o)
            {
                if (o["cell"] is JsonArray ca) return new CellValue(1, ca.Count, ca.Select(x => (Value)MatrixValue.Str((string?)x ?? "")).ToArray());
                int r = (int?)o["rows"] ?? 0, c = (int?)o["cols"] ?? 0;
                return new MatrixValue(r, c, Dbl(o["data"]));
            }
            return null;
        }

        static JsonArray Arr(IEnumerable<double> xs) => new(xs.Select(d => (JsonNode?)(double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(Fmt(d)))).ToArray());
        static double[] Dbl(JsonNode? n) => n is JsonArray a ? a.Select(x => x is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.Parse((string?)x ?? "NaN", IC)).ToArray() : Array.Empty<double>();
        static JsonArray RgbJ(Rgb c) => Arr(new[] { c.R, c.G, c.B });

        static JsonObject PropsJ(GObject o, IEnumerable<string> names)
        {
            var j = new JsonObject();
            foreach (var p in names) if (o.Props.TryGetValue(p, out var v) && ValJson(v) is { } n) j[p] = n;
            return j;
        }

        public static string ToJson(Figure f)
        {
            var fj = new JsonObject
            {
                ["format"] = "MatOct-fig", ["version"] = 1, ["name"] = f.Name, ["colormap"] = f.Colormap,
                ["width"] = f.Width, ["height"] = f.Height, ["numbertitle"] = f.NumberTitle,
                ["props"] = PropsJ(f, new[] { "color" })
            };
            var axes = new JsonArray();
            foreach (var a in f.Axes.Where(a => !a.Deleted))
            {
                var aj = new JsonObject
                {
                    ["rows"] = a.Rows, ["cols"] = a.Cols, ["cells"] = new JsonArray(a.Cells.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()),
                    ["title"] = a.Title, ["xlabel"] = a.XLabel, ["ylabel"] = a.YLabel, ["zlabel"] = a.ZLabel,
                    ["grid"] = a.Grid, ["minorgrid"] = a.MinorGrid, ["box"] = a.Box, ["xlog"] = a.XLog, ["ylog"] = a.YLog,
                    ["az"] = a.Az, ["el"] = a.El, ["ydirreverse"] = a.YDirReverse, ["equal"] = a.Equal, ["colorbar"] = a.Colorbar,
                    ["props"] = PropsJ(a, AxesStyleProps)
                };
                if (a.XLim != null) aj["xlim"] = Arr(a.XLim);
                if (a.YLim != null) aj["ylim"] = Arr(a.YLim);
                if (a.ZLim != null) aj["zlim"] = Arr(a.ZLim);
                if (a.CLim != null) aj["clim"] = Arr(a.CLim);
                var lj = new JsonObject();
                foreach (var (w, lo) in new[] { ("title", a.TitleObj), ("xlabel", a.XLabelObj), ("ylabel", a.YLabelObj), ("zlabel", a.ZLabelObj) })
                    if (lo != null) lj[w] = PropsJ(lo, TextStyleProps);
                aj["labelprops"] = lj;
                if (a.LegendVisible)
                    aj["legend"] = new JsonObject
                    {
                        ["labels"] = a.LegendLabels == null ? null : new JsonArray(a.LegendLabels.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
                        ["location"] = a.LegendLocation,
                        ["props"] = a.LegendObj != null ? PropsJ(a.LegendObj, LegendStyleProps) : new JsonObject()
                    };
                var kids = new JsonArray();
                foreach (var s in a.Children.Where(s => !s.Deleted))
                {
                    JsonObject? k = s switch
                    {
                        LineSeries l => new JsonObject
                        {
                            ["type"] = "line", ["x"] = Arr(l.X), ["y"] = Arr(l.Y), ["z"] = l.Z == null ? null : Arr(l.Z), ["color"] = RgbJ(l.Color),
                            ["linestyle"] = LineStyleStr(l.Style), ["marker"] = MarkerStr(l.Marker), ["linewidth"] = l.LineWidth, ["markersize"] = l.MarkerSize,
                            ["markerfilled"] = l.MarkerFilled, ["markeredge"] = l.MarkerEdge is { } me ? RgbJ(me) : null, ["markerface"] = l.MarkerFace is { } mf ? RgbJ(mf) : null,
                            ["markerindices"] = l.MarkerIndices == null ? null : Arr(l.MarkerIndices.Select(i => (double)i)), ["props"] = PropsJ(l, LineExtraProps)
                        },
                        ScatterSeries sc => new JsonObject
                        {
                            ["type"] = "scatter", ["x"] = Arr(sc.X), ["y"] = Arr(sc.Y), ["sizes"] = Arr(sc.Sizes), ["color"] = RgbJ(sc.Color), ["filled"] = sc.Filled,
                            ["marker"] = MarkerStr(sc.Marker), ["cdata"] = sc.CData == null ? null : Arr(sc.CData)
                        },
                        BarSeries b => new JsonObject
                        {
                            ["type"] = "bar", ["x"] = Arr(b.X), ["y"] = Arr(b.Y), ["width"] = b.Width, ["color"] = RgbJ(b.Color), ["group"] = b.GroupIndex,
                            ["groups"] = b.GroupCount, ["horizontal"] = b.Horizontal
                        },
                        PatchSeries p => new JsonObject
                        {
                            ["type"] = "patch", ["polys"] = new JsonArray(p.Polys.Select(q => (JsonNode?)new JsonObject { ["x"] = Arr(q.x), ["y"] = Arr(q.y) }).ToArray()),
                            ["facecolor"] = p.FaceColor is { } fc ? RgbJ(fc) : null, ["edgecolor"] = p.EdgeColor is { } ec ? RgbJ(ec) : null, ["facealpha"] = p.FaceAlpha, ["linewidth"] = p.LineWidth
                        },
                        TextSeries t => new JsonObject
                        {
                            ["type"] = "text", ["x"] = t.X, ["y"] = t.Y, ["string"] = t.Text, ["color"] = RgbJ(t.Color), ["fontsize"] = t.FontSize, ["halign"] = t.HAlign,
                            ["props"] = PropsJ(t, new[] { "fontname", "fontweight", "fontangle", "rotation", "interpreter" })
                        },
                        SurfaceSeries sf => new JsonObject
                        {
                            ["type"] = "surface", ["rows"] = sf.Z.GetLength(0), ["cols"] = sf.Z.GetLength(1), ["x"] = Arr(Flat(sf.X)), ["y"] = Arr(Flat(sf.Y)), ["z"] = Arr(Flat(sf.Z)),
                            ["mesh"] = sf.Mesh, ["shading"] = sf.Shading
                        },
                        ImageSeries im => new JsonObject
                        {
                            ["type"] = "image", ["rows"] = im.C.GetLength(0), ["cols"] = im.C.GetLength(1), ["c"] = Arr(Flat(im.C)), ["x"] = Arr(new[] { im.X0, im.X1 }), ["y"] = Arr(new[] { im.Y0, im.Y1 })
                        },
                        _ => null
                    };
                    if (k != null) { if (s.DisplayName != "") k["displayname"] = s.DisplayName; kids.Add(k); }
                }
                aj["children"] = kids;
                axes.Add(aj);
            }
            fj["axes"] = axes;
            return fj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        // column-major flatten of [row,col]
        static double[] Flat(double[,] a)
        {
            int r = a.GetLength(0), c = a.GetLength(1);
            var o = new double[r * c];
            for (int j = 0; j < c; j++) for (int i = 0; i < r; i++) o[j * r + i] = a[i, j];
            return o;
        }

        // ------------------------------------------------------------------ JSON -> .m code
        static string Q(string s) => "'" + s.Replace("'", "''").Replace("\r", "").Replace("\n", "' newline '") + "'";
        static string NumJ(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? Fmt(d) : n is JsonValue sv && sv.TryGetValue<string>(out var s) ? s : "0";
        static string Vec(JsonNode? n) { var d = Dbl(n); return d.Length == 1 ? Fmt(d[0]) : "[" + string.Join(" ", d.Select(Fmt)) + "]"; }
        static string Mat(JsonNode? n, int r, int c)
        {
            var d = Dbl(n);
            return "[" + string.Join("; ", Enumerable.Range(0, r).Select(i => string.Join(" ", Enumerable.Range(0, c).Select(j => Fmt(d[j * r + i]))))) + "]";
        }
        static string ValCode(Value v) => v switch
        {
            MatrixValue m when m.IsChar => Q(m.AsString()),
            MatrixValue m => m.Numel == 1 ? Fmt(m.Re[0]) : m.R <= 1 ? "[" + string.Join(" ", m.Re.Select(Fmt)) + "]" : "[" + string.Join("; ", Enumerable.Range(0, m.R).Select(r => string.Join(" ", Enumerable.Range(0, m.C).Select(c => Fmt(m.Re[c * m.R + r]))))) + "]",
            CellValue c => "{" + string.Join(", ", c.Data.Select(ValCode)) + "}",
            _ => "[]"
        };
        static string PairsCode(JsonNode? props)
        {
            if (props is not JsonObject o) return "";
            var sb = new StringBuilder();
            foreach (var (k, n) in o) if (JsonVal(n) is { } v) sb.Append(", ").Append(Q(Cap(k))).Append(", ").Append(ValCode(v));
            return sb.ToString();
        }
        static readonly Dictionary<string, string> Caps = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fontname"] = "FontName", ["fontsize"] = "FontSize", ["fontweight"] = "FontWeight", ["fontangle"] = "FontAngle", ["tickdir"] = "TickDir", ["ticklength"] = "TickLength",
            ["linewidth"] = "LineWidth", ["xcolor"] = "XColor", ["ycolor"] = "YColor", ["color"] = "Color", ["xtick"] = "XTick", ["ytick"] = "YTick", ["xticklabel"] = "XTickLabel",
            ["yticklabel"] = "YTickLabel", ["xticklabelrotation"] = "XTickLabelRotation", ["xminorgrid"] = "XMinorGrid", ["yminorgrid"] = "YMinorGrid", ["xgrid"] = "XGrid",
            ["ygrid"] = "YGrid", ["gridcolor"] = "GridColor", ["gridlinestyle"] = "GridLineStyle", ["textcolor"] = "TextColor", ["box"] = "Box", ["orientation"] = "Orientation",
            ["interpreter"] = "Interpreter", ["visible"] = "Visible", ["tag"] = "Tag", ["edgecolor"] = "EdgeColor", ["numcolumns"] = "NumColumns", ["rotation"] = "Rotation",
            ["horizontalalignment"] = "HorizontalAlignment", ["gridalpha"] = "GridAlpha", ["layer"] = "Layer"
        };
        static string Cap(string k) => Caps.TryGetValue(k, out var c) ? c : k;

        /// <summary>MATLAB code that recreates the figure described by a .fig JSON document.</summary>
        public static string CodeFromJson(string json, string funcName = "")
        {
            var f = JsonNode.Parse(json)!.AsObject();
            var sb = new StringBuilder();
            bool fn = funcName != "";
            string ind = fn ? "  " : "";
            if (fn) sb.AppendLine($"function fig = {funcName} ()");
            sb.AppendLine($"{ind}% Generated by MatOct (Generate Code) on {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.Append($"{ind}fig = figure ('Name', {Q((string?)f["name"] ?? "")}, 'NumberTitle', '{(((bool?)f["numbertitle"] ?? true) ? "on" : "off")}', 'Position', [100 100 {(int?)f["width"] ?? 560} {(int?)f["height"] ?? 420}]");
            sb.AppendLine(PairsCode(f["props"]) + ");");
            sb.AppendLine($"{ind}colormap ({Q((string?)f["colormap"] ?? "viridis")});");
            foreach (var an in f["axes"]!.AsArray())
            {
                var a = an!.AsObject();
                var cells = a["cells"]!.AsArray().Select(c => (int)c!).ToArray();
                sb.AppendLine($"{ind}ax = subplot ({a["rows"]}, {a["cols"]}, {(cells.Length == 1 ? cells[0].ToString() : "[" + string.Join(" ", cells) + "]")});");
                sb.AppendLine($"{ind}hold (ax, 'on');");
                var kids = a["children"]!.AsArray();
                var legendHandles = new List<string>();
                int hk = 0;
                for (int i = 0; i < kids.Count; i++)
                {
                    var k = kids[i]!.AsObject();
                    var dn = (string?)k["displayname"];
                    var h = $"h{++hk}";
                    switch ((string?)k["type"])
                    {
                        case "line":
                        {
                            var z = k["z"];
                            sb.Append(z != null ? $"{ind}{h} = plot3 (ax, {Vec(k["x"])}, {Vec(k["y"])}, {Vec(z)}" : $"{ind}{h} = plot (ax, {Vec(k["x"])}, {Vec(k["y"])}");
                            sb.Append($", 'Color', {Vec(k["color"])}, 'LineStyle', {Q((string?)k["linestyle"] ?? "-")}, 'LineWidth', {NumJ(k["linewidth"])}");
                            sb.Append($", 'Marker', {Q((string?)k["marker"] ?? "none")}, 'MarkerSize', {NumJ(k["markersize"])}");
                            if (k["markeredge"] != null) sb.Append($", 'MarkerEdgeColor', {Vec(k["markeredge"])}");
                            if (k["markerface"] != null) sb.Append($", 'MarkerFaceColor', {Vec(k["markerface"])}");
                            else if ((bool?)k["markerfilled"] == true) sb.Append(", 'MarkerFaceColor', 'auto'");
                            if (k["markerindices"] != null) sb.Append($", 'MarkerIndices', {Vec(k["markerindices"])}");
                            if (!string.IsNullOrEmpty(dn)) sb.Append($", 'DisplayName', {Q(dn)}");
                            sb.AppendLine(PairsCode(k["props"]) + ");");
                            break;
                        }
                        case "scatter":
                            sb.Append($"{ind}{h} = scatter (ax, {Vec(k["x"])}, {Vec(k["y"])}, {Vec(k["sizes"])}, {(k["cdata"] != null ? Vec(k["cdata"]) : Vec(k["color"]))}, {Q((string?)k["marker"] ?? "o")}");
                            if ((bool?)k["filled"] == true) sb.Append(", 'filled'");
                            sb.AppendLine(");");
                            break;
                        case "bar":
                        {
                            int groups = (int?)k["groups"] ?? 1;
                            var ys = new List<string> { Vec(k["y"]) };
                            var cols = new List<string> { Vec(k["color"]) };
                            while (groups > 1 && ys.Count < groups && i + 1 < kids.Count && (string?)kids[i + 1]!["type"] == "bar") { i++; ys.Add(Vec(kids[i]!["y"])); cols.Add(Vec(kids[i]!["color"])); }
                            var fnb = (bool?)k["horizontal"] == true ? "barh" : "bar";
                            if (ys.Count == 1) sb.AppendLine($"{ind}{h} = {fnb} (ax, {Vec(k["x"])}, {ys[0]}, {NumJ(k["width"])}, 'FaceColor', {cols[0]});");
                            else
                            {
                                sb.AppendLine($"{ind}{h} = {fnb} (ax, {Vec(k["x"])}, [{string.Join("; ", ys)}].', {NumJ(k["width"])});");
                                for (int g = 0; g < cols.Count; g++) sb.AppendLine($"{ind}set ({h}({g + 1}), 'FaceColor', {cols[g]});");
                            }
                            break;
                        }
                        case "patch":
                            foreach (var p in k["polys"]!.AsArray())
                            {
                                sb.Append($"{ind}{h} = patch (ax, {Vec(p!["x"])}, {Vec(p["y"])}, {(k["facecolor"] != null ? Vec(k["facecolor"]) : "'w'")}");
                                if (k["facecolor"] == null) sb.Append(", 'FaceColor', 'none'");
                                sb.Append(k["edgecolor"] != null ? $", 'EdgeColor', {Vec(k["edgecolor"])}" : ", 'EdgeColor', 'none'");
                                sb.AppendLine($", 'FaceAlpha', {NumJ(k["facealpha"])}, 'LineWidth', {NumJ(k["linewidth"])});");
                            }
                            break;
                        case "text":
                            sb.AppendLine($"{ind}{h} = text (ax, {NumJ(k["x"])}, {NumJ(k["y"])}, {Q((string?)k["string"] ?? "")}, 'Color', {Vec(k["color"])}, 'FontSize', {NumJ(k["fontsize"])}, 'HorizontalAlignment', {Q((string?)k["halign"] ?? "left")}{PairsCode(k["props"])});");
                            break;
                        case "surface":
                        {
                            int r = (int?)k["rows"] ?? 0, c = (int?)k["cols"] ?? 0;
                            sb.AppendLine($"{ind}{h} = {((bool?)k["mesh"] == true ? "mesh" : "surf")} (ax, {Mat(k["x"], r, c)}, {Mat(k["y"], r, c)}, {Mat(k["z"], r, c)});");
                            if ((string?)k["shading"] is { } sh && sh != "faceted") sb.AppendLine($"{ind}shading (ax, {Q(sh)});");
                            break;
                        }
                        case "image":
                        {
                            int r = (int?)k["rows"] ?? 0, c = (int?)k["cols"] ?? 0;
                            sb.AppendLine($"{ind}{h} = imagesc (ax, {Vec(k["x"])}, {Vec(k["y"])}, {Mat(k["c"], r, c)});");
                            break;
                        }
                        default: hk--; continue;
                    }
                    if (!string.IsNullOrEmpty(dn) && (string?)k["type"] != "line") sb.AppendLine($"{ind}set ({h}, 'DisplayName', {Q(dn)});");
                    legendHandles.Add(h);
                }
                sb.AppendLine($"{ind}hold (ax, 'off');");
                foreach (var w in new[] { "title", "xlabel", "ylabel", "zlabel" })
                {
                    var txt = (string?)a[w] ?? "";
                    var lp = a["labelprops"]?[w];
                    if (txt != "" || (lp is JsonObject lo && lo.Count > 0)) sb.AppendLine($"{ind}{w} (ax, {Q(txt)}{PairsCode(lp)});");
                }
                if (a["xlim"] != null) sb.AppendLine($"{ind}xlim (ax, {Vec(a["xlim"])});");
                if (a["ylim"] != null) sb.AppendLine($"{ind}ylim (ax, {Vec(a["ylim"])});");
                if (a["zlim"] != null) sb.AppendLine($"{ind}zlim (ax, {Vec(a["zlim"])});");
                if (a["clim"] != null) sb.AppendLine($"{ind}caxis (ax, {Vec(a["clim"])});");
                double az = (double?)a["az"] ?? 0, el = (double?)a["el"] ?? 90;
                if (!(az == 0 && el == 90)) sb.AppendLine($"{ind}view (ax, {Fmt(az)}, {Fmt(el)});");
                var setp = new StringBuilder();
                if ((bool?)a["xlog"] == true) setp.Append(", 'XScale', 'log'");
                if ((bool?)a["ylog"] == true) setp.Append(", 'YScale', 'log'");
                if ((bool?)a["ydirreverse"] == true) setp.Append(", 'YDir', 'reverse'");
                var ap = a["props"] as JsonObject;
                if ((bool?)a["grid"] == true && ap?["xgrid"] == null) setp.Append(", 'XGrid', 'on', 'YGrid', 'on'");
                if ((bool?)a["minorgrid"] == true && ap?["xminorgrid"] == null) setp.Append(", 'XMinorGrid', 'on', 'YMinorGrid', 'on'");
                setp.Append(", 'Box', ").Append((bool?)a["box"] == false ? "'off'" : "'on'");
                setp.Append(PairsCode(ap));
                sb.AppendLine($"{ind}set (ax{setp});");
                if ((bool?)a["equal"] == true) sb.AppendLine($"{ind}axis (ax, 'equal');");
                if ((bool?)a["colorbar"] == true) sb.AppendLine($"{ind}colorbar (ax);");
                if (a["legend"] is JsonObject lg)
                {
                    var labels = lg["labels"] is JsonArray la ? "{" + string.Join(", ", la.Select(x => Q((string?)x ?? ""))) + "}" : null;
                    sb.AppendLine($"{ind}legend (ax, {labels ?? "'show'"}, 'Location', {Q((string?)lg["location"] ?? "northeast")}{PairsCode(lg["props"])});");
                }
            }
            if (fn) sb.AppendLine("end");
            return sb.ToString();
        }

        public static string GenerateCode(Figure f, string funcName = "") => CodeFromJson(ToJson(f), funcName);

        /// <summary>Recreate a figure from a .fig JSON file; returns the new figure handle.</summary>
        public static double OpenFig(Interpreter it, string path)
        {
            var code = CodeFromJson(File.ReadAllText(path));
            it.Eval(code);
            return it.Graphics.Figures.Count > 0 ? it.Graphics.Figures.Values.Max(x => x.Handle) : 0;
        }
    }

    public static partial class BuiltinLibrary
    {
        internal static void UiSetProps(Interpreter it, GObject o, Value[] a) { SetProps(it, o, a, "set"); it.Graphics.Touch(FigOf(o)); }
        internal static Value UiGetAny(Interpreter it, GObject o, string p) => GetAny(it, o, p);
        internal static IEnumerable<string> UiPropNames(Interpreter it, GObject o) => PropNames(it, o);
        internal static string UiTypeOf(GObject o) => TypeOf(o);
        static Figure? FigOf(GObject o) => o switch
        {
            Figure f => f, Axes a => a.Figure, Series s => s.Parent?.Figure ?? (s is GenericObject g ? g.Owner as Figure : null), LabelObject l => l.Axes.Figure, _ => null
        };

        static Figure FigArg(Interpreter it, Value[] a, ref int i, string fn)
        {
            if (a.Length > i && a[i] is MatrixValue m && !m.IsChar && m.Numel == 1)
            {
                var o = it.Graphics.Lookup(m.Re[0]) ?? throw new OctaveException($"{fn}: H must be a valid figure handle");
                i++;
                return FigOf(o) ?? throw new OctaveException($"{fn}: H must be a valid figure handle");
            }
            return it.Graphics.Current ?? throw new OctaveException($"{fn}: no current figure");
        }

        static void RegisterFigureEdit()
        {
            BuiltinFn save = (it, a, n) =>
            {
                int i = 0;
                var f = FigArg(it, a, ref i, "savefig");
                var path = a.Length > i ? S(a[i], "savefig") : "Untitled.fig";
                if (Path.GetExtension(path) == "") path += ".fig";
                File.WriteAllText(Path.GetFullPath(path), FigureEditApi.ToJson(f));
                return Array.Empty<Value>();
            };
            RegG("savefig", save);
            RegG("hgsave", save);
            BuiltinFn open = (it, a, n) =>
            {
                if (a.Length < 1) throw new OctaveException("Invalid call to openfig");
                var path = S(a[0], "openfig");
                if (Path.GetExtension(path) == "") path += ".fig";
                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) throw new OctaveException($"openfig: unable to find file {path}");
                return new Value[] { Num(FigureEditApi.OpenFig(it, full)) };
            };
            RegG("openfig", open);
            RegG("hgload", open);
            RegG("makemcode", (it, a, n) =>
            {
                int i = 0;
                var f = FigArg(it, a, ref i, "makemcode");
                if (a.Length > i)
                {
                    var path = S(a[i], "makemcode");
                    var func = Path.GetFileNameWithoutExtension(path);
                    File.WriteAllText(Path.GetFullPath(path), FigureEditApi.GenerateCode(f, func));
                    return Array.Empty<Value>();
                }
                var code = FigureEditApi.GenerateCode(f);
                if (n == 0) { it.Out.Write(code); return Array.Empty<Value>(); }
                return new Value[] { Str(code) };
            });
        }
    }
}
