// MatRewrite - GPL-3.0-or-later
// Phase 9: Octave-style graphics property validation, plus native axes/sphere/xline/yline.
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static readonly string[] OnOffV = { "on", "off" };
    static readonly Dictionary<string, string[]> RadioProps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xscale"] = new[] { "linear", "log" }, ["yscale"] = new[] { "linear", "log" }, ["zscale"] = new[] { "linear", "log" },
        ["xdir"] = new[] { "normal", "reverse" }, ["ydir"] = new[] { "normal", "reverse" }, ["zdir"] = new[] { "normal", "reverse" },
        ["box"] = OnOffV, ["xgrid"] = OnOffV, ["ygrid"] = OnOffV, ["zgrid"] = OnOffV, ["xminorgrid"] = OnOffV, ["yminorgrid"] = OnOffV, ["zminorgrid"] = OnOffV,
        ["xminortick"] = OnOffV, ["yminortick"] = OnOffV, ["zminortick"] = OnOffV,
        ["visible"] = OnOffV, ["clipping"] = OnOffV, ["hittest"] = OnOffV, ["numbertitle"] = OnOffV, ["selected"] = OnOffV, ["selectionhighlight"] = OnOffV,
        ["handlevisibility"] = new[] { "on", "off", "callback" }, ["busyaction"] = new[] { "cancel", "queue" }, ["interruptible"] = OnOffV,
        ["xlimmode"] = new[] { "auto", "manual" }, ["ylimmode"] = new[] { "auto", "manual" }, ["zlimmode"] = new[] { "auto", "manual" },
        ["climmode"] = new[] { "auto", "manual" }, ["xtickmode"] = new[] { "auto", "manual" }, ["ytickmode"] = new[] { "auto", "manual" }, ["ztickmode"] = new[] { "auto", "manual" },
        ["fontweight"] = new[] { "normal", "bold" }, ["fontangle"] = new[] { "normal", "italic" },
        ["xaxislocation"] = new[] { "bottom", "top", "origin" }, ["yaxislocation"] = new[] { "left", "right", "origin" },
        ["tickdir"] = new[] { "in", "out", "both", "none" }, ["layer"] = new[] { "bottom", "top" },
        ["projection"] = new[] { "orthographic", "perspective" },
    };
    static readonly HashSet<string> DoubleProps = new(StringComparer.OrdinalIgnoreCase) { "linewidth", "markersize", "fontsize", "rotation", "barwidth", "facealpha", "edgealpha" };
    static readonly HashSet<string> ColorProps = new(StringComparer.OrdinalIgnoreCase) { "color", "xcolor", "ycolor", "zcolor", "markeredgecolor", "markerfacecolor", "edgecolor", "facecolor", "backgroundcolor" };
    static readonly HashSet<string> ColorNoneOK = new(StringComparer.OrdinalIgnoreCase) { "markeredgecolor", "markerfacecolor", "edgecolor", "facecolor", "color", "backgroundcolor" };
    static readonly HashSet<string> ReadOnlyProps = new(StringComparer.OrdinalIgnoreCase) { "type", "beingdeleted", "currentcharacter", "currentpoint", "screendepth", "screensize", "screenpixelsperinch", "extent", "tightinset" };

    /// <summary>Validate VALUE for PROP the way Octave's typed graphics properties do (radio, double, color, read-only).</summary>
    static void ValidatePropValue(GObject o, string p, Value v, string fn)
    {
        if (ReadOnlyProps.Contains(p) && !(o is RootObject && p is "screensize" or "screenpixelsperinch" or "screendepth"))
            throw new OctaveException($"set: \"{p}\" is read-only");
        if (RadioProps.TryGetValue(p, out var opts))
        {
            if (!IsString(v)) throw new OctaveException($"set: invalid value for radio property \"{p}\"");
            var s = S(v, fn);
            if (p is "visible" or "box" or "clipping" or "hittest" && false) { }
            if (!opts.Any(q => q.Equals(s, StringComparison.OrdinalIgnoreCase)))
            {
                if (p.Equals("visible", StringComparison.OrdinalIgnoreCase) || opts == OnOffV)
                    throw new OctaveException($"set: invalid value for bool property \"{p}\" (value = {s})");
                throw new OctaveException($"set: invalid value for radio property \"{p}\" (value = {s})");
            }
            return;
        }
        if (DoubleProps.Contains(p) && !(o is SurfaceSeries && p is "facealpha" or "edgealpha" && IsString(v)))
        {
            if (v is not MatrixValue m || m.IsChar || m.Numel != 1 || m.Im != null || double.IsNaN(m.Re[0]) && p != "rotation")
                throw new OctaveException($"set: invalid value for double property \"{p}\"");
            if (p is "linewidth" or "markersize" or "fontsize" && m.Re[0] <= 0)
                throw new OctaveException($"set: invalid value for double property \"{p}\"");
            return;
        }
        if (ColorProps.Contains(p))
        {
            if (IsString(v))
            {
                var s = S(v, fn).Trim().ToLowerInvariant();
                if (s is "none" && ColorNoneOK.Contains(p) || s is "flat" or "interp" or "auto" && p is "edgecolor" or "facecolor" or "markeredgecolor" or "markerfacecolor") return;
                if (Rgb.FromName(s) == null && !(s.Length == 1 && Rgb.FromChar(s[0]) != null))
                    throw new OctaveException($"set: invalid value for color property \"{p}\" (value = {S(v, fn)})");
                return;
            }
            if (v is MatrixValue m && !m.IsChar && m.Im == null && m.Numel == 3 && (m.R == 1 || m.C == 1))
            {
                if (m.Re.Any(x => !(x >= 0 && x <= 1))) throw new OctaveException($"set: invalid value for color property \"{p}\"");
                return;
            }
            if (p is "facecolor" or "edgecolor" or "color" && v is MatrixValue && o is not (LineSeries or Axes or Figure or TextSeries)) return; // truecolor data on patches
            throw new OctaveException($"set: invalid value for color property \"{p}\"");
        }
    }

    static void RegisterPhase9Graphics()
    {
        RegG("axes", (it, a, nout) =>
        {
            var g = it.Graphics;
            GObject? hpar = null;
            if (a.Length > 0 && a[0] is MatrixValue hm && !hm.IsChar && hm.Numel >= 1 && g.Lookup(hm.Re[0]) is { } ho)
            {
                if (hm.Numel != 1) throw new OctaveException("axes: H must be a scalar handle");
                if (ho is Axes ax && a.Length == 1)
                {
                    g.Current = ax.Figure; ax.Figure.CurrentAxes = ax;
                    ax.Figure.Axes.Remove(ax); ax.Figure.Axes.Add(ax);
                    return nout > 0 ? Out(H(ax)) : Array.Empty<Value>();
                }
                if (ho is Figure || ho is GenericObject go && go.Type is "uipanel" or "uitab") { hpar = ho; a = a[1..]; }
                else throw new OctaveException("axes: H must be a handle to an axes or container");
            }
            if (hpar == null)
            {
                for (int k = 0; k + 1 < a.Length; k += 2)
                    if (IsString(a[k]) && S(a[k], "axes").Equals("parent", StringComparison.OrdinalIgnoreCase))
                    { hpar = HLookup(it, a[k + 1], "axes"); a = a.Take(k).Concat(a.Skip(k + 2)).ToArray(); break; }
                hpar ??= g.Gcf();
            }
            var res = Fc(it, "__go_axes__", Num(hpar.Handle));
            var axo = g.Lookup(D(res, "axes"))!;
            if (a.Length > 0) ApplyPairs(it, axo, a, "axes");
            if (axo is Axes nax && !(axo.Props.TryGetValue("handlevisibility", out var hv) && IsString(hv) && S(hv, "axes") == "off"))
            { nax.Figure.CurrentAxes = nax; g.Current = nax.Figure; }
            return nout > 0 ? Out(res) : Array.Empty<Value>();
        });

        Reg("sphere", (it, a, nout) =>
        {
            Value? hax = null;
            if (a.Length > 0 && a[0] is MatrixValue hm && hm.Numel == 1 && !hm.IsChar && it.Graphics.Lookup(hm.Re[0]) is Axes) { hax = a[0]; a = a[1..]; }
            if (a.Length > 1) Usage("sphere");
            double n = 20;
            if (a.Length == 1)
            {
                if (!(a[0] is MatrixValue nm && !nm.IsChar && nm.Numel == 1 && nm.Im == null && nm.Re[0] > 0)) throw new OctaveException("sphere: N must be a real scalar > 0");
                n = nm.Re[0];
            }
            int N = (int)n + 1;
            var th = Lin(0, 2 * Math.PI, N); var ph = Lin(-Math.PI / 2, Math.PI / 2, N);
            MatrixValue X = new(N, N), Y = new(N, N), Z = new(N, N);
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
                { X.Re[i + N * j] = Math.Cos(ph[i]) * Math.Cos(th[j]); Y.Re[i + N * j] = Math.Cos(ph[i]) * Math.Sin(th[j]); Z.Re[i + N * j] = Math.Sin(ph[i]); }
            if (nout > 0) return Out(X, Y, Z);
            if (hax != null) Fc(it, "surf", hax, X, Y, Z); else Fc(it, "surf", X, Y, Z);
            return Array.Empty<Value>();
        });
    }
}
