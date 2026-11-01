// MatRewrite - GPL-3.0-or-later
using System.Text;

namespace MatRewrite.Core.Runtime;

/// <summary>An open file in the fopen/fclose table.</summary>
public sealed class OctFile
{
    public required string Name;
    public required string Mode;
    public required FileStream Stream;

    public bool AtEof => Stream.Position >= Stream.Length;

    public void Write(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        if (Mode.StartsWith("a")) Stream.Seek(0, SeekOrigin.End);
        Stream.Write(b, 0, b.Length);
    }

    /// <summary>Read one line; null at EOF. keepNewline keeps the terminator.</summary>
    public string? ReadLine(bool keepNewline)
    {
        if (AtEof) return null;
        var bytes = new List<byte>();
        int c;
        while ((c = Stream.ReadByte()) >= 0)
        {
            if (c == '\n') { if (keepNewline) bytes.Add((byte)c); break; }
            bytes.Add((byte)c);
        }
        var s = Encoding.UTF8.GetString(bytes.ToArray());
        if (!keepNewline && s.EndsWith('\r')) s = s[..^1];
        return s;
    }

    public string ReadRest()
    {
        var ms = new MemoryStream();
        Stream.CopyTo(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}

public sealed partial class Interpreter
{
    public readonly Dictionary<int, OctFile> Files = new();
    int nextFid = 3;

    public int OpenFile(string name, string mode)
    {
        var m = mode.Replace("t", "").Replace("b", "");
        if (m == "") m = "r";
        FileMode fm; FileAccess fa;
        switch (m)
        {
            case "r": fm = FileMode.Open; fa = FileAccess.Read; break;
            case "r+": fm = FileMode.Open; fa = FileAccess.ReadWrite; break;
            case "w": fm = FileMode.Create; fa = FileAccess.Write; break;
            case "w+": fm = FileMode.Create; fa = FileAccess.ReadWrite; break;
            case "a": fm = FileMode.Append; fa = FileAccess.Write; break;
            case "a+": fm = FileMode.OpenOrCreate; fa = FileAccess.ReadWrite; break;
            default: throw new OctaveException($"fopen: invalid mode specified");
        }
        var fs = new FileStream(name, fm, fa, FileShare.ReadWrite);
        int fid = nextFid++;
        Files[fid] = new OctFile { Name = name, Mode = m, Stream = fs };
        return fid;
    }

    public OctFile GetFile(Value v, string fn)
    {
        if (v is MatrixValue m && m.Numel == 1 && !m.IsChar && Files.TryGetValue((int)m.Re[0], out var f)) return f;
        throw new OctaveException($"{fn}: invalid stream number = {(v is MatrixValue mm && mm.Numel == 1 ? mm.Re[0].ToString() : "?")}");
    }

    public void CloseAllFiles()
    {
        foreach (var f in Files.Values) f.Stream.Dispose();
        Files.Clear();
    }
}
