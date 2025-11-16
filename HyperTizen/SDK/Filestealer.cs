using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperTizen.Helper;

namespace HyperTizen.SDK
{
    /// <summary>
    /// Credits to Leonardo Rodrigues for this way to download Tizen Operating System files to USB
    /// Tested on Tizen 8+. Now supports Tizen 9.
    /// This tool recursively scans /usr/bin and /usr/lib and copies OS files to USB for research purposes.
    /// </summary>
    public static class Filestealer
    {
        private static int _filesScanned = 0;
        private static int _filesCopied = 0;
        private static int _filesBlocked = 0;
        private static int _symlinks = 0;
        private static readonly string[] _scanPaths = new[] { "/usr/bin", "/usr/lib" };

        private static void ScanDirectory(string dir, [NotNull] Action<string, byte[]> action)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    _filesScanned++;
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);

                        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            _symlinks++;
                            action(file + ".symlink", null);
                            continue;
                        }

                        action(file, File.ReadAllBytes(file));
                        _filesCopied++;
                    }
                    catch
                    {
                        _filesBlocked++;
                        action(file + ".blocked", null);
                    }
                }

                foreach (string subDir in Directory.EnumerateDirectories(dir))
                {
                    DirectoryInfo fileInfo = new DirectoryInfo(subDir);

                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    ScanDirectory(subDir, action);
                }
            }
            catch { }
        }
        /// <summary>
        /// Copy Tizen OS files from /usr/bin and /usr/lib to USB drive asynchronously
        /// Returns a Task that completes when the operation is finished
        /// </summary>
        public static async Task CopyToUsbAsync()
        {
            // Reset counters
            _filesScanned = 0;
            _filesCopied = 0;
            _filesBlocked = 0;
            _symlinks = 0;

            await Task.Run(() =>
            {
                try
                {
                    Log.Write(eLogType.Info, "=== FILESTEALER: Starting USB copy operation ===");
                    Log.Write(eLogType.Info, "Sources: /usr/bin, /usr/lib");
                    Log.Write(eLogType.Info, "Target: /opt/media/USBDriveA1");
                    Log.Write(eLogType.Info, "");

                    foreach (string sourcePath in _scanPaths)
                    {
                        Log.Write(eLogType.Info, $"Scanning {sourcePath}...");

                        ScanDirectory(sourcePath, (file, bytes) =>
                        {
                            try
                            {
                                string fileRelative = file.TrimStart(Path.DirectorySeparatorChar);
                                string fileTarget = Path.Combine(
                                    "/opt/media/USBDriveA1",
                                    fileRelative
                                );

                                string fileTargetDir = Path.GetDirectoryName(fileTarget);

                                if (!Directory.Exists(fileTargetDir))
                                {
                                    _ = Directory.CreateDirectory(fileTargetDir);
                                }

                                if (bytes != null)
                                {
                                    File.WriteAllBytes(fileTarget, bytes);

                                    // Log progress every 100 files
                                    if (_filesCopied % 100 == 0)
                                    {
                                        Log.Write(eLogType.Info,
                                            $"Progress: {_filesCopied} files copied, {_filesBlocked} blocked, {_symlinks} symlinks");
                                    }
                                }
                                else
                                {
                                    // Create empty marker file for symlinks/blocked files
                                    File.WriteAllText(fileTarget, "");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Write(eLogType.Debug, $"Error copying {file}: {ex.Message}");
                            }
                        });

                        Log.Write(eLogType.Info, $"Completed scanning {sourcePath}");
                    }

                    Log.Write(eLogType.Info, "");
                    Log.Write(eLogType.Info, "=== FILESTEALER: Scan completed! ===");
                    Log.Write(eLogType.Info, $"Total files scanned: {_filesScanned}");
                    Log.Write(eLogType.Info, $"Files successfully copied: {_filesCopied}");
                    Log.Write(eLogType.Info, $"Files blocked (permissions): {_filesBlocked}");
                    Log.Write(eLogType.Info, $"Symlinks found: {_symlinks}");
                    Log.Write(eLogType.Info, "");
                }
                catch (Exception ex)
                {
                    Log.Write(eLogType.Error, $"FILESTEALER ERROR: {ex.Message}");
                    Log.Write(eLogType.Error, $"Stack trace: {ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// Legacy synchronous wrapper for backward compatibility
        /// </summary>
        public static void CopyToUsb()
        {
            _ = CopyToUsbAsync();
        }
    }
}
