using System;
using System.Runtime.InteropServices;

namespace OpenNCL_Lancher.Runtime
{
    internal static class OpenNclNative
    {
        // OpenNclNative.dll is produced by native/OpenNclNative (C++) project.
        // It embeds/hosts CPython and calls kernel/openncl_kernel.py directly.

        [DllImport("OpenNclNative.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int openncl_init(string appBaseDir);

        [DllImport("OpenNclNative.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr openncl_exec(string cmd);

        [DllImport("OpenNclNative.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void openncl_free(IntPtr p);

        [DllImport("OpenNclNative.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr openncl_last_error();

        internal static string? Exec(string cmd)
        {
            IntPtr p = IntPtr.Zero;
            try
            {
                p = openncl_exec(cmd);
                if (p == IntPtr.Zero) return null;
                return Marshal.PtrToStringUni(p);
            }
            finally
            {
                if (p != IntPtr.Zero) openncl_free(p);
            }
        }

        internal static string? LastError()
        {
            var p = openncl_last_error();
            if (p == IntPtr.Zero) return null;
            return Marshal.PtrToStringUni(p);
        }
    }
}

