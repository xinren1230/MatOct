// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    // ---- small helpers for set functions ----
    static MatrixValue IdxV(IEnumerable<int> one, bool row = false) { var d = one.Select(z => (double)z).ToArray(); return row ? new MatrixValue(1, d.Length, d, null) : new MatrixValue(d.Length, 1, d, null); }
    static Value SelE(Interpreter it, Value v, IEnumerable<int> idx, bool rows) => rows ? IndexAny(it, v, new Value[] { IdxV(idx), MatrixValue.Str(":") }) : IndexAny(it, v, new Value[] { IdxV(idx) });
    static int NumelOf(Value v) => SizeOf(v).Aggregate(1, (p, q) => p * q);
    static int RowsOf(Value v) => SizeOf(v)[0];
    static bool IsRowV(Value v) { var d = SizeOf(v); return d.Length == 2 && d[0] == 1; }
    static bool IsColV(Value v) { var d = SizeOf(v); return d.Length == 2 && d[1] == 1; }
    static Value ColOf(Interpreter it, Value v) => v is CellValue c ? new CellValue(c.Numel, 1, c.Data.ToArray()) : Col(it, v);
    static Value VCat(Interpreter it, Value a, Value b) => Fc(it, "vertcat", a, b);
    static (Value sorted, int[] idx) SortE(Interpreter it, Value v, bool rows)
    {
        var r = Fn(it, rows ? "sortrows" : "sort", 2, v);
        return (r[0], M(r[1], "sort").Re.Select(z => (int)z).ToArray());
    }
    static string? CellS(Value v) => v is MatrixValue m && (m.IsChar || m.Numel == 0) ? m.AsString() : null;
    /// <summary>match[k] = element (or row) k equals k+1 (0-based, length n-1). NaN never equal.</summary>
    static bool[] AdjEq(Value c, bool rows)
    {
        if (c is CellValue cv)
        {
            var r = new bool[Math.Max(0, cv.Numel - 1)];
            for (int k = 0; k < r.Length; k++) r[k] = CellS(cv.Data[k]) is string s1 && CellS(cv.Data[k + 1]) is string s2 && s1 == s2 && SizeOf(cv.Data[k]).SequenceEqual(SizeOf(cv.Data[k + 1])) || (CellS(cv.Data[k]) == "" && CellS(cv.Data[k + 1]) == "");
            return r;
        }
        var m = c is Sparse.SparseValue ? null : c as MatrixValue;
        if (m == null) throw new OctaveException("set: unsupported value");
        bool Eq(int p, int q) => m.Re[p] == m.Re[q] && (m.Im?[p] ?? 0) == (m.Im?[q] ?? 0);
        if (!rows) { var r = new bool[Math.Max(0, m.Numel - 1)]; for (int k = 0; k < r.Length; k++) r[k] = Eq(k, k + 1); return r; }
        int R = m.R, C = m.C; var rr = new bool[Math.Max(0, R - 1)];
        for (int k = 0; k < rr.Length; k++) { bool e = true; for (int j = 0; j < C && e; j++) e = Eq(j * R + k, j * R + k + 1); rr[k] = e; }
        return rr;
    }
    static Value DelE(Interpreter it, Value v, IEnumerable<int> del, bool rows, int n)
    {
        var ds = new HashSet<int>(del);
        var keep = Enumerable.Range(1, n).Where(k => !ds.Contains(k));
        var r = SelE(it, v, keep, rows);
        if (!rows && IsRowV(v) && v is not CellValue) return Fc(it, "reshape", r, Dn(1), Dn(NumelOf(r)));
        if (!rows && IsRowV(v) && v is CellValue) return Fc(it, "reshape", r, Dn(1), Dn(NumelOf(r)));
        return r;
    }

    static Value[] UniqueImpl(Interpreter it, Value x, string[] opts, int nargout)
    {
        bool optrows = opts.Contains("rows"), optfirst = opts.Contains("first"), optlast = opts.Contains("last"),
             optsorted = opts.Contains("sorted"), optstable = opts.Contains("stable"), optlegacy = opts.Contains("legacy");
        if (opts.Length > 0)
        {
            if (optfirst && optlast) throw new OctaveException("unique: cannot specify both \"first\" and \"last\"");
            if (optsorted && optstable) throw new OctaveException("unique: cannot specify both \"sorted\" and \"stable\"");
            if ((optfirst || optlast) && (optsorted || optstable)) throw new OctaveException("unique: cannot specify \"first\"/\"last\" with \"sorted\"/\"stable\"");
            if (optlegacy && (optsorted || optstable)) throw new OctaveException("unique: cannot specify \"sorted\" or \"stable\" with \"legacy\"");
            if (new[] { optrows, optfirst, optlast, optsorted, optstable, optlegacy }.Count(b => b) != opts.Length) throw new OctaveException("unique: invalid option");
            if (optrows && x is CellValue) { it.Warning("unique: \"rows\" is ignored for cell arrays"); optrows = false; }
        }
        if (!optlast) optfirst = true;
        if (!optstable) optsorted = true;
        if (x is Sparse.SparseValue)
        {
            var full = Fc(it, "full", x);
            var r = UniqueImpl(it, full, opts, nargout);
            r[0] = Fc(it, "sparse", r[0]);
            return r;
        }
        int n = optrows ? RowsOf(x) : NumelOf(x);
        bool isrowvec = !optrows && IsRowV(x);
        var empty = new MatrixValue(0, 0, new double[0], null);
        if (n == 0)
        {
            Value y = x;
            if (!optrows && SizeOf(x).Any(d => d != 0))
                y = x is CellValue ? new CellValue(0, 1, new Value[0]) : Fc(it, "zeros", Dn(0), Dn(1), MatrixValue.Str(((MatrixValue)Fc(it, "class", x)).AsString()));
            return new[] { y, empty, empty };
        }
        if (n == 1) return new[] { x, Dn(1), Dn(1) };
        Value yv = optrows ? x : (IsVec(x) ? x : ColOf(it, x));
        var (ys, j) = SortE(it, yv, optrows);
        var match = AdjEq(ys, optrows);
        Value yout;
        if (optsorted) yout = DelE(it, ys, Enumerable.Range(0, n - 1).Where(k => match[k]).Select(k => k + 2), optrows, n);
        else yout = DelE(it, yv, Enumerable.Range(0, n - 1).Where(k => match[k]).Select(k => j[k + 1]), optrows, n);
        if (nargout <= 1) return new[] { yout };
        int[] iOut, jOut = new int[0];
        if (optsorted)
        {
            var idx = Enumerable.Range(0, n - 1).Where(k => match[k]).Select(k => (!optlegacy && optfirst) ? k + 1 : k).ToHashSet();
            iOut = Enumerable.Range(0, n).Where(k => !idx.Contains(k)).Select(k => j[k]).ToArray();
            if (optlast && !optlegacy) { }
            if (nargout > 2)
            {
                jOut = new int[n]; int cs = 0;
                for (int k = 0; k < n; k++) { if (k == 0 || !match[k - 1]) cs++; jOut[j[k] - 1] = cs; }
            }
        }
        else
        {
            var kk = new int[n]; for (int q = 0; q < n; q++) kk[j[q] - 1] = q + 1;
            var uniq = new bool[n]; for (int q = 0; q < n; q++) uniq[q] = q == 0 || !match[q - 1];
            iOut = Enumerable.Range(0, n).Where(q => uniq[kk[q] - 1]).Select(q => q + 1).ToArray();
            if (nargout > 2)
            {
                var u = Enumerable.Range(0, n).Where(q => uniq[q]).Select(q => q + 1).ToArray();
                var l = new int[n]; int cs = 0; for (int q = 0; q < n; q++) { if (uniq[q]) cs++; l[q] = u[cs - 1]; }
                var p = new int[n]; for (int q = 0; q < iOut.Length; q++) p[iOut[q] - 1] = q + 1;
                jOut = new int[n]; for (int q = 0; q < n; q++) jOut[q] = p[j[l[kk[q] - 1] - 1] - 1];
            }
        }
        // legacy "last" with sorted: idx not incremented -> keeps last of run (sort stable)
        if (optlegacy && optfirst && !optlast)
        {
            // legacy default is last occurrence in Octave legacy mode
        }
        bool tr = optlegacy && isrowvec;
        return new Value[] { yout, IdxV(iOut, tr), IdxV(jOut, tr) };
    }

    static (Value a, Value b) ValidSetArgs(Interpreter it, string caller, Value x, Value y, string[] opts)
    {
        bool Allowed(Value v) => v is MatrixValue || v is Sparse.SparseValue;
        if (opts.Length == 0)
        {
            bool icx = IsCellStr(x), icy = IsCellStr(y);
            if (icx || icy)
            {
                if (icx && y is MatrixValue ym && ym.IsChar) y = Fc(it, "cellstr", y);
                else if (icy && x is MatrixValue xm && xm.IsChar) x = Fc(it, "cellstr", x);
                else if (icy && NumelOf(x) == 0) x = new CellValue(0, 0, new Value[0]);
                else if (icx && NumelOf(y) == 0) y = new CellValue(0, 0, new Value[0]);
                else if (!(icx && icy)) throw new OctaveException($"{caller}: cell array of strings cannot be combined with a nonstring value");
            }
            else if (!(Allowed(x) && Allowed(y))) throw new OctaveException($"{caller}: A and B must be arrays or cell arrays of strings");
        }
        else
        {
            int cnt = 0;
            foreach (var o in opts)
            {
                switch (o)
                {
                    case "legacy": case "sorted": case "stable": cnt++; break;
                    case "rows":
                        if (x is CellValue || y is CellValue) throw new OctaveException($"{caller}: cells not supported with \"rows\" flag");
                        if (!(Allowed(x) && Allowed(y))) throw new OctaveException($"{caller}: A and B must be arrays or cell arrays of strings");
                        if (SizeOf(x).Length > 2 || SizeOf(y).Length > 2) throw new OctaveException($"{caller}: A and B must be 2-dimensional matrices with \"rows\" flag");
                        if (SizeOf(x)[1] != SizeOf(y)[1] && !(NumelOf(x) == 0 || NumelOf(y) == 0)) throw new OctaveException($"{caller}: number of columns in A and B must match");
                        break;
                    default: throw new OctaveException($"{caller}: invalid option: {o}");
                }
            }
            if (cnt > 1) throw new OctaveException($"{caller}: only one of \"sorted\", \"stable\", or \"legacy\" may be specified");
        }
        return (x, y);
    }

    static string[] SetOpts(Value[] a, int from, string fn)
    {
        var r = new List<string>();
        for (int k = from; k < a.Length; k++) { if (!IsString(a[k])) throw new OctaveException($"{fn}: options must be strings"); r.Add(Sx(a[k])); }
        return r.ToArray();
    }

    static void RegisterPhase7Sets()
    {
        Reg("unique", (it, a, n) =>
        {
            if (a.Length < 1) Usage("unique");
            var x = a[0];
            if (!(x is MatrixValue || x is Sparse.SparseValue || IsCellStr(x))) throw new OctaveException("unique: X must be an array or cell array of strings");
            var opts = SetOpts(a, 1, "unique");
            return UniqueImpl(it, x, opts, n);
        });
        Reg("union", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("union");
            var opts = SetOpts(a, 2, "union");
            var (A, B) = ValidSetArgs(it, "union", a[0], a[1], opts);
            bool rows = opts.Contains("rows"), legacy = opts.Contains("legacy");
            bool isrow = legacy ? !IsColV(A) || !IsColV(B) : IsRowV(A) && IsRowV(B);
            Value y = rows ? VCat(it, A, B) : VCat(it, ColOf(it, A), ColOf(it, B));
            if (!rows && isrow) y = y is CellValue cy ? new CellValue(1, cy.Numel, cy.Data) : Fc(it, "transpose", y);
            var r = UniqueImpl(it, y, opts.Where(o => o != "sorted" || true).ToArray(), Math.Max(n, 1) > 1 ? 2 : 1);
            if (!rows && r[0] is CellValue rc && rc.Numel == 0) r[0] = isrow ? new CellValue(1, 0, new Value[0]) : new CellValue(0, 1, new Value[0]);
            if (n <= 1) return new[] { r[0] };
            int na = rows ? RowsOf(A) : NumelOf(A);
            var idx = M(r[1], "union").Re;
            bool rr = M(r[1], "union").R == 1 && idx.Length > 1;
            return new Value[] { r[0], IdxV(idx.Where(v => v <= na).Select(v => (int)v), rr), IdxV(idx.Where(v => v > na).Select(v => (int)v - na), rr) };
        });
        Reg("intersect", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("intersect");
            var opts = SetOpts(a, 2, "intersect");
            var (A, B) = ValidSetArgs(it, "intersect", a[0], a[1], opts);
            var empty = new MatrixValue(0, 0, new double[0], null);
            if (NumelOf(A) == 0 || NumelOf(B) == 0)
            {
                Value c = IsNum(A) && IsNum(B) ? empty : B is CellValue ? new CellValue(0, 0, new Value[0]) : MatrixValue.Str("");
                return new Value[] { c, empty, empty };
            }
            bool rows = opts.Contains("rows"), sorted = !opts.Contains("stable"), legacy = opts.Contains("legacy");
            bool isrow = legacy ? !IsColV(A) || !IsColV(B) : IsRowV(A) && IsRowV(B);
            var ua = UniqueImpl(it, A, opts, 2); var ub = UniqueImpl(it, B, opts, 2);
            Value a1 = ua[0], b1 = ub[0];
            var ia = M(ua[1], "intersect").Re; var ib = M(ub[1], "intersect").Re;
            Value cc = rows ? VCat(it, a1, b1) : VCat(it, ColOf(it, a1), ColOf(it, b1));
            var (cs, ic) = SortE(it, cc, rows);
            var adj = AdjEq(cs, rows);
            var match = Enumerable.Range(0, adj.Length).Where(k => adj[k]).ToArray(); // 0-based positions k (pair k,k+1)
            int lenA = rows ? RowsOf(a1) : NumelOf(a1);
            Value c2; int[] sortIdx = new int[0];
            if (sorted) c2 = SelE(it, cs, match.Select(k => k + 1), rows);
            else
            {
                var cIdx = match.Select(k => ic[k]).ToArray();
                sortIdx = Enumerable.Range(0, cIdx.Length).OrderBy(q => cIdx[q]).ThenBy(q => q).Select(q => q + 1).ToArray();
                c2 = SelE(it, cc, cIdx.OrderBy(z => z), rows);
            }
            if (!rows && isrow) c2 = c2 is CellValue cy ? new CellValue(1, cy.Numel, cy.Data) : Fc(it, "reshape", c2, Dn(1), Dn(NumelOf(c2)));
            if (n <= 1) return new[] { c2 };
            var iaO = match.Select(k => (int)ia[ic[k] - 1]).ToArray();
            var ibO = match.Select(k => (int)ib[ic[k + 1] - lenA - 1]).ToArray();
            if (!sorted)
            {
                iaO = iaO.OrderBy(z => z).ToArray();
                var ibInd = new int[sortIdx.Length]; for (int q = 0; q < sortIdx.Length; q++) ibInd[sortIdx[q] - 1] = q + 1;
                var nb = new int[ibO.Length]; for (int q = 0; q < ibO.Length; q++) nb[ibInd[q] - 1] = ibO[q];
                ibO = nb;
            }
            bool tr = legacy && isrow && !rows;
            return new Value[] { c2, IdxV(iaO, tr), IdxV(ibO, tr) };
        });
        Reg("setdiff", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("setdiff");
            var opts = SetOpts(a, 2, "setdiff");
            var (A, B) = ValidSetArgs(it, "setdiff", a[0], a[1], opts);
            bool rows = opts.Contains("rows"), legacy = opts.Contains("legacy");
            bool isrow = legacy ? !IsColV(A) || !IsColV(B) : IsRowV(A);
            var ua = UniqueImpl(it, A, opts, 2);
            Value c = ua[0]; var ia = ua.Length > 1 ? ua[1] : null;
            if (NumelOf(c) != 0 && NumelOf(B) != 0)
            {
                var b1 = rows ? UniqueImpl(it, B, opts, 1)[0] : UniqueImpl(it, B, new string[0], 1)[0];
                Value cat = rows ? VCat(it, c, b1) : VCat(it, ColOf(it, c), ColOf(it, b1));
                var (cs, idx) = SortE(it, cat, rows);
                var adj = AdjEq(cs, rows);
                var dups = Enumerable.Range(0, adj.Length).Where(k => adj[k]).Select(k => idx[k]).ToArray();
                int nc = rows ? RowsOf(c) : NumelOf(c);
                c = DelE(it, c, dups, rows, nc);
                if (!rows) c = isrow ? (c is CellValue cv ? new CellValue(1, cv.Numel, cv.Data) : Fc(it, "reshape", c, Dn(1), Dn(NumelOf(c)))) : ColOf(it, c);
                if (ia != null)
                {
                    var iv = M(ia, "setdiff"); var ds = dups.ToHashSet();
                    var keep = Enumerable.Range(1, iv.Numel).Where(k => !ds.Contains(k)).Select(k => (int)iv.Re[k - 1]);
                    ia = IdxV(keep, legacy && isrow && !rows);
                }
            }
            return n > 1 ? new[] { c, ia ?? new MatrixValue(0, 1, new double[0], null) } : new[] { c };
        });
        Reg("setxor", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("setxor");
            var opts = SetOpts(a, 2, "setxor");
            var (A, B) = ValidSetArgs(it, "setxor", a[0], a[1], opts);
            bool rows = opts.Contains("rows"), sorted = !opts.Contains("stable"), legacy = opts.Contains("legacy");
            bool isrow = legacy ? !IsColV(A) || !IsColV(B) : IsRowV(A) && IsRowV(B);
            var ua = UniqueImpl(it, A, opts, 2); var ub = UniqueImpl(it, B, opts, 2);
            Value a1 = ua[0], b1 = ub[0];
            var ia = M(ua[1], "setxor").Re; var ib = M(ub[1], "setxor").Re;
            Value c; int[] iv = new int[0]; int na = rows ? RowsOf(a1) : NumelOf(a1);
            if (NumelOf(a1) == 0) return n <= 1 ? new[] { b1 } : new[] { b1, ua[1], ub[1] };
            else if (NumelOf(b1) == 0) return n <= 1 ? new[] { a1 } : new[] { a1, ua[1], ub[1] };
            else
            {
                Value cat = rows ? VCat(it, a1, b1) : VCat(it, ColOf(it, a1), ColOf(it, b1));
                var (cs, i) = SortE(it, cat, rows);
                var adj = AdjEq(cs, rows);
                var del = new HashSet<int>(); for (int k = 0; k < adj.Length; k++) if (adj[k]) { del.Add(k); del.Add(k + 1); }
                int nn = i.Length;
                if (sorted) { c = SelE(it, cs, Enumerable.Range(0, nn).Where(k => !del.Contains(k)).Select(k => k + 1), rows); iv = Enumerable.Range(0, nn).Where(k => !del.Contains(k)).Select(k => i[k]).ToArray(); }
                else
                {
                    var rem = del.Select(k => i[k]).ToHashSet();
                    c = SelE(it, cat, Enumerable.Range(1, nn).Where(k => !rem.Contains(k)), rows);
                    iv = Enumerable.Range(0, nn).Where(k => !del.Contains(k)).Select(k => i[k]).OrderBy(z => z).ToArray();
                }
                if (!rows && isrow) c = c is CellValue cv ? new CellValue(1, cv.Numel, cv.Data) : Fc(it, "reshape", c, Dn(1), Dn(NumelOf(c)));
            }
            if (n <= 1) return new[] { c };
            bool tr = legacy && isrow && !rows;
            return new Value[] { c, IdxV(iv.Where(z => z <= na).Select(z => (int)ia[z - 1]), tr), IdxV(iv.Where(z => z > na).Select(z => (int)ib[z - na - 1]), tr) };
        });
        Reg("ismember", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("ismember");
            var opts = a.Skip(2).Select(v => IsString(v) ? Sx(v).ToLowerInvariant() : "?").ToArray();
            bool rows = opts.Contains("rows"), legacy = opts.Contains("legacy");
            if (opts.Length != (rows ? 1 : 0) + (legacy ? 1 : 0)) throw new OctaveException("ismember: only \"rows\" and \"legacy\" are valid options");
            Value x = a[0], s = a[1];
            if (x is Sparse.SparseValue) x = Fc(it, "full", x);
            if (s is Sparse.SparseValue) s = Fc(it, "full", s);
            if (x is MatrixValue xm0 && xm0.IsChar && xm0.R <= 1 && s is CellValue) x = new CellValue(1, 1, new Value[] { x });
            (x, s) = ValidSetArgs(it, "ismember", x, s, opts.Where(o => o == "rows").ToArray());
            var dims = SizeOf(x);
            if (!rows)
            {
                int nx = NumelOf(x), ns = NumelOf(s);
                var tf = new double[nx]; var si = new double[nx];
                if (x is CellValue xc)
                {
                    var sc = (CellValue)s; var map = new Dictionary<string, int>();
                    for (int k = 0; k < ns; k++) { var key = CellS(sc.Data[k]) ?? "\u0001"; if (legacy || !map.ContainsKey(key)) map[key] = k + 1; }
                    for (int k = 0; k < nx; k++) if (CellS(xc.Data[k]) is string ks && map.TryGetValue(ks, out int p)) { tf[k] = 1; si[k] = p; }
                }
                else
                {
                    var xm = M(x, "ismember"); var sm = M(s, "ismember");
                    bool cplx = xm.Im != null || sm.Im != null;
                    var mapR = new Dictionary<double, int>(); var mapI = new Dictionary<double, int>();
                    for (int k = 0; k < ns; k++)
                    {
                        double r = sm.Re[k] == 0 ? 0 : sm.Re[k], im = (sm.Im?[k] ?? 0) == 0 ? 0 : sm.Im![k];
                        if (!double.IsNaN(r) && (legacy || !mapR.ContainsKey(r))) mapR[r] = k + 1;
                        if (cplx && !double.IsNaN(im) && (legacy || !mapI.ContainsKey(im))) mapI[im] = k + 1;
                    }
                    for (int k = 0; k < nx; k++)
                    {
                        double r = xm.Re[k] == 0 ? 0 : xm.Re[k];
                        bool okR = mapR.TryGetValue(r, out int pr);
                        if (!cplx) { if (okR) { tf[k] = 1; si[k] = pr; } continue; }
                        double im = (xm.Im?[k] ?? 0) == 0 ? 0 : xm.Im![k];
                        bool okI = mapI.TryGetValue(im, out int pi);
                        if (okR && okI) { tf[k] = 1; si[k] = legacy ? Math.Min(pr, pi) : Math.Max(pr, pi); }
                    }
                }
                var T = new MatrixValue(1, 1, tf, null, NumKind.Logical); T.R = dims[0]; T.C = nx / Math.Max(1, dims[0]); if (dims[0] == 0) T.C = dims.Length > 1 ? dims[1] : 0; T.SetDims(dims);
                var I2 = new MatrixValue(1, 1, si, null); I2.R = T.R; I2.C = T.C; I2.SetDims(dims);
                return new Value[] { T, I2 };
            }
            else
            {
                var xm = M(x, "ismember"); var sm = M(s, "ismember");
                int R = xm.R, RS = sm.R; var tf = new double[R]; var si = new double[R];
                if (xm.Numel != 0 && sm.Numel != 0)
                {
                    for (int i = 0; i < R; i++)
                    {
                        for (int q = legacy ? RS - 1 : 0; legacy ? q >= 0 : q < RS; q += legacy ? -1 : 1)
                        {
                            bool e = true;
                            for (int j = 0; j < xm.C && e; j++) e = xm.Re[j * R + i] == sm.Re[j * RS + q] && (xm.Im?[j * R + i] ?? 0) == (sm.Im?[j * RS + q] ?? 0);
                            if (e) { tf[i] = 1; si[i] = q + 1; break; }
                        }
                    }
                    if (RS == 1) for (int i = 0; i < R; i++) si[i] = tf[i];
                }
                return new Value[] { new MatrixValue(R, 1, tf, null, NumKind.Logical), new MatrixValue(R, 1, si, null) };
            }
        });
    }
}
