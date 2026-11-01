// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    sealed class MovSlice
    {
        public int N; public double[] Wlen = new double[2]; public bool Scalar;
        public int[] Lo = new int[0], Hi = new int[0]; // 1-based inclusive window per center (unclipped for standard)
        public List<int> Cpre = new(), C = new(), Cpost = new();
        public bool Standard = true;
    }

    static MovSlice MakeSlice(int N, double[] w, double[]? sp)
    {
        if (w.Any(v => !(v >= 0))) throw new OctaveException("movslice: WLEN must be a positive scalar or 2-element array", "Octave:invalid-input-arg");
        if (w.Length != 1 && w.Length != 2) throw new OctaveException("movslice: WLEN must be a positive scalar or 2-element array", "Octave:invalid-input-arg");
        var s = new MovSlice { N = N, Scalar = w.Length == 1 };
        bool standard = sp == null || sp.Length < 2 || Enumerable.Range(1, sp.Length - 1).All(k => sp[k] - sp[k - 1] == 1);
        if (sp != null && sp.Length != N) throw new OctaveException("movslice: SamplePoints must have length N", "Octave:invalid-input-arg");
        bool intv = w.All(v => Math.Truncate(v) == v);
        double[] wl;
        if (standard && intv && s.Scalar) wl = w[0] % 2 == 1 ? new[] { (w[0] - 1) / 2, (w[0] - 1) / 2 } : new[] { w[0] / 2, w[0] / 2 - 1 };
        else
        {
            wl = s.Scalar ? new[] { w[0] / 2, w[0] / 2 } : w.ToArray();
            if (standard) wl = wl.Select(Math.Truncate).ToArray();
        }
        s.Wlen = wl; s.Standard = standard;
        s.Lo = new int[N]; s.Hi = new int[N];
        if (standard)
        {
            int nb = (int)wl[0], na = (int)wl[1];
            for (int c = 1; c <= Math.Min(nb, N); c++) s.Cpre.Add(c);
            int cnf = Math.Max(1, N - na + 1);
            for (int c = cnf; c <= N; c++) s.Cpost.Add(c);
            for (int c = nb + 1; c <= cnf - 1; c++) s.C.Add(c);
            for (int c = 1; c <= N; c++) { s.Lo[c - 1] = c - nb; s.Hi[c - 1] = c + na; }
        }
        else
        {
            var p = sp!;
            bool uniform = N > 1 && Enumerable.Range(2, N - 2 < 0 ? 0 : N - 2).All(k => Math.Abs((p[k] - p[k - 1]) - (p[1] - p[0])) <= 1e-12 * Math.Abs(p[1] - p[0]));
            double e1 = p[1] - p[0], e2 = p[N - 1] - p[N - 2];
            for (int i = 0; i < N; i++)
            {
                bool pre = p[i] - wl[0] <= p[0] - e1;
                bool post = s.Scalar ? p[i] + wl[1] > p[N - 1] + e2 : p[i] + wl[1] >= p[N - 1] + e2;
                if (pre) s.Cpre.Add(i + 1);
                if (post) s.Cpost.Add(i + 1);
                if (!pre && !post) s.C.Add(i + 1);
                int lo = 0; while (lo < N && !(p[lo] >= p[i] - wl[0])) lo++;
                int hi = N - 1; while (hi >= 0 && !(s.Scalar ? p[hi] < p[i] + wl[1] : p[hi] <= p[i] + wl[1])) hi--;
                s.Lo[i] = lo + 1; s.Hi[i] = hi + 1;
                if (uniform)
                {
                    double h = e1;
                    if (lo == 0) { int q = 0; while (p[0] + (q - 1) * h >= p[i] - wl[0] - 1e-12 * Math.Abs(h)) q--; s.Lo[i] = q + 1; }
                    if (hi == N - 1) { int q = N - 1; while (s.Scalar ? p[0] + (q + 1) * h < p[i] + wl[1] - 1e-12 * Math.Abs(h) : p[0] + (q + 1) * h <= p[i] + wl[1] + 1e-12 * Math.Abs(h)) q++; s.Hi[i] = q + 1; }
                }
            }
        }
        return s;
    }

    static string UintFor(int N) => N <= 255 ? "uint8" : N <= 65535 ? "uint16" : "uint32";

    static Value[] MovFun(Interpreter it, Func<Value, Value> call, Value fcn, Value x, Value wlenV, Value[] opts)
    {
        if (fcn is not FunctionHandleValue && fcn is not null) throw new OctaveException("movfun: FCN must be a valid function handle");
        if (!IsNum(wlenV)) throw new OctaveException("movfun: WLEN must be numeric");
        int dim = 0; bool omitnan = false; double nanval = double.NaN; double[]? outdim = null; double[]? sp = null;
        string bc = "shrink"; double bcVal = double.NaN; bool bcNum = false;
        if (opts.Length % 2 == 1) throw new OctaveException("movfun: Each PROPERTY must have a VALUE");
        for (int k = 0; k < opts.Length; k += 2)
        {
            var pv = opts[k];
            if (!(pv is MatrixValue pm && pm.IsChar && pm.R == 1)) throw new OctaveException("movfun: PROPERTY name must be a valid string");
            var val = opts[k + 1];
            switch (Sx(pv).ToLowerInvariant())
            {
                case "dim":
                    if (val is MatrixValue dm0 && dm0.Numel == 0) break;
                    if (!(IsNum(val) && IsScal(val) && D(val, "movfun") >= 1 && D(val, "movfun") == Math.Floor(D(val, "movfun")))) throw new OctaveException("movfun: DIM must be a positive integer");
                    dim = I(val, "movfun"); break;
                case "endpoints":
                    if (IsNum(val) && IsScal(val)) { bcNum = true; bcVal = D(val, "movfun"); bc = "value"; }
                    else if (val is MatrixValue em && em.IsChar && em.R == 1 && new[] { "shrink", "discard", "fill", "same", "periodic" }.Contains(Sx(val).ToLowerInvariant())) bc = Sx(val).ToLowerInvariant();
                    else if (IsScal(val) && val is MatrixValue vm && x is MatrixValue xm1 && (vm.IsChar ? xm1.IsChar : vm.Kind == xm1.Kind && vm.Kind != NumKind.Logical && !xm1.IsChar)) { bcNum = true; bcVal = vm.Re[0]; bc = "value"; }
                    else throw new OctaveException("movfun: ENDPOINTS must be a numeric scalar, a scalar the same class as X, or a valid Endpoint method");
                    break;
                case "nancond":
                    if (!(val is MatrixValue nm && nm.IsChar && nm.R == 1 && new[] { "includenan", "includemissing", "omitnan", "omitmissing" }.Contains(Sx(val).ToLowerInvariant())))
                        throw new OctaveException("movfun: NANCOND must be 'includenan', 'includemissing', 'omitnan', or 'omitmissing'");
                    omitnan = Sx(val).ToLowerInvariant().StartsWith("omit"); break;
                case "nanval":
                    if (!(IsScal(val) && IsNum(val))) throw new OctaveException("movfun: NANVAL must be a numeric scalar or NaN");
                    nanval = D(val, "movfun"); break;
                case "outdim":
                    if (!(IsNum(val) && IsVec(val) && M(val, "movfun").Re.All(v => v >= 1 && v == Math.Floor(v)))) throw new OctaveException("movfun: OUTDIM must be a numeric, positive, integer-valued scalar or vector");
                    outdim = M(val, "movfun").Re; break;
                case "samplepoints":
                    {
                        var sv = IsNum(val) && IsVec(val) ? M(val, "movfun").Re : null;
                        if (sv == null || Enumerable.Range(1, sv.Length - 1).Any(q => !(sv[q] > sv[q - 1]))) throw new OctaveException("movfun: SAMPLEPOINTS must be a sorted, non-repeating, numeric vector");
                        sp = sv; break;
                    }
                default: throw new OctaveException($"movfun: unknown PROPERTY '{Sx(pv)}'");
            }
        }
        var szx = SizeOf(x).ToList();
        if (NumelOf(x) == 0)
        {
            var cls = ((MatrixValue)Fc(it, "class", x)).AsString();
            var szv = new MatrixValue(1, szx.Count, szx.Select(z => (double)z).ToArray(), null);
            try { return new[] { Fc(it, "zeros", szv, MatrixValue.Str(cls)) }; } catch (OctaveException) { return new[] { Fc(it, "zeros", szv) }; }
        }
        if (dim == 0) { dim = szx.FindIndex(z => z > 1) + 1; if (dim == 0) dim = 1; }
        while (szx.Count < dim) szx.Add(1);
        int nd = szx.Count, N = szx[dim - 1];
        if (sp != null)
        {
            if (sp.Length != N) throw new OctaveException("movfun: SamplePoints must be the same size as x in operating dimension");
            var spc = Enumerable.Range(1, N - 1).Select(q => sp[q] - sp[q - 1]).ToArray();
            bool uniform = spc.All(v => v == spc[0]);
            if (spc.All(v => v == 1)) sp = null;
            else if (!uniform && bc != "shrink") throw new OctaveException("movfun: when SamplePoints are not uniformly spaced the only valid EndPoints option is 'shrink'");
        }
        var wl = M(wlenV, "movfun").Re;
        var sl = MakeSlice(N, wl, sp);
        var dperm = new List<int> { dim }; for (int q = 1; q <= nd; q++) if (q != dim) dperm.Add(q);
        var xp = Fc(it, "permute", x, new MatrixValue(1, nd, dperm.Select(z => (double)z).ToArray(), null));
        int ncols = NumelOf(x) / Math.Max(1, N);
        var xm = M(xp, "movfun"); bool isLogicalOrChar = xm.IsChar || xm.Kind == NumKind.Logical;
        var X = xm;
        // centers to output
        List<int> centers = bc == "discard" ? sl.C.ToList() : Enumerable.Range(1, N).ToList();
        if (bc == "discard" && !sl.Standard) centers = sl.C.ToList();
        int No = centers.Count;
        var cpreSet = sl.Cpre.ToHashSet(); var cpostSet = sl.Cpost.ToHashSet();
        Value Window(int col, int c, out bool hasNaN, out bool allNaN, out int count)
        {
            int lo = sl.Lo[c - 1], hi = sl.Hi[c - 1];
            var vals = new List<double>(); var ims = X.Im != null ? new List<double>() : null; var isPad = new List<bool>();
            for (int q = lo; q <= hi; q++)
            {
                int qi = q; bool pad = false; double pvv = 0;
                if (q < 1 || q > N)
                {
                    switch (bc)
                    {
                        case "shrink": case "discard": continue;
                        case "same": qi = q < 1 ? 1 : N; break;
                        case "periodic": qi = ((q - 1) % N + N) % N + 1; break;
                        case "fill": pad = true; pvv = double.NaN; break;
                        default: pad = true; pvv = bcVal; break;
                    }
                }
                if (pad) { vals.Add(pvv); ims?.Add(0); isPad.Add(true); }
                else { vals.Add(X.Re[col * N + qi - 1]); ims?.Add(X.Im![col * N + qi - 1]); isPad.Add(false); }
            }
            hasNaN = false; allNaN = vals.Count > 0;
            var keep = new List<int>();
            for (int q = 0; q < vals.Count; q++)
            {
                bool isn = !isPad[q] && (double.IsNaN(vals[q]) || ims != null && double.IsNaN(ims[q]));
                if (isn) hasNaN = true; else allNaN = false;
                if (!omitnan || !isn) keep.Add(q);
            }
            count = keep.Count;
            var w = new MatrixValue(keep.Count, 1, keep.Select(q => vals[q]).ToArray(), ims == null ? null : keep.Select(q => ims[q]).ToArray(), X.Kind == NumKind.Logical || X.IsChar ? NumKind.Double : X.Kind);
            if (X.IsChar || X.Kind == NumKind.Logical) w = (MatrixValue)Fc(it, X.IsChar ? "char" : "logical", w);
            return w;
        }
        // determine output class / outdims from first window
        Value fout;
        {
            int len = Math.Min((int)(sl.Wlen[0] + sl.Wlen[1] + 1), N);
            var seg = new MatrixValue(len, 1, X.Re.Take(len).ToArray(), X.Im?.Take(len).ToArray(), isLogicalOrChar ? NumKind.Double : X.Kind);
            Value segv = X.IsChar ? Fc(it, "char", seg) : X.Kind == NumKind.Logical ? Fc(it, "logical", seg) : seg;
            fout = call(segv);
        }
        string yclass = ((MatrixValue)Fc(it, "class", fout)).AsString();
        int nout = NumelOf(fout);
        if (outdim != null && outdim.Max() > nout) throw new OctaveException($"movfun: output dimension OUTDIM ({outdim.Max()}) is larger than largest available dimension ({nout})", "Octave:invalid-input-arg");
        var od = outdim?.Select(v => (int)v - 1).ToArray() ?? Enumerable.Range(0, nout).ToArray();
        int so = od.Length;
        var re = new double[No * ncols * so]; double[]? im = null;
        for (int col = 0; col < ncols; col++)
        {
            bool colAllNaN = true;
            for (int q = 0; q < N; q++) if (!double.IsNaN(X.Re[col * N + q])) { colAllNaN = false; break; }
            if (colAllNaN)
            {
                double v = !omitnan || double.IsNaN(nanval) ? double.NaN : nanval;
                for (int r = 0; r < No; r++) for (int o = 0; o < so; o++) re[(o * ncols + col) * No + r] = v;
                continue;
            }
            for (int r = 0; r < No; r++)
            {
                int c = centers[r];
                var w = Window(col, c, out bool hasNaN, out bool allNaN, out int cnt);
                bool center = !cpreSet.Contains(c) && !cpostSet.Contains(c);
                double[] res = new double[so]; double[]? resI = null;
                if (!omitnan && hasNaN) { for (int o = 0; o < so; o++) res[o] = double.NaN; }
                else if (omitnan && cnt == 0) { for (int o = 0; o < so; o++) res[o] = nanval; }
                else
                {
                    var fr = call(w);
                    var fm = M(fr, "movfun");
                    for (int o = 0; o < so; o++) { res[o] = od[o] < fm.Numel ? fm.Re[od[o]] : double.NaN; if (fm.Im != null) { resI ??= new double[so]; resI[o] = fm.Im[od[o]]; } }
                }
                for (int o = 0; o < so; o++)
                {
                    re[(o * ncols + col) * No + r] = res[o];
                    if (resI != null) { im ??= new double[re.Length]; im[(o * ncols + col) * No + r] = resI[o]; }
                }
            }
        }
        var y = new MatrixValue(No, ncols * so, re, im);
        var shape = dperm.Select(q => q == dim ? No : szx[q - 1]).ToList(); shape.Add(so);
        Value yv = Fc(it, "reshape", y, new MatrixValue(1, shape.Count, shape.Select(z => (double)z).ToArray(), null));
        var ip = dperm.Select(z => (double)z).Concat(new[] { (double)(nd + 1) }).ToArray();
        yv = Fc(it, "ipermute", yv, new MatrixValue(1, ip.Length, ip, null));
        if (NumelOf(yv) != 0) yv = Fc(it, "squeeze", yv);
        if (yclass != "double") yv = Fc(it, "cast", yv, MatrixValue.Str(yclass));
        return new[] { yv };
    }

    static Value[] ParseMovArgs(string caller, Value[] v)
    {
        var args = new List<Value>(); bool haveDim = false, haveNan = false;
        for (int idx = 0; idx < v.Length; idx++)
        {
            var arg = v[idx];
            if (arg is MatrixValue m && m.IsChar)
            {
                var s = m.AsString().ToLowerInvariant();
                if (s is "omitnan" or "includenan" or "omitmissing" or "includemissing") { args.Add(MatrixValue.Str("nancond")); args.Add(arg); haveNan = true; }
                else
                {
                    idx++;
                    if (idx >= v.Length) throw new OctaveException($"{caller}: property '{m.AsString()}' missing value argument");
                    args.Add(arg); args.Add(v[idx]);
                }
            }
            else if (IsNum(arg)) { args.Add(MatrixValue.Str("dim")); args.Add(arg); haveDim = true; }
            else throw new OctaveException($"{caller}: invalid input at position {idx + 1}", "Octave:invalid-input-arg");
            if (haveNan && haveDim) { args.AddRange(v.Skip(idx + 1)); break; }
        }
        return args.ToArray();
    }

    static void RegisterPhase7Mov()
    {
        Reg("__parse_movargs__", (it, a, n) =>
        {
            if (a.Length < 1) Usage("__parse_movargs__");
            var r = ParseMovArgs(Sx(a[0]), a.Skip(1).ToArray());
            return new Value[] { new CellValue(r.Length == 0 ? 0 : 1, r.Length, r) };
        });
        Reg("movfun", (it, a, n) =>
        {
            if (a.Length < 3) Usage("movfun");
            return MovFun(it, w => Fn(it, "feval", 1, a[0], w)[0], a[0], a[1], a[2], a.Skip(3).ToArray());
        });
        Reg("movslice", (it, a, n) =>
        {
            if (a.Length < 2) Usage("movslice");
            if (!(IsNum(a[0]) && IsScal(a[0]) && D(a[0], "movslice") >= 1 && D(a[0], "movslice") == Math.Floor(D(a[0], "movslice")))) throw new OctaveException("movslice: N must be a positive integer", "Octave:invalid-input-arg");
            if (!IsNum(a[1]) || M(a[1], "movslice").Re.Any(v => !(v >= 0))) throw new OctaveException("movslice: WLEN must be a positive scalar or 2-element array", "Octave:invalid-input-arg");
            int N = I(a[0], "movslice");
            double[]? sp = a.Length > 2 && NumelOf(a[2]) > 0 ? M(a[2], "movslice").Re : null;
            var s = MakeSlice(N, M(a[1], "movslice").Re, sp);
            string ucls = UintFor(N);
            Value Cast(Value v) => Fc(it, "cast", v, MatrixValue.Str(ucls));
            Value slc; Value win;
            if (s.Standard)
            {
                int nb = (int)s.Wlen[0], na = (int)s.Wlen[1], wn = nb + na + 1;
                slc = Cast(Mat(wn, s.C.Count, (i, j) => s.C[j] - nb + i));
                win = Mat(wn, 1, (i, j) => i - nb);
            }
            else
            {
                slc = Cast(Mat(2, N, (i, j) => i == 0 ? Math.Clamp(s.Lo[j], 1, N) : Math.Clamp(s.Hi[j], 1, N)));
                win = Mat(2, N, (i, j) => i == 0 ? sp![j] - s.Wlen[0] : sp![j] + s.Wlen[1]);
            }
            Value C = s.C.Count == 0 ? new MatrixValue(1, 0, new double[0], null) : IdxV(s.C, true); if (s.Standard) C = Cast(C);
            Value Row(List<int> l) => l.Count == 0 ? new MatrixValue(1, 0, new double[0], null) : IdxV(l, true);
            return new Value[] { slc, C, Row(s.Cpre), Row(s.Cpost), win, new MatrixValue(1, 2, s.Wlen.ToArray(), null), MatrixValue.Bool(s.Scalar) };
        });
        void Simple(string name, string fn, params Value[] pre)
        {
            Reg(name, (it, a, n) =>
            {
                if (a.Length < 2) Usage(name);
                var extra = new Value[0];
                bool hasSp = a.Skip(2).Any(v => v is MatrixValue m && m.IsChar && m.AsString().Equals("samplepoints", StringComparison.OrdinalIgnoreCase));
                var pp = hasSp ? pre.Take(2).ToArray() : pre;
                return MovFun(it, w => Fn(it, fn, 1, new[] { w }.Concat(extra).ToArray())[0], null!, a[0], a[1], pp.Concat(ParseMovArgs(name, a.Skip(2).ToArray())).ToArray());
            });
        }
        Simple("movmean", "mean"); Simple("movmedian", "median"); Simple("movmax", "max", MatrixValue.Str("nancond"), MatrixValue.Str("omitnan"), MatrixValue.Str("Endpoints"), Dn(double.NegativeInfinity)); Simple("movmin", "min", MatrixValue.Str("nancond"), MatrixValue.Str("omitnan"), MatrixValue.Str("Endpoints"), Dn(double.PositiveInfinity));
        Simple("movsum", "sum", MatrixValue.Str("nanval"), Dn(0), MatrixValue.Str("Endpoints"), Dn(0)); Simple("movprod", "prod", MatrixValue.Str("nanval"), Dn(1), MatrixValue.Str("Endpoints"), Dn(1));
        foreach (var nm in new[] { "movstd", "movvar" })
        {
            string fn = nm == "movstd" ? "std" : "var";
            Reg(nm, (it, a, n) =>
            {
                if (a.Length < 2) Usage(nm);
                var rest = a.Skip(2).ToList(); Value[] extra = new Value[0];
                if (rest.Count > 0 && IsNum(rest[0]) && NumelOf(rest[0]) > 0)
                {
                    if (!IsScal(rest[0])) throw new OctaveException($"{nm}: OPT must be 0 or 1");
                    double o = D(rest[0], nm);
                    if (o == 1) extra = new Value[] { Dn(1) }; else if (o != 0) throw new OctaveException($"{nm}: OPT must be 0 or 1");
                    rest.RemoveAt(0);
                }
                return MovFun(it, w => Fn(it, fn, 1, new[] { w }.Concat(extra).ToArray())[0], null!, a[0], a[1], ParseMovArgs(nm, rest.ToArray()));
            });
        }
        Reg("movmad", (it, a, n) =>
        {
            if (a.Length < 2) Usage("movmad");
            var args = ParseMovArgs("movmad", a.Skip(2).ToArray()).ToList();
            Value[] extra = new Value[] { Dn(1) };
            for (int k = args.Count - 2; k >= 0; k -= 1)
            {
                if (args[k] is MatrixValue m && m.IsChar && m.AsString().Equals("method", StringComparison.OrdinalIgnoreCase) && k % 2 == 0)
                {
                    var t = Sx(args[k + 1]).ToLowerInvariant();
                    if (t == "mean") extra = new Value[0]; else if (t != "median") throw new OctaveException("movmad: METHOD must be either MEAN or MEDIAN");
                    for (int j = args.Count - 2; j >= 0; j--) if (args[j] is MatrixValue mj && mj.IsChar && mj.AsString().Equals("method", StringComparison.OrdinalIgnoreCase)) args.RemoveRange(j, 2);
                    break;
                }
            }
            return MovFun(it, w => Fn(it, "mad", 1, new[] { w }.Concat(extra).ToArray())[0], null!, a[0], a[1], args.ToArray());
        });
    }
}
