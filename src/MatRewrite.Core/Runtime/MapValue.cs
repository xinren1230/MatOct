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
            throw new OctaveException($"Map: unsupported KeyType '{keyType}'");
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
            throw new OctaveException("specified key type does not match the type of this container");
        }
        if (k is MatrixValue s && (s.IsChar || s.Numel == 0)) return s.AsString();
        throw new OctaveException("specified key type does not match the type of this container");
    }

    public bool Has(Value k) => Key(k) is string s ? StrMap.ContainsKey(s) : NumMap.ContainsKey((double)Key(k));

    public Value Get(Value k)
    {
        var key = Key(k);
        if (key is string s) return StrMap.TryGetValue(s, out var v) ? v : throw new OctaveException($"the given key is not present in the container", "Octave:index-out-of-bounds");
        return NumMap.TryGetValue((double)key, out var nv) ? nv : throw new OctaveException($"the given key is not present in the container", "Octave:index-out-of-bounds");
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
        ? CellValue.RowOf(NumMap.Keys.Select(x => (Value)MatrixValue.Scalar(x)))
        : CellValue.RowOf(StrMap.Keys.Select(x => (Value)MatrixValue.Str(x)));

    public CellValue Values() => NumericKeys ? CellValue.RowOf(NumMap.Values) : CellValue.RowOf(StrMap.Values);

    public CellValue Values(Value keys) => CellValue.RowOf(Expand(keys).Select(Get));
}
