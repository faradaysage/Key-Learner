// Compiler-only support for records while targeting Unity's .NET Standard 2.1 API.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

// System.Numerics.BitOperations is not part of .NET Standard 2.1. These bounded
// integer counts preserve the 256-bit physical ledger and quantity masks.
namespace System.Numerics
{
    internal static class BitOperations
    {
        internal static int PopCount(uint value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
        internal static int PopCount(ulong value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
    }
}
