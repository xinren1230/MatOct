// MatRewrite - GPL-3.0-or-later
// Phase 9: signal/general/optimisation helpers and simple plot types, as native C# ports.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static MatrixValue ColD(double[] a) => new(a.Length, 1, a, null);
    static double[] Win(Value[] a, string fn, Func<int, int, double> w)
    {
        if (a.Length != 1) Usage(fn);
        if (!(a[0] is MatrixValue m && !m.IsChar && m.Numel == 1 && m.Re[0] == Math.Floor(m.Re[0]) && m.Re[0] > 0))
            throw new OctaveException($"{fn}: M must be a positive integer");
        int n = (int)m.Re[0];
        return Enumerable.Range(0, n).Select(k => w(k, n)).ToArray();
    }

    static Value Shift(Interpreter it, Value[] a, string fn, bool inv)
    {
        if (a.Length is < 1 or > 2) Usage(fn);
        var x = a[0];
        if (x is not ArrayValue av) throw new OctaveException($"{fn}: X must be a vector or matrix");
        var sz = M(Fc(it, "size", x), fn).Re;
        IEnumerable<int> dims = a.Length == 2 ? new[] { I(a[1], fn) } : Enumerable.Range(1, sz.Length);
        if (a.Length == 2 && !(a[1] is MatrixValue dm && dm.Numel == 1 && dm.Re[0] >= 1 && dm.Re[0] == Math.Floor(dm.Re[0])))
            throw new OctaveException($"{fn}: DIM must be a positive integer");
        foreach (var d in dims)
        {
            double n = d <= sz.Length ? sz[d - 1] : 1;
            double k = inv ? -Math.Floor(n / 2) : Math.Floor(n / 2);
            if (a.Length == 1 && av.Numel > 0 && (sz.Length == 2 && (sz[0] == 1 || sz[1] == 1)) && n == 1) continue;
            x = Fc(it, "circshift", x, Num(k), Num(d));
        }
        return x;
    }

    static double Scalar(Interpreter it, Value f, double x) => D(Fc(it, "feval", f, Num(x)), "feval");

    static void RegisterPhase9Misc()
    {
        Reg("fftshift", (it, a, n) => Out(Shift(it, a, "fftshift", false)));
        Reg("ifftshift", (it, a, n) => Out(Shift(it, a, "ifftshift", true)));
        Reg("hanning", (it, a, n) => Out(ColD(Win(a, "hanning", (k, N) => N == 1 ? 1 : 0.5 - 0.5 * Math.Cos(2 * Math.PI * (k + 1) / (N + 1))))));
        Reg("hamming", (it, a, n) => Out(ColD(Win(a, "hamming", (k, N) => N == 1 ? 1 : 0.54 - 0.46 * Math.Cos(2 * Math.PI * k / (N - 1))))));
        Reg("blackman", (it, a, n) => Out(ColD(Win(a, "blackman", (k, N) => N == 1 ? 1 : 0.42 - 0.5 * Math.Cos(2 * Math.PI * k / (N - 1)) + 0.08 * Math.Cos(4 * Math.PI * k / (N - 1))))));
        Reg("beta", (it, a, n) =>
        {
            if (a.Length != 2) Usage("beta");
            var la = Fc(it, "gammaln", a[0]); var lb = Fc(it, "gammaln", a[1]); var lab = Fc(it, "gammaln", Fc(it, "plus", a[0], a[1]));
            return Out(Fc(it, "real", Fc(it, "exp", Fc(it, "minus", Fc(it, "plus", la, lb), lab))));
        });
        Reg("bincoeff", (it, a, n) =>
        {
            if (a.Length != 2) Usage("bincoeff");
            var N = M(a[0], "bincoeff"); var K = M(a[1], "bincoeff");
            if (N.Numel != 1 && K.Numel != 1 && N.Numel != K.Numel) throw new OctaveException("bincoeff: N and K must be of common size or scalars");
            var big = N.Numel >= K.Numel ? N : K; var r = new MatrixValue(big.R, big.C) { ND = (int[]?)big.ND?.Clone() };
            for (int i = 0; i < big.Numel; i++)
            {
                double nn = N.Re[N.Numel == 1 ? 0 : i], kk = K.Re[K.Numel == 1 ? 0 : i], v;
                if (kk != Math.Floor(kk) || kk < 0) v = kk == Math.Floor(kk) ? 0 : double.NaN;
                else if (kk == 0) v = 1;
                else
                {
                    v = 1; for (int j = 1; j <= kk; j++) v *= (nn - kk + j) / j;
                    if (nn == Math.Floor(nn)) v = Math.Round(v);
                }
                r.Re[i] = v;
            }
            return Out(r);
        });
        Reg("strtrunc", (it, a, n) =>
        {
            if (a.Length != 2) Usage("strtrunc");
            if (!(a[1] is MatrixValue lm && lm.Numel == 1 && lm.Re[0] >= 0 && lm.Re[0] == Math.Floor(lm.Re[0])))
                throw new OctaveException("strtrunc: length L must be a positive integer (L >= 0)");
            int L = (int)((MatrixValue)a[1]).Re[0];
            Value Tr(Value s)
            {
                var m = (MatrixValue)s; if (m.C <= L) return s;
                return Fc(it, "subsref_cols", s, Num(L));
            }
            if (a[0] is MatrixValue sm && sm.IsChar)
            {
                if (sm.C <= L) return Out(sm);
                var r = new MatrixValue(sm.R, L, NumKind.Char);
                for (int j = 0; j < L; j++) for (int i = 0; i < sm.R; i++) r.Re[i + sm.R * j] = sm.Re[i + sm.R * j];
                return Out(r);
            }
            if (a[0] is CellValue c)
            {
                var r = new CellValue(c.R, c.C);
                for (int i = 0; i < c.Data.Length; i++)
                {
                    if (!(c.Data[i] is MatrixValue e && e.IsChar)) throw new OctaveException("strtrunc: S must be a character string or a cell array of strings");
                    var s = S(e, "strtrunc"); r.Data[i] = Str(s.Length > L ? s[..L] : s);
                }
                return Out(r);
            }
            throw new OctaveException("strtrunc: S must be a character string or a cell array of strings");
        });
        Reg("rectint", (it, a, n) =>
        {
            if (a.Length != 2) Usage("rectint");
            var A = M(a[0], "rectint"); var B = M(a[1], "rectint");
            int cols = A.Numel != 0 ? A.C : B.C;
            if (A.Numel != 0 && B.Numel != 0 && A.C != B.C || (cols != 4 && cols != 6)) throw new OctaveException("rectint: A and B must have 4 or 6 columns");
            if (A.Numel == 0 || B.Numel == 0) return Out(new MatrixValue(A.R, B.R));
            int d = cols / 2; var r = new MatrixValue(A.R, B.R);
            for (int i = 0; i < A.R; i++) for (int j = 0; j < B.R; j++)
            {
                double v = 1;
                for (int k = 0; k < d; k++)
                {
                    double ap = A.Re[i + k * A.R], al = A.Re[i + (k + d) * A.R], bp = B.Re[j + k * B.R], bl = B.Re[j + (k + d) * B.R];
                    v *= Math.Max(0, Math.Min(ap + al, bp + bl) - Math.Max(ap, bp));
                }
                r.Re[i + A.R * j] = v;
            }
            return Out(r);
        });
        Reg("bitcmp", (it, a, n) =>
        {
            if (a.Length is < 1 or > 2) Usage("bitcmp");
            var m = M(a[0], "bitcmp");
            int bits = m.Kind switch { NumKind.Int8 or NumKind.UInt8 => 8, NumKind.Int16 or NumKind.UInt16 => 16, NumKind.Int32 or NumKind.UInt32 => 32, NumKind.Int64 or NumKind.UInt64 => 64, NumKind.Single => 24, _ => 53 };
            if (a.Length == 2) bits = I(a[1], "bitcmp");
            double mx = Math.Pow(2, bits) - 1;
            bool signed = m.Kind is NumKind.Int8 or NumKind.Int16 or NumKind.Int32 or NumKind.Int64;
            var r = m.Copy();
            for (int i = 0; i < m.Numel; i++) r.Re[i] = signed ? -m.Re[i] - 1 : mx - m.Re[i];
            return Out(r);
        });
        Reg("base2dec", (it, a, n) =>
        {
            if (a.Length != 2) Usage("base2dec");
            string sym; int b;
            if (IsString(a[1])) { sym = S(a[1], "base2dec"); b = sym.Length; if (sym.Distinct().Count() != b) throw new OctaveException("base2dec: symbols representing digits must be unique"); if (sym.Any(char.IsWhiteSpace)) throw new OctaveException("base2dec: whitespace characters are not valid symbols"); }
            else { b = I(a[1], "base2dec"); if (b < 2 || b > 36) throw new OctaveException("base2dec: BASE must be between 2 and 36, or a string of symbols"); sym = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[..b]; }
            var strs = a[0] is CellValue c ? c.Data.Select(v => S(v, "base2dec")).ToArray() : a[0] is MatrixValue cm && cm.IsChar
                ? Enumerable.Range(0, cm.R).Select(i => new string(Enumerable.Range(0, cm.C).Select(j => (char)cm.Re[i + cm.R * j]).ToArray())).ToArray()
                : throw new OctaveException("base2dec: S must be a string or cellstring");
            bool ci = !IsString(a[1]);
            var res = strs.Select(s =>
            {
                s = s.TrimEnd(' ', '\0'); s = ci ? s.TrimStart().ToUpperInvariant() : s;
                double v = 0; bool bad = false;
                foreach (var ch in s) { if (ch == ' ' && ci) continue; int d = sym.IndexOf(ch); if (d < 0) { bad = true; break; } v = v * b + d; }
                return bad ? double.NaN : v;
            }).ToArray();
            return Out(ColD(res));
        });
        Reg("nargchk", (it, a, n) =>
        {
            if (a.Length is < 3 or > 4) Usage("nargchk");
            double lo = D(a[0], "nargchk"), hi = D(a[1], "nargchk"), na = D(a[2], "nargchk");
            bool st = a.Length == 4 && S(a[3], "nargchk") == "struct";
            if (a.Length == 4 && !st && S(a[3], "nargchk") != "string") throw new OctaveException("nargchk: OUTTYPE must be either \"string\" or \"struct\"");
            if (hi < lo) throw new OctaveException("nargchk: MINARGS must be <= MAXARGS");
            string msg = na < lo ? "not enough input arguments" : na > hi ? "too many input arguments" : "";
            string id = na < lo ? "Octave:nargchk:not-enough-inputs" : na > hi ? "Octave:nargchk:too-many-inputs" : "";
            if (!st) return Out(msg == "" ? new MatrixValue(0, 0, NumKind.Char) : Str(msg));
            var s = new StructValue(); if (msg == "") { s = Fc(it, "struct", Str("message"), new CellValue(0, 1), Str("identifier"), new CellValue(0, 1)) as StructValue ?? s; return Out(s); }
            return Out(Fc(it, "struct", Str("message"), Str(msg), Str("identifier"), Str(id)));
        });
        Reg("inpolygon", (it, a, n) =>
        {
            if (a.Length != 4) Usage("inpolygon");
            bool Real(Value v) => v is MatrixValue mv && !mv.IsComplex;
            if (!Real(a[0]) || !Real(a[1])) throw new OctaveException("inpolygon: X and Y must be real");
            if (!Real(a[2]) || !Real(a[3]) || !((MatrixValue)a[2]).IsVector || !((MatrixValue)a[3]).IsVector) throw new OctaveException("inpolygon: XV and YV must be real vectors");
            var X = M(a[0], "inpolygon"); var Y = M(a[1], "inpolygon"); var xv = M(a[2], "inpolygon").Re; var yv = M(a[3], "inpolygon").Re;
            var szX = (MatrixValue)it.CallFunction("size", new[] { a[0] }, 1)[0]; var szY = (MatrixValue)it.CallFunction("size", new[] { a[1] }, 1)[0];
            if (!szX.Re.SequenceEqual(szY.Re)) throw new OctaveException("inpolygon: X and Y must be arrays of the same size");
            if (xv.Length != yv.Length) throw new OctaveException("inpolygon: XV and YV must be vectors of the same size");
            var inn = new MatrixValue(X.R, X.C, NumKind.Logical); var on = new MatrixValue(X.R, X.C, NumKind.Logical);
            int nv = xv.Length;
            for (int p = 0; p < X.Numel; p++)
            {
                double x = X.Re[p], y = Y.Re[p]; bool ins = false, bnd = false;
                for (int i = 0, j = nv - 1; i < nv; j = i++)
                {
                    double xi = xv[i], yi = yv[i], xj = xv[j], yj = yv[j];
                    double cr = (x - xi) * (yj - yi) - (y - yi) * (xj - xi);
                    if (Math.Abs(cr) < 1e-12 && x >= Math.Min(xi, xj) && x <= Math.Max(xi, xj) && y >= Math.Min(yi, yj) && y <= Math.Max(yi, yj)) bnd = true;
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) ins = !ins;
                }
                inn.Re[p] = ins || bnd ? 1 : 0; on.Re[p] = bnd ? 1 : 0;
            }
            var ri = it.CallFunction("reshape", new Value[] { inn, szX }, 1)[0]; var ro = it.CallFunction("reshape", new Value[] { on, szX }, 1)[0];
            return Out(ri, ro).Take(Math.Max(1, n)).ToArray();
        });
        Reg("fzero", (it, a, nout) =>
        {
            if (a.Length is < 2 or > 3) Usage("fzero");
            var f = a[0]; var x0 = M(a[1], "fzero").Re;
            double lo, hi, flo, fhi;
            if (x0.Length == 2) { lo = x0[0]; hi = x0[1]; flo = Scalar(it, f, lo); fhi = Scalar(it, f, hi); }
            else
            {
                lo = hi = x0[0]; flo = fhi = Scalar(it, f, lo);
                if (flo != 0)
                {
                    bool ok = false;
                    foreach (var s in new[] { -.01, .025, -.05, .10, -.25, .50, -1, 2.5, -5, 10, -50, 100, -500, 1000 })
                    {
                        hi = x0[0] == 0 ? s : x0[0] * (1 + s); fhi = Scalar(it, f, hi);
                        if (Math.Sign(flo) * Math.Sign(fhi) <= 0) { ok = true; break; }
                    }
                    if (!ok) throw new OctaveException("fzero: unable to find a valid initial bracketing");
                }
            }
            if (Math.Sign(flo) * Math.Sign(fhi) > 0) throw new OctaveException("fzero: not a valid initial bracketing");
            int it2 = 0; double x = flo == 0 ? lo : hi, fx = flo == 0 ? flo : fhi;
            while (fx != 0 && it2++ < 400 && Math.Abs(hi - lo) > 2 * double.Epsilon * Math.Max(1, Math.Abs(x)))
            {
                double c = (lo * fhi - hi * flo) / (fhi - flo), mid = (lo + hi) / 2;
                double t = (c > Math.Min(lo, hi) && c < Math.Max(lo, hi) && it2 % 3 != 0) ? c : mid;
                double ft = Scalar(it, f, t);
                if (t == lo || t == hi) { t = mid; ft = Scalar(it, f, t); }
                if (Math.Sign(ft) == Math.Sign(flo)) { lo = t; flo = ft; } else { hi = t; fhi = ft; }
                x = Math.Abs(flo) < Math.Abs(fhi) ? lo : hi; fx = Math.Min(Math.Abs(flo), Math.Abs(fhi)) == Math.Abs(flo) ? flo : fhi;
                if (Math.Abs(hi - lo) <= 2 * 2.2e-16 * Math.Abs(x)) break;
            }
            return Out(Num(x), Num(fx), Num(1)).Take(Math.Max(1, nout)).ToArray();
        });
        Reg("fminbnd", (it, a, nout) =>
        {
            if (a.Length is < 3 or > 4) Usage("fminbnd");
            var f = a[0]; double lo = D(a[1], "fminbnd"), hi = D(a[2], "fminbnd");
            if (lo > hi) throw new OctaveException("fminbnd: the lower bound cannot be greater than the upper one");
            double g = (3 - Math.Sqrt(5)) / 2, v = lo + g * (hi - lo), w = v, x = v, e = 0, d = 0, fx = Scalar(it, f, x), fv = fx, fw = fx;
            for (int k = 0; k < 500; k++)
            {
                double xm = (lo + hi) / 2, tol = 1e-8 * Math.Abs(x) + 1e-10;
                if (Math.Abs(x - xm) <= 2 * tol - (hi - lo) / 2) break;
                bool gs = true;
                if (Math.Abs(e) > tol)
                {
                    double r = (x - w) * (fx - fv), q = (x - v) * (fx - fw), p = (x - v) * q - (x - w) * r; q = 2 * (q - r);
                    if (q > 0) p = -p; q = Math.Abs(q); double et = e; e = d;
                    if (Math.Abs(p) < Math.Abs(.5 * q * et) && p > q * (lo - x) && p < q * (hi - x)) { d = p / q; gs = false; double u2 = x + d; if (u2 - lo < 2 * tol || hi - u2 < 2 * tol) d = x < xm ? tol : -tol; }
                }
                if (gs) { e = (x >= xm ? lo : hi) - x; d = g * e; }
                double u = x + (Math.Abs(d) >= tol ? d : d > 0 ? tol : -tol), fu = Scalar(it, f, u);
                if (fu <= fx) { if (u >= x) lo = x; else hi = x; v = w; fv = fw; w = x; fw = fx; x = u; fx = fu; }
                else { if (u < x) lo = u; else hi = u; if (fu <= fw || w == x) { v = w; fv = fw; w = u; fw = fu; } else if (fu <= fv || v == x || v == w) { v = u; fv = fu; } }
            }
            return Out(Num(x), Num(fx), Num(1)).Take(Math.Max(1, nout)).ToArray();
        });

        Reg("trace", (it, a, n) =>
        {
            if (a.Length != 1) Usage("trace");
            if (D(Fc(it, "ndims", a[0]), "trace") > 2) throw new OctaveException("trace: only valid on 2-D objects");
            var m = M(a[0], "trace");
            if (m.Numel == 0) return Out(Num(0));
            if (m.R == 1 || m.C == 1) return Out(m.Im != null ? new MatrixValue(1, 1, new[] { m.Re[0] }, new[] { m.Im[0] }) : Num(m.Re[0]));
            return Out(Fc(it, "sum", Fc(it, "diag", a[0])));
        });
        Reg("cond", (it, a, n) =>
        {
            if (a.Length is < 1 or > 2) Usage("cond");
            if (D(Fc(it, "ndims", a[0]), "cond") > 2) throw new OctaveException("cond: A must be a 2-D matrix");
            var A = a[0];
            bool p2 = a.Length == 1 || !IsString(a[1]) && D(a[1], "cond") == 2;
            if (p2)
            {
                var m = M(A, "cond");
                if (m.Numel == 0) return Out(Num(0));
                if (m.Re.Any(x => !double.IsFinite(x)) || m.Im != null && m.Im.Any(x => !double.IsFinite(x))) throw new OctaveException("cond: A must not contain Inf or NaN values");
                var sg = M(Fc(it, "svd", A), "cond").Re;
                double s1 = sg[0], sn = sg[^1];
                return Out(Num(s1 == 0 || sn == 0 ? double.PositiveInfinity : s1 / sn));
            }
            return Out(Fc(it, "times", Fc(it, "norm", A, a[1]), Fc(it, "norm", Fc(it, "inv", A), a[1])));
        });
        // ---- simple plot types built on line/patch ----
        RegG("stairs", (it, a, nout) =>
        {
            if (a.Length < 1) Usage("stairs");
            int k = 0; MatrixValue x, y;
            if (a.Length >= 2 && a[0] is MatrixValue { Numel: 1, IsChar: false } h0 && it.Graphics.Lookup(h0.Re[0]) is Graphics.Axes) a = a.Skip(1).ToArray();
            static bool Num2D(Value v) => v is MatrixValue { IsChar: false } mv && mv.Dims.Length <= 2;
            if (a.Length >= 2 && !IsString(a[1]))
            {
                if (!Num2D(a[0]) || !Num2D(a[1])) throw new OctaveException("stairs: X and Y must be numeric 2-D vectors or matrices");
                x = M(a[0], "stairs"); y = M(a[1], "stairs"); k = 2;
                if (!(x.Numel == y.Numel || (x.R == 1 || x.C == 1) && x.Numel == y.R)) throw new OctaveException("stairs: X and Y sizes must match");
            }
            else
            {
                if (!Num2D(a[0])) throw new OctaveException("stairs: Y must be a numeric 2-D vector or matrix");
                y = M(a[0], "stairs"); x = RowD(Enumerable.Range(1, y.Numel).Select(i => (double)i)); k = 1;
            }
            if (x.Numel != y.Numel) throw new OctaveException("stairs: X and Y sizes must match");
            int N = y.Numel; var xs = new double[Math.Max(0, 2 * N - 1)]; var ys = new double[xs.Length];
            for (int i = 0; i < N; i++) { xs[2 * i] = x.Re[i]; ys[2 * i] = y.Re[i]; if (i + 1 < N) { xs[2 * i + 1] = x.Re[i + 1]; ys[2 * i + 1] = y.Re[i]; } }
            if (nout >= 2) return Out(ColD(xs), ColD(ys));
            var h = Fc(it, "line", new Value[] { RowD(xs), RowD(ys) }.Concat(a.Skip(k).Where((v, i) => !(i == 0 && IsString(v) && S(v, "stairs").Length <= 4))).ToArray());
            return nout > 0 ? Out(h) : Array.Empty<Value>();
        });
        RegG("stem", (it, a, nout) =>
        {
            if (a.Length < 1) Usage("stem");
            MatrixValue x, y; int k;
            if (a.Length >= 2 && !IsString(a[1])) { x = M(a[0], "stem"); y = M(a[1], "stem"); k = 2; } else { y = M(a[0], "stem"); x = RowD(Enumerable.Range(1, y.Numel).Select(i => (double)i)); k = 1; }
            if (x.Numel != y.Numel) throw new OctaveException("stem: inconsistent sizes for X and Y");
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < y.Numel; i++) { xs.AddRange(new[] { x.Re[i], x.Re[i], double.NaN }); ys.AddRange(new[] { 0, y.Re[i], double.NaN }); }
            Fc(it, "line", RowD(xs), RowD(ys));
            var h = Fc(it, "line", RowD(x.Re), RowD(y.Re), Str("linestyle"), Str("none"), Str("marker"), Str("o"));
            return nout > 0 ? Out(h) : Array.Empty<Value>();
        });
        foreach (var (nm, isX) in new[] { ("xline", true), ("yline", false) })
            RegG(nm, (it, a, nout) =>
            {
                if (a.Length < 1) Usage(nm);
                string V = isX ? "X" : "Y";
                if (a[0] is not MatrixValue v || v.IsChar || v.IsComplex) throw new OctaveException($"{nm}: {V} must be a real numeric value");
                if (v.Numel > 0 && v.R != 1 && v.C != 1) throw new OctaveException($"{nm}: {V} must be a scalar or vector");
                if (v.Numel == 0) return nout > 0 ? Out(new MatrixValue(0, 1)) : Array.Empty<Value>();
                int k = 1; string? spec = null; Value? label = null;
                if (k < a.Length && IsString(a[k]) && GraphicsFmt(S(a[k], nm))) spec = S(a[k++], nm);
                if (k < a.Length && (a[k] is CellValue || (IsString(a[k]) && (a.Length - k) % 2 == 1))) label = a[k++];
                var ax = it.Graphics.Gca();
                var lim = M(Fc(it, "get", Num(ax.Handle), Str(isX ? "ylim" : "xlim")), nm).Re;
                var hs = new List<double>();
                for (int q = 0; q < v.Numel; q++)
                {
                    double p = v.Re[q];
                    var gh = D(Fc(it, "hggroup", Num(ax.Handle)), nm);
                    var g = (Graphics.GenericObject)it.Graphics.Lookup(gh)!;
                    var h = isX ? Fc(it, "line", RowD(new[] { p, p }), RowD(lim), Str("parent"), Num(gh)) : Fc(it, "line", RowD(lim), RowD(new[] { p, p }), Str("parent"), Num(gh));
                    if (spec != null && it.Graphics.Lookup(D(h, nm)) is Graphics.LineSeries ls) ApplyFmt(ls, spec);
                    g.Props["value"] = Num(p);
                    Value lab = label is CellValue lc && lc.Numel == v.Numel && v.Numel > 1 && lc.Data[q] is not CellValue ? lc.Data[q] : label ?? Str("");
                    g.Props["label"] = lab;
                    for (int r = k; r + 1 < a.Length; r += 2) g.Props[S(a[r], nm).ToLowerInvariant()] = a[r + 1];
                    hs.Add(gh);
                }
                return nout > 0 ? Out(ColD(hs.ToArray())) : Array.Empty<Value>();
            });
        RegG("polar", (it, a, nout) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("polar");
            MatrixValue th, r; Value? fmt = null;
            if (a.Length == 1 || (a.Length == 2 && IsString(a[1])))
            {
                var z = M(a[0], "polar");
                th = new MatrixValue(z.R, z.C); r = new MatrixValue(z.R, z.C);
                for (int i = 0; i < z.Numel; i++) { double re = z.Re[i], im = z.Im?[i] ?? 0; th.Re[i] = Math.Atan2(im, re); r.Re[i] = Math.Sqrt(re * re + im * im); }
                if (a.Length == 2) fmt = a[1];
            }
            else
            {
                if (a.Length == 3) { if (!IsString(a[2])) throw new OctaveException("polar: FMT argument must be a string"); fmt = a[2]; }
                if (a[0].NDims > 2 || a[1].NDims > 2) throw new OctaveException("polar: THETA and RHO must be 2-D objects");
                th = M(a[0], "polar"); r = M(a[1], "polar");
            }
            bool tv = th.R == 1 || th.C == 1, rv = r.R == 1 || r.C == 1;
            MatrixValue X, Y;
            if (th.Numel == 1 && r.Numel == 1) { X = MatrixValue.Scalar(r.Re[0] * Math.Cos(th.Re[0])); Y = MatrixValue.Scalar(r.Re[0] * Math.Sin(th.Re[0])); }
            else if (th.Numel == 1) throw new OctaveException("polar: Can't plot constant THETA with varying RHO");
            else if (tv && rv)
            {
                if (th.Numel != r.Numel) throw new OctaveException("polar: THETA and RHO vector lengths must match");
                X = new MatrixValue(th.Numel, 1); Y = new MatrixValue(th.Numel, 1);
                for (int i = 0; i < th.Numel; i++) { X.Re[i] = r.Re[i] * Math.Cos(th.Re[i]); Y.Re[i] = r.Re[i] * Math.Sin(th.Re[i]); }
            }
            else if (tv || rv)
            {
                var v = tv ? th : r; var m = tv ? r : th; int n = v.Numel;
                bool byRow;
                if (m.R == n) byRow = false; else if (m.C == n) byRow = true;
                else throw new OctaveException(tv ? "polar: THETA vector and RHO matrix sizes must match" : "polar: THETA matrix and RHO vector sizes must match");
                // lines are the columns of the (oriented) data
                int R = byRow ? m.C : m.R, C = byRow ? m.R : m.C;
                X = new MatrixValue(R, C); Y = new MatrixValue(R, C);
                for (int i = 0; i < R; i++) for (int j = 0; j < C; j++)
                {
                    double mv = byRow ? m.Re[j + i * m.R] : m.Re[i + j * m.R], vv = v.Re[i];
                    double t = tv ? vv : mv, rr = tv ? mv : vv;
                    X.Re[i + j * R] = rr * Math.Cos(t); Y.Re[i + j * R] = rr * Math.Sin(t);
                }
            }
            else
            {
                if (th.R != r.R || th.C != r.C) throw new OctaveException("polar: THETA and RHO matrix dimensions must match");
                X = new MatrixValue(th.R, th.C); Y = new MatrixValue(th.R, th.C);
                for (int i = 0; i < th.Numel; i++) { X.Re[i] = r.Re[i] * Math.Cos(th.Re[i]); Y.Re[i] = r.Re[i] * Math.Sin(th.Re[i]); }
            }
            var pa = fmt != null ? new Value[] { X, Y, fmt } : new Value[] { X, Y };
            var hs = it.CallFunction("plot", pa, 1);
            var ax = it.Graphics.Gca();
            double rmax = r.Numel > 0 ? r.Re.Select(Math.Abs).Max() : 1;
            if (!(rmax > 0) || double.IsInfinity(rmax)) rmax = 1;
            double raw = rmax / 3, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / mag;
            double stp = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * mag;
            int nt = (int)Math.Ceiling(rmax / stp - 1e-9);
            ax.Props["rtick"] = RowD(Enumerable.Range(0, nt + 1).Select(k => k * stp).ToArray());
            ax.Props["ttick"] = RowD(Enumerable.Range(0, 12).Select(k => k * 30.0).ToArray());
            it.CallFunction("axis", new Value[] { Str("equal") }, 0);
            return nout > 0 && hs.Length > 0 ? Out(hs[0]) : Array.Empty<Value>();
        });
        RegG("quiver", (it, a, nout) =>
        {
            if (a.Length < 2) Usage("quiver");
            var nums = a.TakeWhile(v => !IsString(v)).Select(v => M(v, "quiver")).ToList();
            MatrixValue X, Y, U, V;
            if (nums.Count >= 4) { X = nums[0]; Y = nums[1]; U = nums[2]; V = nums[3]; }
            else { U = nums[0]; V = nums[1]; X = new MatrixValue(U.R, U.C); Y = new MatrixValue(U.R, U.C); for (int i = 0; i < U.Numel; i++) { X.Re[i] = i / U.R + 1; Y.Re[i] = i % U.R + 1; } }
            if (U.Numel != V.Numel) throw new OctaveException("quiver: U and V must be the same size");
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < U.Numel; i++)
            {
                double x0 = X.Re[X.Numel == U.Numel ? i : i / U.R % X.Numel], y0 = Y.Re[Y.Numel == U.Numel ? i : i % U.R % Y.Numel];
                xs.AddRange(new[] { x0, x0 + U.Re[i], double.NaN }); ys.AddRange(new[] { y0, y0 + V.Re[i], double.NaN });
            }
            var h = Fc(it, "line", RowD(xs), RowD(ys));
            return nout > 0 ? Out(h) : Array.Empty<Value>();
        });
        RegG("rose", (it, a, nout) =>
        {
            if (a.Length is < 1 or > 2) Usage("rose");
            if (nout >= 2) return it.CallFunction("__rose_bins__", a, 2);
            var th = M(a[0], "rose").Re; int nb = 20; double[] ctr;
            if (a.Length == 2 && M(a[1], "rose").Numel > 1) ctr = M(a[1], "rose").Re; else { if (a.Length == 2) nb = I(a[1], "rose"); ctr = Enumerable.Range(0, nb).Select(i => (i + .5) * 2 * Math.PI / nb).ToArray(); }
            var cnt = new double[ctr.Length];
            foreach (var t0 in th) { double t = ((t0 % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI); int bi = 0; double bd = 1e300; for (int j = 0; j < ctr.Length; j++) { var dd = Math.Abs(t - ctr[j]); if (dd < bd) { bd = dd; bi = j; } } cnt[bi]++; }
            var tt = new List<double>(); var rr = new List<double>();
            for (int j = 0; j < ctr.Length; j++)
            {
                double w = ctr.Length > 1 ? (ctr[Math.Min(j + 1, ctr.Length - 1)] - ctr[Math.Max(j - 1, 0)]) / (j == 0 || j == ctr.Length - 1 ? 2 : 4) * 2 / 2 : Math.PI;
                double l = ctr[j] - w / 2, u = ctr[j] + w / 2;
                tt.AddRange(new[] { 0, l, l, u, u, 0 }); rr.AddRange(new[] { 0, 0, cnt[j], cnt[j], 0, 0 });
            }
            if (nout >= 2) return Out(RowD(tt), RowD(rr));
            var h = Fc(it, "line", RowD(tt.Zip(rr, (t, r) => r * Math.Cos(t))), RowD(tt.Zip(rr, (t, r) => r * Math.Sin(t))));
            return nout > 0 ? Out(h) : Array.Empty<Value>();
        });
        RegG("trisurf", (it, a, nout) =>
        {
            if (a.Length < 3) Usage("trisurf");
            if (a.Length < 4) throw new OctaveException("trisurf: Invalid call");
            var T = M(a[0], "trisurf"); var X = M(a[1], "trisurf"); var Y = M(a[2], "trisurf"); var Z = M(a[3], "trisurf");
            var V = new MatrixValue(X.Numel, 3);
            for (int i = 0; i < X.Numel; i++) { V.Re[i] = X.Re[i]; V.Re[i + X.Numel] = Y.Re[i]; V.Re[i + 2 * X.Numel] = Z.Re[i]; }
            var h = Fc(it, "patch", Str("faces"), T, Str("vertices"), V, Str("facecolor"), Str("flat"), Str("facevertexcdata"), ColD(Z.Re));
            return nout > 0 ? Out(h) : Array.Empty<Value>();
        });
    }
}
