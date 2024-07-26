using System.Collections.Generic;
using System.Text;
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

                string lobbyVers = lobbySlot.thisLobby.GetData("vers");
                if (MainClass.showVanillaLobbies.Value && lobbyVers != GameNetworkManager.Instance.gameVersionNum.ToString())
                {
                    lobbySlot.LobbyName.text = string.Format("[v{0}] {1}", lobbyVers, lobbySlot.thisLobby.GetData("name"));
                }
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
                __instance.versionNumberText.text = string.Format("{0} (MC)", __instance.versionNumberText.text);
            }
        }
    }

    [HarmonyPatch]
    public static class VersionPatch
    {
        public const int VersionIncAmount = 9950;

        [HarmonyPatch(typeof(GameNetworkManager), "SubscribeToConnectionCallbacks")]
        [HarmonyPostfix]
        public static void SubscribeToConnectionCallbacks_Postfix(GameNetworkManager __instance)
        {
            if (__instance.currentLobby.HasValue)
            {
                __instance.currentLobby.Value.SetData("morecompany", "t");
                int currentVersion = GameNetworkManager.Instance.gameVersionNum;
                __instance.currentLobby.Value.SetData("vers", (MainClass.newPlayerCount > 4 ? currentVersion + VersionIncAmount : currentVersion).ToString());
            }
        }

        [HarmonyPatch(typeof(LobbyQuery), "ApplyFilters")]
        [HarmonyPrefix]
        public static void ApplyFilters_Prefix(ref LobbyQuery __instance, ref Dictionary<string, string> ___stringFilters)
        {
            ___stringFilters.Remove("vers");

            int currentVersion = GameNetworkManager.Instance.gameVersionNum;
            if (MainClass.showVanillaLobbies.Value)
            {
                // Since steam doesn't allow the Equal comparison on the same key multiple times using an OR we have to exclude disallowed versions instead
                __instance = __instance.WithHigher("vers", currentVersion - 1);
                __instance = __instance.WithLower("vers", (currentVersion + 1) + VersionIncAmount);
                for (int i = currentVersion + 1; i < currentVersion + VersionIncAmount; i++)
                {
                    __instance = __instance.WithNotEqual("vers", i);
                }
            }
            else
            {
                __instance = __instance.WithKeyValue("vers", (currentVersion + VersionIncAmount).ToString());
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "LobbyDataIsJoinable")]
        [HarmonyPrefix]
        public static void LobbyDataIsJoinable_Prefix(ref GameNetworkManager __instance, ref int __state, ref Lobby lobby)
        {
            if (int.TryParse(lobby.GetData("vers"), out int lobbyVer))
            {
                __state = __instance.gameVersionNum;
                __instance.gameVersionNum = lobbyVer;
                MainClass.StaticLogger.LogInfo($"[LobbyDataIsJoinable] Temp version override from {__state} to {__instance.gameVersionNum}");
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "LobbyDataIsJoinable")]
        [HarmonyPostfix]
        public static void LobbyDataIsJoinable_Postfix(ref GameNetworkManager __instance, ref int __state)
        {
            MainClass.StaticLogger.LogInfo($"[LobbyDataIsJoinable] Reverted temp version override from {__instance.gameVersionNum} to {__state}");
            __instance.gameVersionNum = __state;
        }
    }
}
