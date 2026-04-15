using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HkVoiceMod.Recognition.Sherpa
{
    internal static class SherpaNativeLoader
    {
        private static readonly object Sync = new object();
        private static readonly string[] NativeLibraryNames =
        {
            "onnxruntime.dll",
            "sherpa-onnx-c-api.dll"
        };

        private static bool _loaded;

        public static void EnsureLoaded(string assemblyDirectory, Action<string> logInfo, Action<string> logWarn, Action<string> logError)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            if (_loaded)
            {
                return;
            }

            lock (Sync)
            {
                if (_loaded)
                {
                    return;
                }

                var nativeDirectory = ResolveNativeDirectory(assemblyDirectory);

                foreach (var libraryName in NativeLibraryNames)
                {
                    LoadLibraryOrThrow(nativeDirectory, libraryName, logInfo);
                }

                _loaded = true;
            }
        }

        private static string ResolveNativeDirectory(string assemblyDirectory)
        {
            var architecture = Environment.Is64BitProcess ? "win-x64" : "win-x86";
            var candidateDirectories = new[]
            {
                Path.Combine(assemblyDirectory, "native", architecture),
                Path.Combine(assemblyDirectory, "runtimes", architecture, "native"),
                Path.Combine(assemblyDirectory, "native"),
                assemblyDirectory
            };

            foreach (var candidate in candidateDirectories)
            {
                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                if (NativeLibraryNames.All(libraryName => File.Exists(Path.Combine(candidate, libraryName))))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException(
                $"Could not find Sherpa native libraries for {(Environment.Is64BitProcess ? "x64" : "x86")} under '{assemblyDirectory}'. Checked: {string.Join(", ", candidateDirectories)}");
        }

        private static void LoadLibraryOrThrow(string nativeDirectory, string libraryName, Action<string> logInfo)
        {
            var fullPath = Path.Combine(nativeDirectory, libraryName);
            var handle = NativeMethods.LoadLibrary(fullPath);
            if (handle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Failed to load native library '{fullPath}' (Win32Error={error}).");
            }

            logInfo($"Preloaded native library: {fullPath}");
        }

        private static class NativeMethods
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);
        }
    }
}
