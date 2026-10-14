using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using WandEnhancer.Core.Patching.Shared;
using WandEnhancer.Models;

namespace WandEnhancer.Core
{
    /// <summary>
    /// Extra read-only diagnostics for the Wand 12.63 compatibility investigation.
    /// Does not modify the inspected files.
    /// </summary>
    internal static class DiagnosticReporter
    {
        private static readonly string[] InterestingFiles =
        {
            "WandAuxiliaryService.exe",
            "TrainerLibPlugin_x64.dll",
            "tophat_service_x64.dll",
            "overlay_game_x64.dll"
        };

        public static void Report(
            WeModConfig install,
            string unpackedRoot,
            string asarPath,
            PatchConfig config,
            string phase,
            Action<string, ELogType> log)
        {
            if (log == null)
            {
                return;
            }

            log($"[DIAG 12.63] === {phase} ===", ELogType.Info);
            log($"[DIAG 12.63] Strategy={config?.Strategy}; AutoApply={config?.AutoApplyAfterUpdate}; Patches={DescribePatches(config)}", ELogType.Info);

            ReportFile("Wand.exe", install?.ExecutablePath, log, includeVersion: true);

            if (!string.IsNullOrEmpty(asarPath) && File.Exists(asarPath))
            {
                try
                {
                    var info = new FileInfo(asarPath);
                    string headerHash = AsarHeaderHash.Compute(asarPath);
                    log($"[DIAG 12.63] app.asar size={info.Length}; headerSha256={headerHash}", ELogType.Info);
                }
                catch (Exception e)
                {
                    log($"[DIAG 12.63] app.asar inspect failed: {e.GetType().Name}: {e.Message}", ELogType.Warn);
                }
            }

            if (string.IsNullOrEmpty(unpackedRoot) || !Directory.Exists(unpackedRoot))
            {
                log("[DIAG 12.63] app.asar.unpacked not available for component inspection.", ELogType.Warn);
                return;
            }

            foreach (string name in InterestingFiles)
            {
                List<string> matches;
                try
                {
                    matches = Directory.EnumerateFiles(unpackedRoot, name, SearchOption.AllDirectories).ToList();
                }
                catch (Exception e)
                {
                    log($"[DIAG 12.63] scan for {name} failed: {e.GetType().Name}: {e.Message}", ELogType.Warn);
                    continue;
                }

                if (matches.Count == 0)
                {
                    log($"[DIAG 12.63] {name}: not found", ELogType.Warn);
                    continue;
                }

                foreach (string path in matches)
                {
                    ReportFile(name, path, log, includeVersion: true);
                }
            }
        }

        private static string DescribePatches(PatchConfig config)
        {
            if (config?.PatchTypes == null || config.PatchTypes.Count == 0)
            {
                return "(none)";
            }

            return string.Join(",", config.PatchTypes.OrderBy(x => x.ToString()).Select(x => x.ToString()));
        }

        private static void ReportFile(
            string label,
            string path,
            Action<string, ELogType> log,
            bool includeVersion)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                log($"[DIAG 12.63] {label}: not found", ELogType.Warn);
                return;
            }

            try
            {
                var info = new FileInfo(path);
                string version = "";
                if (includeVersion)
                {
                    try
                    {
                        var v = FileVersionInfo.GetVersionInfo(path);
                        version = $"; fileVersion={v.FileVersion ?? "(none)"}; productVersion={v.ProductVersion ?? "(none)"}";
                    }
                    catch
                    {
                        version = "; version=(unavailable)";
                    }
                }

                log($"[DIAG 12.63] {label}: size={info.Length}{version}; sha256={Sha256(path)}", ELogType.Info);
            }
            catch (Exception e)
            {
                log($"[DIAG 12.63] {label} inspect failed: {e.GetType().Name}: {e.Message}", ELogType.Warn);
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
