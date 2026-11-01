// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Runtime;

/// <summary>containers.Map: a handle object (reference semantics) with sorted char or numeric keys.</summary>
public sealed class MapValue : Value
{
    public readonly bool NumericKeys;
    public readonly string KeyType, ValueType;
    public readonly SortedDictionary<string, Value> StrMap = new(StringComparer.Ordinal);
    public readonly SortedDictionary<double, Value> NumMap = new();

    public MapValue(string keyType = "char", string valueType = "any")
    {
        KeyType = keyType; ValueType = valueType;
        NumericKeys = keyType != "char";
        if (!(keyType is "char" or "double" or "single" or "int32" or "uint32" or "int64" or "uint64"))
            throw new OctaveException($"containers.Map: unsupported KeyType");
        if (!(valueType is "any" or "char" or "logical" or "double" or "single" or "int8" or "uint8" or "int16" or "uint16" or "int32" or "uint32" or "int64" or "uint64"))
            throw new OctaveException($"containers.Map: unsupported ValueType");
    }

    public int Count => NumericKeys ? NumMap.Count : StrMap.Count;
    public override int Rows => Count;
    public override int Cols => 1;
    public override string ClassName => "containers.Map";

    object Key(Value k)
    {
        if (NumericKeys)
        {
            if (k is MatrixValue m && !m.IsChar && m.Numel == 1) return m.Re[0];
            if (k is MatrixValue) throw new OctaveException("specified key type does not match the type of this container");
            throw new OctaveException("specified key type does not match the type of this container");
        }
        if (k is MatrixValue s && (s.IsChar || s.Numel == 0)) return new string(s.Re.Select(ch => (char)ch).ToArray());
        throw new OctaveException("specified key type does not match the type of this container");
    }

    public bool Has(Value k)
    {
        if (NumericKeys ? !(k is MatrixValue m && !m.IsChar && m.Numel == 1) : !(k is MatrixValue c && (c.IsChar || c.Numel == 0))) return false;
        return Key(k) is string s ? StrMap.ContainsKey(s) : NumMap.ContainsKey((double)Key(k));
    }

    public Value Get(Value k)
    {
        var key = Key(k);
        if (key is string s) return StrMap.TryGetValue(s, out var v) ? v : throw new OctaveException($"the given key is not present in the container: specified key <{s}> does not exist", "Octave:index-out-of-bounds");
        return NumMap.TryGetValue((double)key, out var nv) ? nv : throw new OctaveException($"the given key is not present in the container: specified key <{((double)key).ToString("G5", System.Globalization.CultureInfo.InvariantCulture)}> does not exist", "Octave:index-out-of-bounds");
    }

    public void Set(Value k, Value v)
    {
        var key = Key(k);
        if (ValueType is "double" or "single" or "logical" && !(v is MatrixValue vm && vm.Numel == 1 && !vm.IsChar))
            throw new OctaveException("specified value type does not match the type of this container");
        if (ValueType == "char" && !(v is MatrixValue cm && cm.IsChar))
            throw new OctaveException("specified value type does not match the type of this container");
        if (key is string s) StrMap[s] = v; else NumMap[(double)key] = v;
    }

    /// <summary>Constructor insert: no value-type check (the type was inferred from these values).</summary>
    public void SetRaw(Value k, Value v)
    {
        var key = Key(k);
        if (key is string s) StrMap[s] = v; else NumMap[(double)key] = v;
    }

    public static MapValue Merge(List<Value> items)
    {
        var maps = items.Where(v => !(v is MatrixValue em && em.Numel == 0)).Select(v => v as MapValue ?? throw new OctaveException("concatenation operator not implemented for 'containers.Map' by 'matrix' operations")).ToList();
        var first = maps[0];
        if (maps.Any(m => m.NumericKeys != first.NumericKeys)) throw new OctaveException("containers.Map: cannot concatenate maps with different key types");
        var vt = maps.All(m => m.ValueType == first.ValueType) ? first.ValueType : "any";
        var r = new MapValue(first.KeyType, vt);
        foreach (var m in maps)
        {
            foreach (var kv in m.StrMap) r.StrMap[kv.Key] = kv.Value;
            foreach (var kv in m.NumMap) r.NumMap[kv.Key] = kv.Value;
        }
        return r;
    }

    public void Remove(Value k)
    {
        foreach (var one in Expand(k))
        {
            var key = Key(one);
            bool ok = key is string s ? StrMap.Remove(s) : NumMap.Remove((double)key);
            if (!ok) throw new OctaveException("the given key is not present in the container");
        }
    }

    /// <summary>A cell of keys or a numeric vector becomes a list of single keys.</summary>
    public IEnumerable<Value> Expand(Value k)
    {
        if (k is CellValue c) return c.Data;
        if (NumericKeys && k is MatrixValue m && !m.IsChar && m.Numel != 1) return m.Re.Select(x => (Value)MatrixValue.Scalar(x));
        return new[] { k };
    }

    public CellValue Keys() => NumericKeys
        ? CellValue.RowOf(NumMap.Keys.Select(x => { var sv = MatrixValue.Scalar(x); var kk = NumTypes.FromName(KeyType); if (kk != null && kk != NumKind.Double) sv.CastInPlace(kk.Value); return (Value)sv; }))
        : CellValue.RowOf(StrMap.Keys.Select(x => (Value)MatrixValue.Str(x)));

    public CellValue Values() => NumericKeys ? CellValue.RowOf(NumMap.Values) : CellValue.RowOf(StrMap.Values);

    public CellValue Values(Value keys)
    {
        if (keys is CellValue kc) { var r = new CellValue(kc.R, kc.C, kc.Data.Select(Get).ToArray()); if (kc.ND != null) r.SetDims(kc.Dims); return r; }
        return CellValue.RowOf(Expand(keys).Select(Get));
    }
}
