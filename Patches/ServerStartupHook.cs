using HarmonyLib;
using Unity.Scenes;

namespace DailyQuest.Patches;

[HarmonyPatch(typeof(SceneSectionStreamingSystem), nameof(SceneSectionStreamingSystem.ShutdownAsynchrnonousStreamingSupport))]
public static class ServerStartupHook
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        Core.InitializeAfterLoaded();
        Core.Log.LogInfo("[Quest] DailyQuest initialized.");

        Plugin.Harmony?.Unpatch(typeof(SceneSectionStreamingSystem).GetMethod("ShutdownAsynchrnonousStreamingSupport"),typeof(ServerStartupHook).GetMethod("Postfix"));
    }
}
