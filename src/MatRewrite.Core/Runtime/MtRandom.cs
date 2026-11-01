// MatRewrite - GPL-3.0-or-later
// MT19937 Mersenne Twister with exportable state (625 values: 624 words + index), as Octave's rand("state").
namespace MatRewrite.Core.Runtime;

public sealed class MtRandom : Random
{
    readonly uint[] mt = new uint[624]; int mti;
    public MtRandom() { Seed((uint)Environment.TickCount ^ (uint)Guid.NewGuid().GetHashCode()); }
    public MtRandom(uint seed) { Seed(seed); }
    public void Seed(uint s)
    {
        mt[0] = s;
        for (mti = 1; mti < 624; mti++) mt[mti] = 1812433253u * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti;
    }
    public void SeedArray(uint[] key)
    {
        Seed(19650218u); int i = 1, j = 0;
        for (int k = Math.Max(624, key.Length); k > 0; k--)
        {
            mt[i] = (mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525u)) + key[j] + (uint)j; i++; j++;
            if (i >= 624) { mt[0] = mt[623]; i = 1; }
            if (j >= key.Length) j = 0;
        }
        for (int k = 623; k > 0; k--)
        {
            mt[i] = (mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1566083941u)) - (uint)i; i++;
            if (i >= 624) { mt[0] = mt[623]; i = 1; }
        }
        mt[0] = 0x80000000u; mti = 624;
    }
    public double[] GetState() { var r = new double[625]; for (int k = 0; k < 624; k++) r[k] = mt[k]; r[624] = mti; return r; }
    public void SetState(double[] s)
    {
        if (s.Length == 625) { for (int k = 0; k < 624; k++) mt[k] = (uint)(long)s[k]; mti = Math.Clamp((int)s[624], 0, 624); return; }
        SeedArray(s.Select(v => (uint)(long)Math.Abs(v)).DefaultIfEmpty(0u).ToArray());
    }
    uint NextU()
    {
        if (mti >= 624)
        {
            for (int k = 0; k < 624; k++)
            {
                uint y = (mt[k] & 0x80000000u) | (mt[(k + 1) % 624] & 0x7fffffffu);
                mt[k] = mt[(k + 397) % 624] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfu : 0u);
            }
            mti = 0;
        }
        uint z = mt[mti++];
        z ^= z >> 11; z ^= (z << 7) & 0x9d2c5680u; z ^= (z << 15) & 0xefc60000u; z ^= z >> 18;
        return z;
    }
    protected override double Sample()
    {
        double r;
        do { uint a = NextU() >> 5, b = NextU() >> 6; r = (a * 67108864.0 + b) / 9007199254740992.0; } while (r == 0);
        return r;
    }
    public override double NextDouble() => Sample();
    public override int Next() => (int)(NextU() >> 1);
    public override int Next(int maxValue) => maxValue <= 0 ? 0 : (int)(Sample() * maxValue);
    public override int Next(int minValue, int maxValue) => minValue + Next(maxValue - minValue);
}
