// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>odeset/odeget and the explicit Runge-Kutta solvers ode45 (Dormand-Prince) and ode23 (Bogacki-Shampine).</summary>
public static partial class BuiltinLibrary
{
    static readonly string[] OdeFields = { "AbsTol", "BDF", "Events", "InitialSlope", "InitialStep", "Jacobian", "JConstant", "JPattern",
        "Mass", "MassSingular", "MaxOrder", "MaxStep", "MStateDependence", "MvPattern", "NonNegative", "NormControl", "OutputFcn",
        "OutputSel", "Refine", "RelTol", "Stats", "Vectorized" };

    static void RegisterOde()
    {
        Reg1("odeset", a =>
        {
            var st = new StructValue(1, 1, OdeFields.ToList());
            foreach (var f in OdeFields) st.Set(f, MatrixValue.Empty());
            int k = 0;
            while (k < a.Length && a[k] is StructValue old)
            {
                foreach (var f in old.Fields) if (!old.Elems[0][f].IsEmptyValue()) st.Set(Canon(f), old.Elems[0][f]);
                k++;
            }
            for (; k + 1 < a.Length; k += 2) st.Set(Canon(S(a[k], "odeset")), a[k + 1]);
            if (k < a.Length) throw new OctaveException("odeset: FIELD/VALUE arguments must occur in pairs");
            return st;
        });
        Reg("odeget", (it, a, n) =>
        {
            var st = a[0] as StructValue ?? throw new OctaveException("odeget: ODE_OPT must be a valid ODE_OPT structure");
            var f = Canon(S(a[1], "odeget"));
            var v = st.HasField(f) ? st.Elems[0][f] : MatrixValue.Empty();
            if (v.IsEmptyValue() && a.Length > 2) v = a[2];
            return Out(v);
        });
        Reg("ode45", (it, a, n) => Ode(it, a, n, false));
        Reg("ode23", (it, a, n) => Ode(it, a, n, true));
    }

    static string Canon(string f) => OdeFields.FirstOrDefault(x => x.Equals(f, StringComparison.OrdinalIgnoreCase)) ?? f;

    static bool IsEmptyValue(this Value v) => v.Rows * v.Cols == 0;

    static double OptD(StructValue? o, string f, double def)
    {
        if (o == null || !o.HasField(f)) return def;
        var v = o.Elems[0][f];
        return v is MatrixValue m && m.Numel > 0 ? m.Re[0] : def;
    }

    static Value[] Ode(Interpreter it, Value[] a, int nargout, bool low)
    {
        string fn = low ? "ode23" : "ode45";
        if (a.Length < 3) throw new OctaveException($"Invalid call to {fn}");
        var f = a[0];
        var tspan = M(a[1], fn).Re;
        if (tspan.Length < 2) throw new OctaveException($"{fn}: TRANGE must contain at least 2 elements");
        var y0m = M(a[2], fn);
        var y0 = (double[])y0m.Re.Clone();
        var opts = a.Length > 3 ? a[3] as StructValue : null;
        var extra = a.Skip(4).ToArray();
        double rtol = OptD(opts, "RelTol", 1e-3), atol = OptD(opts, "AbsTol", 1e-6);
        double t0 = tspan[0], tf = tspan[^1], dir = Math.Sign(tf - t0);
        if (dir == 0) throw new OctaveException($"{fn}: invalid time span, TRANGE(1) == TRANGE(2)");
        double hmax = OptD(opts, "MaxStep", Math.Abs(tf - t0) / 10);
        int refine = (int)OptD(opts, "Refine", low ? 1 : 4);
        int neq = y0.Length;

        double[] F(double t, double[] y)
        {
            var args = new List<Value> { MatrixValue.Scalar(t), new MatrixValue(neq, 1, (double[])y.Clone()) };
            args.AddRange(extra);
            var r = it.CallCallable(f, args.ToArray(), 1);
            if (r.Length == 0) throw new OctaveException($"{fn}: function returned no value");
            var m = M(r[0], fn);
            if (m.Numel != neq) throw new OctaveException($"{fn}: function must return a vector of length {neq} (got {m.Numel})");
            return (double[])m.Re.Clone();
        }

        // Butcher tableaux
        double[] c; double[][] A; double[] b; double[] e; int order;
        if (low)
        {
            c = new[] { 0, 0.5, 0.75, 1.0 };
            A = new[] { new double[0], new[] { 0.5 }, new[] { 0.0, 0.75 }, new[] { 2 / 9.0, 1 / 3.0, 4 / 9.0 } };
            b = new[] { 2 / 9.0, 1 / 3.0, 4 / 9.0, 0 };
            var bs = new[] { 7 / 24.0, 1 / 4.0, 1 / 3.0, 1 / 8.0 };
            e = b.Zip(bs, (x, y) => x - y).ToArray(); order = 3;
        }
        else
        {
            c = new[] { 0, 1 / 5.0, 3 / 10.0, 4 / 5.0, 8 / 9.0, 1.0, 1.0 };
            A = new[]
            {
                new double[0],
                new[] { 1 / 5.0 },
                new[] { 3 / 40.0, 9 / 40.0 },
                new[] { 44 / 45.0, -56 / 15.0, 32 / 9.0 },
                new[] { 19372 / 6561.0, -25360 / 2187.0, 64448 / 6561.0, -212 / 729.0 },
                new[] { 9017 / 3168.0, -355 / 33.0, 46732 / 5247.0, 49 / 176.0, -5103 / 18656.0 },
                new[] { 35 / 384.0, 0, 500 / 1113.0, 125 / 192.0, -2187 / 6784.0, 11 / 84.0 },
            };
            b = new[] { 35 / 384.0, 0, 500 / 1113.0, 125 / 192.0, -2187 / 6784.0, 11 / 84.0, 0 };
            var bs = new[] { 5179 / 57600.0, 0, 7571 / 16695.0, 393 / 640.0, -92097 / 339200.0, 187 / 2100.0, 1 / 40.0 };
            e = b.Zip(bs, (x, y) => x - y).ToArray(); order = 5;
        }
        int stages = c.Length;

        var ts = new List<double> { t0 };
        var ys = new List<double[]> { (double[])y0.Clone() };
        var fs = new List<double[]>();
        double t = t0; var y = (double[])y0.Clone();
        var k = new double[stages][];
        k[0] = F(t, y); fs.Add(k[0]);
        double Norm(double[] v, double[] ya, double[] yb)
        {
            double s = 0;
            for (int i = 0; i < neq; i++) { double sc = atol + rtol * Math.Max(Math.Abs(ya[i]), Math.Abs(yb[i])); s = Math.Max(s, Math.Abs(v[i]) / sc); }
            return s;
        }
        // initial step (Hairer's heuristic)
        double h = OptD(opts, "InitialStep", 0);
        if (h <= 0)
        {
            double d0 = Norm(y, y, y), d1 = Norm(k[0], y, y);
            double h0 = d0 < 1e-5 || d1 < 1e-5 ? 1e-6 : 0.01 * d0 / d1;
            h0 = Math.Min(h0, Math.Abs(tf - t0));
            var y1 = y.Select((v, i) => v + dir * h0 * k[0][i]).ToArray();
            var f1 = F(t + dir * h0, y1);
            double d2 = Norm(f1.Select((v, i) => v - k[0][i]).ToArray(), y, y) / h0;
            double h1 = Math.Max(d1, d2) <= 1e-15 ? Math.Max(1e-6, h0 * 1e-3) : Math.Pow(0.01 / Math.Max(d1, d2), 1.0 / order);
            h = Math.Min(100 * h0, h1);
        }
        h = Math.Min(h, hmax);
        int nsteps = 0, nfail = 0;
        // ---- Events option: [value, isterminal, direction] = events(t, y)
        Value? evFn = opts != null && opts.HasField("Events") && !opts.Elems[0]["Events"].IsEmptyValue() ? opts.Elems[0]["Events"] : null;
        var te = new List<double>(); var ye = new List<double[]>(); var ie = new List<int>();
        bool terminated = false;
        (double[] v, double[] term, double[] dirn) Ev(double tt, double[] yy)
        {
            var args = new List<Value> { MatrixValue.Scalar(tt), new MatrixValue(neq, 1, (double[])yy.Clone()) };
            args.AddRange(extra);
            var r = it.CallCallable(evFn!, args.ToArray(), 3);
            if (r.Length < 3) throw new OctaveException($"{fn}: Events function must return [value, isterminal, direction]");
            var v = M(r[0], fn).Re.ToArray();
            double[] Pad(Value x) { var m = M(x, fn).Re; return Enumerable.Range(0, v.Length).Select(i => m.Length == 0 ? 0 : m[Math.Min(i, m.Length - 1)]).ToArray(); }
            return (v, Pad(r[1]), Pad(r[2]));
        }
        double[]? evPrev = evFn != null ? Ev(t, y).v : null;
        while (dir * (tf - t) > 1e-14 * Math.Max(1, Math.Abs(tf)))
        {
            it.CheckInterrupt();
            if (h < 16 * double.Epsilon * Math.Abs(t) || nsteps > 1_000_000)
            {
                it.Warning($"{fn}: Solving was not successful. The iterative integration loop exited at time t = {t} before the endpoint at tend = {tf} was reached. This may happen if the stepsize becomes too small. Try to reduce the value of 'InitialStep' and/or 'MaxStep' with the command 'odeset'.");
                break;
            }
            if (dir * (t + dir * h - tf) > 0) h = Math.Abs(tf - t);
            for (int s = 1; s < stages; s++)
            {
                var yt = (double[])y.Clone();
                for (int j = 0; j < s; j++) if (A[s][j] != 0) for (int i = 0; i < neq; i++) yt[i] += dir * h * A[s][j] * k[j][i];
                k[s] = F(t + dir * c[s] * h, yt);
            }
            var yn = (double[])y.Clone(); var err = new double[neq];
            for (int s = 0; s < stages; s++)
                for (int i = 0; i < neq; i++) { yn[i] += dir * h * b[s] * k[s][i]; err[i] += dir * h * e[s] * k[s][i]; }
            double en = Norm(err, y, yn);
            if (en <= 1 || h <= 1e-12)
            {
                double tn = t + dir * h;
                double[] fn1 = low || stages < 7 ? F(tn, yn) : k[stages - 1];
                if (evFn != null)
                {
                    var (vn, term, dirs) = Ev(tn, yn);
                    double tStop = double.NaN; double[]? yStop = null;
                    var found = new List<(double t, double[] y, int i, bool term)>();
                    for (int q = 0; q < vn.Length && q < evPrev!.Length; q++)
                    {
                        double va = evPrev[q], vb = vn[q];
                        bool cross = (va < 0 && vb >= 0 && dirs[q] >= 0) || (va > 0 && vb <= 0 && dirs[q] <= 0);
                        if (!cross) continue;
                        // Illinois (regula falsi) on the Hermite interpolant, in units of the step fraction
                        double a0 = 0, b0 = 1, fa = va, fb = vb; int side = 0; double th = 1;
                        for (int itr = 0; itr < 60; itr++)
                        {
                            th = fb - fa == 0 ? (a0 + b0) / 2 : (a0 * fb - b0 * fa) / (fb - fa);
                            if (!(th > a0 && th < b0)) th = (a0 + b0) / 2;
                            double fm = Ev(t + dir * h * th, Hermite(y, yn, k[0], fn1, h * dir, th)).v[q];
                            if (Math.Abs(b0 - a0) * h < 4 * double.Epsilon * Math.Max(1, Math.Abs(t)) || fm == 0) break;
                            if (Math.Sign(fm) == Math.Sign(fb)) { b0 = th; fb = fm; if (side == -1) fa /= 2; side = -1; }
                            else { a0 = th; fa = fm; if (side == 1) fb /= 2; side = 1; }
                            if (Math.Abs(b0 - a0) < 1e-12) break;
                        }
                        found.Add((t + dir * h * th, Hermite(y, yn, k[0], fn1, h * dir, th), q, term[q] != 0));
                    }
                    found.Sort((p1, p2) => (dir * p1.t).CompareTo(dir * p2.t));
                    foreach (var ev in found)
                    {
                        te.Add(ev.t); ye.Add(ev.y); ie.Add(ev.i + 1);
                        if (ev.term) { tStop = ev.t; yStop = ev.y; break; }
                    }
                    evPrev = vn;
                    if (yStop != null)
                    {
                        double hs = Math.Abs(tStop - t);
                        var fstop = F(tStop, yStop);
                        for (int r = 1; r < refine; r++)
                        {
                            double th = (double)r / refine;
                            if (h * th >= hs) break;
                            ys.Add(Hermite(y, yn, k[0], fn1, h * dir, th)); ts.Add(t + dir * h * th); fs.Add(null!);
                        }
                        t = tStop; y = yStop; k[0] = fstop;
                        ts.Add(t); ys.Add((double[])y.Clone()); fs.Add(fstop);
                        nsteps++; terminated = true;
                        break;
                    }
                }
                // dense refinement with cubic Hermite interpolation
                for (int r = 1; r < refine; r++)
                {
                    double th = (double)r / refine;
                    ys.Add(Hermite(y, yn, k[0], fn1, h * dir, th)); ts.Add(t + dir * h * th); fs.Add(null!);
                }
                t = tn; y = yn; k[0] = fn1;
                ts.Add(t); ys.Add((double[])y.Clone()); fs.Add(fn1);
                nsteps++;
                double fac = en == 0 ? 5 : Math.Min(5, Math.Max(0.2, 0.9 * Math.Pow(1 / en, 1.0 / order)));
                h = Math.Min(hmax, h * fac);
            }
            else
            {
                nfail++;
                h *= Math.Max(0.1, 0.9 * Math.Pow(1 / en, 1.0 / order));
            }
        }

        List<double> outT; List<double[]> outY;
        if (tspan.Length > 2)
        {
            outT = new(); outY = new();
            // build list of accepted nodes (with derivatives) for interpolation
            var nodes = new List<(double t, double[] y, double[] f)>();
            for (int i = 0; i < ts.Count; i++) if (fs[i] != null) nodes.Add((ts[i], ys[i], fs[i]));
            int seg = 0;
            foreach (var tq in tspan.Where(x => !terminated || dir * (x - t) < 0).Concat(terminated ? new[] { t } : Array.Empty<double>()))
            {
                while (seg < nodes.Count - 2 && dir * (tq - nodes[seg + 1].t) > 0) seg++;
                if (nodes.Count == 1) { outT.Add(tq); outY.Add(nodes[0].y); continue; }
                var (ta, ya, fa) = nodes[seg]; var (tb, yb, fb) = nodes[seg + 1];
                double hh = tb - ta, th = hh == 0 ? 0 : (tq - ta) / hh;
                outT.Add(tq); outY.Add(Hermite(ya, yb, fa, fb, hh, th));
            }
        }
        else { outT = ts; outY = ys; }

        var tm = new MatrixValue(outT.Count, 1, outT.ToArray());
        var ym = new MatrixValue(outT.Count, neq);
        for (int i = 0; i < outT.Count; i++) for (int j = 0; j < neq; j++) ym.Re[j * outT.Count + i] = outY[i][j];
        if (nargout <= 1)
        {
            var st = new StructValue();
            st.Set("ie", new MatrixValue(1, ie.Count, ie.Select(x => (double)x).ToArray()));
            st.Set("xe", new MatrixValue(1, te.Count, te.ToArray()));
            var yeT = new MatrixValue(neq, ye.Count);
            for (int i = 0; i < ye.Count; i++) for (int j = 0; j < neq; j++) yeT.Re[i * neq + j] = ye[i][j];
            st.Set("ye", yeT);
            st.Set("solver", Str(fn));
            st.Set("x", new MatrixValue(1, outT.Count, outT.ToArray()));
            var yt = new MatrixValue(neq, outT.Count);
            for (int i = 0; i < outT.Count; i++) for (int j = 0; j < neq; j++) yt.Re[i * neq + j] = outY[i][j];
            st.Set("y", yt);
            var stats = new StructValue(); stats.Set("nsteps", Num(nsteps)); stats.Set("nfailed", Num(nfail));
            st.Set("stats", stats);
            return Out(st);
        }
        var teM = new MatrixValue(te.Count, 1, te.ToArray());
        var yeM = new MatrixValue(ye.Count, neq);
        for (int i = 0; i < ye.Count; i++) for (int j = 0; j < neq; j++) yeM.Re[j * ye.Count + i] = ye[i][j];
        var ieM = new MatrixValue(ie.Count, 1, ie.Select(x => (double)x).ToArray());
        return new Value[] { tm, ym, teM, yeM, ieM };
    }

    static double[] Hermite(double[] y0, double[] y1, double[] f0, double[] f1, double h, double th)
    {
        double h00 = (1 + 2 * th) * (1 - th) * (1 - th), h10 = th * (1 - th) * (1 - th), h01 = th * th * (3 - 2 * th), h11 = th * th * (th - 1);
        var r = new double[y0.Length];
        for (int i = 0; i < r.Length; i++) r[i] = h00 * y0[i] + h10 * h * f0[i] + h01 * y1[i] + h11 * h * f1[i];
        return r;
    }
}
