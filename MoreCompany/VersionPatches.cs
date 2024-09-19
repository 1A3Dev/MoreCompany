using HarmonyLib;
using Steamworks.Data;
using Unity.Netcode;

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

    [HarmonyPatch(typeof(LobbySlot), "Update")]
    public static class LoadLobbyListAndFilterPatch
    {
        public static void Postfix(LobbySlot __instance)
        {
            __instance.playerCount.text = string.Format("{0} / {1}", __instance.thisLobby.MemberCount, __instance.thisLobby.MaxMembers);
        }
    }
}
