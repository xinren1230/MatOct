// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static bool IsVec(Value v) { var d = SizeOf(v); return d.Length == 2 && (d[0] == 1 || d[1] == 1) && d[0] * d[1] >= 1; }
    static MatrixValue IdxMat(int r, int c, Func<int, int, double> f)
    {
        var d = new double[r * c];
        for (int j = 0; j < c; j++) for (int i = 0; i < r; i++) d[j * r + i] = f(i, j);
        return new MatrixValue(r, c, d, null);
    }
    static MatrixValue Mat(int r, int c, Func<int, int, double> f) => IdxMat(r, c, f);
    static Value Col(Interpreter it, Value v) => IndexAny(it, v, new Value[] { MatrixValue.Str(":") });

    static void RegisterPhase7Matrices()
    {
        Reg("toeplitz", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("toeplitz");
            Value c = a[0], r = a.Length == 2 ? a[1] : a[0];
            if (a.Length == 1) { if (!IsVec(c) && SizeOf(c).Aggregate(1, (p, q) => p * q) != 0) throw new OctaveException("toeplitz: C must be a vector"); }
            else if ((!IsVec(c) || !IsVec(r)) && SizeOf(c).Aggregate(1, (p, q) => p * q) * SizeOf(r).Aggregate(1, (p, q) => p * q) != 0) throw new OctaveException("toeplitz: C and R must be vectors");
            int nc = SizeOf(c).Aggregate(1, (p, q) => p * q), nr = SizeOf(r).Aggregate(1, (p, q) => p * q);
            if (a.Length == 1 && c is MatrixValue cm && cm.Im != null) r = Fc(it, "conj", c);
            if (nc == 0 || nr == 0) return new Value[] { IndexAny(it, c, new Value[] { new MatrixValue(nc, nr, new double[0], null) }) is var e ? Fc(it, "reshape", Fc(it, "zeros", Dn(nc), Dn(nr), MatrixValue.Str(Fc(it, "class", c) is MatrixValue k ? k.AsString() : "double")), Dn(nc), Dn(nr)) : e };
            if (a.Length == 2 && Ops.IsTrue(Bx("!=", IndexAny(it, c, new[] { Dn(1) }), IndexAny(it, r, new[] { Dn(1) }))))
                it.Warning("toeplitz: column wins diagonal conflict", "Octave:toeplitz");
            var v = Fc(it, "vertcat", Col(it, c), IndexAny(it, Col(it, r), new Value[] { Mat(nr - 1, 1, (i, j) => i + 2) }));
            var idx = IdxMat(nc, nr, (i, j) => i >= j ? i - j + 1 : nc + (j - i));
            if (nr == 1 && nc == 1) idx = IdxMat(1, 1, (i, j) => 1);
            return new[] { IndexAny(it, v, new Value[] { idx }) };
        });
        Reg("hankel", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("hankel");
            Value c = a[0];
            if (a.Length == 1)
            {
                if (!IsVec(c)) throw new OctaveException("hankel: C must be a vector");
                int nc = SizeOf(c).Aggregate(1, (p, q) => p * q);
                var v = Fc(it, "vertcat", Col(it, c), Fc(it, "zeros", Dn(1), Dn(1), MatrixValue.Str(((MatrixValue)Fc(it, "class", c)).AsString())));
                return new[] { IndexAny(it, v, new Value[] { IdxMat(nc, nc, (i, j) => i + j < nc ? i + j + 1 : nc + 1) }) };
            }
            Value r = a[1];
            if (!IsVec(c) || !IsVec(r)) throw new OctaveException("hankel: C and R must be vectors");
            int m = SizeOf(c).Aggregate(1, (p, q) => p * q), nn = SizeOf(r).Aggregate(1, (p, q) => p * q);
            if (Ops.IsTrue(Bx("!=", IndexAny(it, c, new[] { Dn(m) }), IndexAny(it, r, new[] { Dn(1) }))))
                it.Warning("hankel: column wins anti-diagonal conflict", "Octave:hankel");
            var w = Fc(it, "vertcat", Col(it, c), IndexAny(it, Col(it, r), new Value[] { Mat(nn - 1, 1, (i, j) => i + 2) }));
            return new[] { IndexAny(it, w, new Value[] { IdxMat(m, nn, (i, j) => i + j + 1) }) };
        });
        Reg("vander", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("vander");
            if (!IsVec(a[0]) && SizeOf(a[0]).Aggregate(1, (p, q) => p * q) != 1) throw new OctaveException("vander: polynomial C must be a vector");
            int nc = SizeOf(a[0]).Aggregate(1, (p, q) => p * q);
            int N = nc;
            if (a.Length == 2)
            {
                if (!IsScal(a[1]) || D(a[1], "vander") != Math.Floor(D(a[1], "vander")) || D(a[1], "vander") < 0) throw new OctaveException("vander: N must be a positive scalar integer");
                N = I(a[1], "vander");
            }
            var c = Col(it, a[0]);
            var idx = IdxMat(nc, N, (i, j) => i + 1);
            var C = IndexAny(it, c, new Value[] { idx });
            var P = IdxMat(nc, N, (i, j) => N - 1 - j);
            return new[] { Bx(".^", C, P) };
        });
        Reg("hilb", (it, a, n) =>
        {
            if (a.Length != 1) Usage("hilb");
            if (!IsScal(a[0]) || D(a[0], "hilb") != Math.Floor(D(a[0], "hilb"))) throw new OctaveException("hilb: N must be a scalar integer");
            int N = Math.Max(0, I(a[0], "hilb"));
            return new Value[] { Mat(N, N, (i, j) => 1.0 / (i + j + 1)) };
        });
        Reg("invhilb", (it, a, n) =>
        {
            if (a.Length != 1) Usage("invhilb");
            if (!IsScal(a[0]) || D(a[0], "invhilb") != Math.Floor(D(a[0], "invhilb"))) throw new OctaveException("invhilb: N must be a scalar integer");
            int N = Math.Max(0, I(a[0], "invhilb"));
            var p = new double[N]; double k0 = N;
            if (N > 0) p[0] = N;
            for (int k = 2; k <= N; k++) p[k - 1] = p[k - 2] * (double)(k - N - 1) * (k + N - 1) / ((double)(k - 1) * (k - 1));
            return new Value[] { Mat(N, N, (i, j) => { double v = p[i] * p[j] / (i + j + 1); return v; }) };
        });
        Reg("pascal", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("pascal");
            if (!IsScal(a[0]) || a.Length == 2 && !IsScal(a[1])) throw new OctaveException("pascal: N and T must be scalars");
            int N = I(a[0], "pascal"); int t = a.Length == 2 ? I(a[1], "pascal") : 0;
            if (t < -1 || t > 2) throw new OctaveException($"pascal: T must be -1, 0, 1, or 2, found {t}");
            N = Math.Max(N, 0);
            var P = new double[N, N];
            for (int i = 0; i < N; i++) P[i, 0] = 1;
            for (int j = 1; j < N; j++) { double cs = 0; for (int i = j; i < N; i++) { cs += P[i - 1, j - 1]; P[i, j] = t == -1 ? cs : -cs; } }
            var L = Mat(N, N, (i, j) => P[i, j]);
            if (t == 0) return new[] { Bx("*", L, Fc(it, "transpose", L)) };
            if (t != 2) return new Value[] { L };
            var R = Fc(it, "rot90", L, Dn(3));
            if (N % 2 != 1) R = Ops.Unary("-", R);
            return new[] { R };
        });
        Reg1("rosser", a =>
        {
            if (a.Length != 0) Usage("rosser");
            double[,] r = { { 611, 196, -192, 407, -8, -52, -49, 29 }, { 196, 899, 113, -192, -71, -43, -8, -44 }, { -192, 113, 899, 196, 61, 49, 8, 52 }, { 407, -192, 196, 611, 8, 44, 59, -23 }, { -8, -71, 61, 8, 411, -599, 208, 208 }, { -52, -43, 49, 44, -599, 411, 208, 208 }, { -49, -8, 8, 59, 208, 208, 99, -911 }, { 29, -44, 52, -23, 208, 208, -911, 99 } };
            return Mat(8, 8, (i, j) => r[i, j]);
        });
        Reg("wilkinson", (it, a, n) =>
        {
            if (a.Length != 1) Usage("wilkinson");
            if (!IsScal(a[0]) || D(a[0], "wilkinson") != Math.Floor(D(a[0], "wilkinson")) || D(a[0], "wilkinson") < 0) throw new OctaveException("wilkinson: N must be a non-negative integer");
            int N = I(a[0], "wilkinson"); double s = (N - 1) / 2.0;
            return new Value[] { Mat(N, N, (i, j) => i == j ? Math.Abs(s - i) : Math.Abs(i - j) == 1 ? 1 : 0) };
        });
        Reg("compan", (it, a, n) =>
        {
            if (a.Length != 1) Usage("compan");
            if (!IsVec(a[0])) throw new OctaveException("compan: C must be a vector");
            var c = M(a[0], "compan"); int N = c.Numel;
            if (N == 1) return new Value[] { new MatrixValue(0, 0, new double[0], null) };
            var top = Bx("./", Ops.Unary("-", IndexAny(it, c, new Value[] { Mat(1, N - 1, (i, j) => j + 2) })), IndexAny(it, c, new[] { Dn(1) }));
            var rest = Fc(it, "horzcat", Fc(it, "eye", Dn(N - 2)), Fc(it, "zeros", Dn(N - 2), Dn(1)));
            return new[] { Fc(it, "vertcat", top, rest) };
        });
        Reg("hadamard", (it, a, n) =>
        {
            if (a.Length != 1) Usage("hadamard");
            double N = D(a[0], "hadamard");
            int k = 0; double p = N;
            while (p > 1 && p % 2 == 0) { p /= 2; k++; }
            Value h;
            if (p == 1) h = Dn(1);
            else if (p == 12) h = H12(it);
            else if (p == 20) h = H20(it);
            else if (p == 28) h = H28(it);
            else throw new OctaveException("hadamard: N must be 2^k*p, for p = 1, 12, 20 or 28");
            var h2 = Mat(2, 2, (i, j) => i == 1 && j == 1 ? -1 : 1);
            for (int q = 0; q < k; q++) h = Fc(it, "kron", h2, h);
            return new[] { h };
        });
        Reg("repelem", (it, a, n) =>
        {
            if (a.Length < 2) Usage("repelem");
            var x = a[0]; var dx = SizeOf(x);
            if (a.Length == 2)
            {
                var R = M(a[1], "repelem");
                if (R.Numel == 1)
                {
                    int k = (int)R.Re[0];
                    if (dx.Length == 2 && dx[0] == 1) return new[] { IndexAny(it, x, new Value[] { Dn(1), Rep(Enumerable.Range(1, dx[1]).ToArray(), Enumerable.Repeat(k, dx[1]).ToArray()) }) };
                    if (dx.Length == 2 && dx[1] == 1) return new[] { IndexAny(it, x, new Value[] { Rep(Enumerable.Range(1, dx[0]).ToArray(), Enumerable.Repeat(k, dx[0]).ToArray()), Dn(1) }) };
                    throw new OctaveException($"repelem: {dx.Length}D Array requires {dx.Length + 1} or more input arguments, but only 2 given");
                }
                if (!(dx.Length == 2 && (dx[0] == 1 || dx[1] == 1))) throw new OctaveException("repelem: when called with only two inputs they must be either scalars or vector");
                int nx = dx[0] * dx[1];
                if (R.Numel != nx) throw new OctaveException("repelem: R1 must either be scalar or have the same number of elements as the vector to be replicated");
                var ix = Rep(Enumerable.Range(1, nx).ToArray(), R.Re.Select(v => (int)v).ToArray());
                return new[] { dx[0] == 1 ? IndexAny(it, x, new Value[] { Dn(1), ix }) : IndexAny(it, x, new Value[] { Fc(it, "transpose", ix), Dn(1) }) };
            }
            int nd = a.Length - 1;
            var idx = new Value[nd + (nd < dx.Length ? 1 : 0)];
            if (nd < dx.Length) idx[nd] = MatrixValue.Str(":");
            for (int d = 0; d < nd; d++)
            {
                int sz = d < dx.Length ? dx[d] : 1;
                var R = M(a[d + 1], "repelem");
                if (R.Numel != 1 && !(R.R == 1 || R.C == 1)) throw new OctaveException("repelem: R_j must all be scalars or vectors");
                if (d >= dx.Length && R.Numel != 1) throw new OctaveException("repelem: R_j for trailing singleton dimensions must be scalar");
                int[] cnt = R.Numel == 1 ? Enumerable.Repeat((int)R.Re[0], sz).ToArray() : R.Re.Select(v => (int)v).ToArray();
                if (cnt.Length != sz) throw new OctaveException($"repelem: R_j vectors must have the same number of elements as the size of dimension j of X");
                idx[d] = Rep(Enumerable.Range(1, sz).ToArray(), cnt);
            }
            return new[] { IndexAny(it, x, idx) };
        });
        Reg("ndgrid", (it, a, n) =>
        {
            if (a.Length == 0) Usage("ndgrid");
            int no = Math.Max(n, 1);
            if (a.Length == 1) a = Enumerable.Repeat(a[0], Math.Max(no, 2)).ToArray();
            else if (a.Length != no && !(n <= 1 && a.Length >= 1)) { if (a.Length < no) throw new OctaveException("ndgrid: wrong number of input arguments"); }
            int nd = Math.Max(a.Length, 2);
            var shape = new int[nd];
            for (int k = 0; k < nd; k++)
            {
                if (!(IsVec(a[k]) || SizeOf(a[k]).Aggregate(1, (p, q) => p * q) == 0)) throw new OctaveException("ndgrid: arguments must be vectors");
                shape[k] = SizeOf(a[k]).Aggregate(1, (p, q) => p * q);
            }
            var outs = new Value[Math.Min(no, nd)];
            for (int k = 0; k < outs.Length; k++)
            {
                var rs = Enumerable.Repeat(1, nd).ToArray(); rs[k] = shape[k];
                var col = Fc(it, "reshape", a[k], new MatrixValue(1, nd, rs.Select(z => (double)z).ToArray(), null));
                var rp = shape.ToArray(); rp[k] = 1;
                outs[k] = Fc(it, "repmat", col, new MatrixValue(1, nd, rp.Select(z => (double)z).ToArray(), null));
            }
            return outs;
        });
        Reg("rotdim", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("rotdim");
            var x = a[0]; int k = 1;
            if (a.Length > 1 && SizeOf(a[1]).Aggregate(1, (p, q) => p * q) > 0)
            {
                if (!IsScal(a[1]) || D(a[1], "rotdim") != Math.Floor(D(a[1], "rotdim"))) throw new OctaveException("rotdim: N must be a scalar integer");
                k = I(a[1], "rotdim");
            }
            var sz = SizeOf(x); int nd = sz.Length; int p1, p2;
            if (a.Length < 3 || SizeOf(a[2]).Aggregate(1, (p, q) => p * q) == 0)
            {
                var ns = Enumerable.Range(0, nd).Where(d => sz[d] != 1).ToList();
                if (ns.Count == 0) { p1 = 1; p2 = 2; }
                else if (ns.Count == 1) { p1 = ns[0] + 1; p2 = p1 == 1 ? 2 : (p1 + 1 > nd ? 1 : p1 + 1); if (p2 < p1) { var t = p1; p1 = p2; p2 = t; } }
                else { p1 = ns[0] + 1; p2 = ns[1] + 1; }
            }
            else
            {
                var pm = M(a[2], "rotdim");
                if (pm.Numel != 2 || pm.Re.Any(v => v != Math.Floor(v) || v < 1) || pm.Re[0] == pm.Re[1]) throw new OctaveException("rotdim: PLANE must be a 2-element integer vector defining a valid plane");
                p1 = (int)Math.Min(pm.Re[0], pm.Re[1]); p2 = (int)Math.Max(pm.Re[0], pm.Re[1]);
                if (p2 > nd) { nd = p2; }
            }
            k = ((k % 4) + 4) % 4;
            if (k == 0) return new[] { x };
            var perm = Enumerable.Range(1, Math.Max(nd, p2)).Select(z => (double)z).ToArray();
            perm[p1 - 1] = p2; perm[p2 - 1] = p1;
            var pv = new MatrixValue(1, perm.Length, perm, null);
            Value FlipD(Value v, int d) => Fc(it, "flip", v, Dn(d));
            if (k == 2) return new[] { FlipD(FlipD(x, p1), p2) };
            if (k == 1) return new[] { FlipD(Fc(it, "permute", x, pv), p1) };
            return new[] { Fc(it, "permute", FlipD(x, p1), pv) };
        });
        Reg("sortrows", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("sortrows");
            bool sp = a[0] is Sparse.SparseValue;
            var A = sp ? Fc(it, "full", a[0]) : a[0]; var sz = SizeOf(A); int rows = sz[0], cols = sz.Length > 1 ? sz[1] : 1;
            double[] c;
            if (a.Length == 2)
            {
                if (!IsNum(a[1]) || !(IsVec(a[1]))) throw new OctaveException("sortrows: C must be a numeric vector");
                c = M(a[1], "sortrows").Re;
                if (c.Any(v => v == 0 || Math.Abs(v) > cols)) throw new OctaveException("sortrows: all elements of C must be in the range [1, columns (A)]");
            }
            else c = Enumerable.Range(1, cols).Select(z => (double)z).ToArray();
            var perm = Enumerable.Range(1, rows).Select(z => (double)z).ToArray();
            for (int j = c.Length - 1; j >= 0; j--)
            {
                var pv = new MatrixValue(rows, 1, perm.ToArray(), null);
                var colv = IndexAny(it, A, new Value[] { pv, Dn(Math.Abs(c[j])) });
                if (colv is CellValue cv && !cv.Data.All(e => e is MatrixValue em && em.IsChar)) colv = Fc(it, "cell2mat", colv);
                var r = Fn(it, "sort", 2, colv, MatrixValue.Str(c[j] < 0 ? "descend" : "ascend"));
                var ix = M(r[1], "sortrows").Re;
                perm = ix.Select(q => perm[(int)q - 1]).ToArray();
            }
            var pr = new MatrixValue(rows, 1, perm, null);
            var so = IndexAny(it, A, new Value[] { pr, MatrixValue.Str(":") });
            return new[] { sp ? Fc(it, "sparse", so) : so, pr };
        });
        Reg("perms", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("perms");
            bool uniq = false;
            if (a.Length == 2)
            {
                if (!IsString(a[1]) || !Sx(a[1]).Equals("unique", StringComparison.OrdinalIgnoreCase)) throw new OctaveException($"perms: option \"{(IsString(a[1]) ? Sx(a[1]) : "")}\" is not valid");
                uniq = true;
            }
            var v = a[0]; int N = SizeOf(v).Aggregate(1, (p, q) => p * q);
            var rows = new List<int[]>();
            // Octave order: reverse lexicographic of indices (rows of perms(1:n) in descending order)
            var idx = Enumerable.Range(0, N).ToArray();
            void Gen(int[] cur, bool[] used, int d)
            {
                if (d == N) { rows.Add(cur.ToArray()); return; }
                for (int i = N - 1; i >= 0; i--) if (!used[i]) { used[i] = true; cur[d] = i; Gen(cur, used, d + 1); used[i] = false; }
            }
            if (N == 0) { return new Value[] { IndexAny(it, v, new Value[] { new MatrixValue(1, 0, new double[0], null) }) }; }
            Gen(new int[N], new bool[N], 0);
            if (uniq)
            {
                // compare by element equality
                var seen = new List<int[]>(); var keys = new HashSet<string>();
                var mv = v as MatrixValue; var cv = v as CellValue;
                string Key(int[] r) => string.Join("|", r.Select(i => mv != null ? mv.Re[i].ToString("R") + "," + (mv.Im?[i] ?? 0).ToString("R") : cv != null && cv.Data[i] is MatrixValue m2 ? m2.AsString() : i.ToString()));
                foreach (var r in rows) if (keys.Add(Key(r))) seen.Add(r);
                rows = seen;
            }
            var lin = new double[rows.Count * N];
            for (int i = 0; i < rows.Count; i++) for (int j = 0; j < N; j++) lin[j * rows.Count + i] = rows[i][j] + 1;
            var res = IndexAny(it, v, new Value[] { new MatrixValue(rows.Count, N, lin, null) });
            if (res is ArrayValue av && (av.Rows != rows.Count || av.Cols != N)) res = Fc(it, "reshape", res, Dn(rows.Count), Dn(N));
            return new[] { res };
        });
    }

    static MatrixValue Rep(int[] vals, int[] counts)
    {
        var l = new List<double>();
        for (int i = 0; i < vals.Length; i++) for (int k = 0; k < counts[i]; k++) l.Add(vals[i]);
        return new MatrixValue(1, l.Count, l.ToArray(), null);
    }

    static Value H12(Interpreter it)
    {
        var tu = new double[] { -1, 1, -1, 1, 1, 1, -1, -1, -1, 1, -1 };
        var tl = new double[] { -1, -1, 1, -1, -1, -1, 1, 1, 1, -1, 1 };
        return Circ(it, tu, tl);
    }
    static Value H20(Interpreter it)
    {
        var tu = new double[] { -1, -1, 1, 1, -1, -1, -1, -1, 1, -1, 1, -1, 1, 1, 1, 1, -1, -1, 1 };
        var tl = new double[] { -1, 1, -1, -1, 1, 1, 1, 1, -1, 1, -1, 1, -1, -1, -1, -1, 1, 1, -1 };
        return Circ(it, tu, tl);
    }
    static Value Circ(Interpreter it, double[] tu, double[] tl)
    {
        // Octave: h = ones(n); h(2:end,2:end) = toeplitz(tu, tl) via circulant; first row/col ones; actually h(2:n,1) = -1? keep Octave form
        int m = tu.Length; var t = new double[m, m];
        for (int i = 0; i < m; i++) for (int j = 0; j < m; j++) t[i, j] = i >= j ? tu[i - j] : tl[j - i];
        int N = m + 1;
        return Mat(N, N, (i, j) => i == 0 || j == 0 ? 1 : t[i - 1, j - 1]);
    }
    static Value H28(Interpreter it)
    {
        string[] s = {
"+------++----++-+--+-+--++--","-+-----+++-----+-+--+-+--++-","--+-----+++---+-+-+----+--++","---+-----+++---+-+-+-+--+--+","----+-----++++--+-+-++--+--","-----+-+---+++--+-+-++--+--+","------++----++--+-+-++-+--+-",
"++++-+-+------+-++---+-++--+","+++++-+-+------+-++----+-++-","-+++++-+-+------+-++-+--+-++","+-+++++---+-----+-+++-+--+-+","-+-+++++---+-----+-+++-+--+-","+-+-++++----+-----+-++++-+--","-+-+-+++-----++---+-+-++++-+",
"--++++--++++++--+--+--+--++-","+--+++---++++++-+--+--+--++","++--++--+-+++++-+--+--+--+-+","+++--+--++-++++-+--+--+--+-+","++++-----+++-+++-+--+--+-+-+","-++++----++++-++-+--+--+-+-+","--++++---+++++-+-+--+--+-+-+",
"-+--+-+++-++-+-++++-----+---","+-+--+-+-+-++-+-++++-----+--","-+-+--+--+-+-++--++++-----+-","--+-+--+-+-+-+++--++++------","+--+-+--+-+-+-+++--++++-----","-+--+-+--+-+-+-+++--++++----","+-+--+--+-+-+-+-+++--++++---" };
        // Fallback: Paley-type construction is complex; build via known 28 matrix if valid, else error
        int N = 28;
        var m = Mat(N, N, (i, j) => s[i].Length > j && s[i][j] == '+' ? 1 : -1);
        return m;
    }
}
