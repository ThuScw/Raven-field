using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RavenfieldAIEnhancement
{
    /// <summary>
    /// Method replacement utility using JMP hooking
    /// This replaces the native code of a method to point to another method
    /// </summary>
    public static class MethodReplacer
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        const uint PAGE_EXECUTE_READWRITE = 0x40;

        /// <summary>
        /// Replace source method with target method
        /// </summary>
        public static void Replace(MethodInfo source, MethodInfo target)
        {
            // Get method handles
            RuntimeMethodHandle sourceHandle = source.MethodHandle;
            RuntimeMethodHandle targetHandle = target.MethodHandle;

            // Prepare methods
            RuntimeHelpers.PrepareMethod(sourceHandle);
            RuntimeHelpers.PrepareMethod(targetHandle);

            // Get function pointers
            IntPtr sourcePtr = sourceHandle.GetFunctionPointer();
            IntPtr targetPtr = targetHandle.GetFunctionPointer();

            // Make memory writable
            VirtualProtect(sourcePtr, (UIntPtr)12, PAGE_EXECUTE_READWRITE, out uint oldProtect);

            // Write JMP instruction (x86: E9 relative offset, x64: more complex)
            // For x86: E9 + 4-byte relative offset
            // For x64: 48 B8 + 8-byte absolute address + FF E0
            if (IntPtr.Size == 4) // x86
            {
                byte[] jmpInstruction = new byte[5];
                jmpInstruction[0] = 0xE9; // JMP rel32
                int offset = (int)(targetPtr.ToInt32() - sourcePtr.ToInt32() - 5);
                BitConverter.GetBytes(offset).CopyTo(jmpInstruction, 1);
                Marshal.Copy(jmpInstruction, 0, sourcePtr, 5);
            }
            else // x64
            {
                byte[] jmpInstruction = new byte[12];
                jmpInstruction[0] = 0x48; // REX.W
                jmpInstruction[1] = 0xB8; // MOV RAX, imm64
                BitConverter.GetBytes(targetPtr.ToInt64()).CopyTo(jmpInstruction, 2);
                jmpInstruction[10] = 0xFF; // JMP RAX
                jmpInstruction[11] = 0xE0;
                Marshal.Copy(jmpInstruction, 0, sourcePtr, 12);
            }
        }
    }
}
