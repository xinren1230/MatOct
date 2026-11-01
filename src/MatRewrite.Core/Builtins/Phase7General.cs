// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase7General()
    {
        Reg("nextpow2", (it, a, n) =>
        {
            if (a.Length != 1) Usage("nextpow2");
            if (!IsNumL(a[0])) throw new OctaveException("nextpow2: X must be numeric");
            if (IsCplxV(a[0])) throw new OctaveException("nextpow2: X must be real");
            var m = M(a[0], "nextpow2"); var r = new double[m.Numel];
            for (int k = 0; k < r.Length; k++)
            {
                double x = Math.Abs(m.Re[k]);
                if (x == 0) r[k] = 0; else if (double.IsInfinity(x)) r[k] = double.PositiveInfinity; else if (double.IsNaN(x)) r[k] = double.NaN;
                else { int e = Math.ILogB(x); double f = x / Math.ScaleB(1.0, e); r[k] = f == 1 ? e : e + 1; }
            }
            var res = new MatrixValue(m.R, m.C, r, null) { ND = m.ND?.ToArray() };
            return new Value[] { m.Kind == NumKind.Single || m.IsInteger ? Fc(it, "cast", res, MatrixValue.Str(NumTypes.Name(m.Kind))) : res };
        });
        Reg("blkdiag", (it, a, n) =>
        {
            if (a.Length < 1) Usage("blkdiag");
            if (!a.All(IsNum)) throw new OctaveException("blkdiag: all arguments must be numeric matrices");
            if (a.Any(x => SizeOf(x).Length > 2)) throw new OctaveException("blkdiag: all arguments must be two-dimensional matrices");
            var ms = a.Select(x => x is Sparse.SparseValue ? M(Fc(it, "full", x), "blkdiag") : M(x, "blkdiag")).ToArray();
            int R = ms.Sum(x => x.R), C = ms.Sum(x => x.C);
            bool cx = ms.Any(x => x.Im != null);
            var re = new double[R * C]; var im = cx ? new double[R * C] : null;
            int r0 = 0, c0 = 0;
            foreach (var m in ms)
            {
                for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++)
                { re[(c0 + j) * R + r0 + i] = m.Re[j * m.R + i]; if (im != null && m.Im != null) im[(c0 + j) * R + r0 + i] = m.Im[j * m.R + i]; }
                r0 += m.R; c0 += m.C;
            }
            Value res = new MatrixValue(R, C, re, im);
            if (a.Any(x => x is Sparse.SparseValue)) res = Fc(it, "sparse", res);
            else
            {
                var z = a.Select(x => Fc(it, "zeros", Dn(0), Dn(0), Fc(it, "class", x))).ToArray();
                var cls = Fc(it, "class", Fc(it, "horzcat", z));
                if (Sx(cls) != "double") res = Fc(it, "cast", res, cls);
            }
            return new[] { res };
        });
        RegCoords();
        Reg("cumtrapz", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("cumtrapz");
            Value? x = null; Value y; int dim;
            bool haveDim = a.Length == 3 || a.Length == 2 && IsScal(a[1]) && !IsScal(a[0]) && false;
            if (a.Length == 3) { x = a[0]; y = a[1]; }
            else if (a.Length == 2 && IsScal(a[1]) && !IsScal(a[0])) { y = a[0]; }
            else if (a.Length == 2) { x = a[0]; y = a[1]; }
            else y = a[0];
            bool d2 = a.Length == 2 && x == null;
            if (a.Length == 3 || d2)
            {
                var dm = a[a.Length - 1];
                if (!IsScal(dm) || D(dm, "cumtrapz") != Math.Floor(D(dm, "cumtrapz"))) throw new OctaveException("cumtrapz: DIM must be an integer and a valid dimension");
                dim = (int)D(dm, "cumtrapz");
                if (dim < 1 || dim > SizeOf(y).Length) throw new OctaveException("cumtrapz: DIM must be an integer and a valid dimension");
            }
            else dim = FirstNonSingleton(y);
            var ys = SizeOf(y); int nd = Math.Max(ys.Length, dim);
            var sz = new int[nd]; for (int k = 0; k < nd; k++) sz[k] = k < ys.Length ? ys[k] : 1;
            int len = sz[dim - 1];
            int stride = 1; for (int k = 0; k < dim - 1; k++) stride *= sz[k];
            int outer = sz.Aggregate(1, (p, q) => p * q) / Math.Max(1, len * stride);
            var ym = M(y, "cumtrapz"); var res = new double[ym.Numel];
            double[]? xv = null; MatrixValue? xm = null;
            if (x != null)
            {
                xm = M(x, "cumtrapz");
                if (xm.R == 1 || xm.C == 1)
                {
                    if (xm.Numel == 1) { xv = null; }
                    else { if (xm.Numel != len) throw new OctaveException("cumtrapz: length of X and length of Y along DIM must match"); xv = xm.Re; }
                }
                else if (!SizeOf(x).SequenceEqual(ys)) throw new OctaveException("cumtrapz: X and Y must have same shape");
            }
            for (int o = 0; o < outer; o++) for (int s = 0; s < stride; s++)
            {
                double acc = 0; int b = o * len * stride + s;
                if (len > 0) res[b] = 0;
                for (int k = 1; k < len; k++)
                {
                    int i1 = b + k * stride, i0 = b + (k - 1) * stride;
                    double h = xv != null ? xv[k] - xv[k - 1] : xm != null && xm.Numel == 1 ? xm.Re[0] : xm != null ? xm.Re[i1] - xm.Re[i0] : 1;
                    acc += h * (ym.Re[i1] + ym.Re[i0]) / 2; res[i1] = acc;
                }
            }
            return new Value[] { new MatrixValue(ym.R, ym.C, res, null) { ND = ym.ND?.ToArray() } };
        });
        RegBits();
        Reg("rescale", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length == 2 || a.Length > 7 || a.Length % 2 == 0 && a.Length > 3 && false) Usage("rescale");
            if (a.Length is 4 or 6) Usage("rescale");
            var A = a[0];
            if (!IsNumL(A)) throw new OctaveException("rescale: A must be a numeric or logical matrix");
            Value l = Dn(0), u = Dn(1); int p = 1;
            if (a.Length >= 3 && !IsString(a[1])) { l = a[1]; u = a[2]; p = 3; }
            if (!IsNumL(l)) throw new OctaveException("rescale: L must be numeric");
            if (!IsNumL(u)) throw new OctaveException("rescale: U must be numeric");
            if (Tr(Fc(it, "any", Bx(">", Fc(it, "vec", l), Fc(it, "vec", Bx("+", u, Dn(0))))))) throw new OctaveException("rescale: L must be smaller than or equal to U");
            var Ad = Fc(it, "double", A);
            Value inmin = Fc(it, "min", Fc(it, "vec", Ad)), inmax = Fc(it, "max", Fc(it, "vec", Ad));
            for (; p + 1 < a.Length; p += 2)
            {
                var opt = IsString(a[p]) ? Sx(a[p]).ToLowerInvariant() : "";
                if (opt == "inputmin") { if (!IsNumL(a[p + 1])) throw new OctaveException("rescale: INMIN must be numeric"); inmin = a[p + 1]; }
                else if (opt == "inputmax") { if (!IsNumL(a[p + 1])) throw new OctaveException("rescale: INMAX must be numeric"); inmax = a[p + 1]; }
                else throw new OctaveException($"rescale: invalid option '{(IsString(a[p]) ? Sx(a[p]) : "")}'");
            }
            var cl = Fc(it, "min", Fc(it, "max", Ad, inmin), inmax);
            var B = Bx("+", l, Bx(".*", Bx("./", Bx("-", cl, inmin), Bx("-", inmax, inmin)), Bx("-", u, l)));
            var Bm = Copy(M(B, "rescale"));
            for (int k = 0; k < Bm.Numel; k++) if (double.IsNaN(Bm.Re[k]) && !double.IsNaN(M(Ad, "rescale").Re[k])) Bm.Re[k] = M(Fc(it, "double", l), "").Re[0];
            return new Value[] { Bm };
        });
        foreach (var herm in new[] { false, true })
        {
            string fn = herm ? "ishermitian" : "issymmetric";
            Reg(fn, (it, a, n) =>
            {
                if (a.Length < 1 || a.Length > 3) Usage(fn);
                var A = a[0];
                bool skew = false; double tol = 0;
                if (a.Length >= 2)
                {
                    if (IsString(a[1]))
                    {
                        var s = Sx(a[1]).ToLowerInvariant();
                        if (s == "skew") skew = true; else if (s != "nonskew") throw new OctaveException($"{fn}: SKEWOPT must be 'skew' or 'nonskew'");
                        if (a.Length == 3) { if (a[2] is not MatrixValue tm || tm.Numel != 1 || tm.IsChar || tm.Re[0] < 0) throw new OctaveException($"{fn}: TOL must be a scalar >= 0"); tol = tm.Re[0]; }
                    }
                    else if (a[1] is MatrixValue tm && a.Length == 2)
                    { if (tm.Numel != 1 || tm.Re[0] < 0) throw new OctaveException($"{fn}: TOL must be a scalar >= 0"); tol = tm.Re[0]; }
                    else throw new OctaveException($"{fn}: second argument must be a non-negative scalar TOL, or one of the strings: 'skew' / 'nonskew'");
                }
                if (!(IsNumL(A) || A is MatrixValue cm && cm.IsChar)) { if (!herm) throw new OctaveException($"{fn}: A must be a numeric, logical, or character matrix"); return new Value[] { MatrixValue.Bool(false) }; }
                var dims = SizeOf(A);
                if (dims.Length != 2 || dims[0] != dims[1]) return new Value[] { MatrixValue.Bool(false) };
                var m = M(A is Sparse.SparseValue ? Fc(it, "full", A) : A, fn); int N = m.R;
                double sgn = skew ? -1 : 1;
                if (tol == 0)
                {
                    for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
                    {
                        double ar = m.Re[j * N + i], br = m.Re[i * N + j];
                        double ai = m.Im?[j * N + i] ?? 0, bi = m.Im?[i * N + j] ?? 0;
                        if (herm) bi = -bi;
                        if (ar != sgn * br || ai != sgn * bi) return new Value[] { MatrixValue.Bool(false) };
                    }
                    return new Value[] { MatrixValue.Bool(true) };
                }
                var At = herm ? Fc(it, "ctranspose", m) : Fc(it, "transpose", m);
                var diff = skew ? Bx("+", m, At) : Bx("-", m, At);
                var nd = D(Fc(it, "norm", diff, Dn(double.PositiveInfinity)), fn);
                var na = D(Fc(it, "norm", m, Dn(double.PositiveInfinity)), fn);
                return new Value[] { MatrixValue.Bool(nd / na <= tol) };
            });
        }
        RegPad("postpad", false); RegPad("prepad", true);
        Reg("isequaln", (it, a, n) =>
        {
            if (a.Length < 2) Usage("isequaln");
            for (int k = 1; k < a.Length; k++) if (!EqN(it, a[0], a[k])) return new Value[] { MatrixValue.Bool(false) };
            return new Value[] { MatrixValue.Bool(true) };
        });
        Reg("isdiag", (it, a, n) =>
        {
            if (a.Length != 1) Usage("isdiag");
            var A = a[0];
            if (!(IsNumL(A) || A is MatrixValue c && c.IsChar) || SizeOf(A).Length > 2) return new Value[] { MatrixValue.Bool(false) };
            var m = M(A is Sparse.SparseValue ? Fc(it, "full", A) : A, "isdiag");
            for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++)
                if (i != j && (m.Re[j * m.R + i] != 0 || (m.Im?[j * m.R + i] ?? 0) != 0)) return new Value[] { MatrixValue.Bool(false) };
            return new Value[] { MatrixValue.Bool(true) };
        });
        Reg("istriu", (it, a, n) => { if (a.Length != 1) Usage("istriu"); return new Value[] { MatrixValue.Bool(TriCheck(it, a[0], true)) }; });
        Reg("istril", (it, a, n) => { if (a.Length != 1) Usage("istril"); return new Value[] { MatrixValue.Bool(TriCheck(it, a[0], false)) }; });
        Reg("bandwidth", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("bandwidth");
            var A = a[0];
            if (!IsNumL(A) || SizeOf(A).Length != 2) throw new OctaveException("bandwidth: A must be a 2-D numeric or logical matrix");
            var m = M(A is Sparse.SparseValue ? Fc(it, "full", A) : A, "bandwidth");
            int lo = 0, up = 0;
            for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++)
                if (m.Re[j * m.R + i] != 0 || (m.Im?[j * m.R + i] ?? 0) != 0) { lo = Math.Max(lo, i - j); up = Math.Max(up, j - i); }
            if (a.Length == 2)
            {
                var t = IsString(a[1]) ? Sx(a[1]) : "";
                if (t == "lower") return new Value[] { Dn(lo) };
                if (t == "upper") return new Value[] { Dn(up) };
                throw new OctaveException("bandwidth: TYPE must be \"lower\" or \"upper\"");
            }
            return new Value[] { Dn(lo), Dn(up) };
        });
        Reg("isbanded", (it, a, n) =>
        {
            if (a.Length != 3) Usage("isbanded");
            bool ok(Value v) => v is MatrixValue m && !m.IsChar && m.Numel == 1 && m.Re[0] >= 0 && m.Re[0] == Math.Floor(m.Re[0]);
            if (!ok(a[1]) || !ok(a[2])) throw new OctaveException("isbanded: LOWER and UPPER must be non-negative integers");
            var A = a[0];
            if (!(IsNumL(A)) || SizeOf(A).Length != 2) return new Value[] { MatrixValue.Bool(false) };
            var bw = Fn(it, "bandwidth", 2, A);
            return new Value[] { MatrixValue.Bool(D(bw[0], "") <= D(a[1], "") && D(bw[1], "") <= D(a[2], "")) };
        });
        Reg("vecnorm", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("vecnorm");
            if (!IsNum(a[0]) && !(a[0] is MatrixValue lm && lm.Kind == NumKind.Logical)) throw new OctaveException("vecnorm: A must be numeric");
            double p = 2;
            if (a.Length > 1)
            {
                if (a[1] is not MatrixValue pm || pm.Numel != 1 || pm.Im != null || !(pm.Re[0] > 0)) throw new OctaveException("vecnorm: P must be positive real scalar or Inf");
                p = pm.Re[0];
            }
            int dim;
            if (a.Length > 2)
            {
                if (a[2] is not MatrixValue dm || dm.Numel != 1 || dm.Re[0] < 1 || dm.Re[0] != Math.Floor(dm.Re[0])) throw new OctaveException("vecnorm: DIM must be a positive integer scalar");
                dim = (int)dm.Re[0];
            }
            else dim = FirstNonSingleton(a[0]);
            var A = a[0]; var dv = Dn(dim);
            Value r;
            if (p == 2) r = Fc(it, "sqrt", Fc(it, "sum", Bx(".*", Fc(it, "abs", A), Fc(it, "abs", A)), dv));
            else if (p == 1) r = Fc(it, "sum", Fc(it, "abs", A), dv);
            else if (double.IsPositiveInfinity(p)) r = Fn(it, "max", 1, Fc(it, "abs", A), new MatrixValue(0, 0, Array.Empty<double>(), null), dv)[0];
            else r = Bx(".^", Fc(it, "sum", Bx(".^", Fc(it, "abs", A), Dn(p)), dv), Dn(1 / p));
            return new[] { r };
        });
        Reg("common_size", (it, a, n) =>
        {
            if (a.Length < 2) throw new OctaveException("common_size: only makes sense if nargin >= 2");
            int[]? sz = null; bool err = false;
            foreach (var v in a) if (!IsScal(v)) { var s = SizeOf(v); if (sz == null) sz = s; else if (!sz.SequenceEqual(s)) err = true; }
            var outs = new Value[a.Length + 1];
            outs[0] = Dn(err ? 1 : 0);
            for (int k = 0; k < a.Length; k++)
                outs[k + 1] = !err && sz != null && IsScal(a[k]) ? Fc(it, "repmat", a[k], new MatrixValue(1, sz.Length, sz.Select(x => (double)x).ToArray(), null)) : a[k];
            return outs;
        });
        Reg("fail", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("fail");
            var code = Sx(a[0]);
            bool warn = a.Length == 3 || a.Length == 2 && IsString(a[1]) && Sx(a[1]) == "warning" && false;
            string pattern = a.Length == 1 ? "." : a.Length == 2 ? Sx(a[1]) : Sx(a[2]);
            if (a.Length == 2 && Sx(a[1]) == "warning") { warn = true; pattern = "."; }
            if (a.Length == 3 && Sx(a[1]) != "warning") throw new OctaveException("fail: expecting 'warning' as second argument");
            if (pattern == "") pattern = ".";
            bool Match(string msg) { try { return System.Text.RegularExpressions.Regex.IsMatch(msg, pattern); } catch { return msg.Contains(pattern); } }
            if (!warn)
            {
                try { it.EvalInCurrentScope(code); }
                catch (OctaveException ex)
                {
                    if (Match(ex.Message)) return Array.Empty<Value>();
                    throw new OctaveException($"expected error <{pattern}>\nbut got <{ex.Message}>");
                }
                throw new OctaveException($"expected error <{pattern}> but got none");
            }
            var saved = it.LastWarning; it.LastWarning = "";
            try { it.EvalInCurrentScope(code); }
            catch (OctaveException ex) { throw new OctaveException($"expected warning <{pattern}>\nbut got error <{ex.Message}>"); }
            var w = it.LastWarning;
            if (string.IsNullOrEmpty(w)) throw new OctaveException($"expected warning <{pattern}> but got none");
            if (!Match(w)) throw new OctaveException($"expected warning <{pattern}>\nbut got <{w}>");
            return Array.Empty<Value>();
        });
    }

    static bool TriCheck(Interpreter it, Value A, bool upper)
    {
        if (!(IsNumL(A) || A is MatrixValue c && c.IsChar) || SizeOf(A).Length > 2) return false;
        var m = M(A is Sparse.SparseValue ? Fc(it, "full", A) : A, "istri");
        for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++)
            if ((upper ? i > j : j > i) && (m.Re[j * m.R + i] != 0 || (m.Im?[j * m.R + i] ?? 0) != 0)) return false;
        return true;
    }

    static bool EqN(Interpreter it, Value x, Value y)
    {
        if (x is MatrixValue a && y is MatrixValue b)
        {
            if (!a.Dims.SequenceEqual(b.Dims)) return false;
            for (int k = 0; k < a.Numel; k++)
            {
                double ar = a.Re[k], br = b.Re[k], ai = a.Im?[k] ?? 0, bi = b.Im?[k] ?? 0;
                bool an = double.IsNaN(ar) || double.IsNaN(ai), bn = double.IsNaN(br) || double.IsNaN(bi);
                if (an || bn) { if (an != bn) return false; continue; }
                if (ar != br || ai != bi) return false;
            }
            return true;
        }
        if (x is CellValue c1 && y is CellValue c2)
        {
            if (!c1.Dims.SequenceEqual(c2.Dims)) return false;
            for (int k = 0; k < c1.Data.Length; k++) if (!EqN(it, c1.Data[k], c2.Data[k])) return false;
            return true;
        }
        if (x is StructValue s1 && y is StructValue s2)
        {
            if (!s1.Dims.SequenceEqual(s2.Dims)) return false;
            var f1 = s1.Fields.OrderBy(q => q, StringComparer.Ordinal).ToArray(); var f2 = s2.Fields.OrderBy(q => q, StringComparer.Ordinal).ToArray();
            if (!f1.SequenceEqual(f2)) return false;
            foreach (var f in f1) for (int k = 0; k < s1.Numel; k++) if (!EqN(it, s1.Get(f, k), s2.Get(f, k))) return false;
            return true;
        }
        if (x is Sparse.SparseValue || y is Sparse.SparseValue)
            return EqN(it, x is Sparse.SparseValue ? Fc(it, "full", x) : x, y is Sparse.SparseValue ? Fc(it, "full", y) : y);
        return Tr(Fc(it, "isequal", x, y));
    }

    static void RegPad(string fn, bool pre)
    {
        Reg(fn, (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage(fn);
            var x = a[0];
            if (a[1] is not MatrixValue lm || lm.Numel != 1 || lm.Re[0] < 0 || lm.Re[0] != Math.Floor(lm.Re[0])) throw new OctaveException($"{fn}: L must be a positive scalar");
            int l = (int)lm.Re[0];
            Value c = Dn(0);
            if (a.Length >= 3) { if (a[2] is ArrayValue cv && cv.Numel > 1) throw new OctaveException($"{fn}: C must be empty or a scalar"); if (a[2] is ArrayValue cv2 && cv2.Numel == 1) c = a[2]; }
            var sz = SizeOf(x);
            int dim;
            if (a.Length == 4)
            {
                if (a[3] is not MatrixValue dm || dm.Numel != 1 || dm.Re[0] < 1 || dm.Re[0] != Math.Floor(dm.Re[0])) throw new OctaveException($"{fn}: DIM must be an integer and a valid dimension");
                dim = (int)dm.Re[0];
            }
            else dim = FirstNonSingleton(x);
            int nd = Math.Max(sz.Length, dim);
            var s = new int[nd]; for (int k = 0; k < nd; k++) s[k] = k < sz.Length ? sz[k] : 1;
            int d = s[dim - 1];
            if (d == l) return new[] { x };
            if (d > l)
            {
                var idx = new Value[nd];
                for (int k = 0; k < nd; k++) idx[k] = MatrixValue.Str(":");
                idx[dim - 1] = pre ? Fc(it, "colon", Dn(d - l + 1), Dn(d)) : Fc(it, "colon", Dn(1), Dn(l));
                return new[] { IndexAny(it, x, idx) };
            }
            var ps = s.ToArray(); ps[dim - 1] = l - d;
            var pad = Fc(it, "repmat", c, new MatrixValue(1, nd, ps.Select(q => (double)q).ToArray(), null));
            if (x is MatrixValue xm0 && xm0.Kind != NumKind.Double) pad = Fc(it, "cast", pad, Fc(it, "class", x));
            return new[] { pre ? Fc(it, "cat", Dn(dim), pad, x) : Fc(it, "cat", Dn(dim), x, pad) };
        });
    }

    static Value IndexAny(Interpreter it, Value x, Value[] idx)
    {
        if (x is ArrayValue av) return Indexing.Read(av, idx);
        throw new OctaveException("index: unsupported value");
    }

    static void RegCoords()
    {
        // returns list of component arrays (after validation/broadcast)
        Value[] Split(Interpreter it, string fn, Value[] a, string matName)
        {
            if (a.Length == 1)
            {
                var P = a[0];
                if (!IsNumL(P) || SizeOf(P).Length > 2) throw new OctaveException($"{fn}: matrix input {matName}must be 2-D numeric array");
                var m = M(P, fn);
                if (m.C != 2 && m.C != 3) throw new OctaveException($"{fn}: matrix input must be a 2- or 3-element vector or a 2- or 3-column matrix");
                var cols = new Value[m.C];
                for (int j = 0; j < m.C; j++) cols[j] = new MatrixValue(m.R, 1, m.Re.Skip(j * m.R).Take(m.R).ToArray(), m.Im?.Skip(j * m.R).Take(m.R).ToArray(), m.Kind == NumKind.Single ? NumKind.Single : NumKind.Double);
                return cols;
            }
            if (a.Any(v => !IsNumL(v))) throw new OctaveException($"{fn}: {string.Join(", ", new[] { "THETA", "R", "Z" }.Take(a.Length))} must be numeric arrays or scalars".Replace("THETA, R, Z", "THETA, R, Z"));
            int[]? sz = null;
            foreach (var v in a) if (!IsScal(v)) { var s = SizeOf(v); if (sz == null) sz = s; else if (!sz.SequenceEqual(s)) throw new OctaveException($"{fn}: arguments must be the same size or scalars"); }
            return a;
        }
        Reg("pol2cart", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("pol2cart");
            bool mat = a.Length == 1;
            var p = Split(it, "pol2cart", a, "P ");
            var x = Bx(".*", p[1], Fc(it, "cos", p[0])); var y = Bx(".*", p[1], Fc(it, "sin", p[0]));
            if (mat && n <= 1) return new[] { Fc(it, "horzcat", p.Length == 3 ? new[] { x, y, p[2] } : new[] { x, y }) };
            return p.Length == 3 ? new[] { x, y, p[2] } : new[] { x, y };
        });
        Reg("cart2pol", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("cart2pol");
            bool mat = a.Length == 1;
            var p = Split(it, "cart2pol", a, "");
            var th = Fc(it, "atan2", p[1], p[0]); var r = Fc(it, "sqrt", Bx("+", Bx(".^", p[0], Dn(2)), Bx(".^", p[1], Dn(2))));
            if (mat && n <= 1) return new[] { Fc(it, "horzcat", p.Length == 3 ? new[] { th, r, p[2] } : new[] { th, r }) };
            return p.Length == 3 ? new[] { th, r, p[2] } : new[] { th, r };
        });
        Reg("sph2cart", (it, a, n) =>
        {
            if (a.Length != 1 && a.Length != 3) Usage("sph2cart");
            bool mat = a.Length == 1;
            var p = a;
            if (mat) { var m = M(a[0], "sph2cart"); if (!IsNumL(a[0]) || m.C != 3) throw new OctaveException("sph2cart: matrix input must be a 3-element vector or 3-column array"); p = Split(it, "sph2cart", a, ""); }
            else
            {
                if (a.Any(v => !IsNumL(v))) throw new OctaveException("sph2cart: THETA, PHI, R must be numeric arrays or scalars");
                int[]? sz = null; foreach (var v in a) if (!IsScal(v)) { var s = SizeOf(v); if (sz == null) sz = s; else if (!sz.SequenceEqual(s)) throw new OctaveException("sph2cart: THETA, PHI, R must be the same size or scalars"); }
            }
            var x = Bx(".*", Bx(".*", p[2], Fc(it, "cos", p[1])), Fc(it, "cos", p[0]));
            var y = Bx(".*", Bx(".*", p[2], Fc(it, "cos", p[1])), Fc(it, "sin", p[0]));
            var z = Bx(".*", p[2], Fc(it, "sin", p[1]));
            if (mat && n <= 1) return new[] { Fc(it, "horzcat", x, y, z) };
            return new[] { x, y, z };
        });
        Reg("cart2sph", (it, a, n) =>
        {
            if (a.Length != 1 && a.Length != 3) Usage("cart2sph");
            bool mat = a.Length == 1;
            var p = a;
            if (mat) { var m = M(a[0], "cart2sph"); if (!IsNumL(a[0]) || m.C != 3) throw new OctaveException("cart2sph: matrix input must be a 3-element vector or 3-column array"); p = Split(it, "cart2sph", a, ""); }
            else
            {
                if (a.Any(v => !IsNumL(v))) throw new OctaveException("cart2sph: X, Y, Z must be numeric arrays or scalars");
                int[]? sz = null; foreach (var v in a) if (!IsScal(v)) { var s = SizeOf(v); if (sz == null) sz = s; else if (!sz.SequenceEqual(s)) throw new OctaveException("cart2sph: X, Y, Z must be the same size or scalars"); }
            }
            var th = Fc(it, "atan2", p[1], p[0]);
            var xy2 = Bx("+", Bx(".^", p[0], Dn(2)), Bx(".^", p[1], Dn(2)));
            var phi = Fc(it, "atan2", p[2], Fc(it, "sqrt", xy2));
            var r = Fc(it, "sqrt", Bx("+", xy2, Bx(".^", p[2], Dn(2))));
            if (mat && n <= 1) return new[] { Fc(it, "horzcat", th, phi, r) };
            return new[] { th, phi, r };
        });
    }

    static void RegBits()
    {
        int MaxBits(MatrixValue m) => m.Kind switch
        {
            NumKind.Double => 53, NumKind.Single => 24, NumKind.Int8 => 7, NumKind.UInt8 => 8, NumKind.Int16 => 15, NumKind.UInt16 => 16,
            NumKind.Int32 => 31, NumKind.UInt32 => 32, NumKind.Int64 => 63, NumKind.UInt64 => 64, _ => 0
        };
        Reg("bitget", (it, a, n) =>
        {
            if (a.Length != 2) Usage("bitget");
            var A = M(a[0], "bitget");
            if (A.IsChar || A.Kind == NumKind.Logical) throw new OctaveException($"bitget: invalid class {NumTypes.Name(A.Kind)}");
            int mb = A.IsInteger ? MaxBits(A) + (A.Kind is NumKind.Int8 or NumKind.Int16 or NumKind.Int32 or NumKind.Int64 ? 1 : 0) : MaxBits(A);
            var N = M(a[1], "bitget");
            if (N.Re.Any(v => v < 1 || v > mb)) throw new OctaveException($"bitget: N must be in the range [1,{mb}]");
            var r = Fc(it, "bitand", a[0], Fc(it, "bitshift", Fc(it, "cast", Dn(1), MatrixValue.Str(NumTypes.Name(A.Kind))), Bx("-", Fc(it, "double", a[1]), Dn(1))));
            return new[] { Bx("!=", r, Dn(0)) is var b ? Fc(it, "cast", b, MatrixValue.Str(NumTypes.Name(A.Kind))) : null! };
        });
        Reg("bitset", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 3) Usage("bitset");
            var A = M(a[0], "bitset");
            if (A.IsChar || A.Kind == NumKind.Logical) throw new OctaveException($"bitset: invalid class {NumTypes.Name(A.Kind)}");
            if (A.Re.Any(v => v < 0)) throw new OctaveException("bitset: A must be >= 0");
            var N = M(a[1], "bitset"); var V = a.Length == 3 ? M(a[2], "bitset") : MatrixValue.Scalar(1);
            int[]? sz = null; foreach (var v in new Value[] { A, N, V }) if (!IsScal(v)) { var s = SizeOf(v); if (sz == null) sz = s; else if (!sz.SequenceEqual(s)) throw new OctaveException("bitset: A, N, and VAL must be the same size or scalar"); }
            int mb = MaxBits(A);
            if (N.Re.Any(v => v < 1 || v > mb)) throw new OctaveException($"bitset: N must be in the range [1,{mb}]");
            int cnt = new[] { A.Numel, N.Numel, V.Numel }.Max();
            var res = Copy(A.Numel == cnt ? A : M(Fc(it, "repmat", A, new MatrixValue(1, sz!.Length, sz.Select(q => (double)q).ToArray(), null)), "bitset"));
            for (int k = 0; k < cnt; k++)
            {
                double x = res.Re[k]; int b = (int)N.Re[N.Numel == 1 ? 0 : k] - 1; bool on = V.Re[V.Numel == 1 ? 0 : k] != 0;
                if (res.IsInteger && A.Kind is NumKind.Int64 or NumKind.UInt64)
                { ulong u = (ulong)(long)x; u = on ? u | (1UL << b) : u & ~(1UL << b); x = A.Kind == NumKind.Int64 ? (long)u : u; }
                else { ulong u = (ulong)x; u = on ? u | (1UL << b) : u & ~(1UL << b); x = u; }
                res.Re[k] = x;
            }
            return new Value[] { res };
        });
    }
}
