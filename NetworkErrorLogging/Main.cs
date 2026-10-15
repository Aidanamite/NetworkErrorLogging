using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

#if DESKTOP
using BepInEx;
using Microsoft.AppCenter.Unity.Crashes;
#elif MOBILE
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMicrosoft.AppCenter.Unity.Crashes;
using MelonLoader;

[assembly: MelonInfo(typeof(NetworkErrorLogging.Main), "Network Error Logging", NetworkErrorLogging.Main.VERSION, "Aidanamite")]
#endif

namespace NetworkErrorLogging
{
#if DESKTOP
    [BepInPlugin("com.aidanamite.NetworkErrorLogging", "Network Error Logging", VERSION)]
    public class Main : BaseUnityPlugin
#elif MOBILE
    public class Main : MelonMod
#endif
    {
        public const string VERSION = "1.0.0";
#if DESKTOP
        static BepInEx.Logging.ManualLogSource logger;
        public void Awake()
        {
            logger = Logger;
            new Harmony("com.aidanamite.NetworkErrorLogging").PatchAll();
#elif MOBILE
        static MelonLogger.Instance logger;
        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            base.OnInitializeMelon();
            HarmonyInstance.PatchAll();
#endif
            LogInfo("Loaded");
        }
#if DESKTOP
        public static void LogInfo(object msg) => logger.LogInfo(msg);
        public static void LogError(object msg) => logger.LogError(msg);
#elif MOBILE
        public static void LogInfo(object msg) => logger.Msg(msg);
        public static void LogError(object msg) => logger.Error(msg);
#endif
    }
    
    [HarmonyPatch]
    static class Patch_LogError
    {
        static bool calling = false;

        [HarmonyPatch(typeof(UtWWWAsync.WWWProcess), "ProcessError")]
        [HarmonyPrefix]
#if DESKTOP
        static void ProcessError_Prefix(out bool __state)
        {
#elif MOBILE
        static void ProcessError_Prefix(out bool __state, UtWWWAsync.WWWProcess __instance, string errorText, Il2CppSystem.Exception exception)
        {
            Main.LogError(
$@"[
  URL={__instance.mWebRequest.url}{
    (__instance.mNumRetries != 0 ? "\n  Retries=" + __instance.mNumRetries : "")}
  ErrorText={errorText}
  Exception={(exception != null ? exception.GetIl2CppType().FullName + ": " + exception.Message + "\n" + exception.GetStackTrace(true) : "")}
]");
#endif
            __state = calling;
            calling = true;
        }

        [HarmonyPatch(typeof(UtWWWAsync.WWWProcess), "ProcessError")]
        [HarmonyFinalizer]
        static void ProcessError_Postfix(bool __state) => calling = __state;

#if DESKTOP
        [HarmonyPatch(typeof(UtDebug), "LogError", typeof(object))]
        [HarmonyPrefix]
        static void LogError_Prefix(out int? __state)
        {
            if (!calling)
            {
                __state = null;
                return;
            }
            __state = UtDebug._Level;
            UtDebug._Level = 100;
        }

        [HarmonyPatch(typeof(UtDebug), "LogError", typeof(object))]
        [HarmonyFinalizer]
        static void LogError_Postfix(int? __state)
        {
            if (__state != null)
                UtDebug._Level = __state.Value;
        }
#elif false // MOBILE // Note: this if is here to disable this patch entirely on mobile until lemon loader fixes the crash caused by patching it
        [HarmonyPatch(typeof(UtDebug), "LogError", typeof(Il2CppSystem.Object))]
        [HarmonyPrefix]
        static void LogError_Prefix(Il2CppSystem.Object message)
        {
            if (calling && message != null)
                Main.LogError(message.ToString());
        }
#endif


#if DESKTOP // Note: this if is here to disable this patch entirely on mobile until lemon loader fixes the crash caused by patching it
        [HarmonyPatch(typeof(Crashes), "TrackError")]
        [HarmonyPrefix]
#if DESKTOP
        static void TrackError_Prefix(Exception exception, IDictionary<string, string> properties, ErrorAttachmentLog[] attachments)
#elif MOBILE
        static void TrackError_Prefix(Il2CppSystem.Exception exception, Il2CppSystem.Collections.Generic.IDictionary<string, string> properties, Il2CppReferenceArray<ErrorAttachmentLog> attachments) PATCHING THIS METHOD AT ALL CAUSES MOBILE TO CRASH
#endif
        {
            if (exception == null)
                return;
            var str = new StringBuilder();
            str.Append("Properties: [ ");
            var first = true;
#if DESKTOP
            foreach (var pair in properties)
            {
#elif MOBILE
            var e = properties.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Il2CppSystem.Collections.Generic.KeyValuePair<string, string>>>().GetEnumerator();
            var e2 = e.Cast<Il2CppSystem.Collections.IEnumerator>();
            while (e2.MoveNext())
            {
                var pair = e.Current;
#endif
                if (first)
                    first = false;
                else
                    str.Append(", ");
                str.Append(pair.Key).Append("=").Append(pair.Value);
            }
            Main.LogError(str.Append(" ]\nException: ").Append(exception).ToString());
        }
#endif
    }
}