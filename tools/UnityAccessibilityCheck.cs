using System;
using System.Runtime.InteropServices;

// Native acceptance helper. Reads accessibility values; restoration writes only
// the shortcut/confirmation bits still matching the lease's applied state.
public static class UnityAccessibilityCheck
{
    static readonly uint[] Gets = { 0x3A, 0x32, 0x34 };
    static readonly uint[] Sets = { 0x3B, 0x33, 0x35 };
    static readonly int[] Sizes = { 2, 6, 2 };
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint size, [In, Out] uint[] values, uint flags);
    public static uint[][] Read()
    {
        var values = new uint[Gets.Length][];
        for (int i = 0; i < Gets.Length; i++)
        {
            values[i] = new uint[Sizes[i]];
            values[i][0] = (uint)(Sizes[i] * 4);
            if (!SystemParametersInfo(Gets[i], values[i][0], values[i], 0))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        return values;
    }
    public static bool Equal(uint[][] a, uint[][] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Length != b[i].Length) return false;
            for (int j = 0; j < a[i].Length; j++) if (a[i][j] != b[i][j]) return false;
        }
        return true;
    }
    public static bool OnlyOwnedBitsApplied(uint[][] before, uint[][] during)
    {
        for (int i = 0; i < before.Length; i++)
            for (int j = 0; j < before[i].Length; j++)
                if (during[i][j] != (j == 1 ? before[i][j] & ~12u : before[i][j])) return false;
        return true;
    }
    public static void RestoreOwned(uint[][] before)
    {
        var current = Read();
        Exception failure = null;
        for (int i = 0; i < current.Length; i++)
        {
            if ((current[i][1] & 12u) != 0 || (before[i][1] & 12u) == 0) continue;
            current[i][1] = (current[i][1] & ~12u) | (before[i][1] & 12u);
            if (!SystemParametersInfo(Sets[i], current[i][0], current[i], 0))
                failure = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        if (failure != null) throw failure;
    }
}
