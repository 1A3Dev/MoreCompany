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

    [HarmonyPatch(typeof(StartOfRound), "Start")]
    public static class StartPatch
    {
        public static void Postfix(StartOfRound __instance)
        {
            __instance.livingPlayers = MainClass.newPlayerCount;
        }
    }
}
