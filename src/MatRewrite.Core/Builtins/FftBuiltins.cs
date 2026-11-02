// MatOct - GPL-3.0-or-later
// fft / ifft / fft2 / ifft2 (any length, via MathNet Bluestein for non powers of two).
using System.Numerics;
using MatRewrite.Core.Runtime;
using MathNet.Numerics.IntegralTransforms;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static MatrixValue FftAlong(MatrixValue x, int nfft, int dim, bool inverse)
    {
        int R = x.R, C = x.C;
        int len = dim == 1 ? R : C, other = dim == 1 ? C : R;
        if (nfft < 0) nfft = len;
        int oR = dim == 1 ? nfft : R, oC = dim == 1 ? C : nfft;
        var re = new double[oR * oC]; var im = new double[oR * oC];
        var buf = new Complex[nfft];
        for (int o = 0; o < other; o++)
        {
            Array.Clear(buf);
            for (int k = 0; k < Math.Min(len, nfft); k++)
            {
                int idx = dim == 1 ? o * R + k : k * R + o;
                buf[k] = new Complex(x.Re[idx], x.Im?[idx] ?? 0);
            }
            if (nfft > 0)
            {
                if (inverse) Fourier.Inverse(buf, FourierOptions.Matlab); else Fourier.Forward(buf, FourierOptions.Matlab);
            }
            for (int k = 0; k < nfft; k++)
            {
                int idx = dim == 1 ? o * oR + k : k * oR + o;
                re[idx] = buf[k].Real; im[idx] = buf[k].Imaginary;
            }
        }
        return new MatrixValue(oR, oC, re, im);
    }

    static Value[] FftCmd(Value[] a, string fn, bool inverse)
    {
        if (a.Length < 1 || a.Length > 4) throw new OctaveException($"Invalid call to {fn}");
        var x = M(a[0], fn);
        if (x.IsChar) x = MatrixValue.Row(x.Re.ToArray());
        int nfft = -1;
        if (a.Length > 1 && M(a[1], fn).Numel > 0)
        {
            var nv = D(a[1], fn);
            if (nv < 1 || nv != Math.Floor(nv)) throw new OctaveException($"{fn}: number of points N must be greater than zero");
            nfft = (int)nv;
        }
        int dim = a.Length > 2 ? I(a[2], fn) : (x.R == 1 ? 2 : 1);
        if (dim < 1) throw new OctaveException($"{fn}: DIM must be a valid dimension along which to perform FFT");
        bool symmetric = inverse && a.Length > 0 && a[^1] is MatrixValue sm && sm.IsChar && sm.AsString() == "symmetric";
        if (dim > 2) return Out(x);
        if (x.Numel == 0 && nfft < 0) return Out(x);
        var r = FftAlong(x, nfft, dim, inverse);
        if (symmetric) r.Im = null;
        else if (r.Im != null && r.Im.All(v => v == 0) && inverse && !x.IsComplex) r.Im = null;
        return Out(r);
    }

    static void RegisterFft()
    {
        Reg("fft", (it, a, n) => FftCmd(a.Where(v => !(v is MatrixValue m && m.IsChar && m.R == 1 && a[0] != v)).ToArray(), "fft", false));
        Reg("ifft", (it, a, n) =>
        {
            bool sym = a.Length > 1 && a[^1] is MatrixValue s && s.IsChar && s.AsString() is "symmetric";
            var args = a.Where((v, i) => i == 0 || !(v is MatrixValue m && m.IsChar)).ToArray();
            var r = (MatrixValue)FftCmd(args, "ifft", true)[0];
            if (sym) r.Im = null;
            return Out(r);
        });
        BuiltinFn two(bool inv, string fn) => (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException($"Invalid call to {fn}");
            var x = M(a[0], fn);
            int m = a.Length > 1 ? I(a[1], fn) : -1, k = a.Length > 2 ? I(a[2], fn) : -1;
            var r = FftAlong(FftAlong(x, m, 1, inv), k, 2, inv);
            return Out(r);
        };
        Reg("fft2", two(false, "fft2"));
        Reg("ifft2", two(true, "ifft2"));
    }
}
