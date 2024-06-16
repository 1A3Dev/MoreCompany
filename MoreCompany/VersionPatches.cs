using HarmonyLib;
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
}
