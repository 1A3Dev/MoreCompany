using HarmonyLib;
using Steamworks.Data;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MoreCompany
{
    [HarmonyPatch(typeof(LobbyQuery), "RequestAsync")]
    public static class RequestAsyncPatch
    {
        public static void Prefix(ref LobbyQuery __instance)
        {
            __instance.WithKeyValue("serverVersion", NetworkManager.Singleton.NetworkConfig.ProtocolVersion.ToString());
        }
    }

    [HarmonyPatch(typeof(SteamLobbyManager), "LoadServerList")]
    public static class LoadLobbyListAndFilterPatch
    {
        public static void Postfix()
        {
            LobbySlot[] lobbySlots = Object.FindObjectsOfType<LobbySlot>();
            foreach (LobbySlot lobbySlot in lobbySlots)
            {
                lobbySlot.playerCount.text = string.Format("{0} / {1}", lobbySlot.thisLobby.MemberCount, lobbySlot.thisLobby.MaxMembers);
            }
        }
    }
}
