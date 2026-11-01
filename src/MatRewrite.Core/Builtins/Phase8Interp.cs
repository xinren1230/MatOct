// MatRewrite - GPL-3.0-or-later
// interp1 / interp2 ports of Octave scripts/general/interp1.m, interp2.m.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static Value Ix(Interpreter it, Value v, params Value[] idx) => IndexAny(it, v, idx);
    static Value Colon => MatrixValue.Str(":");
    static Value RangeV(int a, int b) => RowD(Enumerable.Range(a, Math.Max(0, b - a + 1)).Select(q => (double)q));
    static Value SzTail(int[] sz) => RowD(sz.Skip(1).Select(q => (double)q));
    static Value SetOrient(Value pp) { if (pp is StructValue s) s.Set("orient", MatrixValue.Str("first")); return pp; }

    static void RegisterPhase8Interp()
    {
        Reg("interp1", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 6) Usage("interp1");
            Value x = a[0], y = a[1];
            string method = "linear"; Value? extrap = null; bool extrapStr = false; Value? xi = null;
            bool ispp = false, haveXi = false; bool? rc = null;
            for (int i = 2; i < a.Length; i++)
            {
                var arg = a[i];
                if (arg is MatrixValue am && am.IsChar)
                {
                    var s = am.AsString().ToLowerInvariant();
                    switch (s)
                    {
                        case "extrap": extrapStr = true; extrap = null; break;
                        case "pp": ispp = true; break;
                        case "right": case "-right": rc = true; break;
                        case "left": case "-left": rc = false; break;
                        default: method = s; break;
                    }
                }
                else if (i == 2) { xi = arg; haveXi = true; }
                else { extrap = arg; extrapStr = false; }
            }
            if (!haveXi && !ispp)
            {
                xi = y; y = x;
                x = IsVec(y) ? RangeV(1, NumelOf(y)) : RangeV(1, SizeOf(y)[0]);
            }
            if (extrap == null && !extrapStr)
                extrap = y is MatrixValue ym0 && ym0.Im != null ? new MatrixValue(1, 1, new[] { double.NaN }, new[] { double.NaN }) : Dn(double.NaN);
            x = Ix(it, x, Colon);
            int nx = SizeOf(x)[0];
            var szx = SizeOf(xi ?? Dn(0));
            if (IsVec(y)) y = Ix(it, y, Colon);
            var szy = SizeOf(y);
            y = Ix(it, y, Colon, Colon);
            var yd = SizeOf(y); int ny = yd[0];
            if (xi != null) xi = Ix(it, xi, Colon);
            if (nx < 2 || ny < 2) throw new OctaveException("interp1: minimum of 2 points required in each dimension");
            if (!Bool(Fc(it, "issorted", x, MatrixValue.Str("either"))))
            {
                var r = Fn(it, "sort", 2, x); x = r[0]; y = Ix(it, y, r[1], Colon);
            }
            double X(int k) => M(x, "interp1").Re[k];
            if (method is "previous" or "*previous" or "next" or "*next") rc = null;
            void Flip() { x = Fc(it, "flipud", x); y = Fc(it, "flipud", y); }
            if (rc == null) rc = !(X(nx - 1) < X(0));
            else if ((rc.Value && X(nx - 1) < X(0)) || (!rc.Value && X(nx - 1) > X(0))) Flip();
            if (method == "next") { Flip(); method = "previous"; }
            else if (method == "*next") { Flip(); method = "*previous"; }
            bool star = method.StartsWith("*");
            double dx = 0; bool haveJumps = false; bool[] jumps = Array.Empty<bool>();
            if (star) dx = X(1) - X(0);
            else
            {
                jumps = Enumerable.Range(0, nx - 1).Select(k => X(k) == X(k + 1)).ToArray();
                haveJumps = jumps.Any(b => b);
                if (haveJumps)
                {
                    if (method is "linear" or "nearest")
                    {
                        for (int k = 0; k + 1 < jumps.Length; k++) if (jumps[k] && jumps[k + 1]) { Fn(it, "warning", 0, MatrixValue.Str("interp1: multiple discontinuities at the same X value")); break; }
                    }
                    else throw new OctaveException($"interp1: discontinuities not supported for METHOD '{method}'");
                }
            }
            Value tail = SzTail(szy);
            Value xiR() => Fc(it, "reshape", xi!, RowD(szx.Select(q => (double)q)));
            Value Finish(Value pp) { SetOrient(pp); return ispp ? pp : Fc(it, "ppval", pp, xiR()); }
            Value yi;
            var xr = M(x, "interp1").Re.Take(nx).ToArray();
            switch (method)
            {
                case "nearest":
                    {
                        var br = new List<double> { xr[0] };
                        for (int k = 0; k + 1 < nx; k++) br.Add((xr[k] + xr[k + 1]) / 2);
                        br.Add(xr[nx - 1]);
                        yi = Finish(Fc(it, "mkpp", RowD(br), Fc(it, "shiftdim", y, Dn(1)), tail)); break;
                    }
                case "*nearest":
                    {
                        var br = new List<double> { xr[0] };
                        for (int k = 0; k < nx - 1; k++) br.Add(xr[0] + (0.5 + k) * dx);
                        br.Add(xr[nx - 1]);
                        yi = Finish(Fc(it, "mkpp", RowD(br), Fc(it, "shiftdim", y, Dn(1)), tail)); break;
                    }
                case "previous":
                    {
                        var br = xr.Concat(new[] { 2 * xr[nx - 1] - xr[nx - 2] });
                        yi = Finish(Fc(it, "mkpp", RowD(br), Fc(it, "shiftdim", y, Dn(1)), tail)); break;
                    }
                case "*previous":
                    {
                        var br = Enumerable.Range(0, nx + 1).Select(k => xr[0] + k * dx);
                        yi = Finish(Fc(it, "mkpp", RowD(br), Fc(it, "shiftdim", y, Dn(1)), tail)); break;
                    }
                case "linear":
                case "*linear":
                    {
                        var keep = Enumerable.Range(0, nx).Where(k => method == "*linear" || !haveJumps || k >= jumps.Length || !jumps[k]).ToArray();
                        var xx = keep.Select(k => xr[k]).ToArray(); int nxx = xx.Length;
                        var kv = RowD(keep.Select(k => (double)(k + 1)));
                        var yy = Ix(it, y, kv, Colon);
                        var dkeep = RowD(Enumerable.Range(0, nx - 1).Where(k => method == "*linear" || !haveJumps || !jumps[k]).Select(k => (double)(k + 1)));
                        var dy = Ix(it, Fc(it, "diff", y), dkeep, Colon);
                        var dxv = new MatrixValue(nxx - 1, 1, Enumerable.Range(0, nxx - 1).Select(k => xx[k + 1] - xx[k]).ToArray(), null);
                        var slope = Bx("./", dy, dxv);
                        var y0 = Ix(it, yy, RangeV(1, nxx - 1), Colon);
                        var coefs = Fc(it, "horzcat", Fc(it, "reshape", Fc(it, "transpose", slope), E0(), Dn(1)), Fc(it, "reshape", Fc(it, "transpose", y0), E0(), Dn(1)));
                        yi = Finish(Fc(it, "mkpp", RowD(xx), coefs, tail)); break;
                    }
                case "pchip": case "*pchip": case "cubic": case "*cubic":
                case "spline": case "*spline":
                    {
                        string f = method.TrimStart('*') == "spline" ? "spline" : "pchip";
                        Value xv = x;
                        if (nx == 2 || star) xv = Fc(it, "linspace", Dn(xr[0]), Dn(xr[nx - 1]), Dn(ny));
                        if (ispp)
                        {
                            var yy = Fc(it, "shiftdim", Fc(it, "reshape", y, RowD(szy.Select(q => (double)q))), Dn(1));
                            yi = SetOrient(Fc(it, f, xv, yy));
                        }
                        else
                        {
                            var yy = Fc(it, "shiftdim", y, Dn(1));
                            yi = Fc(it, f, xv, yy, xiR());
                            if (!IsVec(yy)) yi = Fc(it, "shiftdim", yi, Dn(1));
                        }
                        break;
                    }
                default: throw new OctaveException($"interp1: invalid METHOD '{method}'");
            }
            if (!ispp && extrap != null && (extrap is MatrixValue em && !em.IsChar))
            {
                double minx = Math.Min(xr[0], xr[nx - 1]), maxx = Math.Max(xr[0], xr[nx - 1]);
                var xiv = M(xiR(), "interp1");
                var outl = new MatrixValue(szx[0], szx.Length > 1 ? szx[1] : 1, xiv.Re.Take(xiv.Numel).Select(v => (v < minx || !(v <= maxx)) ? 1.0 : 0.0).ToArray(), null, NumKind.Logical);
                if (szx.Length > 2) outl.SetDims(szx);
                var yiA = (ArrayValue)yi;
                var ysz = SizeOf(yi);
                if (ysz.SequenceEqual(szx))
                {
                    if (outl.Re.Any(v => v != 0)) yi = Indexing.Assign(yiA, new Value[] { outl }, em);
                }
                else if (NumelOf(yi) != 1)
                {
                    if (outl.Re.Any(v => v != 0)) yi = Indexing.Assign(yiA, new Value[] { Ix(it, outl, Colon), Colon }, em);
                }
                else if (outl.Re.Any(v => v != 0)) yi = Indexing.Assign(yiA, new Value[] { outl }, em);
            }
            return new[] { yi };
        });
    }
    static double Keys(double h)
    {
        double a = Math.Abs(h), a2 = a * a, a3 = a2 * a;
        if (a <= 1) return 1.5 * a3 - 2.5 * a2 + 1;
        if (a <= 2) return -0.5 * a3 + 2.5 * a2 - 4 * a + 2;
        return 0;
    }
    static int LookupL(double[] t, double v)
    {   // max j (1-based) with t[j-1] <= v, at least 1
        if (double.IsNaN(v)) return t.Length;
        int lo = 0, hi = t.Length; while (lo < hi) { int mid = (lo + hi) / 2; if (t[mid] <= v) lo = mid + 1; else hi = mid; }
        return Math.Max(lo, 1);
    }
    static int LookupLR(double[] t, double v) => Math.Min(LookupL(t, v), t.Length - 1);

    static void RegisterPhase8Interp2()
    {
        Reg("interp2", (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to interp2.  Correct usage is:\n\n -- interp2 (...)");
            if (a.Length > 7) throw new OctaveException("interp2: called with too many inputs");
            int nargs = a.Length; string method = "linear"; Value? extrap = null;
            if (nargs > 1 && IsString(a[^2]))
            {
                if (!IsNum(a[^1]) || !IsScal(a[^1])) throw new OctaveException("interp2: EXTRAP must be a numeric scalar");
                extrap = a[^1]; method = Sx(a[^2]); nargs -= 2;
            }
            else if (IsString(a[^1])) { method = Sx(a[^1]); nargs -= 1; }
            if (method.StartsWith("*")) { Fn(it, "warning", 0, MatrixValue.Str("interp2: ignoring unsupported '*' flag to METHOD")); method = method[1..]; }
            method = Sx(Fc(it, "validatestring", MatrixValue.Str(method), CellValue.RowOf(new[] { "nearest", "linear", "pchip", "cubic", "spline" }.Select(q => (Value)MatrixValue.Str(q)))));
            Value? Z = null, X = null, Y = null, XI = null, YI = null, N = null;
            switch (nargs)
            {
                case 1: Z = a[0]; N = Dn(1); break;
                case 2: Z = a[0]; N = a[1]; break;
                case 3: Z = a[0]; XI = a[1]; YI = a[2]; break;
                case 5: X = a[0]; Y = a[1]; Z = a[2]; XI = a[3]; YI = a[4]; break;
                default: throw new OctaveException("Invalid call to interp2");
            }
            if (!IsNum(Z) || IsVec(Z) || SizeOf(Z).Length > 2) throw new OctaveException("interp2: Z must be a numeric 2-D matrix");
            if (N != null && !(IsScal(N) && D(N, "interp2") >= 0 && D(N, "interp2") == Math.Floor(D(N, "interp2")))) throw new OctaveException("interp2: N must be an integer >= 0");
            var zs = SizeOf(Z); int zr = zs[0], zc = zs[1];
            if (Z is MatrixValue zcm && zcm.Im != null && zcm.Im.Any(v => v != 0))
            {
                var ar = (Value[])a.Clone(); var ai = (Value[])a.Clone(); int zi = nargs == 5 ? 2 : 0;
                ar[zi] = Fc(it, "real", Z); ai[zi] = Fc(it, "imag", Z);
                if (extrap != null) { ar[^1] = Fc(it, "real", extrap); ai[^1] = Fc(it, "imag", extrap); }
                var rr = Fc(it, "interp2", ar); var ri = Fc(it, "interp2", ai);
                return new[] { Fc(it, "complex", rr, ri) };
            }
            var Zm = M(Fc(it, "double", Z), "interp2"); var z = Zm.Re.Take(zr * zc).ToArray();
            if (X == null) { X = RangeV(1, zc); Y = RangeV(1, zr); }
            if (!IsNum(X) || !IsNum(Y!)) throw new OctaveException("interp2: X, Y must be numeric matrices");
            if (N != null)
            {
                double p = Math.Pow(2, D(N, "interp2"));
                XI = RowD(Enumerable.Range((int)p, (int)(p * zc - p + 1)).Select(q => q / p));
                var yv = Enumerable.Range((int)p, (int)(p * zr - p + 1)).Select(q => q / p).ToArray();
                YI = new MatrixValue(yv.Length, 1, yv, null);
            }
            if (!IsNum(XI!) || !IsNum(YI!)) throw new OctaveException("interp2: XI, YI must be numeric");
            double[] xs, ys;
            if (IsVec(X) && IsVec(Y!)) { xs = M(X, "interp2").Re.Take(NumelOf(X)).ToArray(); ys = M(Y!, "interp2").Re.Take(NumelOf(Y!)).ToArray(); }
            else if (SizeOf(X).SequenceEqual(SizeOf(Y!)))
            {
                var xm = M(X, "interp2"); var ym2 = M(Y!, "interp2"); int r0 = SizeOf(X)[0], c0 = SizeOf(X)[1];
                xs = Enumerable.Range(0, c0).Select(j => xm.Re[j * r0]).ToArray(); ys = Enumerable.Range(0, r0).Select(i => ym2.Re[i]).ToArray();
            }
            else throw new OctaveException("interp2: X and Y must be matrices of equal size");
            if (zc != xs.Length || zr != ys.Length) throw new OctaveException("interp2: X and Y size must match the dimensions of Z");
            double Zg(int i, int j) => z[i + zr * j];
            if (xs.Length > 1 && Enumerable.Range(0, xs.Length - 1).All(k => xs[k + 1] - xs[k] < 0))
            { Array.Reverse(xs); var nz = new double[z.Length]; for (int i = 0; i < zr; i++) for (int j = 0; j < zc; j++) nz[i + zr * j] = z[i + zr * (zc - 1 - j)]; z = nz; }
            else if (Enumerable.Range(0, xs.Length - 1).Any(k => xs[k + 1] - xs[k] <= 0)) throw new OctaveException("interp2: X must be strictly monotonic");
            if (ys.Length > 1 && Enumerable.Range(0, ys.Length - 1).All(k => ys[k + 1] - ys[k] < 0))
            { Array.Reverse(ys); var nz = new double[z.Length]; for (int i = 0; i < zr; i++) for (int j = 0; j < zc; j++) nz[i + zr * j] = z[(zr - 1 - i) + zr * j]; z = nz; }
            else if (Enumerable.Range(0, ys.Length - 1).Any(k => ys[k + 1] - ys[k] <= 0)) throw new OctaveException("interp2: Y must be strictly monotonic");
            if (method == "cubic" && (zr < 3 || zc < 3))
            {
                Fn(it, "warning", 0, MatrixValue.Str("interp2: cubic requires at least 3 points in each dimension.  Falling back to linear interpolation."));
                method = "linear";
            }
            var xiS = SizeOf(XI!); var yiS = SizeOf(YI!);
            bool IsRow(int[] s) => s.Length == 2 && s[0] == 1; bool IsCol(int[] s) => s.Length == 2 && s[1] == 1;
            double[] xiv = M(XI!, "interp2").Re, yiv = M(YI!, "interp2").Re;
            int[] outS; double[] XIa, YIa;
            if (method is "nearest" or "linear" or "pchip")
            {
                if ((IsRow(xiS) && IsCol(yiS)) || (IsCol(xiS) && IsRow(yiS)))
                {
                    int nx = NumelOf(XI!), ny = NumelOf(YI!); outS = new[] { ny, nx };
                    XIa = new double[nx * ny]; YIa = new double[nx * ny];
                    for (int j = 0; j < nx; j++) for (int i = 0; i < ny; i++) { XIa[i + ny * j] = xiv[j]; YIa[i + ny * j] = yiv[i]; }
                }
                else if (!xiS.SequenceEqual(yiS)) throw new OctaveException("interp2: XI and YI must be matrices of equal size");
                else { outS = xiS; XIa = xiv.Take(NumelOf(XI!)).ToArray(); YIa = yiv.Take(NumelOf(YI!)).ToArray(); }
                int cnt = XIa.Length; var ZI = new double[cnt];
                if (method == "pchip")
                {
                    if (xs.Length < 2 || ys.Length < 2) throw new OctaveException("interp2: pchip requires at least 2 points in each dimension");
                    var Zv = new MatrixValue(zr, zc, z, null);
                    var DXv = Fc(it, "__pchip_deriv__", RowD(xs), Zv, Dn(2));
                    var DYv = Fc(it, "__pchip_deriv__", RowD(ys), Zv, Dn(1));
                    var DXY = M(Bx("/", Bx("+", Fc(it, "__pchip_deriv__", RowD(xs), DYv, Dn(2)), Fc(it, "__pchip_deriv__", RowD(ys), DXv, Dn(1))), Dn(2)), "interp2").Re;
                    var DX = M(DXv, "interp2").Re; var DY = M(DYv, "interp2").Re;
                    for (int q = 0; q < cnt; q++)
                    {
                        int xi = LookupLR(xs, XIa[q]) - 1, yi = LookupLR(ys, YIa[q]) - 1;
                        double hx = xs[xi + 1] - xs[xi], hy = ys[yi + 1] - ys[yi];
                        double tx = (XIa[q] - xs[xi]) / hx, ty = (YIa[q] - ys[yi]) / hy;
                        (double b11, double b12, double b21, double b22) Basis(double t, double h)
                        {
                            double t1 = t * t, t2 = t * t1 - t1; double b22_ = h * t2; t1 = t2 - t1; double b21_ = h * (t1 + t); t2 += t1; return (t2 + 1, -t2, b21_, b22_);
                        }
                        var (x11, x12, x21, x22) = Basis(tx, hx); var (y11, y12, y21, y22) = Basis(ty, hy);
                        double[] xb1 = { x11, x12 }, xb2 = { x21, x22 }, yb1 = { y11, y12 }, yb2 = { y21, y22 };
                        double acc = 0;
                        for (int ix = 0; ix < 2; ix++) for (int iy = 0; iy < 2; iy++)
                        {
                            int zi = (yi + iy) + zr * (xi + ix);
                            acc += xb1[ix] * yb1[iy] * z[zi]; acc += xb2[ix] * yb1[iy] * DX[zi];
                            acc += xb1[ix] * yb2[iy] * DY[zi]; acc += xb2[ix] * yb2[iy] * DXY[zi];
                        }
                        ZI[q] = acc;
                    }
                }
                else
                    for (int q = 0; q < cnt; q++)
                    {
                        int xi = LookupLR(xs, XIa[q]) - 1, yi = LookupLR(ys, YIa[q]) - 1;
                        if (method == "linear")
                        {
                            double A = z[yi + zr * xi], B = z[yi + zr * (xi + 1)] - A, C = z[yi + 1 + zr * xi] - A, Dd = z[yi + 1 + zr * (xi + 1)] - A - B - C;
                            double xsc = (XIa[q] - xs[xi]) / (xs[xi + 1] - xs[xi]), ysc = (YIa[q] - ys[yi]) / (ys[yi + 1] - ys[yi]);
                            ZI[q] = A + B * xsc + C * ysc + Dd * xsc * ysc;
                        }
                        else
                        {
                            int ii = XIa[q] - xs[xi] >= xs[xi + 1] - XIa[q] ? 1 : 0, jj = YIa[q] - ys[yi] >= ys[yi + 1] - YIa[q] ? 1 : 0;
                            ZI[q] = (double.IsNaN(XIa[q]) || double.IsNaN(YIa[q])) ? double.NaN : z[(yi + jj) + zr * (xi + ii)];
                        }
                    }
                return new[] { Finish2(ZI, outS, XIa, YIa) };
            }
            // cubic / spline
            if (IsVec(XI!) && IsVec(YI!) && !xiS.SequenceEqual(yiS))
            {
                xiS = new[] { 1, NumelOf(XI!) }; yiS = new[] { NumelOf(YI!), 1 };
            }
            else if (!xiS.SequenceEqual(yiS)) throw new OctaveException("interp2: XI and YI must be matrices of equal size");
            if (method == "spline")
            {
                double[] gx, gy;
                if (IsRow(xiS) && IsCol(yiS) && !xiS.SequenceEqual(yiS)) { gx = xiv.Take(xiS[1]).ToArray(); gy = yiv.Take(yiS[0]).ToArray(); }
                else
                {
                    int r0 = xiS[0], c0 = xiS.Length > 1 ? xiS[1] : 1;
                    bool gridX = Enumerable.Range(0, c0).All(j => Enumerable.Range(0, r0).All(i => xiv[i + r0 * j] == xiv[r0 * j]));
                    bool gridY = Enumerable.Range(0, r0).All(i => Enumerable.Range(0, c0).All(j => yiv[i + r0 * j] == yiv[i]));
                    if (!gridX || !gridY) throw new OctaveException("interp2: XI, YI must have uniform spacing ('meshgrid' format)");
                    gx = Enumerable.Range(0, c0).Select(j => xiv[r0 * j]).ToArray(); gy = Enumerable.Range(0, r0).Select(i => yiv[i]).ToArray();
                }
                var Zv = new MatrixValue(zr, zc, z, null);
                var t1 = Fc(it, "spline", RowD(xs), Zv, RowD(gx));          // zr x ngx
                var t2 = Fc(it, "spline", RowD(ys), Fc(it, "transpose", t1), RowD(gy)); // ngx x ngy
                var res = (MatrixValue)Fc(it, "transpose", t2);
                if (extrap != null)
                {
                    double ev = D(extrap, "interp2"); var rr = res.Re;
                    for (int j = 0; j < gx.Length; j++) for (int i = 0; i < gy.Length; i++)
                        if (gx[j] < xs.Min() || gx[j] > xs.Max() || gy[i] < ys.Min() || gy[i] > ys.Max()) rr[i + gy.Length * j] = ev;
                }
                return new[] { (Value)res };
            }
            {
                // cubic: padded grid (zr+3) x (zc+3)
                int er = zr + 3, ec = zc + 3; var E = new double[er * ec];
                double Zp(int i, int j) => z[i + zr * j];
                double Row1(int j) => 3 * Zp(0, j) - 3 * Zp(1, j) + Zp(2, j);
                double RowE(int j) => 3 * Zp(zr - 1, j) - 3 * Zp(zr - 2, j) + Zp(zr - 3, j);
                double Ext(Func<int, double> f) => 3 * f(0) - 3 * f(1) + f(2);
                double ExtE(Func<int, double> f) => 3 * f(zc - 1) - 3 * f(zc - 2) + f(zc - 3);
                void Put(int i, int j, double v) => E[i + er * j] = v;
                Put(0, 0, Ext(Row1)); for (int j = 0; j < zc; j++) Put(0, j + 1, Row1(j)); Put(0, zc + 1, ExtE(Row1));
                for (int i = 0; i < zr; i++)
                {
                    int ii = i; Put(i + 1, 0, Ext(j => Zp(ii, j))); for (int j = 0; j < zc; j++) Put(i + 1, j + 1, Zp(i, j)); Put(i + 1, zc + 1, ExtE(j => Zp(ii, j)));
                }
                Put(zr + 1, 0, Ext(RowE)); for (int j = 0; j < zc; j++) Put(zr + 1, j + 1, RowE(j)); Put(zr + 1, zc + 1, ExtE(RowE));
                double sx = Math.Abs(xs[0] - xs[1]), sy = Math.Abs(ys[0] - ys[1]);
                double Eval(double xv, double yv)
                {
                    int ix = LookupL(xs, xv), iy = LookupL(ys, yv);
                    double hx = (xv - xs[ix - 1]) / sx, hy = (yv - ys[iy - 1]) / sy;
                    double acc = 0;
                    for (int s2 = -1; s2 <= 2; s2++)
                    {
                        double ky = Keys(s2 - hy); if (ky == 0 && !double.IsNaN(hy)) continue;
                        double rowacc = 0;
                        for (int s1 = -1; s1 <= 2; s1++)
                        {
                            int ei = iy + s2, ej = ix + s1; // 0-based into E (1-based idx+1+shift-1)
                            rowacc += E[ei + er * ej] * Keys(s1 - hx);
                        }
                        acc += rowacc * ky;
                    }
                    return acc;
                }
                int rr0 = xiS[0], cc0 = xiS.Length > 1 ? xiS[1] : 1;
                bool grid = xiS.SequenceEqual(yiS) && Enumerable.Range(0, cc0).All(j => Enumerable.Range(0, rr0).All(i => xiv[i + rr0 * j] == xiv[rr0 * j] && yiv[i + rr0 * j] == yiv[i]));
                double[] gx, gy;
                if (IsRow(xiS) && IsCol(yiS) && !xiS.SequenceEqual(yiS)) { gx = xiv.Take(xiS[1]).ToArray(); gy = yiv.Take(yiS[0]).ToArray(); grid = true; }
                else if (grid) { gx = Enumerable.Range(0, cc0).Select(j => xiv[rr0 * j]).ToArray(); gy = Enumerable.Range(0, rr0).Select(i => yiv[i]).ToArray(); }
                else { gx = gy = Array.Empty<double>(); }
                if (grid)
                {
                    int nx = gx.Length, ny = gy.Length; var o = new double[nx * ny]; XIa = new double[nx * ny]; YIa = new double[nx * ny];
                    for (int j = 0; j < nx; j++) for (int i = 0; i < ny; i++) { o[i + ny * j] = Eval(gx[j], gy[i]); XIa[i + ny * j] = gx[j]; YIa[i + ny * j] = gy[i]; }
                    return new[] { Finish2(o, new[] { ny, nx }, XIa, YIa) };
                }
                else
                {
                    int cnt = NumelOf(XI!); var o = new double[cnt];
                    for (int q = 0; q < cnt; q++) o[q] = Eval(xiv[q], yiv[q]);
                    return new[] { Finish2(o, xiS, xiv.Take(cnt).ToArray(), yiv.Take(cnt).ToArray()) };
                }
            }
            Value Finish2(double[] zi, int[] shape, double[] xa, double[] ya)
            {
                double ev = extrap != null ? D(extrap, "interp2") : double.NaN;
                double x1 = xs[0], xe = xs[^1], y1 = ys[0], ye = ys[^1];
                double evi = extrap is MatrixValue exm && exm.Im != null ? exm.Im[0] : 0;
                double[]? im = evi != 0 ? new double[zi.Length] : null;
                for (int q = 0; q < zi.Length; q++)
                    if (xa[q] < x1 || xa[q] > xe || ya[q] < y1 || ya[q] > ye) { zi[q] = ev; if (im != null) im[q] = evi; }
                var r = new MatrixValue(1, zi.Length, zi, im); r.SetDims(shape.Length < 2 ? new[] { shape[0], 1 } : shape);
                if (Zm.IsSingle) r.CastInPlace(NumKind.Single);
                return r;
            }
        });
    }
}
