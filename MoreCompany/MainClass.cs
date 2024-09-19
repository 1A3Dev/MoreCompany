using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using MoreCompany.Utils;
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
        public static int defaultPlayerCount = 32;
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

        public static Texture2D mainLogo;
        public static GameObject quickMenuScrollParent;

        public static GameObject playerEntry;
        public static GameObject crewCountUI;

        public static GameObject cosmeticGUIInstance;
        public static GameObject cosmeticButton;

        public static ManualLogSource StaticLogger;

        public static Dictionary<int, List<string>> playerIdsAndCosmetics = new Dictionary<int, List<string>>();

        public static string dynamicCosmeticsPath;
        public static string cosmeticSavePath;

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

            cosmeticsSyncOther.SettingChanged += (sender, args) => {
                foreach (PlayerControllerB playerController in FindObjectsOfType<PlayerControllerB>())
                {
                    Transform cosmeticRoot = playerController.transform.Find("ScavengerModel").Find("metarig");
                    if (cosmeticRoot == null) continue;
                    CosmeticApplication cosmeticApplication = cosmeticRoot.gameObject.GetComponent<CosmeticApplication>();
                    if (cosmeticApplication == null) continue;

                    foreach (var spawnedCosmetic in cosmeticApplication.spawnedCosmetics)
                    {
                        if (spawnedCosmetic.cosmeticType == CosmeticType.HAT && cosmeticApplication.detachedHead) continue;
                        spawnedCosmetic.gameObject.SetActive(cosmeticsSyncOther.Value);
                    }
                }
            };

            cosmeticsDeadBodies.SettingChanged += (sender, args) => {
                foreach (PlayerControllerB playerController in FindObjectsOfType<PlayerControllerB>())
                {
                    Transform cosmeticRoot = playerController.DeadPlayerRagdoll.transform;
                    if (cosmeticRoot == null) continue;
                    CosmeticApplication cosmeticApplication = cosmeticRoot.GetComponent<CosmeticApplication>();
                    if (cosmeticApplication == null) continue;

                    foreach (var spawnedCosmetic in cosmeticApplication.spawnedCosmetics)
                    {
                        if (spawnedCosmetic.cosmeticType == CosmeticType.HAT && cosmeticApplication.detachedHead) continue;
                        spawnedCosmetic.gameObject.SetActive(cosmeticsDeadBodies.Value);
                    }
                }
            };


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

            StaticLogger.LogInfo("Loading SETTINGS...");
            //ReadSettingsFromFile();

            dynamicCosmeticsPath = Paths.PluginPath + "/MoreCompanyCosmetics";

            if (cosmeticsPerProfile.Value)
            {
                cosmeticSavePath = $"{Application.persistentDataPath}/morecompanycosmetics-{Directory.GetParent(Paths.BepInExRootPath).Name}.txt";
            }
            else
            {
                cosmeticSavePath = $"{Application.persistentDataPath}/morecompanycosmetics.txt";
            }
            cosmeticsPerProfile.SettingChanged += (sender, args) => {
                if (cosmeticsPerProfile.Value)
                {
                    cosmeticSavePath = $"{Application.persistentDataPath}/MCCosmeticsSave-{Directory.GetParent(Paths.BepInExRootPath).Name}.mcs";
                }
                else
                {
                    cosmeticSavePath = $"{Application.persistentDataPath}/MCCosmeticsSave.mcs";
                }
            };

            StaticLogger.LogInfo("Checking: " + dynamicCosmeticsPath);
            if (!Directory.Exists(dynamicCosmeticsPath))
            {
                StaticLogger.LogInfo("Creating cosmetics directory");
                Directory.CreateDirectory(dynamicCosmeticsPath);
            }
            StaticLogger.LogInfo("Loading COSMETICS...");
            ReadCosmeticsFromFile();

            //if (defaultCosmetics.Value)
            //{
            //    StaticLogger.LogInfo("Loading DEFAULT COSMETICS...");
            //    AssetBundle cosmeticsBundle = BundleUtilities.LoadBundleFromInternalAssembly("morecompany.cosmetics", Assembly.GetExecutingAssembly());
            //    CosmeticRegistry.LoadCosmeticsFromBundle(cosmeticsBundle, "morecompany.cosmetics");
            //    cosmeticsBundle.Unload(false);
            //}

            //StaticLogger.LogInfo("Loading USER COSMETICS...");
            //RecursiveCosmeticLoad(Paths.PluginPath);

            AssetBundle bundle = BundleUtilities.LoadBundleFromInternalAssembly("morecompany.assets", Assembly.GetExecutingAssembly());
            LoadAssets(bundle);

            StaticLogger.LogInfo("Loaded MoreCompany FULLY");
        }

        private void RecursiveCosmeticLoad(string directory)
        {
            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                RecursiveCosmeticLoad(subDirectory);
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                if (file.EndsWith(".cosmetics"))
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(file);
                    CosmeticRegistry.LoadCosmeticsFromBundle(bundle, file);
                    bundle.Unload(false);
                }
            }
        }

        private void ReadCosmeticsFromFile()
        {
            if (System.IO.File.Exists(cosmeticSavePath))
            {
                string[] lines = System.IO.File.ReadAllLines(cosmeticSavePath);
                foreach (var line in lines)
                {
                    CosmeticRegistry.locallySelectedCosmetics.Add(line);
                }
            }
        }

        public static void WriteCosmeticsToFile()
        {
            string built = "";
            foreach (var cosmetic in CosmeticRegistry.locallySelectedCosmetics)
            {
                built += cosmetic + "\n";
            }
            System.IO.File.WriteAllText(cosmeticSavePath, built);
        }

        public static void SaveSettingsToFile()
        {
            playerCount.Value = newPlayerCount;
            StaticConfig.Save();
        }

        public static void ReadSettingsFromFile()
        {
            try
            {
                newPlayerCount = Mathf.Clamp(playerCount.Value, minPlayerCount, maxPlayerCount);
            }
            catch
            {
                newPlayerCount = defaultPlayerCount;
                playerCount.Value = newPlayerCount;
                StaticConfig.Save();
            }
        }

        private static void LoadAssets(AssetBundle bundle)
        {
            if (bundle)
            {
                mainLogo = bundle.LoadPersistentAsset<Texture2D>("assets/morecompanyassets/morecompanytransparentred.png");
                quickMenuScrollParent = bundle.LoadPersistentAsset<GameObject>("assets/morecompanyassets/quickmenuoverride.prefab");
                playerEntry = bundle.LoadPersistentAsset<GameObject>("assets/morecompanyassets/playerlistslot.prefab");
                cosmeticGUIInstance = bundle.LoadPersistentAsset<GameObject>("assets/morecompanyassets/testoverlay.prefab");
                cosmeticButton = bundle.LoadPersistentAsset<GameObject>("assets/morecompanyassets/cosmeticinstance.prefab");
                crewCountUI = bundle.LoadPersistentAsset<GameObject>("assets/morecompanyassets/crewcountfield.prefab");
                bundle.Unload(false);
            }
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
                //Array.Resize(ref round.gameStats.allPlayerStats, newPlayerCount);
                Array.Resize(ref round.playerSpawnPositions, newPlayerCount);

                StaticLogger.LogInfo($"Resizing player cache from {originalLength} to {newPlayerCount} with difference of {difference}");

                if (difference > 0)
                {
                    //GameObject playerPrefab = round.playerPrefab;
                    //GameObject firstPlayerObject = round.allPlayerObjects[0];
                    GameObject firstPlayerObject = round.allPlayerObjects[3];
                    for (int i = 0; i < difference; i++)
                    {
                        uint newId = starting + (uint)i;
                        //GameObject copy = GameObject.Instantiate(playerPrefab, firstPlayerObject.transform.parent);
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
                        //round.gameStats.allPlayerStats[originalLength + i] = new PlayerStats();
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
            //MainClass.ReadSettingsFromFile();
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
