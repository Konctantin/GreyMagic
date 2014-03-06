using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace GreyMagic.OOPTests
{
    internal class Program
    {
        private static readonly IntPtr LocalPlayerNumKnownSpells = (IntPtr) 0x010C837C - 0x400000;
        private static readonly IntPtr LocalPlayerKnownSpells = (IntPtr) 0x010C8380 - 0x400000;

        private static void Main(string[] args)
        {
            Process[] procs = Process.GetProcessesByName("Wow");

            if (!procs.Any())
            {
                Console.WriteLine("You must run WoW first!");
                Console.ReadLine();
                return;
            }

            // First-run caching stuff. This can cause the perf tests to be skewed.
            int size = MarshalCache<int>.Size;
            size += MarshalCache<IntPtr>.Size;
            size += MarshalCache<MarshalStruct>.Size;
            size += MarshalCache<NonMarshalStruct>.Size;
            Console.WriteLine(size);

            var reader = new ExternalProcessReader(procs.First());

            var timer = new Stopwatch();

            // Cache these. They are somewhat slow to grab.
            SafeMemoryHandle handle = reader.ProcessHandle;
            IntPtr imgBase = reader.ImageBase;
            // Get JIT to compile our stuffs kthx
            PerfTestKnownSpells(reader);
            PerfTestKnownSpellsRPMDirect(handle, imgBase);
            PerfTestReadBytesDirect(reader);
            PerfTestMarshalStructRead(reader);
            PerfTestNonMarshalStructRead(reader);

            timer.Start();

            for (int i = 0; i < 1000000; i++)
            {
                PerfTestKnownSpells(reader);
            }

            timer.Stop();
            // This will take ~5k ticks to run. It has to go through the helper funcs for abstraction
            Console.WriteLine("PerfTestKnownSpells Took " + (timer.ElapsedTicks / 1000000f));
            timer.Reset();

            timer.Start();

            // This should be the absolute fastest. It skips all the abstraction API and uses RPM directly
            for (int i = 0; i < 1000000; i++)
                PerfTestKnownSpellsRPMDirect(handle, imgBase);

            timer.Stop();
            Console.WriteLine("PerfTestKnownSpellsRPMDirect Took " + (timer.ElapsedTicks / 1000000f));
            timer.Reset();

            timer.Start();

            // This should be only slightly slower than RPMDirect. It derefs a byte buffer directly.
            for (int i = 0; i < 1000000; i++)
                PerfTestReadBytesDirect(reader);

            timer.Stop();
            Console.WriteLine("PerfTestReadBytesDirect Took " + (timer.ElapsedTicks / 1000000f));
            timer.Reset();

            timer.Start();

            // This should be only slightly slower than RPMDirect. It derefs a byte buffer directly.
            for (int i = 0; i < 1000000; i++)
                PerfTestNonMarshalStructRead(reader);

            timer.Stop();
            Console.WriteLine("PerfTestNonMarshalStructRead Took " + (timer.ElapsedTicks / 1000000f));
            timer.Reset();

            timer.Start();

            // This should be only slightly slower than RPMDirect. It derefs a byte buffer directly.
            for (int i = 0; i < 1000000; i++)
                PerfTestMarshalStructRead(reader);

            timer.Stop();
            Console.WriteLine("PerfTestMarshalStructRead Took " + (timer.ElapsedTicks / 1000000f));
            timer.Reset();

            Console.ReadLine();
        }

        private static void PerfTestKnownSpells(ExternalProcessReader reader)
        {
            reader.Read<int>(LocalPlayerNumKnownSpells, true);
        }

        private static unsafe void PerfTestKnownSpellsRPMDirect(SafeMemoryHandle processHandle, IntPtr imgbase)
        {
            int numRead;
            int numSpells = 0;
            ReadProcessMemory(processHandle, imgbase + (int) LocalPlayerNumKnownSpells, &numSpells, 4, out numRead);
        }

        private static unsafe void PerfTestReadBytesDirect(ExternalProcessReader reader)
        {
            fixed (byte* ptr = reader.ReadBytes(LocalPlayerNumKnownSpells, MarshalCache<int>.Size, true))
            {
                int val = *(int*) ptr;
            }
        }

        private static void PerfTestNonMarshalStructRead(ExternalProcessReader reader)
        {
            reader.Read<NonMarshalStruct>(LocalPlayerNumKnownSpells, true);
        }

        private static void PerfTestMarshalStructRead(ExternalProcessReader reader)
        {
            reader.Read<MarshalStruct>(LocalPlayerNumKnownSpells, true);
        }

        [DllImport("kernel32.dll")]
        [SuppressUnmanagedCodeSecurity]
        private static extern unsafe bool ReadProcessMemory(SafeMemoryHandle hProcess, IntPtr lpBaseAddress,
            void* lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        #region Nested type: MarshalStruct

        [StructLayout(LayoutKind.Sequential)]
        private struct MarshalStruct
        {
            [MarshalAs(UnmanagedType.I4)]
            public readonly int Val1;
        }

        #endregion

        #region Nested type: NonMarshalStruct

        [StructLayout(LayoutKind.Sequential)]
        private struct NonMarshalStruct
        {
            public readonly int Val1;
        }

        #endregion
    }
}