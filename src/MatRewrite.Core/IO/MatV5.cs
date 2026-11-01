// MatRewrite - GPL-3.0-or-later
using System.IO.Compression;
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.IO;

/// <summary>MATLAB Level 5 MAT-file reader (incl. compressed v7) and writer (uncompressed).</summary>
public static class MatV5
{
    const int miINT8 = 1, miUINT8 = 2, miINT16 = 3, miUINT16 = 4, miINT32 = 5, miUINT32 = 6, miSINGLE = 7,
              miDOUBLE = 9, miINT64 = 12, miUINT64 = 13, miMATRIX = 14, miCOMPRESSED = 15, miUTF8 = 16, miUTF16 = 17, miUTF32 = 18;
    const int mxCELL = 1, mxSTRUCT = 2, mxOBJECT = 3, mxCHAR = 4, mxSPARSE = 5, mxDOUBLE = 6, mxSINGLE = 7,
              mxINT8 = 8, mxUINT8 = 9, mxINT16 = 10, mxUINT16 = 11, mxINT32 = 12, mxUINT32 = 13, mxINT64 = 14, mxUINT64 = 15;

    public static bool IsMat(byte[] head) => head.Length >= 128 && Encoding.ASCII.GetString(head, 0, 6) == "MATLAB";

    // ------------------------------------------------------------------ reading
    public static List<(string name, Value v)> Read(byte[] data)
    {
        if (!IsMat(data)) throw new OctaveException("load: not a MAT-file");
        if (data[126] != 'I' || data[127] != 'M') throw new OctaveException("load: big-endian MAT-files are not supported");
        var res = new List<(string, Value)>();
        ReadElements(data, 128, data.Length, res);
        return res;
    }

    static void ReadElements(byte[] d, int pos, int end, List<(string, Value)> res)
    {
        while (pos + 8 <= end)
        {
            var (type, n, dataPos, next) = Tag(d, pos);
            if (type == miCOMPRESSED)
            {
                using var zs = new ZLibStream(new MemoryStream(d, dataPos, n), CompressionMode.Decompress);
                var ms = new MemoryStream(); zs.CopyTo(ms);
                var u = ms.ToArray();
                ReadElements(u, 0, u.Length, res);
            }
            else if (type == miMATRIX)
            {
                var (name, v) = ReadMatrix(d, dataPos, n);
                if (v != null) res.Add((name, v));
            }
            pos = next;
        }
    }

    static (int type, int n, int dataPos, int next) Tag(byte[] d, int pos)
    {
        uint t = BitConverter.ToUInt32(d, pos);
        if ((t >> 16) != 0) // small data element
            return ((int)(t & 0xFFFF), (int)(t >> 16), pos + 4, pos + 8);
        int n = BitConverter.ToInt32(d, pos + 4);
        int type = (int)t;
        int padded = type == miCOMPRESSED ? n : (n + 7) & ~7;
        return (type, n, pos + 8, pos + 8 + padded);
    }

    static double[] Nums(byte[] d, int type, int pos, int n)
    {
        int sz = type switch { miINT8 or miUINT8 or miUTF8 => 1, miINT16 or miUINT16 or miUTF16 => 2, miINT32 or miUINT32 or miSINGLE or miUTF32 => 4, _ => 8 };
        var r = new double[n / sz];
        for (int k = 0; k < r.Length; k++)
        {
            int p = pos + k * sz;
            r[k] = type switch
            {
                miINT8 => (sbyte)d[p],
                miUINT8 or miUTF8 => d[p],
                miINT16 => BitConverter.ToInt16(d, p),
                miUINT16 or miUTF16 => BitConverter.ToUInt16(d, p),
                miINT32 => BitConverter.ToInt32(d, p),
                miUINT32 or miUTF32 => BitConverter.ToUInt32(d, p),
                miSINGLE => BitConverter.ToSingle(d, p),
                miDOUBLE => BitConverter.ToDouble(d, p),
                miINT64 => BitConverter.ToInt64(d, p),
                miUINT64 => BitConverter.ToUInt64(d, p),
                _ => throw new OctaveException($"load: unsupported MAT data type {type}")
            };
        }
        return r;
    }

    static (string name, Value? v) ReadMatrix(byte[] d, int pos, int n)
    {
        if (n == 0) return ("", MatrixValue.Empty());
        int end = pos + n;
        var (ft, fn, fp, fnext) = Tag(d, pos);
        uint flags = BitConverter.ToUInt32(d, fp);
        int cls = (int)(flags & 0xFF);
        bool complex = (flags & 0x800) != 0, logical = (flags & 0x200) != 0;
        pos = fnext;
        var (dt, dn, dp, dnext) = Tag(d, pos);
        var dims = Nums(d, dt, dp, dn).Select(x => (int)x).ToArray();
        pos = dnext;
        var (nt, nn, np, nnext) = Tag(d, pos);
        string name = Encoding.ASCII.GetString(d, np, nn);
        pos = nnext;
        int numel = NDArray.Prod(dims);
        switch (cls)
        {
            case mxCELL:
            {
                var c = new CellValue(1, numel);
                for (int k = 0; k < numel; k++)
                {
                    var (et, en, ep, enext) = Tag(d, pos);
                    c.Data[k] = ReadMatrix(d, ep, en).v ?? MatrixValue.Empty();
                    pos = enext;
                }
                c.SetDims(dims);
                return (name, c);
            }
            case mxSTRUCT:
            {
                var (lt, ln, lp, lnext) = Tag(d, pos);
                int flen = BitConverter.ToInt32(d, lp);
                pos = lnext;
                var (namT, namN, namP, namNext) = Tag(d, pos);
                int nf = flen == 0 ? 0 : namN / flen;
                var fields = new List<string>();
                for (int f = 0; f < nf; f++)
                    fields.Add(Encoding.ASCII.GetString(d, namP + f * flen, flen).TrimEnd('\0'));
                pos = namNext;
                var s = new StructValue(1, numel, fields);
                for (int k = 0; k < numel; k++)
                    foreach (var f in fields)
                    {
                        var (et, en, ep, enext) = Tag(d, pos);
                        s.Elems[k][f] = ReadMatrix(d, ep, en).v ?? MatrixValue.Empty();
                        pos = enext;
                    }
                s.SetDims(dims);
                return (name, s);
            }
            case mxCHAR:
            {
                var (rt, rn, rp, rnext) = Tag(d, pos);
                double[] re;
                if (rt == miUTF8) { var str = Encoding.UTF8.GetString(d, rp, rn); re = str.Select(ch => (double)ch).ToArray(); }
                else re = Nums(d, rt, rp, rn);
                var m = new MatrixValue(1, re.Length, re, null, NumKind.Char);
                m.SetDims(dims);
                return (name, m);
            }
            case mxSPARSE:
            case mxOBJECT:
                return (name, null); // unsupported: skipped
            default:
            {
                NumKind kind = cls switch
                {
                    mxDOUBLE => NumKind.Double, mxSINGLE => NumKind.Single, mxINT8 => NumKind.Int8, mxUINT8 => NumKind.UInt8,
                    mxINT16 => NumKind.Int16, mxUINT16 => NumKind.UInt16, mxINT32 => NumKind.Int32, mxUINT32 => NumKind.UInt32,
                    mxINT64 => NumKind.Int64, mxUINT64 => NumKind.UInt64,
                    _ => throw new OctaveException($"load: unsupported MAT class {cls}")
                };
                if (logical) kind = NumKind.Logical;
                var (rt, rn, rp, rnext) = Tag(d, pos);
                var re = Nums(d, rt, rp, rn);
                double[]? im = null;
                if (complex) { var (it, inn, ip, inext) = Tag(d, rnext); im = Nums(d, it, ip, inn); }
                var m = new MatrixValue(1, re.Length, re, im, kind);
                m.SetDims(dims);
                return (name, m);
            }
        }
    }

    // ------------------------------------------------------------------ writing
    public static byte[] Write(IEnumerable<(string name, Value v)> vars)
    {
        var ms = new MemoryStream();
        var hdr = $"MATLAB 5.0 MAT-file, Platform: MatRewrite, Created on: {DateTime.Now:ddd MMM dd HH:mm:ss yyyy}".PadRight(116);
        ms.Write(Encoding.ASCII.GetBytes(hdr), 0, 116);
        ms.Write(new byte[8]);
        ms.Write(BitConverter.GetBytes((ushort)0x0100));
        ms.Write(Encoding.ASCII.GetBytes("IM"));
        foreach (var (name, v) in vars) WriteMatrix(ms, name, v);
        return ms.ToArray();
    }

    static void Elem(Stream s, int type, byte[] data)
    {
        s.Write(BitConverter.GetBytes(type));
        s.Write(BitConverter.GetBytes(data.Length));
        s.Write(data);
        int pad = (8 - data.Length % 8) % 8;
        s.Write(new byte[pad]);
    }

    static void WriteMatrix(Stream outer, string name, Value v)
    {
        var s = new MemoryStream();
        int cls; uint flags = 0;
        var dims = v.Dims;
        switch (v)
        {
            case CellValue: cls = mxCELL; break;
            case StructValue: cls = mxSTRUCT; break;
            case MatrixValue m:
                cls = m.Kind switch
                {
                    NumKind.Char => mxCHAR, NumKind.Logical => mxUINT8, NumKind.Single => mxSINGLE,
                    NumKind.Int8 => mxINT8, NumKind.UInt8 => mxUINT8, NumKind.Int16 => mxINT16, NumKind.UInt16 => mxUINT16,
                    NumKind.Int32 => mxINT32, NumKind.UInt32 => mxUINT32, NumKind.Int64 => mxINT64, NumKind.UInt64 => mxUINT64,
                    _ => mxDOUBLE
                };
                if (m.IsLogical) flags |= 0x200;
                if (m.IsComplex) flags |= 0x800;
                break;
            default: throw new OctaveException($"save: wrong type argument '{v.ClassName}'");
        }
        var fl = new byte[8];
        BitConverter.GetBytes(flags | (uint)cls).CopyTo(fl, 0);
        Elem(s, miUINT32, fl);
        Elem(s, miINT32, dims.SelectMany(BitConverter.GetBytes).ToArray());
        Elem(s, miINT8, Encoding.ASCII.GetBytes(name));
        switch (v)
        {
            case CellValue c:
                foreach (var e in c.Data) WriteMatrix(s, "", e);
                break;
            case StructValue st:
            {
                int flen = Math.Max(st.Fields.Count == 0 ? 1 : st.Fields.Max(f => f.Length) + 1, 1);
                Elem(s, miINT32, BitConverter.GetBytes(flen));
                var names = new byte[flen * st.Fields.Count];
                for (int f = 0; f < st.Fields.Count; f++) Encoding.ASCII.GetBytes(st.Fields[f]).CopyTo(names, f * flen);
                Elem(s, miINT8, names);
                foreach (var e in st.Elems)
                    foreach (var f in st.Fields) WriteMatrix(s, "", e[f]);
                break;
            }
            case MatrixValue m:
            {
                (int type, Func<double, byte[]> enc) = m.Kind switch
                {
                    NumKind.Char => (miUINT16, (Func<double, byte[]>)(x => BitConverter.GetBytes((ushort)x))),
                    NumKind.Logical or NumKind.UInt8 => (miUINT8, x => new[] { (byte)x }),
                    NumKind.Int8 => (miINT8, x => new[] { (byte)(sbyte)x }),
                    NumKind.Int16 => (miINT16, x => BitConverter.GetBytes((short)x)),
                    NumKind.UInt16 => (miUINT16, x => BitConverter.GetBytes((ushort)x)),
                    NumKind.Int32 => (miINT32, x => BitConverter.GetBytes((int)x)),
                    NumKind.UInt32 => (miUINT32, x => BitConverter.GetBytes((uint)x)),
                    NumKind.Int64 => (miINT64, x => BitConverter.GetBytes((long)x)),
                    NumKind.UInt64 => (miUINT64, x => BitConverter.GetBytes((ulong)x)),
                    NumKind.Single => (miSINGLE, x => BitConverter.GetBytes((float)x)),
                    _ => (miDOUBLE, x => BitConverter.GetBytes(x))
                };
                Elem(s, type, m.Re.SelectMany(enc).ToArray());
                if (m.IsComplex) Elem(s, type, m.Im!.SelectMany(enc).ToArray());
                break;
            }
        }
        Elem(outer, miMATRIX, s.ToArray());
    }
}
