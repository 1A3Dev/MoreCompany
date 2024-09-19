using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace MoreCompany
{
    public static class PluginInformation
    {
        public const string PLUGIN_NAME = "MoreCompany";
        public const string PLUGIN_VERSION = "1.10.0";
        public const string PLUGIN_GUID = "me.swipez.melonloader.morecompany";
    }

    [BepInPlugin(PluginInformation.PLUGIN_GUID, PluginInformation.PLUGIN_NAME, PluginInformation.PLUGIN_VERSION)]
    public class MainClass : BaseUnityPlugin
    {
        public static int defaultPlayerCount = 100;
        public static int minPlayerCount = 4;
        public static int maxPlayerCount = 50;
        public static int newPlayerCount = 32;

        public static ConfigFile StaticConfig;
        public static ConfigEntry<int> playerCount;
        public static ConfigEntry<bool> cosmeticsDeadBodies;
        public static ConfigEntry<bool> cosmeticsMaskedEnemy;
        public static ConfigEntry<bool> cosmeticsSyncOther;
        public static ConfigEntry<bool> defaultCosmetics;
        public static ConfigEntry<bool> cosmeticsPerProfile;
        public static ConfigEntry<string> disabledCosmetics;

        public static ManualLogSource StaticLogger;

        private void Awake()
        {
            StaticLogger = Logger;
            StaticConfig = Config;

            playerCount = StaticConfig.Bind("General", "Player Count", defaultPlayerCount, new ConfigDescription("How many players can be in your lobby?", new AcceptableValueRange<int>(minPlayerCount, maxPlayerCount)));
            cosmeticsSyncOther = StaticConfig.Bind("Cosmetics", "Show Cosmetics", true, "Should you be able to see cosmetics of other players?"); // This is the one linked to the UI button
            cosmeticsDeadBodies = StaticConfig.Bind("Cosmetics", "Show On Dead Bodies", true, "Should you be able to see cosmetics on dead bodies?");
            cosmeticsMaskedEnemy = StaticConfig.Bind("Cosmetics", "Show On Masked Enemy", true, "Should you be able to see cosmetics on the masked enemy?");
            defaultCosmetics = StaticConfig.Bind("Cosmetics", "Default Cosmetics", true, "Should the default cosmetics be enabled?");
            cosmeticsPerProfile = StaticConfig.Bind("Cosmetics", "Per Profile Cosmetics", false, "Should the cosmetics be saved per-profile?");
            disabledCosmetics = StaticConfig.Bind("Cosmetics", "Disabled Cosmetics", "", "Comma separated list of cosmetics to disable");

            Harmony harmony = new Harmony(PluginInformation.PLUGIN_GUID);
            try
            {
                harmony.PatchAll();
            }
            catch (Exception e)
            {
                StaticLogger.LogError("Failed to patch: " + e);
            }

            StaticLogger.LogInfo("Loading MoreCompany...");

            SteamFriends.OnGameLobbyJoinRequested += (lobby, steamId) =>
            {
                newPlayerCount = lobby.MaxMembers;
            };

            SteamMatchmaking.OnLobbyEntered += (lobby) =>
            {
                newPlayerCount = lobby.MaxMembers;
            };

            StaticLogger.LogInfo("Loaded MoreCompany FULLY");
        }

        public static void ResizePlayerCache(Dictionary<uint, Dictionary<int, NetworkObject>> ScenePlacedObjects)
        {
            StartOfRound round = UnityEngine.Object.FindObjectOfType<StartOfRound>();
            if (round.allPlayerObjects.Length != newPlayerCount)
            {
                StaticLogger.LogInfo($"ResizePlayerCache: {newPlayerCount}");
                uint starting = 10000;

                int originalLength = round.allPlayerObjects.Length;

                int difference = newPlayerCount - originalLength;

                Array.Resize(ref round.allPlayerObjects, newPlayerCount);
                Array.Resize(ref round.allPlayerScripts, newPlayerCount);
                Array.Resize(ref round.playerSpawnPositions, newPlayerCount);

                StaticLogger.LogInfo($"Resizing player cache from {originalLength} to {newPlayerCount} with difference of {difference}");

                if (difference > 0)
                {
                    GameObject firstPlayerObject = round.allPlayerObjects[3];
                    for (int i = 0; i < difference; i++)
                    {
                        uint newId = starting + (uint)i;
                        GameObject copy = GameObject.Instantiate(firstPlayerObject, firstPlayerObject.transform.parent);
                        NetworkObject copyNetworkObject = copy.GetComponent<NetworkObject>();
                        ReflectionUtils.SetFieldValue(copyNetworkObject, "GlobalObjectIdHash", (uint) newId);
                        int handle = copyNetworkObject.gameObject.scene.handle;
                        uint globalObjectIdHash = newId;

                        if (!ScenePlacedObjects.ContainsKey(globalObjectIdHash))
                        {
                            ScenePlacedObjects.Add(globalObjectIdHash, new Dictionary<int, NetworkObject>());
                        }
                        if (ScenePlacedObjects[globalObjectIdHash].ContainsKey(handle))
                        {
                            string text = ((ScenePlacedObjects[globalObjectIdHash][handle] != null) ? ScenePlacedObjects[globalObjectIdHash][handle].name : "Null Entry");
                            throw new Exception(copyNetworkObject.name + " tried to registered with ScenePlacedObjects which already contains " + string.Format("the same {0} value {1} for {2}!", "GlobalObjectIdHash", globalObjectIdHash, text));
                        }
                        ScenePlacedObjects[globalObjectIdHash].Add(handle, copyNetworkObject);

                        copy.name = $"Player ({4 + i})";

                        PlayerControllerB newPlayerScript = copy.GetComponentInChildren<PlayerControllerB>();

                        // Reset
                        newPlayerScript.playerClientId = (ulong)(4 + i);
                        newPlayerScript.playerUsername = $"Player #{newPlayerScript.playerClientId}";
                        newPlayerScript.isPlayerControlled = false;
                        newPlayerScript.isPlayerDead = false;

                        // Set new player object
                        round.allPlayerObjects[originalLength + i] = copy;
                        round.allPlayerScripts[originalLength + i] = newPlayerScript;
                        round.playerSpawnPositions[originalLength + i] = round.playerSpawnPositions[3];
                    }
                }
            }

            foreach (PlayerControllerB newPlayerScript in round.allPlayerScripts) // Fix for billboards showing as Player # with no number in LAN (base game issue)
            {
                newPlayerScript.usernameBillboardText.text = newPlayerScript.playerUsername;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkSceneManager), "PopulateScenePlacedObjects")]
    public static class ScenePlacedObjectsInitPatch
    {
        public static void Postfix(ref Dictionary<uint, Dictionary<int, NetworkObject>> ___ScenePlacedObjects)
        {
            MainClass.ResizePlayerCache(___ScenePlacedObjects);
        }
    }

    [HarmonyPatch(typeof(SteamMatchmaking), "CreateLobbyAsync")]
    public static class LobbyThingPatch
    {
        public static void Prefix(ref int maxMembers)
        {
            MainClass.newPlayerCount = MainClass.defaultPlayerCount;
            maxMembers = MainClass.newPlayerCount;
        }
    }

    [HarmonyPatch]
    public static class TogglePlayerObjectsPatch
    {
        [HarmonyPatch(typeof(PlayerControllerB), "SpawnPlayerAnimation")]
        [HarmonyPrefix]
        private static void SpawnPlayerAnimation()
        {
            StartOfRound startOfRound = UnityEngine.Object.FindObjectOfType<StartOfRound>();
            foreach (PlayerControllerB playerControllerB in startOfRound.allPlayerScripts)
            {
                bool flag = playerControllerB.isPlayerControlled || playerControllerB.isPlayerDead;
                if (flag)
                {
                    playerControllerB.gameObject.SetActive(true);
                }
                else
                {
                    playerControllerB.gameObject.SetActive(false);
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerConnectedClientRpc")]
        [HarmonyPrefix]
        private static void OnPlayerConnectedClientRpc(StartOfRound __instance, ulong clientId, int connectedPlayers, ulong[] connectedPlayerIdsOrdered, int assignedPlayerObjectId)
        {
            __instance.allPlayerScripts[assignedPlayerObjectId].gameObject.SetActive(true);
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDisconnectedClientRpc")]
        [HarmonyPostfix]
        private static void OnPlayerDisconnectedClientRpc(StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            __instance.allPlayerScripts[playerObjectNumber].gameObject.SetActive(false);
        }
    }
}
