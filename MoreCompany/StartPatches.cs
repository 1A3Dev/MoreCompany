using HarmonyLib;
using UnityEngine;

namespace MoreCompany
{
    [HarmonyPatch(typeof(StartOfRound), "setPlayerToSpawnPosition")]
    public static class SpawnPositionClampPatch
    {
        public static void Prefix(StartOfRound __instance, Transform playerBody, ref int id)
        {
			if (!__instance.playerSpawnPositions[id])
			{
				id = __instance.playerSpawnPositions.Length - 1;
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), "OnPlayerDisconnectedClientRpc")]
    public static class OnPlayerDCPatch
    {
        public static void Postfix(int playerObjectNumber)
        {
            if (MainClass.playerIdsAndCosmetics.ContainsKey(playerObjectNumber))
            {
                MainClass.playerIdsAndCosmetics.Remove(playerObjectNumber);
            }
        }
    }
}
