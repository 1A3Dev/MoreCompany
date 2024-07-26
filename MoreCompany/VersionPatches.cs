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
        public static void Prefix()
        {
            if (GameNetworkManager.Instance == null) return;

            if (VersionPatch.ActualGameVersion == 0)
            {
                VersionPatch.ActualGameVersion = GameNetworkManager.Instance.gameVersionNum;
            }
            else if (VersionPatch.ActualGameVersion != GameNetworkManager.Instance.gameVersionNum)
            {
                MainClass.StaticLogger.LogInfo($"[LobbyDataIsJoinable] Reverted temp version override from {GameNetworkManager.Instance.gameVersionNum} to {VersionPatch.ActualGameVersion}");
                GameNetworkManager.Instance.gameVersionNum = VersionPatch.ActualGameVersion;
            }
        }
        public static void Postfix(MenuManager __instance)
        {
            if (__instance.versionNumberText != null)
            {
                __instance.versionNumberText.text = string.Format("{0} (MC)", __instance.versionNumberText.text);
            }
        }
    }

    [HarmonyPatch]
    public static class VersionPatch
    {
        public static int ActualGameVersion = 0;
        public const int VersionIncAmount = 9950;

        [HarmonyPatch(typeof(GameNetworkManager), "SteamMatchmaking_OnLobbyCreated")]
        [HarmonyPrefix]
        public static void SteamMatchmaking_OnLobbyCreated(GameNetworkManager __instance, Steamworks.Result result, ref Lobby lobby)
        {
            int newVersionNumber = (MainClass.newPlayerCount > 4 ? ActualGameVersion + VersionIncAmount : ActualGameVersion);
            if (__instance.gameVersionNum != newVersionNumber)
            {
                MainClass.StaticLogger.LogInfo($"[SteamMatchmaking_OnLobbyCreated] Version override from {__instance.gameVersionNum} to {newVersionNumber}");
                __instance.gameVersionNum = newVersionNumber;
            }
            lobby.SetData("morecompany", "t");
        }

        [HarmonyPatch(typeof(GameNetworkManager), "SetInstanceValuesBackToDefault")]
        [HarmonyPostfix]
        public static void SetInstanceValuesBackToDefault_Postfix(GameNetworkManager __instance)
        {
            if (__instance.gameVersionNum != ActualGameVersion)
            {
                MainClass.StaticLogger.LogInfo($"[SetInstanceValuesBackToDefault] Reverted version override from {__instance.gameVersionNum} to {ActualGameVersion}");
                __instance.gameVersionNum = ActualGameVersion;
            }
        }

        [HarmonyPatch(typeof(LobbyQuery), "ApplyFilters")]
        [HarmonyPrefix]
        public static void ApplyFilters_Prefix(ref LobbyQuery __instance, ref Dictionary<string, string> ___stringFilters)
        {
            ___stringFilters.Remove("vers");

            if (MainClass.showVanillaLobbies.Value)
            {
                // Since steam doesn't allow the Equal comparison on the same key multiple times using an OR we have to exclude disallowed versions instead
                __instance = __instance.WithHigher("vers", ActualGameVersion - 1);
                __instance = __instance.WithLower("vers", (ActualGameVersion + 1) + VersionIncAmount);
                for (int i = ActualGameVersion + 1; i < ActualGameVersion + VersionIncAmount; i++)
                {
                    __instance = __instance.WithNotEqual("vers", i);
                }
            }
            else
            {
                __instance = __instance.WithKeyValue("vers", (ActualGameVersion + VersionIncAmount).ToString());
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "LobbyDataIsJoinable")]
        [HarmonyPrefix]
        public static void LobbyDataIsJoinable_Prefix(GameNetworkManager __instance, ref Lobby lobby)
        {
            if (int.TryParse(lobby.GetData("vers"), out int lobbyVer) && __instance.gameVersionNum != lobbyVer)
            {
                MainClass.StaticLogger.LogInfo($"[LobbyDataIsJoinable] Temp version override from {__instance.gameVersionNum} to {lobbyVer}");
                __instance.gameVersionNum = lobbyVer;
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "LobbyDataIsJoinable")]
        [HarmonyPostfix]
        public static void LobbyDataIsJoinable_Postfix(GameNetworkManager __instance)
        {
            if (__instance.gameVersionNum != ActualGameVersion)
            {
                MainClass.StaticLogger.LogInfo($"[LobbyDataIsJoinable] Reverted temp version override from {__instance.gameVersionNum} to {ActualGameVersion}");
                __instance.gameVersionNum = ActualGameVersion;
            }
        }
    }
}
