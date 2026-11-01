// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static readonly double[] MonthStart = { 306, 337, 0, 31, 61, 92, 122, 153, 184, 214, 245, 275 };
    static readonly double[] MonthLen = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
    static readonly string[] MonAbbr = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    static readonly string[] MonFull = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    static readonly string[] DayAbbr = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    static readonly string[] DayFull = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    static bool IsLeap(double y) => y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);
    static double PMod(double a, double m) => a - Math.Floor(a / m) * m;

    static (double day, double frac) DateNum1(double y, double mo, double d, double h, double mi, double s)
    {
        if (mo < 1) mo = 1;
        if (mo != Math.Truncate(mo))
        {
            double fm = mo - Math.Floor(mo); mo = Math.Floor(mo);
            int mi0 = (int)PMod(mo - 1, 12);
            if (mi0 != 1 || !IsLeap(Math.Floor(y))) d += fm * MonthLen[mi0]; else d += fm * 29;
        }
        y += Math.Ceiling((mo - 14) / 12);
        d += MonthStart[(int)PMod(mo - 1, 12)] + 60;
        if (y != Math.Truncate(y)) { double fy = y - Math.Floor(y); y = Math.Floor(y); d += fy * (365 + (IsLeap(y + 1) ? 1 : 0)); }
        d += 365 * y + Math.Floor(y / 4) - Math.Floor(y / 100) + Math.Floor(y / 400);
        return (d, (h + (mi + s / 60) / 60) / 24);
    }

    static double[] DateVec1(double date)
    {
        double z = Math.Floor(date) - 60;
        double a = Math.Floor((z - 0.25) / 36524.25);
        double b = z - 0.25 + a - Math.Floor(a / 4);
        double y = Math.Floor(b / 365.25);
        double c = Math.Truncate(b - Math.Floor(365.25 * y)) + 1;
        double m = Math.Truncate((5 * c + 456) / 153);
        double d = c - Math.Truncate((153 * m - 457) / 5);
        if (m > 12) { y++; m -= 12; }
        double fracd = date - Math.Floor(date);
        double tmps = Math.Abs(2.220446049250313e-16 * 86400 * date); if (tmps == 0) tmps = 1;
        double srnd = Math.Pow(2, Math.Floor(-Math.Log2(tmps)));
        double s = Math.Round(86400 * fracd * srnd, MidpointRounding.AwayFromZero) / srnd;
        double h = Math.Floor(s / 3600); s -= 3600 * h;
        double mi = Math.Floor(s / 60); s -= 60 * mi;
        return new[] { y, m, d, h, mi, s };
    }

    static readonly string[] StdFormats = {
        "dd-mmm-yyyy HH:MM:SS", "dd-mmm-yyyy", "mm/dd/yy", "mm/dd", "HH:MM:SS", "HH:MM:SS PM", "HH:MM", "HH:MM PM",
        "mm/dd/yyyy", "mmmyy", "mmm.dd,yyyy HH:MM:SS", "mmm.dd,yyyy", "yyyy/mm/dd", "yyyy-mm-dd", "yyyy-mm-dd HH:MM:SS",
        "mmm-dd-yyyy HH:MM:SS", "mmm-dd-yyyy", "dd mmm yyyy HH:MM:SS", "dd mmm yyyy", "mmm dd yyyy HH:MM:SS", "mmm dd yyyy",
        "dd.mmm.yyyy HH:MM:SS", "dd.mmm.yyyy", "mmm.dd.yyyy HH:MM:SS", "mmm.dd.yyyy", "mm/dd/yyyy HH:MM", "yyyy", "yyyy-mm",
        "yyyy-mm-ddTHH:MM:SSZ", "yyyy-mm-ddTHH:MM:SS.FFFZ" };

    sealed class SFmt { public string F = ""; public int rY, ry; public bool fy, fm, fd, fh, fmi, fs; }

    static SFmt Vfmt2Sfmt(Interpreter it, string f)
    {
        string orig = f;
        if (f.IndexOfAny("hsfYD".ToCharArray()) >= 0)
            it.Warning("datevec: Format specifiers for dates should be lowercase, format specifiers for time should be uppercase.  Possible issue with 'm' (month) and 'M' (minutes)?", "Octave:datevec:date-format-spec");
        f = f.Replace("%", "%%");
        bool ampm = f.Contains("PM") || f.Contains("AM");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Yy][Yy][Yy][Yy]", "%Y");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Yy][Yy]", "%y");
        f = f.Replace("mmmm", "%B").Replace("mmm", "%b").Replace("mm", "%m");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Dd][Dd][Dd][Dd]", "%A");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Dd][Dd][Dd]", "%a");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Dd][Dd]", "%d");
        if (ampm) f = f.Replace("HH", "%I").Replace("PM", "%p").Replace("AM", "%p"); else f = f.Replace("HH", "%H");
        f = f.Replace("MM", "%M");
        f = System.Text.RegularExpressions.Regex.Replace(f, "[Ss][Ss]", "%S");
        int Cnt(string t) { int c = 0, i = 0; while ((i = f.IndexOf(t, i, StringComparison.Ordinal)) >= 0) { c++; i++; } return c; }
        string? err = null;
        if (Cnt("%Y") + Cnt("%y") > 1) err = $"datevec: multiple year specifiers in {orig}";
        else if (Cnt("%m") + Cnt("%b") + Cnt("%B") > 1) err = $"datevec: multiple month specifiers in {orig}";
        else if (Cnt("%d") > 1) err = $"datevec: multiple day specifiers in {orig}";
        else if (Cnt("%a") + Cnt("%A") > 1) err = $"datevec: multiple day of week specifiers in {orig}";
        else if (Cnt("%H") + Cnt("%I") > 1) err = $"datevec: multiple hour specifiers in {orig}";
        else if (Cnt("%M") > 1) err = $"datevec: multiple minute specifiers in {orig}";
        else if (Cnt("%S") > 1) err = $"datevec: multiple second specifiers in {orig}";
        if (err != null) { if (orig.Contains('%')) it.Warning(err, ""); else throw new OctaveException(err); }
        var r = new SFmt { F = f, rY = f.LastIndexOf("%Y", StringComparison.Ordinal) + 1, ry = f.LastIndexOf("%y", StringComparison.Ordinal) + 1 };
        r.fy = r.rY > 0 || r.ry > 0;
        r.fm = f.Contains("%m") || f.Contains("%b") || f.Contains("%B");
        r.fd = f.Contains("%d") || f.Contains("%a") || f.Contains("%A");
        r.fh = f.Contains("%H") || f.Contains("%I");
        r.fmi = f.Contains("%M"); r.fs = f.Contains("%S");
        return r;
    }

    static Dictionary<string, double> NewTm() => new() { ["usec"] = 0, ["sec"] = 0, ["min"] = 0, ["hour"] = 0, ["mday"] = 0, ["mon"] = 0, ["year"] = 0, ["wday"] = 0, ["yday"] = 0, ["isdst"] = 0, ["gmtoff"] = 0 };

    static double[]? Str2Vec(string ds, double p, SFmt sf, bool exact)
    {
        string f = sf.F; var tm = NewTm(); int nc;
        int idx = f.IndexOf("FFF", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var tm0 = NewTm(); int pos = StrpTime(ds, f.Substring(0, idx), tm0); nc = pos < 0 ? 0 : pos + 1;
            if (nc != 0)
            {
                string msec = ds.Substring(nc - 1, Math.Min(3, ds.Length - nc + 1));
                int mi = msec.ToList().FindIndex(ch => !char.IsDigit(ch));
                if (mi >= 0) { msec = msec.Substring(0, mi).PadRight(3, '0'); ds = ds.Substring(0, nc - 1) + msec + ds.Substring(nc - 1 + mi); }
                else if (msec.Length < 3) { int ml = msec.Length; msec = msec.PadRight(3, '0'); ds = ds.Substring(0, nc - 1) + msec + ds.Substring(nc - 1 + ml); }
                f = f.Substring(0, idx) + msec + f.Substring(idx + 3);
                pos = StrpTime(ds, f, tm); nc = pos < 0 ? 0 : pos + 1;
                tm["usec"] = 1000 * double.Parse(msec);
            }
        }
        else { int pos = StrpTime(ds, f, tm); nc = pos < 0 ? 0 : pos + 1; }
        if (!((!exact && nc > 0) || nc == ds.Length + 1)) return null;
        double y = tm["year"] + 1900, m = tm["mon"] + 1, d = tm["mday"], h = tm["hour"], mi2 = tm["min"], s = tm["sec"] + tm["usec"] / 1e6;
        if (sf.rY < sf.ry)
        {
            y -= y > 1999 ? 2000 : 1900;
            y += p - PMod(p, 100);
            if (y < p) y += 100;
        }
        int curY = DateTime.Now.Year;
        if (!sf.fy && !sf.fm && !sf.fd) { y = curY; m = 1; d = 1; }
        else if (!sf.fy && sf.fm && sf.fd) y = curY;
        else if (sf.fy && sf.fm && !sf.fd) d = 1;
        if (!sf.fh && !sf.fmi && !sf.fs) h = mi2 = s = 0;
        else if (sf.fh && sf.fmi && !sf.fs) s = 0;
        return new[] { y, m, d, h, mi2, s };
    }

    static List<string> StrList(Value v)
    {
        if (v is CellValue c) return c.Data.Select(x => ((MatrixValue)x).AsString()).ToList();
        var m = (MatrixValue)v; var r = new List<string>();
        for (int i = 0; i < m.R; i++) { var sb = new System.Text.StringBuilder(); for (int j = 0; j < m.C; j++) sb.Append((char)m.Re[i + j * m.R]); r.Add(sb.ToString().TrimEnd()); }
        if (m.R == 0) r.Add("");
        return r;
    }

    // returns rows of 6-element vectors and shape for numeric inputs
    static (List<double[]> rows, int[]? shape) DateVecCore(Interpreter it, Value date, Value? fv, Value? pv)
    {
        if (fv != null && IsNum(fv)) { pv = fv; fv = null; }
        if (fv != null && fv is MatrixValue fm0 && fm0.Numel == 0) fv = null;
        double p = pv != null && NumelOf(pv) > 0 ? D(pv, "datevec") : DateTime.Now.Year - 50;
        var rows = new List<double[]>();
        if (date is CellValue || date is MatrixValue dm && dm.IsChar)
        {
            var ss = StrList(date);
            if (fv == null)
            {
                var fmts = StdFormats.Select(f => Vfmt2Sfmt(it, f)).ToArray();
                foreach (var s in ss)
                {
                    double[]? r = null;
                    foreach (var sf in fmts) { r = Str2Vec(s, p, sf, true); if (r != null) break; }
                    rows.Add(r ?? throw new OctaveException("datevec: none of the standard formats match the DATE string"));
                }
            }
            else
            {
                var sf = Vfmt2Sfmt(it, Sx(fv));
                foreach (var s in ss) rows.Add(Str2Vec(s, p, sf, false) ?? throw new OctaveException("datevec: DATE not parsed correctly with given format"));
            }
            return (rows, null);
        }
        var m = M(date, "datevec");
        foreach (var v in m.Re) rows.Add(DateVec1(v));
        return (rows, SizeOf(date));
    }

    static MatrixValue Rows6(List<double[]> rows)
    {
        int n = rows.Count; var re = new double[n * 6];
        for (int i = 0; i < n; i++) for (int j = 0; j < 6; j++) re[i + j * n] = rows[i][j];
        return new MatrixValue(n, 6, re, null);
    }

    static Value[] DateNumImpl(Interpreter it, Value[] a, int nout)
    {
        if (a.Length == 0 || a.Length > 2 && (a[0] is CellValue || a[0] is MatrixValue m0 && m0.IsChar)) Usage("datenum");
        double[][] cols; int[] shape;
        if (a[0] is CellValue || a[0] is MatrixValue mc && mc.IsChar)
        {
            var (rows, _) = DateVecCore(it, a[0], a.Length > 1 ? a[1] : null, null);
            cols = Enumerable.Range(0, 6).Select(j => rows.Select(r => r[j]).ToArray()).ToArray();
            shape = new[] { rows.Count, 1 };
        }
        else if (a.Length == 1)
        {
            var m = M(a[0], "datenum");
            if (m.C > 6 || m.C < 3) throw new OctaveException("datenum: expected date vector containing [YEAR, MONTH, DAY, HOUR, MINUTE, SECOND]");
            if (m.Kind != NumKind.Double || m.IsChar) throw new OctaveException("datenum: all inputs must be of class double");
            cols = Enumerable.Range(0, 6).Select(j => j < m.C ? Enumerable.Range(0, m.R).Select(i => m.Re[i + j * m.R]).ToArray() : new double[m.R]).ToArray();
            shape = new[] { m.R, 1 };
        }
        else
        {
            var args = Enumerable.Range(0, 6).Select(k => k < a.Length ? a[k] : null).ToArray();
            for (int k = 0; k < 6; k++) if (args[k] != null && !(args[k] is MatrixValue am && am.Kind == NumKind.Double && !am.IsChar)) throw new OctaveException("datenum: all inputs must be of class double");
            var mats = args.Select((v, k) => v == null ? MatrixValue.Scalar(k < 3 ? 0 : 0) : (MatrixValue)v).ToArray();
            if (a.Length == 2 || mats[2].Numel == 0) { }
            int n = 1; int[]? sh = null;
            for (int k = 0; k < 3; k++) if (mats[k].Numel != 1) { if (n != 1 && mats[k].Numel != n) throw new OctaveException("datenum: incompatible sizes for YEAR, MONTH, DAY"); n = mats[k].Numel; sh = SizeOf(mats[k]); }
            for (int k = 3; k < 6; k++) if (mats[k].Numel != 1) { if (n != 1 && mats[k].Numel != n) throw new OctaveException("operator +: nonconformant arguments"); if (n == 1) { n = mats[k].Numel; sh = SizeOf(mats[k]); } }
            shape = sh ?? new[] { 1, 1 };
            cols = mats.Select(mm => mm.Numel == 1 ? Enumerable.Repeat(mm.Re[0], n).ToArray() : mm.Numel == 0 ? new double[n] : mm.Re.ToArray()).ToArray();
        }
        int N = cols[0].Length; var days = new double[N]; var secs = new double[N];
        for (int i = 0; i < N; i++)
        {
            var (d, fr) = DateNum1(cols[0][i], cols[1][i], cols[2][i], cols[3][i], cols[4][i], cols[5][i]);
            days[i] = d + fr; secs[i] = d * 86400 + cols[3][i] * 3600 + cols[4][i] * 60 + cols[5][i];
        }
        var o1 = new MatrixValue(1, 1, days, null); o1.SetDims(shape);
        var o2 = new MatrixValue(1, 1, secs, null); o2.SetDims(shape);
        return new Value[] { o1, o2 };
    }

    static string FormatDate(double[] v, string df)
    {
        int wday = (int)PMod(Math.Floor(DateNum1(v[0], v[1], v[2], 0, 0, 0).day) - 733048, 7); if (wday == 0) wday = 7;
        var sb = new System.Text.StringBuilder();
        bool ampm = df.Contains("AM") || df.Contains("PM");
        int i = 0;
        bool At(string t, bool ci = false) => string.Compare(df, i, t, 0, t.Length, ci ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0 && i + t.Length <= df.Length;
        while (i < df.Length)
        {
            if (At("yyyy", true)) { sb.Append(((int)v[0]).ToString("0000")); i += 4; }
            else if (At("yy", true)) { sb.Append(((int)PMod(v[0], 100)).ToString("00")); i += 2; }
            else if (At("mmmm")) { sb.Append(MonFull[(int)v[1] - 1]); i += 4; }
            else if (At("mmm")) { sb.Append(MonAbbr[(int)v[1] - 1]); i += 3; }
            else if (At("mm")) { sb.Append(((int)v[1]).ToString("00")); i += 2; }
            else if (At("m")) { sb.Append(MonAbbr[(int)v[1] - 1][0]); i += 1; }
            else if (At("dddd", true)) { sb.Append(DayFull[wday - 1]); i += 4; }
            else if (At("ddd", true)) { sb.Append(DayAbbr[wday - 1]); i += 3; }
            else if (At("dd", true)) { sb.Append(((int)v[2]).ToString("00")); i += 2; }
            else if (At("d", true)) { sb.Append(DayAbbr[wday - 1][0]); i += 1; }
            else if (At("HH")) { if (ampm) { int hr = (int)PMod(v[3], 12); if (hr == 0) hr = 12; sb.Append(hr.ToString().PadLeft(2)); } else sb.Append(((int)v[3]).ToString("00")); i += 2; }
            else if (At("MM")) { sb.Append(((int)v[4]).ToString("00")); i += 2; }
            else if (At("SS", true)) { sb.Append(((int)Math.Truncate(v[5])).ToString("00")); i += 2; }
            else if (At("FFF")) { sb.Append(((int)Math.Round(1000 * (v[5] - Math.Truncate(v[5])), MidpointRounding.AwayFromZero)).ToString("000")); i += 3; }
            else if (At("AM") || At("PM")) { sb.Append(v[3] >= 12 ? "PM" : "AM"); i += 2; }
            else if (At("QQ")) { sb.Append("Q" + (int)Math.Truncate((v[1] + 2) / 3)); i += 2; }
            else { sb.Append(df[i]); i++; }
        }
        return sb.ToString();
    }

    static readonly string[] DateForm = {
        "dd-mmm-yyyy HH:MM:SS", "dd-mmm-yyyy", "mm/dd/yy", "mmm", "m", "mm", "mm/dd", "dd", "ddd", "d", "yyyy", "yy", "mmmyy",
        "HH:MM:SS", "HH:MM:SS PM", "HH:MM", "HH:MM PM", "QQ-YY", "QQ", "dd/mm", "dd/mm/yy", "mmm.dd,yyyy HH:MM:SS", "mmm.dd,yyyy",
        "mm/dd/yyyy", "dd/mm/yyyy", "yy/mm/dd", "yyyy/mm/dd", "QQ-YYYY", "mmmyyyy", "yyyy-mm-dd", "yyyymmddTHHMMSS", "yyyy-mm-dd HH:MM:SS" };

    static Value CharRows(List<string> rows)
    {
        if (rows.Count == 0) return MatrixValue.Str("");
        int w = rows.Max(r => r.Length), n = rows.Count; var re = new double[n * w];
        for (int i = 0; i < n; i++) for (int j = 0; j < w; j++) re[i + j * n] = j < rows[i].Length ? rows[i][j] : ' ';
        var m = new MatrixValue(n, w, re, null, NumKind.Char); return m;
    }

    static void RegisterPhase8Time()
    {
        Reg("is_leap_year", (it, a, n) =>
        {
            if (a.Length > 1) Usage("is_leap_year");
            if (a.Length == 0) return new Value[] { MatrixValue.Bool(IsLeap(DateTime.Now.Year)) };
            var m = M(a[0], "is_leap_year"); var r = new MatrixValue(1, 1, m.Re.Select(y => IsLeap(y) ? 1.0 : 0).ToArray(), null, NumKind.Logical); r.SetDims(SizeOf(a[0]));
            return new Value[] { r };
        });
        Reg("eomday", (it, a, n) =>
        {
            if (a.Length != 2) Usage("eomday");
            var y = M(a[0], "eomday"); var mo = M(a[1], "eomday");
            int N = Math.Max(y.Numel, mo.Numel);
            var re = new double[N];
            for (int i = 0; i < N; i++)
            {
                double yy = y.Numel == 1 ? y.Re[0] : y.Re[i], mm = mo.Numel == 1 ? mo.Re[0] : mo.Re[i];
                if (mm < 1 || mm > 12 || mm != Math.Floor(mm)) throw new OctaveException($"index ({mm}): out of bound; value {mm} out of bound 12");
                re[i] = MonthLen[(int)mm - 1] + (mm == 2 && IsLeap(yy) ? 1 : 0);
            }
            var r = new MatrixValue(1, 1, re, null); r.SetDims(SizeOf(mo.Numel >= y.Numel ? a[1] : a[0])); return new Value[] { r };
        });
        Reg("datenum", (it, a, n) => DateNumImpl(it, a, n));
        Reg("datevec", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("datevec");
            var (rows, shape) = DateVecCore(it, a[0], a.Length > 1 ? a[1] : null, a.Length > 2 ? a[2] : null);
            if (n <= 1) return new Value[] { Rows6(rows) };
            var outs = new Value[6];
            for (int j = 0; j < 6; j++)
            {
                var c = new MatrixValue(rows.Count, 1, rows.Select(r => r[j]).ToArray(), null);
                if (shape != null && !(shape.Length == 2 && shape[1] == 1)) c.SetDims(shape);
                outs[j] = c;
            }
            return outs;
        });
        Reg("datestr", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("datestr");
            Value date = a[0]; Value? f = a.Length > 1 ? a[1] : null; Value? p = a.Length > 2 ? a[2] : null;
            if (f is MatrixValue fm && fm.Numel == 0) f = null;
            List<double[]> v;
            bool isStr = date is CellValue || date is MatrixValue dm && dm.IsChar;
            var dmat = isStr ? null : M(date, "datestr");
            if (isStr || dmat!.C != 6) v = DateVecCore(it, date, p, null).rows;
            else
            {
                v = Enumerable.Range(0, dmat.R).Select(i => Enumerable.Range(0, 6).Select(j => dmat.Re[i + j * dmat.R]).ToArray()).ToList();
                double[] mx = { double.PositiveInfinity, 12, 31, 23, 59, 60 };
                bool bad = v.Any(r => Enumerable.Range(0, 6).Any(j => Math.Max(r[j], 1) > mx[j]) || Enumerable.Range(0, 5).Any(j => r[j] != Math.Floor(r[j])));
                if (bad) v = DateVecCore(it, date, p, null).rows;
            }
            for (int i = 0; i < v.Count; i++)
                if (v[i][5] - Math.Truncate(v[i][5]) >= 0.9995)
                {
                    v[i][5] = Math.Truncate(v[i][5]) + 1;
                    var (d, fr) = DateNum1(v[i][0], v[i][1], v[i][2], v[i][3], v[i][4], v[i][5]); v[i] = DateVec1(d + fr);
                }
            string? dfs = null; int fi = -1;
            if (f == null)
            {
                if (v.All(r => r[3] == 0 && r[4] == 0 && r[5] == 0)) fi = 1;
                else if (v.All(r => r[0] == -1 && r[1] == 12 && r[2] == 31)) fi = 16;
                else fi = 0;
            }
            else if (IsNum(f)) fi = I(f, "datestr"); else dfs = Sx(f);
            if (dfs == null && (fi < 0 || fi >= DateForm.Length)) throw new OctaveException($"index ({fi + 1}): out of bound; value {fi + 1} out of bound {DateForm.Length}");
            var outRows = v.Select(r => FormatDate(r, dfs ?? DateForm[fi])).ToList();
            return new Value[] { CharRows(outRows) };
        });
        Reg("weekday", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("weekday");
            Value d = a[0]; int[] sz;
            double[] vals;
            if (d is CellValue || d is MatrixValue dm && dm.IsChar)
            {
                sz = d is CellValue ? SizeOf(d) : new[] { ((MatrixValue)d).R, 1 };
                vals = ((MatrixValue)DateNumImpl(it, new[] { d }, 1)[0]).Re;
            }
            else { sz = SizeOf(d); vals = M(d, "weekday").Re; }
            var w = vals.Select(x => { double r = Math.Floor(PMod(x - 733048, 7)); return r == 0 ? 7 : r; }).ToArray();
            var o = new MatrixValue(1, 1, w, null); o.SetDims(sz);
            if (n < 2) return new Value[] { o };
            bool lng = a.Length > 1 && Sx(a[1]).Equals("long", StringComparison.OrdinalIgnoreCase);
            return new Value[] { o, CharRows(w.Select(x => (lng ? DayFull : DayAbbr)[(int)x - 1]).ToList()) };
        });
        Reg("etime", (it, a, n) =>
        {
            if (a.Length != 2) Usage("etime");
            var s2 = (MatrixValue)DateNumImpl(it, new[] { a[0] }, 2)[1]; var s1 = (MatrixValue)DateNumImpl(it, new[] { a[1] }, 2)[1];
            return new Value[] { Bx("-", s2, s1) };
        });
        Reg("addtodate", (it, a, n) =>
        {
            if (a.Length != 3) Usage("addtodate");
            if (!(a[2] is MatrixValue fm && fm.IsChar && fm.R == 1)) throw new OctaveException("addtodate: F must be a single character string");
            Value d = a[0]; var q = M(a[1], "addtodate");
            if (NumelOf(d) == 1 && q.Numel != 1) d = Fc(it, "repmat", d, new MatrixValue(1, 2, SizeOf(a[1]).Take(2).Select(z => (double)z).ToArray(), null));
            string f = Sx(a[2]); if (f.EndsWith("s")) f = f[..^1];
            f = f.ToLowerInvariant();
            if (f == "year" || f == "month")
            {
                var dm = M(d, "addtodate"); int N = Math.Max(dm.Numel, q.Numel); var res = new double[N];
                for (int i = 0; i < N; i++)
                {
                    var v = DateVec1(dm.Re[dm.Numel == 1 ? 0 : i]); double qq = q.Re[q.Numel == 1 ? 0 : i];
                    if (f == "year") v[0] += qq;
                    else
                    {
                        v[1] += qq; v[0] += Math.Floor((v[1] - 1) / 12); v[1] = PMod(v[1] - 1, 12) + 1;
                        double eom = MonthLen[(int)v[1] - 1] + (v[1] == 2 && IsLeap(v[0]) ? 1 : 0);
                        if (v[2] > eom) v[2] = eom;
                    }
                    var (dd, fr) = DateNum1(v[0], v[1], v[2], v[3], v[4], v[5]); res[i] = dd + fr;
                }
                var o = new MatrixValue(1, 1, res, null); o.SetDims(dm.Numel == N ? SizeOf(d) : SizeOf(a[1])); return new Value[] { o };
            }
            double mult = f switch { "day" => 1, "hour" => 1.0 / 24, "minute" => 1.0 / 1440, "second" => 1.0 / 86400, "millisecond" => 1.0 / 86400000, _ => throw new OctaveException($"addtodate: Invalid time unit: {f}") };
            return new Value[] { Bx("+", d, Bx("*", a[1], Dn(mult))) };
        });
        Reg("calendar", (it, a, n) =>
        {
            double y, m; double? d = null;
            if (a.Length == 0) { var t = DateTime.Now; y = t.Year; m = t.Month; d = t.Day; }
            else if (a.Length == 1) { var v = DateVecCore(it, a[0], null, null).rows[0]; y = v[0]; m = v[1]; d = v[2]; }
            else if (a.Length == 2) { y = D(a[0], "calendar"); m = D(a[1], "calendar"); }
            else { Usage("calendar"); return null!; }
            var cal = new double[42];
            double day1 = DateNum1(y, m, 1, 0, 0, 0).day;
            int wd = (int)PMod(day1 - 733048, 7); if (wd == 0) wd = 7;
            int nd = (int)(MonthLen[(int)m - 1] + (m == 2 && IsLeap(y) ? 1 : 0));
            for (int k = 1; k <= nd; k++) cal[wd - 2 + k] = k;
            if (n > 0) { var c = new double[42]; for (int r = 0; r < 6; r++) for (int col = 0; col < 7; col++) c[r + col * 6] = cal[col + r * 7]; return new Value[] { new MatrixValue(6, 7, c, null) }; }
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < 42; k++) { sb.Append("    " + ((int)cal[k]).ToString().PadLeft(2)); if (k % 7 == 6) sb.Append('\n'); }
            var str = sb.ToString().ToCharArray();
            if (d != null)
            {
                int pos = wd + (int)d.Value - 1;
                int idx = 6 * pos + (int)Math.Truncate(pos / 7.1) - (d < 10 ? 1 : 2);
                if (idx >= 1 && idx <= str.Length) str[idx - 1] = '*';
            }
            it.Out.Write($"                    {MonAbbr[(int)m - 1]} {(int)y}\n");
            it.Out.Write("     S     M    Tu     W    Th     F     S\n");
            it.Out.Write(new string(str));
            return new Value[0];
        });
        Reg("asctime", (it, a, n) =>
        {
            if (a.Length != 1) Usage("asctime");
            return new Value[] { Fc(it, "strftime", MatrixValue.Str("%a %b %d %H:%M:%S %Y\n"), a[0]) };
        });
        Reg("ctime", (it, a, n) =>
        {
            if (a.Length != 1) Usage("ctime");
            return new Value[] { Fc(it, "asctime", Fc(it, "localtime", a[0])) };
        });
    }
}
