// MatRewrite - GPL-3.0-or-later
// gallery: native port of Octave scripts/special-matrix/gallery.m (Higham test matrices).
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static Value GCol(IEnumerable<double> v) { var a = v.ToArray(); return new MatrixValue(a.Length, 1, a, null); }
    static Value GOnes(int r, int c) => IdxMat(r, c, (i, j) => 1);
    static Value GEye(int n) => IdxMat(n, n, (i, j) => i == j ? 1 : 0);
    static double[] GVec(Value v) { if (v is Sparse.SparseValue sp) v = sp.ToDense(); if (v is MatrixValue m) { if (m.IsChar || m.Kind != NumKind.Double) { var r = new double[m.Numel]; for (int q = 0; q < r.Length; q++) r[q] = m.Re[q]; return r; } return m.Re; } return new[] { D(v, "gallery") }; }

    static void RegisterPhase8Gallery()
    {
        Reg("orth", (it, a, nout) =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to orth");
            var A = a[0]; var sz = SizeOf(A);
            if (NumelOf(A) == 0) return new[] { (Value)MatrixValue.Empty() };
            var o = Fn(it, "svd", 3, A);
            var s = GVec(Fc(it, "diag", o[1]));
            if (sz[0] == 1 || sz[1] == 1) s = new[] { s[0] };
            double tol = Math.Max(sz[0], sz[1]) * (s.Length > 0 ? s[0] : 0) * Math.Pow(2, -52);
            int rank = s.Count(q => q > tol);
            if (rank == 0) return new[] { (Value)new MatrixValue(sz[0], 0) };
            return new[] { IndexAny(it, o[0], new Value[] { Colon, RangeV(1, rank) }) };
        });
        Reg("gallery", (it, a, nout) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to gallery");
            if (!(a[0] is MatrixValue nm && nm.IsChar)) throw new OctaveException("gallery: NAME must be a string");
            string name = nm.AsString().ToLowerInvariant();
            var v = a.Skip(1).ToArray();
            int na = v.Length;
            Value F(string f, params Value[] x) => Fc(it, f, x);
            double Arg(int k, double def) => na > k ? D(v[k], "gallery") : def;
            int N(int k = 0) => (int)D(v[k], "gallery");
            void Nargs(int lo, int hi) { if (na < lo || na > hi) throw new OctaveException($"Invalid call to gallery {name}"); }
            (Value vec, int n) VecOrN(Value x)
            {
                if (IsScal(x) && Math.Floor(D(x, "gallery")) == D(x, "gallery")) { int n = (int)D(x, "gallery"); return (RangeV(1, n), n); }
                if (NumelOf(x) > 1 && IsVec(x)) return (F("reshape", x, Dn(1), MatrixValue.Empty()), NumelOf(x));
                throw new OctaveException($"gallery: {name}: input must be a scalar integer or a vector");
            }
            Value Diag(Value x, int k) => F("diag", x, Dn(k));
            Value Tridiag(Value x, Value y, Value z)
            {
                var xs = GVec(x); var ys = GVec(y); var zs = GVec(z);
                int n = ys.Length;
                if (xs.Length != n - 1 || zs.Length != n - 1) throw new OctaveException("gallery: X, Y, Z must have consistent lengths");
                var full = IdxMat(n, n, (i, j) => i == j ? ys[i] : i == j + 1 ? xs[j] : j == i + 1 ? zs[i] : 0);
                return F("sparse", full);
            }
            Value Triw(int m, int n, double alpha, int k) => IdxMat(m, n, (i, j) => j - i > k ? 0 : i == j ? 1 : j > i ? alpha : 0);
            Value Hilb(int n) => IdxMat(n, n, (i, j) => 1.0 / (i + j + 1));
            Value Cauchy(Value x, Value? y)
            {
                if (y != null && !IsNum(y)) throw new OctaveException("gallery: Y must be a numeric vector");
                Value xv;
                if (IsScal(x) && Math.Floor(D(x, "gallery")) == D(x, "gallery")) xv = RangeV(1, (int)D(x, "gallery"));
                else if (NumelOf(x) > 1 && IsVec(x)) xv = x;
                else throw new OctaveException("gallery: X must be an integer or a vector");
                var xs = GVec(xv); var ys = y == null ? xs : GVec(y);
                if (xs.Length != ys.Length) throw new OctaveException("gallery: X and Y must be vectors of same length");
                return IdxMat(xs.Length, ys.Length, (i, j) => 1.0 / (xs[i] + ys[j]));
            }
            Value Jord(int n, double lam) => IdxMat(n, n, (i, j) => i == j ? lam : j == i + 1 ? 1 : 0);
            Value Toep(Value c, Value? r = null) => r == null ? F("toeplitz", c) : F("toeplitz", c, r);
            Value RandN(int r, int c) => F("randn", Dn(r), Dn(c));
            Value Rand(int r, int c) => F("rand", Dn(r), Dn(c));
            Value[] One(Value x) => new[] { x };
            Value DataGen(string gen, int minArgs)
            {
                if (na < minArgs) throw new OctaveException($"Invalid call to gallery {name}");
                var args = v.ToList(); Value jidx;
                if (IsNum(args[^1])) { jidx = args[^1]; args.RemoveAt(args.Count - 1); }
                else { jidx = args[^2]; args.RemoveAt(args.Count - 2); }
                Value svec = F("horzcat", args.Where(IsNum).Append(jidx).ToArray());
                var st = F(gen, MatrixValue.Str("state"));
                try { F(gen, MatrixValue.Str("state"), svec); return Fc(it, gen == "randn" ? "randn" : gen == "randi" ? "randi" : "rand", args.ToArray()); }
                finally { F(gen == "randi" ? "rand" : gen, MatrixValue.Str("state"), st); }
            }

            switch (name)
            {
                case "binomial": case "leslie": case "randcolu": case "randcorr": case "randjorth": case "sampling":
                    throw new OctaveException($"gallery: matrix {name} not implemented");
                case "cauchy": Nargs(1, 2); return One(Cauchy(v[0], na > 1 ? v[1] : null));
                case "chebspec":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 0);
                    if (k == 1) n++; else if (k != 0) throw new OctaveException("gallery: unknown K for chebspec");
                    n -= 1;
                    var x = Enumerable.Range(0, n + 1).Select(q => Math.Cos(q * Math.PI / n)).ToArray();
                    var d = Enumerable.Repeat(1.0, n + 1).ToArray(); d[0] = 2; d[n] = 2;
                    var C = new double[n + 1, n + 1];
                    for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++) C[i, j] = d[i] / d[j] / (x[i] - x[j] + (i == j ? 1 : 0));
                    C[0, 0] = (2.0 * n * n + 1) / 6;
                    for (int i = 1; i <= n; i++)
                    {
                        if ((i + 1) % 2 == 0) { for (int q = 0; q <= n; q++) C[q, i] = -C[q, i]; for (int q = 0; q <= n; q++) C[i, q] = -C[i, q]; }
                        if (i < n) C[i, i] = -x[i] / (2 * (1 - x[i] * x[i])); else C[n, n] = -C[0, 0];
                    }
                    int o = k == 1 ? 1 : 0;
                    return One(IdxMat(n + 1 - o, n + 1 - o, (i, j) => C[i + o, j + o]));
                }
                case "chebvand":
                {
                    Nargs(1, 2); Value p = na == 1 ? v[0] : v[1]; double[] ps;
                    if (IsScal(p) && Math.Floor(D(p, "gallery")) == D(p, "gallery")) { int nn = (int)D(p, "gallery"); ps = Enumerable.Range(0, nn).Select(q => nn == 1 ? 1.0 : (double)q / (nn - 1)).ToArray(); }
                    else if (NumelOf(p) > 1 && IsVec(p)) ps = GVec(p);
                    else throw new OctaveException("gallery: chebvand: P must be an integer or a vector");
                    int n = ps.Length, m = na == 1 ? n : N(0);
                    var C = new double[m, n];
                    for (int j = 0; j < n; j++) { C[0, j] = 1; if (m > 1) C[1, j] = ps[j]; for (int i = 2; i < m; i++) C[i, j] = 2 * ps[j] * C[i - 1, j] - C[i - 2, j]; }
                    return One(IdxMat(m, n, (i, j) => C[i, j]));
                }
                case "chow":
                {
                    Nargs(1, 3); int n = N(); double al = Arg(1, 1), de = Arg(2, 0);
                    return One(IdxMat(n, n, (i, j) => (j <= i ? Math.Pow(al, i - j + 1) : j == i + 1 ? 1 : 0) + (i == j ? de : 0)));
                }
                case "circul":
                {
                    Nargs(1, 1); var (vec, n) = VecOrN(v[0]); var s = GVec(vec);
                    return One(IdxMat(n, n, (i, j) => s[((j - i) % n + n) % n]));
                }
                case "clement":
                {
                    Nargs(1, 2); int n = N() - 1; int k = (int)Arg(1, 0);
                    if (k != 0 && k != 1) throw new OctaveException("gallery: K must have a value of 0 or 1");
                    return One(IdxMat(n + 1, n + 1, (i, j) =>
                    {
                        if (i == j + 1) return k == 0 ? n - j : Math.Sqrt((double)(n - j) * (j + 1));
                        if (j == i + 1) return k == 0 ? i + 1 : Math.Sqrt((double)(n - i) * (i + 1));
                        return 0;
                    }));
                }
                case "compar":
                {
                    Nargs(1, 2); var A = (MatrixValue)F("double", v[0]); int k = (int)Arg(1, 0);
                    int m = A.R, n = A.C, p = Math.Min(m, n);
                    var ab = A.Re.Select((q, idx) => A.Im != null ? Math.Sqrt(q * q + A.Im[idx] * A.Im[idx]) : Math.Abs(q)).ToArray();
                    if (k == 0) return One(IdxMat(m, n, (i, j) => i == j && i < p ? ab[i + j * m] : -ab[i + j * m]));
                    if (k != 1) throw new OctaveException("gallery: K must have a value of 0 or 1");
                    // C = A'; C(1,1)=0; mx = max(abs(C)) (over rows of A' == cols of A^T, i.e. per column of C)
                    var Ct = new double[n, m]; for (int i = 0; i < m; i++) for (int j = 0; j < n; j++) Ct[j, i] = ab[i + j * m];
                    Ct[0, 0] = 0;
                    var mx = new double[m]; for (int j = 0; j < m; j++) { double b = 0; for (int i = 0; i < n; i++) b = Math.Max(b, Ct[i, j]); mx[j] = b; }
                    return One(IdxMat(m, n, (i, j) => i == j && i < p ? ab[i + j * m] : -mx[i]));
                }
                case "condex":
                {
                    Nargs(1, 3); int n = N(); int k = (int)Arg(1, 4); double th = Arg(2, 100);
                    Value A;
                    if (k == 1) A = IdxMat(4, 4, (i, j) => new double[,] { { 1, -1, -2 * th, 0 }, { 0, 1, th, -th }, { 0, 1, 1 + th, -(th + 1) }, { 0, 0, 0, th } }[i, j]);
                    else if (k == 2) A = IdxMat(3, 3, (i, j) => new double[,] { { 1, 1 - 2 / (th * th), -2 }, { 0, 1 / th, -1 / th }, { 0, 0, 1 } }[i, j]);
                    else if (k == 3) A = IdxMat(n, n, (i, j) => i == n - 1 && j == n - 1 ? -1 : i == j ? 1 : i > j ? -1 : 0);
                    else if (k == 4)
                    {
                        var x = IdxMat(n, 3, (i, j) => j == 0 ? 1 : j == 1 ? (i == 0 ? 1 : 0) : (i % 2 == 0 ? 1 : -1) * (1 + (double)i / (n - 1)));
                        var Q = F("orth", x);
                        A = Bx("+", GEye(n), Bx("*", Dn(th), Bx("-", GEye(n), Bx("*", Q, F("transpose", Q)))));
                    }
                    else throw new OctaveException("gallery: unknown estimator K");
                    var am = (MatrixValue)A; int mm = am.C;
                    if (mm < n) A = IdxMat(n, n, (i, j) => i < am.R && j < mm ? am.Re[i + j * am.R] : i == j ? 1 : 0);
                    return One(A);
                }
                case "cycol":
                {
                    Nargs(1, 2); var nv = GVec(v[0]); int m = (int)nv[0], n = (int)nv[^1];
                    int k = na > 1 ? N(1) : Math.Max((int)Math.Round(n / 4.0, MidpointRounding.AwayFromZero), 1);
                    var B = (MatrixValue)F("double", RandN(m, k));
                    return One(IdxMat(m, n, (i, j) => B.Re[i + (j % k) * m]));
                }
                case "dorr":
                {
                    Nargs(1, 2); int n = N(); double th = Arg(1, 0.01);
                    var c = new double[n]; var e = new double[n]; var d = new double[n];
                    double h = 1.0 / (n + 1); int m = (n + 1) / 2; double term = th / (h * h);
                    for (int i = 1; i <= m; i++) { c[i - 1] = -term; e[i - 1] = c[i - 1] - (0.5 - i * h) / h; d[i - 1] = -(c[i - 1] + e[i - 1]); }
                    for (int i = m + 1; i <= n; i++) { e[i - 1] = -term; c[i - 1] = e[i - 1] + (0.5 - i * h) / h; d[i - 1] = -(c[i - 1] + e[i - 1]); }
                    var cc = GCol(c.Skip(1)); var ee = GCol(e.Take(n - 1)); var dd = GCol(d);
                    if (nout <= 1) return One(Tridiag(cc, dd, ee));
                    return new[] { cc, dd, ee };
                }
                case "dramadah":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 1);
                    if (k == 1)
                    {
                        var c = Enumerable.Repeat(1.0, n).ToArray();
                        for (int i = 2; i <= n; i += 4) { int m = Math.Min(1, n - i); for (int q = i; q <= i + m; q++) c[q - 1] = 0; }
                        var r = new double[Math.Max(n, 4)]; r[0] = 1; r[1] = 1; r[3] = 1;
                        return One(Toep(GCol(c), GCol(r.Take(n))));
                    }
                    if (k == 2) { var r = Enumerable.Range(1, n).Select(i => i >= 3 && i % 2 == 1 ? 0.0 : 1.0); var c = Enumerable.Range(0, n).Select(i => i == 0 ? 1.0 : 0); return One(Toep(GCol(c), GCol(r))); }
                    if (k == 3) { var c = Enumerable.Range(1, n).Select(i => i % 2 == 0 ? 0.0 : 1); var r = Enumerable.Range(0, n).Select(i => i < 2 ? 1.0 : 0); return One(Toep(GCol(c), RowD(r))); }
                    throw new OctaveException("gallery: unknown K for dramadah");
                }
                case "fiedler":
                {
                    Nargs(1, 1); var (vec, n) = VecOrN(v[0]); var s = GVec(vec);
                    return One(IdxMat(n, n, (i, j) => Math.Abs(s[j] - s[i])));
                }
                case "forsythe":
                {
                    Nargs(1, 3); int n = N(); double al = Arg(1, Math.Sqrt(Math.Pow(2, -52))), lam = Arg(2, 0);
                    return One(IdxMat(n, n, (i, j) => i == n - 1 && j == 0 ? al : i == j ? lam : j == i + 1 ? 1 : 0));
                }
                case "frank":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 0);
                    Func<int, int, double> f = (i, j) => j < i - 1 ? 0 : j == i - 1 ? n - j - 1 : n - j;
                    if (k == 0) return One(IdxMat(n, n, f));
                    if (k == 1) return One(IdxMat(n, n, (i, j) => f(n - 1 - j, n - 1 - i)));
                    throw new OctaveException("gallery: K must have a value of 0 or 1");
                }
                case "gcdmat":
                {
                    Nargs(1, 1); int n = N();
                    static long G(long x, long y) { while (y != 0) (x, y) = (y, x % y); return x; }
                    return One(IdxMat(n, n, (i, j) => G(i + 1, j + 1)));
                }
                case "gearmat":
                {
                    Nargs(1, 3); int n = N(); int ii = na > 1 ? N(1) : n, jj = na > 2 ? N(2) : -n;
                    var A = new double[n, n];
                    for (int q = 0; q < n - 1; q++) { A[q + 1, q] = 1; A[q, q + 1] = 1; }
                    A[0, Math.Abs(ii) - 1] = Math.Sign(ii); A[n - 1, n - Math.Abs(jj)] = Math.Sign(jj);
                    return One(IdxMat(n, n, (i, j) => A[i, j]));
                }
                case "grcar":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 3);
                    return One(IdxMat(n, n, (i, j) => i == j + 1 ? -1 : j >= i && j - i <= k ? 1 : 0));
                }
                case "hanowa":
                {
                    Nargs(1, 2); int n = N(); double d = Arg(1, -1);
                    if (n % 2 != 0) throw new OctaveException("gallery: N must be even for hanowa matrix");
                    int m = n / 2;
                    return One(IdxMat(n, n, (i, j) => i == j ? d : (i < m && j == i + m) ? -(i + 1) : (i >= m && j == i - m) ? (j + 1) : 0));
                }
                case "house":
                {
                    Nargs(1, 1);
                    var x = F("reshape", v[0], MatrixValue.Empty(), Dn(1));
                    var x1 = Ix(it, x, Dn(1));
                    double sg = D(F("sign", F("real", x1)), "gallery"); // real case
                    Value s = Bx("*", F("norm", x), Dn(sg + (D(F("abs", x1), "gallery") == 0 ? 1 : 0)));
                    Value beta;
                    if (D(F("abs", s), "gallery") == 0) beta = Dn(1);
                    else
                    {
                        var v1 = Bx("+", x1, s);
                        x = Indexing.Assign((ArrayValue)x, new Value[] { Dn(1) }, (ArrayValue)v1);
                        beta = Bx("/", Dn(1), Bx("*", F("conj", s), v1));
                    }
                    return new[] { x, beta };
                }
                case "integerdata": return One(DataGen("randi", 3));
                case "normaldata": return One(DataGen("randn", 2));
                case "uniformdata": return One(DataGen("rand", 2));
                case "invhess":
                {
                    Nargs(1, 2); double[] x; int n;
                    if (IsScal(v[0]) && Math.Floor(D(v[0], "gallery")) == D(v[0], "gallery")) { n = N(); x = Enumerable.Range(1, n).Select(q => (double)q).ToArray(); }
                    else if (IsVec(v[0])) { x = GVec(v[0]); n = x.Length; }
                    else throw new OctaveException("gallery: X must be an integer scalar, or a vector");
                    var y = na > 1 ? GVec(v[1]) : x.Take(n - 1).Select(q => -q).ToArray();
                    return One(IdxMat(n, n, (i, j) => i < j ? y[i] : x[j]));
                }
                case "invol":
                {
                    Nargs(1, 1); int n = N();
                    var A = new double[n, n]; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) A[i, j] = 1.0 / (i + j + 1);
                    double d = -n; for (int i = 0; i < n; i++) A[i, 0] *= d;
                    for (int i = 1; i <= n - 1; i++) { d = -(double)(n + i) * (n - i) * d / ((double)i * i); for (int j = 0; j < n; j++) A[i, j] *= d; }
                    return One(IdxMat(n, n, (i, j) => A[i, j]));
                }
                case "ipjfact":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 0);
                    var c = new double[n]; double p = 1; for (int i = 0; i < n; i++) { p *= i + 2; c[i] = p; }
                    var dd = new double[n]; p = 1; for (int i = 0; i < n; i++) { p *= n + 1 + i; dd[i] = p * c[n - 2 < 0 ? 0 : n - 2]; }
                    if (n == 1) dd[0] = c[0];
                    var A = (MatrixValue)F("hankel", RowD(c), RowD(dd));
                    if (k == 1) A = (MatrixValue)Bx("./", GOnes(n, n), A); else if (k != 0) throw new OctaveException("gallery: K must have a value of 0 or 1");
                    if (nout < 2) return One(A);
                    double det = 1; static double Pr(int a, int b) { double r = 1; for (int q = a; q <= b; q++) r *= q; return r; }
                    if (k == 0) { for (int i = 1; i <= n - 1; i++) det *= Pr(1, i + 1) * Pr(1, n - i); det *= Pr(1, n + 1); }
                    else { for (int i = 0; i <= n - 1; i++) det *= Pr(1, i) / Pr(1, n + 1 + i); if ((n * (n - 1) / 2) % 2 != 0) det = -det; }
                    return new Value[] { A, Dn(det) };
                }
                case "jordbloc": Nargs(1, 2); return One(Jord(N(), Arg(1, 1)));
                case "kahan":
                {
                    Nargs(1, 3); var nv = GVec(v[0]); int r = (int)nv[0], n = (int)nv[^1]; double th = Arg(1, 1.2), pert = Arg(2, 25);
                    double s = Math.Sin(th), c = Math.Cos(th), eps = Math.Pow(2, -52);
                    return One(IdxMat(r, n, (i, j) => i >= n ? 0 : (Math.Pow(s, i) * (i == j ? 1 : j > i ? -c : 0) + (i == j ? pert * eps * (n - i) : 0))));
                }
                case "kms":
                {
                    Nargs(1, 2); int n = N(); Value rho = na > 1 ? v[1] : Dn(0.5);
                    var A = F("power", rho, IdxMat(n, n, (i, j) => Math.Abs(i - j)));
                    if (D(F("abs", F("imag", rho)), "gallery") != 0) A = Bx("+", F("conj", F("tril", A, Dn(-1))), F("triu", A));
                    return One(A);
                }
                case "krylov":
                {
                    Nargs(1, 3); Value A = v[0]; int n = (int)D(F("length", A), "gallery");
                    if (IsScal(A)) { n = (int)D(A, "gallery"); A = RandN(n, n); }
                    Value x = na > 1 ? v[1] : GOnes(n, 1); int j = na > 2 ? N(2) : n;
                    var cols = new List<Value> { F("reshape", x, MatrixValue.Empty(), Dn(1)) };
                    for (int i = 1; i < j; i++) cols.Add(Bx("*", A, cols[^1]));
                    return One(F("horzcat", cols.ToArray()));
                }
                case "lauchli":
                {
                    Nargs(1, 2); int n = N(); double mu = Arg(1, Math.Sqrt(Math.Pow(2, -52)));
                    return One(IdxMat(n + 1, n, (i, j) => i == 0 ? 1 : i - 1 == j ? mu : 0));
                }
                case "lehmer": { Nargs(1, 1); int n = N(); return One(IdxMat(n, n, (i, j) => (double)(Math.Min(i, j) + 1) / (Math.Max(i, j) + 1))); }
                case "lesp":
                {
                    Nargs(1, 1); int n = N();
                    return One(IdxMat(n, n, (i, j) => i == j ? -(2.0 * (i + 2) + 1) : i == j + 1 ? 1.0 / (i + 1) : j == i + 1 ? i + 2 : 0));
                }
                case "lotkin": { Nargs(1, 1); int n = N(); return One(IdxMat(n, n, (i, j) => i == 0 ? 1 : 1.0 / (i + j + 1))); }
                case "minij": { Nargs(1, 1); int n = Math.Max(0, N()); return One(IdxMat(n, n, (i, j) => Math.Min(i, j) + 1)); }
                case "moler":
                {
                    Nargs(1, 2); int n = N(); double al = Arg(1, -1);
                    var T = Triw(n, n, al, n - 1);
                    return One(Bx("*", F("transpose", T), T));
                }
                case "neumann":
                {
                    Nargs(1, 1); var nv = GVec(v[0]); int n1, n2;
                    if (nv.Length == 1) { double m = Math.Sqrt(nv[0]); if (m * m != nv[0]) throw new OctaveException("gallery: N must be a perfect square for neumann matrix"); n1 = n2 = (int)m; }
                    else { n1 = (int)nv[0]; n2 = (int)nv[1]; }
                    var T = IdxMat(n1, n1, (i, j) => i == j ? 2 : Math.Abs(i - j) == 1 ? -1 : 0);
                    var tm = (MatrixValue)T; if (n1 > 1) { tm.Re[0 + 1 * n1] = -2; tm.Re[(n1 - 1) + (n1 - 2) * n1] = -2; }
                    return new[] { Bx("+", F("kron", T, GEye(n2)), F("kron", GEye(n2), T)), F("sparse", T) };
                }
                case "orthog":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 1);
                    switch (k)
                    {
                        case 1: return One(IdxMat(n, n, (i, j) => Math.Sin((i + 1.0) * (j + 1) * Math.PI / (n + 1)) * Math.Sqrt(2.0 / (n + 1))));
                        case 2: return One(IdxMat(n, n, (i, j) => Math.Sin((i + 1.0) * (j + 1) * 2 * Math.PI / (2 * n + 1)) * (2 / Math.Sqrt(2 * n + 1))));
                        case 3:
                        {
                            var re = new double[n * n]; var im = new double[n * n];
                            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) { double t = (double)i * j * 2 * Math.PI / n; re[i + j * n] = Math.Cos(t) / Math.Sqrt(n); im[i + j * n] = Math.Sin(t) / Math.Sqrt(n); }
                            return One(new MatrixValue(n, n, re, im));
                        }
                        case 4:
                            return One(IdxMat(n, n, (i, j) =>
                            {
                                double q = (i == 0 || j <= i) ? (j == i && i > 0 ? -i : 1) : 0;
                                if (i == 0) q = 1;
                                double dg = Math.Sqrt((i == 0 ? n : i) * (double)(i + 1));
                                return q / dg;
                            }));
                        case 5: return One(IdxMat(n, n, (i, j) => { double t = (double)i * j * 2 * Math.PI / n; return (Math.Cos(t) + Math.Sin(t)) / Math.Sqrt(n); }));
                        case -1: return One(IdxMat(n, n, (i, j) => Math.Cos((double)i * j * Math.PI / (n - 1))));
                        case -2: return One(IdxMat(n, n, (i, j) => Math.Cos(i * (j + 0.5) * Math.PI / n)));
                    }
                    throw new OctaveException("gallery: unknown K for orthog");
                }
                case "parter": { Nargs(1, 1); int n = N(); return One(IdxMat(n, n, (i, j) => 1.0 / ((i + 1.5) - (j + 1)))); }
                case "pei": { Nargs(1, 2); int n = N(); double al = Arg(1, 1); return One(IdxMat(n, n, (i, j) => 1 + (i == j ? al : 0))); }
                case "poisson":
                {
                    Nargs(1, 1); int n = N();
                    var S = IdxMat(n, n, (i, j) => i == j ? 2 : Math.Abs(i - j) == 1 ? -1 : 0);
                    return One(F("sparse", Bx("+", F("kron", GEye(n), S), F("kron", S, GEye(n)))));
                }
                case "prolate":
                {
                    Nargs(1, 2); int n = N(); double w = Arg(1, 0.25);
                    var aa = Enumerable.Range(0, n).Select(q => q == 0 ? 2 * w : Math.Sin(2 * Math.PI * w * q) / (Math.PI * q)).ToArray();
                    return One(IdxMat(n, n, (i, j) => aa[Math.Abs(i - j)]));
                }
                case "randhess":
                {
                    Nargs(1, 1); double[] x; int n; double last;
                    if (IsScal(v[0])) { n = N(); x = GVec(Bx("*", Rand(n - 1, 1), Dn(2 * Math.PI))); last = Math.Sign(D(F("randn"), "gallery")); }
                    else if (IsVec(v[0])) { x = GVec(v[0]); n = x.Length; last = Math.Sign(x[n - 1]) + (x[n - 1] == 0 ? 1 : 0); }
                    else throw new OctaveException("gallery: X must be a scalar or a vector");
                    var H = new double[n, n]; for (int q = 0; q < n; q++) H[q, q] = 1; H[n - 1, n - 1] = last;
                    for (int i = n - 1; i >= 1; i--)
                    {
                        double c = Math.Cos(x[i - 1]), s = Math.Sin(x[i - 1]);
                        for (int j = 0; j < n; j++) { double a1 = H[i - 1, j], a2 = H[i, j]; H[i - 1, j] = c * a1 + s * a2; H[i, j] = -s * a1 + c * a2; }
                    }
                    return One(IdxMat(n, n, (i, j) => H[i, j]));
                }
                case "rando":
                {
                    Nargs(1, 2); var nv = GVec(v[0]); int m = (int)nv[0], n = (int)nv[^1]; int k = (int)Arg(1, 1);
                    var R = GVec(Rand(m, n));
                    Func<double, double> f = k switch
                    {
                        1 => r => Math.Floor(r + 0.5),
                        2 => r => 2 * Math.Floor(r + 0.5) - 1,
                        3 => r => Math.Round(3 * r - 1.5, MidpointRounding.AwayFromZero),
                        _ => throw new OctaveException("gallery: unknown K for rando"),
                    };
                    return One(IdxMat(m, n, (i, j) => f(R[i + j * m])));
                }
                case "randsvd":
                {
                    Nargs(1, 5); var nv = GVec(v[0]); int m = (int)nv[0], n = (int)nv[^1];
                    double kappa = Arg(1, Math.Sqrt(Math.Pow(2, 52))); int mode = (int)Arg(2, 3);
                    if (Math.Abs(kappa) < 1) throw new OctaveException("gallery: KAPPA must be >= 1");
                    bool posdef = kappa < 0; kappa = Math.Abs(kappa);
                    int p = Math.Min(m, n);
                    if (p == 1) { var A1 = RandN(m, n); return One(Bx("/", A1, F("norm", A1))); }
                    double[] sg = Math.Abs(mode) switch
                    {
                        1 => Enumerable.Range(0, p).Select(q => q == 0 ? 1 : 1 / kappa).ToArray(),
                        2 => Enumerable.Range(0, p).Select(q => q == p - 1 ? 1 / kappa : 1).ToArray(),
                        3 => Enumerable.Range(0, p).Select(q => Math.Pow(Math.Pow(kappa, -1.0 / (p - 1)), q)).ToArray(),
                        4 => Enumerable.Range(0, p).Select(q => 1 - (double)q / (p - 1) * (1 - 1 / kappa)).ToArray(),
                        5 => GVec(Rand(p, 1)).Select(r => Math.Exp(-r * Math.Log(kappa))).ToArray(),
                        _ => throw new OctaveException("gallery: unknown MODE for randsvd"),
                    };
                    if (mode < 0) Array.Reverse(sg);
                    // random orthogonal factors via QR of Gaussian matrices
                    Value Qr(int q) { var o = Fn(it, "qr", 2, RandN(q, q)); return o[0]; }
                    if (posdef) { var Q = Qr(p); var A2 = Bx("*", Bx("*", F("transpose", Q), F("diag", RowD(sg))), Q); return One(Bx("/", Bx("+", A2, F("transpose", A2)), Dn(2))); }
                    var S = IdxMat(m, n, (i, j) => i == j ? sg[i] : 0);
                    return One(Bx("*", Bx("*", Qr(m), S), Qr(n)));
                }
                case "redheff": { Nargs(1, 1); int n = N(); return One(IdxMat(n, n, (i, j) => j == 0 || (j + 1) % (i + 1) == 0 ? 1 : 0)); }
                case "riemann": { Nargs(1, 1); int n = N(); return One(IdxMat(n, n, (i, j) => ((j + 2) % (i + 2) == 0 ? i + 2 : 0) - 1)); }
                case "ris": { Nargs(1, 1); int n = N(); var p = Enumerable.Range(1, n).Select(q => -2.0 * q + n + 1.5).ToArray(); return One(IdxMat(n, n, (i, j) => 1.0 / (p[i] + p[j]))); }
                case "smoke":
                {
                    Nargs(1, 2); int n = N(); int k = (int)Arg(1, 0);
                    if (k != 0 && k != 1) throw new OctaveException("gallery: K must have a value of 0 or 1");
                    var re = new double[n * n]; var im = new double[n * n];
                    for (int q = 0; q < n; q++) { double t = q < n - 1 ? 2 * Math.PI * (q + 1) / n : 0; re[q + q * n] = Math.Cos(t); im[q + q * n] = q < n - 1 ? Math.Sin(t) : 0; if (q < n - 1) re[q + (q + 1) * n] = 1; }
                    if (k == 0) re[n - 1] = 1;
                    return One(new MatrixValue(n, n, re, im));
                }
                case "toeppd":
                {
                    Nargs(1, 4); int n = N(); int m = na > 1 ? N(1) : n;
                    var w = na > 2 ? GVec(v[2]) : GVec(Rand(m, 1)); var th = na > 3 ? GVec(v[3]) : GVec(Rand(m, 1));
                    if (w.Length != m || th.Length != m) throw new OctaveException("gallery: W and THETA must be vectors of length M");
                    return One(IdxMat(n, n, (i, j) => { double E = 2 * Math.PI * (i - j), s = 0; for (int q = 0; q < m; q++) s += w[q] * Math.Cos(th[q] * E); return s; }));
                }
                case "toeppen":
                {
                    Nargs(1, 6); int n = N(); double[] c = { Arg(1, 1), Arg(2, -10), Arg(3, 0), Arg(4, 10), Arg(5, 1) };
                    return One(F("sparse", IdxMat(n, n, (i, j) => j - i >= -2 && j - i <= 2 ? c[j - i + 2] : 0)));
                }
                case "tridiag":
                {
                    if (na != 1 && na != 3 && na != 4) throw new OctaveException("Invalid call to gallery tridiag");
                    Value x, y, z; int n = 0;
                    if (na == 1) { n = N(); x = Dn(-1); y = Dn(2); z = Dn(-1); }
                    else if (na == 3) { x = v[0]; y = v[1]; z = v[2]; }
                    else { n = N(); x = v[1]; y = v[2]; z = v[3]; }
                    if (IsScal(x) && IsScal(y) && IsScal(z))
                    {
                        if (na == 3) throw new OctaveException("gallery: X, Y, Z must be vectors");
                        double xs = D(x, "g"), ys = D(y, "g"), zs = D(z, "g");
                        x = GCol(Enumerable.Repeat(xs, n - 1)); y = GCol(Enumerable.Repeat(ys, n)); z = GCol(Enumerable.Repeat(zs, n - 1));
                    }
                    return One(Tridiag(x, y, z));
                }
                case "triw":
                {
                    Nargs(1, 3); var nv = GVec(v[0]); int m = (int)nv[0], n = (int)nv[^1];
                    return One(Triw(m, n, Arg(1, -1), na > 2 ? N(2) : n - 1));
                }
                case "wathen":
                {
                    Nargs(2, 3); int nx = N(0), ny = N(1); int k = (int)Arg(2, 0);
                    double[,] e1 = { { 6, -6, 2, -8 }, { -6, 32, -6, 20 }, { 2, -6, 6, -6 }, { -8, 20, -6, 32 } };
                    double[,] e2 = { { 3, -8, 2, -6 }, { -8, 16, -8, 20 }, { 2, -8, 3, -8 }, { -6, 20, -8, 16 } };
                    double E(int r, int c) => (r < 4 ? (c < 4 ? e1[r, c] : e2[r, c - 4]) : (c < 4 ? e2[c, r - 4] : e1[r - 4, c - 4])) / 45;
                    int n = 3 * nx * ny + 2 * nx + 2 * ny + 1;
                    var A = new double[n, n]; var rho = GVec(Bx("*", Dn(100), Rand(nx, ny)));
                    var nn = new int[8];
                    for (int j = 1; j <= ny; j++) for (int i = 1; i <= nx; i++)
                    {
                        nn[0] = 3 * j * nx + 2 * i + 2 * j + 1; nn[1] = nn[0] - 1; nn[2] = nn[1] - 1;
                        nn[3] = (3 * j - 1) * nx + 2 * j + i - 1; nn[4] = 3 * (j - 1) * nx + 2 * i + 2 * j - 3;
                        nn[5] = nn[4] + 1; nn[6] = nn[5] + 1; nn[7] = nn[3] + 1;
                        double r = rho[(i - 1) + (j - 1) * nx];
                        for (int kr = 0; kr < 8; kr++) for (int kc = 0; kc < 8; kc++) A[nn[kr] - 1, nn[kc] - 1] += E(kr, kc) * r;
                    }
                    if (k != 0) for (int i = 0; i < n; i++) { double d = A[i, i]; for (int j = 0; j < n; j++) A[i, j] /= d; }
                    return One(F("sparse", IdxMat(n, n, (i, j) => A[i, j])));
                }
                case "wilk":
                {
                    Nargs(1, 1); int n = N();
                    if (n == 3) return new[] { IdxMat(3, 3, (i, j) => new double[,] { { 1e-10, 0.9, -0.4 }, { 0, 0.9, -0.4 }, { 0, 0, 1e-10 } }[i, j]), GCol(new double[] { 0, 0, 1 }) };
                    if (n == 4) return new[] { IdxMat(4, 4, (i, j) => new double[,] { { 0.9143e-4, 0, 0, 0 }, { 0.8762, 0.7156e-4, 0, 0 }, { 0.7943, 0.8143, 0.9504e-4, 0 }, { 0.8017, 0.6123, 0.7165, 0.7123e-4 } }[i, j]), GCol(new[] { 0.6524, 0.3127, 0.4186, 0.7853 }) };
                    if (n == 5) return One(IdxMat(5, 5, (i, j) => 1.8144 / (i + j + 2)));
                    if (n == 21) return One(IdxMat(21, 21, (i, j) => i == j ? Math.Abs(i - 10) : Math.Abs(i - j) == 1 ? 1 : 0));
                    throw new OctaveException("gallery: unknown N for wilk matrix");
                }
            }
            throw new OctaveException($"gallery: unknown matrix with NAME {name}");
        });
    }
}
