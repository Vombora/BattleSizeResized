using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.ModuleManager;

namespace BattleSizeResized
{
    public static class TxtLogger
    {
        public static string ModuleRootPath
        {
            get { return ModuleHelper.GetModuleFullPath("BattleSizeResized"); }
        }

        public static string ModuleDataPath
        {
            get { return ModuleRootPath + "ModuleData/"; }
        }

        public static string LogDirPath
        {
            get { return ModuleRootPath + "Logs/"; }
        }

        public static void TryWarnAndLog(Exception ex)
        {
            string dirPath = LogDirPath;
            string logPath = dirPath + "LogFile.txt";

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            using StreamWriter logger = new StreamWriter(logPath, false);
            string report = GetReport(ex);

            logger.Write(report);
            logger.Flush();
            logger.Close();
        }

        private static string GetReport(Exception ex)
        {
            string report =
                "---------------------------------------- Report Details -----------------------------------------\n" +
                "-------------------------------------------------------------------------------------------------\n" +
                GetHeader() + nl +
                "--------------------------------------- Exception Details ---------------------------------------\n" +
                GetExceptionDetailsAndWarn(ex) + nl +
                "-------------------------------------------------------------------------------------------------\n" +
                "----------------------------------------- End of Report -----------------------------------------\n" + nl +
                "*NOTE: This file automatically clears/overwrites itself after each new occurrence. \nYou won't need to manually clear logs as they will never accumulate.";

            return report;
        }

        private static string GetHeader()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string name = Assembly.GetExecutingAssembly().GetName().Name;

            string moduleInfo = $"MODULE: {name}\nVERSION: {version}";

            return nl + moduleInfo + nl + "DATE: " + DateTime.Now.ToString() + nl;
        }

        private static string GetExceptionDetailsAndWarn(Exception ex)
        {
            string source = ex.Source ?? "Could not identify source",
             message = ex.Message ?? "Exception didn't supplied a message.",
             stack = ex.StackTrace ?? "Exception didn't supplied Stack Trace data.",
             local = ex.StackTrace.Split('\n').Last() ?? "Exception didn't supplied Stack Trace last line.",
             extype = ex.GetType().ToString() ?? "Exception didn't supplied type data.",

             name = Assembly.GetExecutingAssembly().GetName().Name,

             advice = "";

            if (source.Equals("0Harmony") || source.Equals("Bannerlord.UIExtenderEx") || source.Contains("MCM") || source.Contains("Bannerlord.ButterLib"))
            {
                advice = $"Please, check if {source}, {name} and Bannerlord versions are compatible.";
            }

            string[] modulesArray = Utilities.GetModulesNames();
            int modCount = modulesArray.Length,
             modCountInDisk = ModuleHelper.GetModules().Count();

            string modules = "";
            foreach (var m in modulesArray)
            {
                modules += $"   {m}\n";
            }

            string harmonyPatches = GetHarmonyPatches();
            int patchCount = Harmony.GetAllPatchedMethods().Count();

            if (harmonyPatches == "" || patchCount < 1)
            {
                advice += $"\nERROR: Harmony was unable to patch.";
            }

            Messenger.Notify($"{source} threw and exception. {advice}", Context.Error);

            string exDetails = nl + "TYPE: " + extype + nl + "SOURCE: " + source + nl + "TRIGGER: \n" + local + dl + "NOTES: \n" + advice + dl + "MESSAGE: \n"
                + message + dl + "FullStack: \n" + stack + dl + $"MODULES - {modCountInDisk} installed, {modCount} loaded: \n" + modules + nl;


            if (harmonyPatches != "")
            {
                exDetails += $"Harmony Patches ({patchCount} total methods patched)" +
                    $"\nThis list is not showing patches from common dependencies (Harmony, MCM, Butterlib or UiExtender): \n" + harmonyPatches + nl;
            }

            return exDetails;
        }

        private static string GetHarmonyPatches()
        {
            var patchedMethods = Harmony.GetAllPatchedMethods();
            string patches = "";
            foreach (var method in patchedMethods)
            {
                foreach (var owner in Harmony.GetPatchInfo(method).Owners)
                {
                    if (!owner.ToLower().Contains("betterexceptionwindow") && !owner.ToLower().Contains("butterlib") && !owner.ToLower().Contains("uiextender")
                        && !owner.ToLower().Contains("mcm.ui") && !owner.ToLower().Contains("bannerlord.mcm") && !owner.ToLower().Contains("harmony"))
                    {
                        string methodName = method.Name ?? "";
                        string methodOrigin = method.DeclaringType.Name ?? "";
                        var info = Harmony.GetPatchInfo(method);
                        var owners = "";

                        foreach (var ownerr in info.Owners)
                        {
                            owners += $"{ownerr} | ";
                        }
                        string prefixes = "";
                        string postfixes = "";
                        string transpilers = "";
                        string finalizers = "";
                        string fullData = "";
                        if (!info.Prefixes.IsEmpty())
                        {
                            foreach (var prefix in info.Prefixes)
                            {
                                prefixes += $"{prefix.PatchMethod.ToString()}";
                            }
                            fullData += $"      Prefixes: {prefixes}\n";
                        }
                        if (!info.Postfixes.IsEmpty())
                        {
                            foreach (var postfix in info.Postfixes)
                            {
                                postfixes += $"{postfix.PatchMethod.ToString()}";
                            }
                            fullData += $"      Postfixes: {postfixes}\n";
                        }
                        if (!info.Transpilers.IsEmpty())
                        {
                            foreach (var transp in info.Transpilers)
                            {
                                transpilers += $"{transp.PatchMethod.ToString()}";
                            }
                            fullData += $"      Transpilers: {transpilers}\n";
                        }
                        if (!info.Finalizers.IsEmpty())
                        {
                            foreach (var final in info.Finalizers)
                            {
                                finalizers += $"{final.PatchMethod.ToString()}";
                            }
                            fullData += $"      Finalizers: {finalizers}\n";
                        }

                        patches += $"\n Method Name: {methodName}\n   Method Origin: {methodOrigin}\n   Method Owners: {owners}\n   Patch Implementation Data:\n{fullData}";
                    }
                }
            }
            return patches;
        }

        private const string nl = "\r\n";

        private const string dl = "\r\n\r\n";
    }
}
