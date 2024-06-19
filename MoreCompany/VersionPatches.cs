using System.Collections.Generic;
using HarmonyLib;
using Steamworks.Data;
using UnityEngine;

namespace MoreCompany
{
    [HarmonyPatch(typeof(SteamLobbyManager), "loadLobbyListAndFilter", MethodType.Enumerator)]
    public static class LoadLobbyListAndFilterPatch
    {
        private static void Postfix()
        {
            LobbySlot[] lobbySlots = Object.FindObjectsOfType<LobbySlot>();
            foreach (LobbySlot lobbySlot in lobbySlots)
            {
                lobbySlot.playerCount.text = string.Format("{0} / {1}", lobbySlot.thisLobby.MemberCount, lobbySlot.thisLobby.MaxMembers);
            }
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), "SubscribeToConnectionCallbacks")]
    public static class SubscribeToConnectionCallbacksPatch
    {
        public static void Postfix(GameNetworkManager __instance)
        {
            if (__instance.currentLobby.HasValue)
            {
                __instance.currentLobby.Value.SetData("morecompany", "t");
                int currentVersion = GameNetworkManager.Instance.gameVersionNum;
                __instance.currentLobby.Value.SetData("vers", (MainClass.newPlayerCount > 4 ? currentVersion + 9950 : currentVersion).ToString());
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "Awake")]
    public static class MenuManagerVersionDisplayPatch
    {
        public static void Postfix(MenuManager __instance)
        {
            if (GameNetworkManager.Instance != null && __instance.versionNumberText != null)
            {
                __instance.versionNumberText.text = string.Format("v{0} (MC)", GameNetworkManager.Instance.gameVersionNum);
            }
        }
    }

    [HarmonyPatch(typeof(LobbyQuery), "ApplyFilters")]
    public static class LobbyQueryApplyFiltersPatch
    {
        public static void Prefix(ref LobbyQuery __instance, ref Dictionary<string, string> ___stringFilters)
        {
            ___stringFilters.Remove("vers");

            int currentVersion = GameNetworkManager.Instance.gameVersionNum;
            __instance = __instance.WithHigher("vers", currentVersion - 1);
            __instance = __instance.WithNotEqual("vers", currentVersion + 16440);

            int minVersion = 38;
            for (int i = minVersion; i < currentVersion; i++)
            {
                __instance = __instance.WithNotEqual("vers", i);
                __instance = __instance.WithNotEqual("vers", i + 9950);
                __instance = __instance.WithNotEqual("vers", i + 16440);
            }
        }
    }
}
