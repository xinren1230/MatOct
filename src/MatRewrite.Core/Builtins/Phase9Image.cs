// MatRewrite - GPL-3.0-or-later
// Phase 9: image class conversions (native ports of Octave's im2double, rgb2gray, gray2ind, ind2rgb, ind2gray).
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static string ClassOf(Interpreter it, Value v) => S(Fc(it, "class", v), "class");

    static void RegisterPhase9Image()
    {
        Reg("im2double", (it, a, n) =>
        {
            if (a.Length < 1) Usage("im2double");
            if (a.Length > 2) TooMany("im2double");
            var img = a[0]; string cls = ClassOf(it, img);
            if (a.Length == 1)
            {
                Value dbl = Fc(it, "double", img);
                return Out(cls switch
                {
                    "uint8" => Bx("/", dbl, Dn(255)),
                    "uint16" => Bx("/", dbl, Dn(65535)),
                    "int16" => Bx("/", Bx("+", dbl, Dn(32768)), Dn(65535)),
                    "single" or "logical" => dbl,
                    "double" => img,
                    _ => throw new OctaveException($"im2double: IMG is of unsupported class \"{cls}\"")
                });
            }
            if (!(IsString(a[1]) && S(a[1], "im2double").Equals("indexed", StringComparison.OrdinalIgnoreCase)))
                throw new OctaveException("im2double: second input argument must be the string \"indexed\"");
            if (cls is "uint8" or "uint16") return Out(Bx("+", Fc(it, "double", img), Dn(1)));
            if (cls is "double" or "single" or "logical") return Out(Fc(it, "double", img));
            throw new OctaveException("im2double: if IMG is indexed, then it must be positive integer floating points, or unsigned integer class");
        });

        Reg("rgb2gray", (it, a, n) =>
        {
            if (a.Length < 1) Usage("rgb2gray");
            if (a.Length > 1) TooMany("rgb2gray");
            var x = a[0]; string cls = ClassOf(it, x); bool isInt = x is MatrixValue xm && xm.IsInteger;
            bool iscm = Fc(it, "iscolormap", x) is MatrixValue c && c.Re[0] != 0;
            var sz = SizeOf(x); bool isNd = false;
            if (!iscm)
            {
                if (!(cls is "uint8" or "int8" or "int16" or "uint16" or "single" or "double"))
                    throw new OctaveException($"rgb2gray: RGB of invalid data type '{cls}'");
                if ((sz.Length > 2 ? sz[2] : 1) != 3) throw new OctaveException("rgb2gray: RGB must be a colormap or RGB image");
                if (x is MatrixValue x0 && x0.Im != null) throw new OctaveException("rgb2gray: RGB must be numeric and real");
                if (sz.Length == 4) { isNd = true; x = Fc(it, "permute", x, RowD(new double[] { 1, 2, 4, 3 })); }
                else if (sz.Length > 4) throw new OctaveException("rgb2gray: invalid RGB with more than 4 dimensions");
            }
            var m = M(x, "rgb2gray"); int N = m.Numel / 3; double lo = 0, sc = 1;
            if (isInt)
            {
                double imax = D(Fc(it, "intmax", Str(cls)), "rgb2gray"), imin = D(Fc(it, "intmin", Str(cls)), "rgb2gray");
                if (imin < 0) { lo = imin; sc = imax - imin; } else sc = imax;
            }
            var o = new MatrixValue(N, 1);
            for (int i = 0; i < N; i++)
                o.Re[i] = (m.Re[i] - lo) / sc * 0.298936 + (m.Re[i + N] - lo) / sc * 0.587043 + (m.Re[i + 2 * N] - lo) / sc * 0.114021;
            Value I = o;
            if (!iscm)
            {
                if (isNd) I = Fc(it, "permute", Fc(it, "reshape", o, RowD(new double[] { sz[0], sz[1], sz[3], 1 })), RowD(new double[] { 1, 2, 4, 3 }));
                else { var s2 = (int[])sz.Clone(); s2[2] = 1; I = Fc(it, "reshape", o, RowD(s2.Select(d => (double)d))); }
            }
            if (isInt)
            {
                if (cls.EndsWith("8")) { I = Bx("*", I, Dn(255)); if (cls[0] == 'i') I = Bx("-", I, Dn(128)); }
                else { I = Bx("*", I, Dn(65535)); if (cls[0] == 'i') I = Bx("-", I, Dn(32768)); }
                I = Fc(it, cls, I);
            }
            else if (cls == "single") I = Fc(it, "single", I);
            return Out(I);
        });

        Reg("gray2ind", (it, a, nout) =>
        {
            if (a.Length < 1) Usage("gray2ind");
            if (a.Length > 2) TooMany("gray2ind");
            var I = a[0];
            if (I is not MatrixValue im || im.Im != null || im.Dims.Length > 2) throw new OctaveException("gray2ind: I must be a grayscale or binary image");
            double n = 64;
            if (a.Length > 1) { if (!IsScal(a[1])) throw new OctaveException("gray2ind: N must be a positive integer in the range [1, 65536]"); n = D(a[1], "gray2ind"); }
            if (n < 1 || n > 65536) throw new OctaveException("gray2ind: N must be a positive integer in the range [1, 65536]");
            if (a.Length == 1 && im.Kind == NumKind.Logical) n = 2;
            string cls = ClassOf(it, I);
            if (!(cls is "logical" or "uint8" or "uint16" or "int16" or "single" or "double")) throw new OctaveException($"gray2ind: invalid data type '{cls}'");
            bool isF = cls is "single" or "double";
            if (isF && im.Re.Any(v => v < 0 || v > 1)) throw new OctaveException("gray2ind: floating point images may only contain values between 0 and 1");
            var map = Fc(it, "gray", Dn(n));
            double low = 0, scale = 1;
            if (im.IsInteger) { low = D(Fc(it, "intmin", Str(cls)), "gray2ind"); scale = D(Fc(it, "intmax", Str(cls)), "gray2ind") - low; }
            var o = new MatrixValue(im.R, im.C);
            for (int k = 0; k < o.Numel; k++) o.Re[k] = (im.Re[k] - low) * (n - 1) / scale;
            Value res = Fc(it, n <= 256 ? "uint8" : "uint16", o);
            return Out(res, map);
        });

        Reg("ind2rgb", (it, a, nout) =>
        {
            if (a.Length != 2) Usage("ind2rgb");
            var (x, map, sz) = Ind2X(it, "ind2rgb", a[0], a[1]);
            int N = x.Length, nc = map.R; var szV = RowD(sz.Select(d => (double)d));
            MatrixValue Ch(int c) { var m = new MatrixValue(N, 1); for (int k = 0; k < N; k++) m.Re[k] = map.Re[x[k] - 1 + nc * c]; return m; }
            Value R = Fc(it, "reshape", Ch(0), szV), G = Fc(it, "reshape", Ch(1), szV), B = Fc(it, "reshape", Ch(2), szV);
            if (nout <= 1)
            {
                var all = new MatrixValue(3 * N, 1);
                for (int c = 0; c < 3; c++) { var ch = Ch(c); Array.Copy(ch.Re, 0, all.Re, c * N, N); }
                if (sz.Length == 2) R = Fc(it, "reshape", all, RowD(new double[] { sz[0], sz[1], 3 }));
                else R = Fc(it, "permute", Fc(it, "reshape", all, RowD(new double[] { sz[0], sz[1], sz[3], 3 })), RowD(new double[] { 1, 2, 4, 3 }));
                return Out(R);
            }
            return Out(R, G, B);
        });

        Reg("ind2gray", (it, a, nout) =>
        {
            if (a.Length != 2) Usage("ind2gray");
            string cls = ClassOf(it, a[0]);
            var (x, map, sz) = Ind2X(it, "ind2gray", a[0], a[1]);
            int nc = map.R; var g = new double[nc];
            for (int i = 0; i < nc; i++) g[i] = map.Re[i] * 0.29894 + map.Re[i + nc] * 0.58704 + map.Re[i + 2 * nc] * 0.11402;
            var o = new MatrixValue(x.Length, 1);
            bool isInt = a[0] is MatrixValue xm && xm.IsInteger;
            double mul = isInt ? D(Fc(it, "intmax", Str(cls)), "ind2gray") : 1;
            for (int k = 0; k < x.Length; k++) o.Re[k] = g[x[k] - 1] * mul;
            Value I = Fc(it, "reshape", o, RowD(sz.Select(d => (double)d)));
            if (isInt) I = Fc(it, cls, I); else if (cls == "single") I = Fc(it, "single", I);
            return Out(I);
        });
    }

    static (int[] x, MatrixValue map, int[] sz) Ind2X(Interpreter it, string caller, Value xv, Value mapv)
    {
        var sz = SizeOf(xv);
        if (xv is not MatrixValue xm || !(sz.Length == 2 || sz.Length == 4) || (sz.Length > 2 && sz[2] != 1) || xm.Im != null || xm.IsChar)
            throw new OctaveException($"{caller}: X must be an indexed image");
        bool isF = xm.Kind is NumKind.Double or NumKind.Single, isInt = xm.IsInteger, warn = false;
        string cls = ClassOf(it, xv);
        if (!(isF && xm.Re.All(v => double.IsNaN(v) || double.IsInfinity(v) || v == Math.Floor(v)) || isInt && D(Fc(it, "intmin", Str(cls)), caller) == 0))
            throw new OctaveException($"{caller}: X must be an indexed image");
        if (!(Fc(it, "iscolormap", mapv) is MatrixValue c && c.Re[0] != 0)) throw new OctaveException($"{caller}: MAP must be a valid colormap");
        var map = (MatrixValue)mapv; int nc = map.R;
        var x = new int[xm.Numel]; double maxidx = 0;
        for (int k = 0; k < x.Length; k++)
        {
            double v = xm.Re[k];
            if (isF) { if (double.IsNaN(v)) { warn = true; v = double.PositiveInfinity; } if (v < 1) { warn = true; v = 1; } }
            else v += 1;
            if (v > nc) { warn = true; v = nc; }
            x[k] = (int)v; maxidx = Math.Max(maxidx, v);
        }
        if (warn) it.Warning($"{caller}: indexed image contains colors outside of colormap", $"Octave:{caller}:invalid-idx-img");
        return (x, map, sz);
    }
}
