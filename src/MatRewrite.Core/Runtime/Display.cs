// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;

namespace MatRewrite.Core.Runtime;

/// <summary>Octave-style value display ("x = 5", matrices with blank lines, cells in braces, ...).</summary>
public static class Display
{
    public static bool LongFormat = false;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Output for "name = value" (statement without semicolon).</summary>
    public static string Show(string name, Value v)
    {
        var sb = new StringBuilder();
        ShowInto(sb, name, v, "");
        return sb.ToString();
    }

    static void ShowInto(StringBuilder sb, string name, Value v, string ind)
    {
        if (v is ArrayValue av && av.ND != null)
        {
            if (av.Numel == 0 || v is not MatrixValue) { sb.Append($"{ind}{name} = {(v is CellValue ? "{}" : v is StructValue ? "" : "[]")}({av.DimString}){(v is StructValue ? " struct array" : "")}\n"); return; }
            sb.Append($"{ind}{name} =\n\n");
            sb.Append(NDPages(av, ind));
            return;
        }
        switch (v)
        {
            case Sparse.SparseValue sp:
                SparseInto(sb, name, sp, ind);
                break;
            case MatrixValue m:
                if (m.Numel == 0)
                {
                    if (m.IsChar && m.R <= 1) sb.Append($"{ind}{name} = \n");
                    else sb.Append($"{ind}{name} = []({m.R}x{m.C})\n");
                }
                else if (m.IsChar)
                {
                    if (m.R == 1) sb.Append($"{ind}{name} = {m.AsString()}\n");
                    else
                    {
                        sb.Append($"{ind}{name} =\n\n");
                        for (int i = 0; i < m.R; i++) sb.Append(ind).Append(m.RowString(i)).Append('\n');
                        sb.Append('\n');
                    }
                }
                else if (m.IsScalar) sb.Append($"{ind}{name} = {FormatScalar(m)}\n");
                else
                {
                    sb.Append($"{ind}{name} =\n\n");
                    foreach (var line in MatrixLines(m)) sb.Append(ind).Append(line).Append('\n');
                    sb.Append('\n');
                }
                break;
            case ObjectValue eo when eo.EnumName != null:
                sb.Append($"{ind}{name} = {eo.EnumName}\n");
                break;
            case ObjectValue o:
                sb.Append($"{ind}{name} =\n\n").Append(ObjectBody(o, ind)).Append('\n');
                break;
            case ObjectArrayValue oa:
                sb.Append($"{ind}{name} =\n\n").Append(ObjArrBody(oa, ind)).Append('\n');
                break;
            case CellValue c:
                if (c.Numel == 0) { sb.Append($"{ind}{name} = {{}}({c.R}x{c.C})\n"); break; }
                sb.Append($"{ind}{name} =\n");
                CellBody(sb, c, ind);
                sb.Append('\n');
                break;
            case StructValue ts when ts.Tag != null:
                sb.Append(Tagged.Show(name, ts, ind));
                break;
            case StructValue s:
                if (s.Numel == 1)
                {
                    sb.Append($"{ind}{name} =\n\n{ind}  scalar structure containing the fields:\n\n");
                    StructFields(sb, s, ind + "    ");
                    sb.Append('\n');
                }
                else
                {
                    sb.Append($"{ind}{name} =\n\n{ind}  {s.R}x{s.C} struct array ");
                    sb.Append(s.Fields.Count > 0 ? "containing the fields:\n\n" : "with no fields\n");
                    foreach (var f in s.Fields) sb.Append($"{ind}    {f}\n");
                    sb.Append('\n');
                }
                break;
            case MapValue mp:
                sb.Append($"{ind}{name} =\n\n{ind}  containers.Map object with properties:\n\n");
                sb.Append($"{ind}    Count     : {mp.Count}\n{ind}    KeyType   : {mp.KeyType}\n{ind}    ValueType : {mp.ValueType}\n\n");
                break;
            case FunctionHandleValue fh:
                if (fh.IsAnonymous) sb.Append($"{ind}{name} =\n\n{ind}{fh}\n\n");
                else sb.Append($"{ind}{name} = {fh}\n");
                break;
            default:
                sb.Append($"{ind}{name} = <{v.ClassName}>\n");
                break;
        }
    }

    static void CellBody(StringBuilder sb, CellValue c, string ind)
    {
        sb.Append(ind).Append("{\n");
        for (int j = 0; j < c.C; j++)
            for (int i = 0; i < c.R; i++)
            {
                var el = c.Data[i + j * c.R];
                string nm = $"[{i + 1},{j + 1}]";
                if (el is CellValue inner)
                {
                    if (inner.Numel == 0) sb.Append($"{ind}  {nm} = {{}}({inner.R}x{inner.C})\n");
                    else { sb.Append($"{ind}  {nm} =\n"); CellBody(sb, inner, ind + "  "); }
                }
                else ShowInto(sb, nm, el, ind + "  ");
            }
        sb.Append(ind).Append("}\n");
    }

    static void StructFields(StringBuilder sb, StructValue s, string ind)
    {
        foreach (var f in s.Fields)
        {
            var v = s.Elems[0][f];
            if (v is StructValue sub && sub.Numel == 1)
            {
                sb.Append($"{ind}{f} =\n\n{ind}  scalar structure containing the fields:\n\n");
                StructFields(sb, sub, ind + "    ");
                sb.Append('\n');
            }
            else if (v is CellValue cv && cv.Numel > 0)
            {
                sb.Append($"{ind}{f} =\n");
                CellBody(sb, cv, ind);
                sb.Append('\n');
            }
            else ShowInto(sb, f, v, ind);
        }
    }

    /// <summary>Output of disp(x).</summary>
    static string NDPages(ArrayValue a, string ind)
    {
        var sb = new StringBuilder();
        var d = a.Dims;
        int pages = NDArray.Prod(d, 2);
        for (int p = 0; p < pages; p++)
        {
            var pg = (MatrixValue)NDArray.Page(a, p);
            sb.Append($"{ind}ans{NDArray.PageLabel(d, p)} =\n\n");
            if (pg.IsChar) for (int i = 0; i < pg.R; i++) sb.Append(ind).Append(pg.RowString(i)).Append('\n');
            else foreach (var line in MatrixLines(pg)) sb.Append(ind).Append(line).Append('\n');
            if (p < pages - 1) sb.Append('\n');
        }
        sb.Append('\n');
        return sb.ToString();
    }

    public static string Disp(Value v)
    {
        if (v is MatrixValue nm && nm.ND != null && nm.Numel > 0) return NDPages(nm, "");
        switch (v)
        {
            case Sparse.SparseValue spv: return SparseBody(spv, "");
            case ObjectValue eo when eo.EnumName != null: return eo.EnumName + "\n";
            case ObjectValue o: return ObjectBody(o, "");
            case ObjectArrayValue oa: return ObjArrBody(oa, "");
            case MatrixValue m:
                if (m.Numel == 0) return m.IsChar ? "\n" : "";
                if (m.IsChar)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < m.R; i++) sb.Append(m.RowString(i)).Append('\n');
                    return sb.ToString();
                }
                if (m.IsScalar) return FormatScalar(m) + "\n";
                return string.Join("", MatrixLines(m).Select(l => l + "\n"));
            case CellValue c:
            {
                if (c.Numel == 0) return $"{{}}({c.R}x{c.C})\n";
                var sb = new StringBuilder(); CellBody(sb, c, ""); return sb.ToString();
            }
            case StructValue ts when ts.Tag != null:
                return Tagged.Body(ts);
            case StructValue s:
            {
                var sb = new StringBuilder();
                if (s.Numel == 1) StructFields(sb, s, "    ");
                else sb.Append($"  {s.R}x{s.C} struct array containing the fields:\n\n").Append(string.Join("", s.Fields.Select(f => "    " + f + "\n")));
                return sb.ToString();
            }
            case FunctionHandleValue fh: return fh + "\n";
        }
        return v.ToString() + "\n";
    }

    // ------------- number formatting -------------
    static bool AllInt(MatrixValue m)
    {
        for (int k = 0; k < m.Numel; k++)
        {
            double x = m.Re[k];
            if (double.IsFinite(x) && x != Math.Floor(x)) return false;
            if (m.Im != null) { double y = m.Im[k]; if (double.IsFinite(y) && y != Math.Floor(y)) return false; }
        }
        return true;
    }

    static int IntDigits(double a) => a < 1 ? 1 : (int)Math.Floor(Math.Log10(a)) + 1;

    static void SparseInto(StringBuilder sb, string name, Sparse.SparseValue m, string ind)
    {
        sb.Append($"{ind}{name} =\n\n");
        sb.Append(SparseBody(m, ind));
    }

    static string SparseBody(Sparse.SparseValue m, string ind)
    {
        var sb = new StringBuilder();
        int nz = m.Nnz;
        double pct = m.Numel == 0 ? 0 : 100.0 * nz / m.Numel;
        string ps = pct == 100 ? "100" : pct >= 10 ? pct.ToString("0", Inv) : pct.ToString("0.#", Inv);
        if (pct > 0 && ps == "0") ps = pct.ToString("0.##", Inv);
        sb.Append($"{ind}Compressed Column Sparse (rows = {m.R}, cols = {m.C}, nnz = {nz} [{ps}%])\n\n");
        if (nz == 0) return sb.ToString();
        int wr = m.R.ToString().Length, wc = m.C.ToString().Length;
        for (int j = 0; j < m.C; j++)
            for (int p = m.Cp[j]; p < m.Cp[j + 1]; p++)
            {
                var e = new MatrixValue(1, 1, new[] { m.Re[p] }, m.Im == null ? null : new[] { m.Im[p] }, m.Logical ? NumKind.Logical : NumKind.Double);
                sb.Append($"{ind}  ({(m.Ri[p] + 1).ToString().PadLeft(wr)}, {(j + 1).ToString().PadLeft(wc)}) -> {FormatScalar(e)}\n");
            }
        sb.Append('\n');
        return sb.ToString();
    }

    static string ObjArrBody(ObjectArrayValue oa, string ind)
    {
        if (oa.Numel > 0 && oa.At(0).EnumName != null)
            return string.Concat(Enumerable.Range(0, oa.Numel).Select(i => $"{ind}  {oa.At(i).EnumName}\n"));
        var names = oa.Numel > 0 ? Interpreter.ObjectProps(oa.At(0)) : new List<string>();
        return $"{ind}  {oa.R}x{oa.C} {oa.Cls.Name} object array with properties:\n\n" + string.Concat(names.Select(n => $"{ind}    {n}\n"));
    }

    static string ObjectBody(ObjectValue o, string ind)
    {
        var sb = new StringBuilder();
        sb.Append($"{ind}  {o.Cls.Name} object with properties:\n\n");
        var names = Interpreter.ObjectProps(o);
        int w = names.Count == 0 ? 0 : names.Max(n => n.Length);
        foreach (var n in names)
        {
            var v = o.Props[n];
            string txt = v switch
            {
                MatrixValue m when m.IsChar && m.R <= 1 => m.AsString(),
                MatrixValue m when m.Numel == 1 => FormatScalar(m),
                MatrixValue m when m.Numel == 0 => "[](" + m.R + "x" + m.C + ")",
                MatrixValue m when m.R == 1 && m.Numel <= 10 => "[" + string.Join(" ", Enumerable.Range(0, m.Numel).Select(k => FormatScalar(new MatrixValue(1, 1, new[] { m.Re[k] }, null, m.Kind)))) + "]",
                _ => $"[{v.DimString} {v.ClassName}]"
            };
            sb.Append($"{ind}    {n.PadLeft(w)}: {txt}\n");
        }
        return sb.ToString();
    }

    public static string FormatReal(double x, int decimals, bool sci)
    {
        if (double.IsNaN(x)) return "NaN";
        if (double.IsInfinity(x)) return x > 0 ? "Inf" : "-Inf";
        if (sci) return FormatE(x, decimals);
        return x.ToString("F" + decimals, Inv);
    }

    public static string FormatE(double x, int prec)
    {
        var s = x.ToString((prec > 0 ? "0." + new string('0', prec) : "0") + "e+00", Inv);
        return s;
    }

    /// <summary>Choose decimals / sci for a set of finite magnitudes (Octave "format short" style).</summary>
    static (int dec, bool sci, int intW) RealFormat(IEnumerable<double> vals, bool scalar)
    {
        int sig = LongFormat ? 16 : 5;
        var fin = vals.Where(double.IsFinite).Select(Math.Abs).ToList();
        if (fin.Count == 0) return (0, false, 3);
        double mx = fin.Max();
        double mnNz = fin.Where(v => v > 0).DefaultIfEmpty(0).Min();
        if (mx >= Math.Pow(10, sig) || (mx > 0 && mx < 1e-5)) return (sig - 1, true, 1);
        int ld = IntDigits(mx);
        if (scalar)
        {
            if (mx < 1 && mx > 0)
            {
                int lead = (int)Math.Floor(Math.Log10(mx)); // e.g. -2 for 0.05
                if (mx < 0.001) return (sig - 1, true, 1);
                int dec = lead >= -1 ? sig - 1 : sig - 1 - lead - 1;
                if (lead < -1) dec = sig - lead - 1 - 0;
                return (Math.Max(dec, sig - 1), false, 1);
            }
            if (ld >= sig) return (sig - 1, true, 1);
            return (Math.Max(sig - ld, 0) == 0 ? 0 : Math.Max(sig - ld, 1), false, ld);
        }
        // matrix: ratio of max/min decides
        if (mnNz > 0 && mx / mnNz >= 1e5) return (sig - 1, true, 1);
        if (ld >= sig + 1) return (sig - 1, true, 1);
        return (sig - 1, false, ld);
    }

    public static string FormatScalar(MatrixValue m)
    {
        double re = m.Re[0];
        if (m.IsComplex)
        {
            double im = m.Im![0];
            if (AllInt(m)) return $"{FmtInt(re)} {(im < 0 || (im == 0 && double.IsNegative(im)) ? "-" : "+")} {FmtInt(Math.Abs(im))}i";
            var (d, sci, _) = RealFormat(new[] { re, im }, true);
            return $"{FormatReal(re, d, sci)} {(im < 0 ? "-" : "+")} {FormatReal(Math.Abs(im), d, sci)}i";
        }
        if (m.IsInteger) return m.Is64 ? m.ExactString(0) : ((long)re).ToString(Inv);
        if (m.IsLogical || m.IsChar || AllInt(m)) return FmtInt(re);
        var (dec, s2, _) = RealFormat(new[] { re }, true);
        return FormatReal(re, dec, s2);
    }

    static string FmtInt(double x)
    {
        if (double.IsNaN(x)) return "NaN";
        if (double.IsInfinity(x)) return x > 0 ? "Inf" : "-Inf";
        if (Math.Abs(x) >= 1e10) return FormatE(x, 4);
        return ((long)x).ToString(Inv) is var s && x == 0 && double.IsNegative(x) ? "0" : ((long)x).ToString(Inv);
    }

    public static List<string> MatrixLines(MatrixValue m)
    {
        var lines = new List<string>();
        var cells = new string[m.R, m.C];
        int w;
        if (m.IsComplex)
        {
            bool ints = AllInt(m);
            var (d, sci, _) = ints ? (0, false, 0) : RealFormat(m.Re.Concat(m.Im!), false);
            var reS = new string[m.Numel]; var imS = new string[m.Numel];
            for (int k = 0; k < m.Numel; k++)
            {
                reS[k] = ints ? FmtInt(m.Re[k]) : FormatReal(m.Re[k], d, sci);
                imS[k] = ints ? FmtInt(Math.Abs(m.Im![k])) : FormatReal(Math.Abs(m.Im![k]), d, sci);
            }
            int wr = reS.Max(s => s.Length), wi = imS.Max(s => s.Length);
            for (int i = 0; i < m.R; i++)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < m.C; j++)
                {
                    int k = i + j * m.R;
                    sb.Append("   ").Append(reS[k].PadLeft(wr)).Append(m.Im![k] < 0 ? " - " : " + ").Append(imS[k].PadLeft(wi)).Append('i');
                }
                lines.Add(sb.ToString());
            }
            return lines;
        }
        if (m.IsInteger)
        {
            var strs = m.Is64 ? Enumerable.Range(0, m.Re.Length).Select(m.ExactString).ToArray() : m.Re.Select(x => ((long)x).ToString(Inv)).ToArray();
            int iw = strs.Max(x => x.Length);
            for (int i = 0; i < m.R; i++)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < m.C; j++) sb.Append("  ").Append(strs[i + j * m.R].PadLeft(iw));
                lines.Add(sb.ToString());
            }
            return lines;
        }
        bool allInt = m.IsLogical || m.IsChar || AllInt(m);
        if (allInt)
        {
            for (int k = 0; k < m.Numel; k++) cells[k % m.R, k / m.R] = FmtInt(m.Re[k]);
            int maxDigits = 1;
            bool anyNeg = false;
            for (int k = 0; k < m.Numel; k++)
            {
                var s = cells[k % m.R, k / m.R];
                if (s.StartsWith("-")) anyNeg = true;
                maxDigits = Math.Max(maxDigits, s.TrimStart('-').Length);
            }
            w = maxDigits + (anyNeg ? 1 : 0);
            string sep = anyNeg ? "  " : "   ";
            if (m.IsLogical || m.IsChar) sep = "  ";
            for (int i = 0; i < m.R; i++)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < m.C; j++) sb.Append(sep).Append(cells[i, j].PadLeft(w));
                lines.Add(sb.ToString());
            }
            return lines;
        }
        var (dec, sciF, intW) = RealFormat(m.Re, false);
        bool neg = m.Re.Any(x => x < 0);
        for (int k = 0; k < m.Numel; k++) cells[k % m.R, k / m.R] = FormatReal(m.Re[k], dec, sciF);
        w = 0;
        foreach (var s in cells) w = Math.Max(w, s.Length);
        for (int i = 0; i < m.R; i++)
        {
            var sb = new StringBuilder();
            for (int j = 0; j < m.C; j++) sb.Append("   ").Append(cells[i, j].PadLeft(w));
            lines.Add(sb.ToString());
        }
        return lines;
    }
}
