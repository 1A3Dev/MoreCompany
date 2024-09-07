using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using MoreCompany.Utils;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

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
        public static int minPlayerCount = 2;
        public static int maxPlayerCount = 64;
        public static int actualPlayerCount = 32;
        public static int newPlayerCount = Mathf.Max(4, actualPlayerCount);

        public static ConfigFile StaticConfig;
        public static ConfigEntry<bool> cosmeticsDeadBodies;
        public static ConfigEntry<bool> cosmeticsMaskedEnemy;
        public static ConfigEntry<bool> cosmeticsSyncOther;
        public static ConfigEntry<bool> defaultCosmetics;
        public static ConfigEntry<bool> cosmeticsPerProfile;
        public static ConfigEntry<string> disabledCosmetics;

        public static ConfigEntry<int> lanDefaultPort;
        public static ConfigEntry<int> lanDiscoveryPort;

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

            cosmeticsSyncOther = StaticConfig.Bind("Cosmetics", "Show Cosmetics", true, "Should you be able to see cosmetics of other players?"); // This is the one linked to the UI button
            cosmeticsDeadBodies = StaticConfig.Bind("Cosmetics", "Show On Dead Bodies", true, "Should you be able to see cosmetics on dead bodies?");
            cosmeticsMaskedEnemy = StaticConfig.Bind("Cosmetics", "Show On Masked Enemy", true, "Should you be able to see cosmetics on the masked enemy?");
            defaultCosmetics = StaticConfig.Bind("Cosmetics", "Default Cosmetics", true, "Should the default cosmetics be enabled?");
            cosmeticsPerProfile = StaticConfig.Bind("Cosmetics", "Per Profile Cosmetics", false, "Should the cosmetics be saved per-profile?");
            disabledCosmetics = StaticConfig.Bind("Cosmetics", "Disabled Cosmetics", "", "Comma separated list of cosmetics to disable");
            AcceptableValueRange<int> acceptablePortRange = new AcceptableValueRange<int>(1, 65535);
            lanDefaultPort = StaticConfig.Bind("LAN", "Default Port", 7777, new ConfigDescription("The port used for hosting a lobby", acceptablePortRange));
            lanDiscoveryPort = StaticConfig.Bind("LAN", "Discovery Port", 47777, new ConfigDescription("The port used for lobby discovery", acceptablePortRange));

            cosmeticsSyncOther.SettingChanged += (sender, args) => {
                foreach (PlayerControllerB playerController in FindObjectsByType<PlayerControllerB>(FindObjectsSortMode.None))
                {
                    Transform cosmeticRoot = playerController.transform.Find("ScavengerModel").Find("metarig");
                    if (cosmeticRoot == null) continue;
                    CosmeticApplication cosmeticApplication = cosmeticRoot.gameObject.GetComponent<CosmeticApplication>();
                    if (cosmeticApplication == null) continue;

                    cosmeticApplication.UpdateAllCosmeticVisibilities((int)playerController.playerClientId == StartOfRound.Instance.thisClientPlayerId);
                }
            };

            cosmeticsDeadBodies.SettingChanged += (sender, args) => {
                foreach (PlayerControllerB playerController in FindObjectsByType<PlayerControllerB>(FindObjectsSortMode.None))
                {
                    Transform cosmeticRoot = playerController.deadBody.transform;
                    if (cosmeticRoot == null) continue;
                    CosmeticApplication cosmeticApplication = cosmeticRoot.GetComponent<CosmeticApplication>();
                    if (cosmeticApplication == null) continue;

                    cosmeticApplication.UpdateAllCosmeticVisibilities(false);
                }
            };

            cosmeticsMaskedEnemy.SettingChanged += (sender, args) => {
                foreach (MaskedPlayerEnemy maskedEnemy in FindObjectsByType<MaskedPlayerEnemy>(FindObjectsSortMode.None))
                {
                    Transform cosmeticRoot = maskedEnemy.transform.Find("ScavengerModel").Find("metarig");
                    if (cosmeticRoot == null) continue;
                    CosmeticApplication cosmeticApplication = cosmeticRoot.GetComponent<CosmeticApplication>();
                    if (cosmeticApplication == null) continue;

                    cosmeticApplication.UpdateAllCosmeticVisibilities(false);

                    maskedEnemy.skinnedMeshRenderers = maskedEnemy.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                    maskedEnemy.meshRenderers = maskedEnemy.gameObject.GetComponentsInChildren<MeshRenderer>();
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
                actualPlayerCount = lobby.MaxMembers;
                newPlayerCount = Mathf.Max(4, actualPlayerCount);
            };

            SteamMatchmaking.OnLobbyEntered += (lobby) =>
            {
                actualPlayerCount = lobby.MaxMembers;
                newPlayerCount = Mathf.Max(4, actualPlayerCount);
            };

            StaticLogger.LogInfo("Loading SETTINGS...");

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

            if (defaultCosmetics.Value)
            {
                StaticLogger.LogInfo("Loading DEFAULT COSMETICS...");
                AssetBundle cosmeticsBundle = BundleUtilities.LoadBundleFromInternalAssembly("morecompany.cosmetics", Assembly.GetExecutingAssembly());
                CosmeticRegistry.LoadCosmeticsFromBundle(cosmeticsBundle, "morecompany.cosmetics");
                cosmeticsBundle.Unload(false);
            }

            StaticLogger.LogInfo("Loading USER COSMETICS...");
            RecursiveCosmeticLoad(Paths.PluginPath);

            AssetBundle bundle = BundleUtilities.LoadBundleFromInternalAssembly("morecompany.assets", Assembly.GetExecutingAssembly());
            LoadAssets(bundle);

            StaticLogger.LogInfo("Loaded MoreCompany FULLY");

            if (Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"))
            {
                Compatibility.LobbyCompatibility.Init();
            }
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
            StartOfRound round = StartOfRound.Instance;
            if (round.allPlayerObjects.Length != newPlayerCount)
            {
                uint starting = 10000;

                int originalLength = round.allPlayerObjects.Length;
                int difference = newPlayerCount - originalLength;

                StaticLogger.LogInfo($"Resizing arrays from {originalLength} to {newPlayerCount} with difference of {difference}");

                Array.Resize(ref round.allPlayerObjects, newPlayerCount);
                Array.Resize(ref round.allPlayerScripts, newPlayerCount);
                Array.Resize(ref round.gameStats.allPlayerStats, newPlayerCount);
                Array.Resize(ref round.playerSpawnPositions, newPlayerCount);

                if (difference > 0)
                {
                    GameObject firstPlayerObject = round.allPlayerObjects[originalLength - 1];
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

                        copy.name = $"Player ({originalLength + i})";

                        PlayerControllerB newPlayerScript = copy.GetComponentInChildren<PlayerControllerB>();

                        // Reset
                        newPlayerScript.playerClientId = (ulong)(originalLength + i);
                        newPlayerScript.playerUsername = $"Player #{newPlayerScript.playerClientId}";
                        newPlayerScript.isPlayerControlled = false;
                        newPlayerScript.isPlayerDead = false;

                        newPlayerScript.DropAllHeldItems(false, false);
                        newPlayerScript.TeleportPlayer(round.notSpawnedPosition.position, false, 0f, false, true);
                        UnlockableSuit.SwitchSuitForPlayer(newPlayerScript, 0, false);

                        // Set new player object
                        round.allPlayerObjects[originalLength + i] = copy;
                        round.gameStats.allPlayerStats[originalLength + i] = new PlayerStats();
                        round.allPlayerScripts[originalLength + i] = newPlayerScript;
                        round.playerSpawnPositions[originalLength + i] = round.playerSpawnPositions[3];

                        StartOfRound.Instance.mapScreen.radarTargets.Add(new TransformAndName(newPlayerScript.transform, newPlayerScript.playerUsername, false));
                    }
                }
            }

            foreach (PlayerControllerB newPlayerScript in StartOfRound.Instance.allPlayerScripts) // Fix for billboards showing as Player # with no number in LAN (base game issue)
            {
                newPlayerScript.usernameBillboardText.text = newPlayerScript.playerUsername;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "Start")]
    public static class PlayerControllerBStartPatch
    {
        public static void Postfix(ref PlayerControllerB __instance)
        {
            Collider[] nearByPlayers = new Collider[MainClass.newPlayerCount];
            ReflectionUtils.SetFieldValue(__instance, "nearByPlayers", nearByPlayers);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SendNewPlayerValuesServerRpc")]
    public static class SendNewPlayerValuesServerRpcPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundCount = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundCount && instruction.ToString() == "callvirt virtual void System.Collections.Generic.List<ulong>::Add(ulong item)")
                    {
                        foundCount = true;
                    }
                    else if (foundCount && instruction.ToString() == "ldc.i4.4 NULL")
                    {
                        alreadyReplaced = true;
                        CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MainClass), "newPlayerCount"));
                        newInstructions.Add(codeInstruction);
                        continue;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) MainClass.StaticLogger.LogWarning("SendNewPlayerValuesServerRpcPatch failed to replace newPlayerCount");

            return newInstructions.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(HUDManager), "SyncAllPlayerLevelsServerRpc", new Type[] {})]
    public static class SyncAllPlayerLevelsPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            int alreadyReplaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.ToString() == "ldc.i4.4 NULL")
                {
                    alreadyReplaced++;
                    CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MainClass), "newPlayerCount"));
                    newInstructions.Add(codeInstruction);
                    continue;
                }

                newInstructions.Add(instruction);
            }

            if (alreadyReplaced != 2) MainClass.StaticLogger.LogWarning($"SyncAllPlayerLevelsPatch failed to replace newPlayerCount: {alreadyReplaced}/2");

            return newInstructions.AsEnumerable();
        }
    }

    [HarmonyPatch]
    public static class SyncShipUnlockablesPatch
    {
        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesServerRpc")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ServerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundCount = false;
            int alreadyReplaced = 0;
            foreach (var instruction in instructions)
            {
                if (alreadyReplaced != 2)
                {
                    if (!foundCount && instruction.ToString() == "callvirt bool Unity.Netcode.NetworkManager::get_IsHost()")
                    {
                        foundCount = true;
                    }
                    else if (instruction.ToString().StartsWith("ldc.i4.4 NULL"))
                    {
                        alreadyReplaced++;
                        CodeInstruction codeInstruction = new CodeInstruction(instruction);
                        codeInstruction.opcode = OpCodes.Ldsfld;
                        codeInstruction.operand = AccessTools.Field(typeof(MainClass), "newPlayerCount");
                        newInstructions.Add(codeInstruction);
                        continue;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (alreadyReplaced != 2) MainClass.StaticLogger.LogWarning($"SyncShipUnlockablesServerRpc failed to replace newPlayerCount: {alreadyReplaced}/2");

            return newInstructions.AsEnumerable();
        }

        [HarmonyPatch(typeof(StartOfRound), "SyncShipUnlockablesClientRpc")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ClientTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundCount = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundCount && instruction.ToString() == "callvirt void UnityEngine.Renderer::set_sharedMaterial(UnityEngine.Material value)")
                    {
                        foundCount = true;
                    }
                    else if (foundCount && instruction.ToString() == "ldc.i4.4 NULL")
                    {
                        alreadyReplaced = true;
                        CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MainClass), "newPlayerCount"));
                        newInstructions.Add(codeInstruction);
                        continue;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) MainClass.StaticLogger.LogWarning("SyncShipUnlockablesClientRpc failed to replace newPlayerCount");

            return newInstructions.AsEnumerable();
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

    [HarmonyPatch(typeof(GameNetworkManager), "LobbyDataIsJoinable")]
    public static class LobbyDataJoinablePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundCount = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundCount && instruction.ToString() == "call int Steamworks.Data.Lobby::get_MemberCount()")
                    {
                        foundCount = true;
                    }
                    else if (foundCount && instruction.ToString() == "ldc.i4.4 NULL")
                    {
                        alreadyReplaced = true;
                        CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MainClass), "maxPlayerCount"));
                        newInstructions.Add(codeInstruction);
                        continue;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) MainClass.StaticLogger.LogWarning("LobbyDataIsJoinable failed to replace maxPlayerCount");

            return newInstructions.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(SteamMatchmaking), "CreateLobbyAsync")]
    public static class LobbyThingPatch
    {
        public static void Prefix(ref int maxMembers)
        {
            maxMembers = MainClass.actualPlayerCount;
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), "SetConnectionDataBeforeConnecting")]
    public static class ConnectionDataPatch
    {
        public static void Postfix(ref GameNetworkManager __instance)
        {
            if (__instance.disableSteam)
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(__instance.gameVersionNum + "," + MainClass.actualPlayerCount);
            }
            else
            {
                NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(__instance.gameVersionNum + "," + (ulong)SteamClient.SteamId + ",morecompany");
            }
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), "ConnectionApproval")]
    public static class ConnectionApproval
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var newInstructions = new List<CodeInstruction>();
            bool foundCount = false;
            bool alreadyReplaced = false;
            foreach (var instruction in instructions)
            {
                if (!alreadyReplaced)
                {
                    if (!foundCount && instruction.ToString() == "ldfld int GameNetworkManager::connectedPlayers")
                    {
                        foundCount = true;
                    }
                    else if (foundCount && instruction.ToString() == "ldc.i4.4 NULL")
                    {
                        alreadyReplaced = true;
                        CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MainClass), "actualPlayerCount"));
                        newInstructions.Add(codeInstruction);
                        continue;
                    }
                }

                newInstructions.Add(instruction);
            }

            if (!alreadyReplaced) MainClass.StaticLogger.LogWarning("ConnectionApproval failed to replace actualPlayerCount");

            return newInstructions.AsEnumerable();
        }

        private static void Postfix(ref GameNetworkManager __instance, ref NetworkManager.ConnectionApprovalRequest request, ref NetworkManager.ConnectionApprovalResponse response)
        {
            // Crew Size Mismatch
            if (response.Approved)
            {
                string @string = Encoding.ASCII.GetString(request.Payload);
                string[] array = @string.Split(",");
                int joinerCrewSize = 4;
                if (!string.IsNullOrEmpty(@string))
                {
                    if (__instance.disableSteam)
                    {
                        if (array.Length > 1 && int.TryParse(array[1], out int tmpCrewSize) && tmpCrewSize > 0)
                        {
                            joinerCrewSize = tmpCrewSize;
                        }
                    }
                    else
                    {
                        if (array.Length > 2 && array[2] == "morecompany")
                        {
                            joinerCrewSize = MainClass.actualPlayerCount;
                        }
                    }
                }

                if (response.Approved && MainClass.actualPlayerCount > 4 && MainClass.actualPlayerCount != joinerCrewSize)
                {
                    response.Reason = $"Crew size mismatch! Their size: {MainClass.actualPlayerCount}. Your size: {joinerCrewSize}";
                    response.Approved = false;
                }
            }
        }
    }

    [HarmonyPatch]
    public static class TogglePlayerObjectsPatch
    {
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        private static void ConnectClientToPlayerObject()
        {
            foreach (PlayerControllerB playerControllerB in StartOfRound.Instance.allPlayerScripts)
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
        private static void OnPlayerConnectedClientRpc(StartOfRound __instance, ulong clientId, int connectedPlayers, ulong[] connectedPlayerIdsOrdered, int assignedPlayerObjectId, int serverMoneyAmount, int levelID, int profitQuota, int timeUntilDeadline, int quotaFulfilled, int randomSeed, bool isChallenge)
        {
            __instance.allPlayerScripts[assignedPlayerObjectId].gameObject.SetActive(true);
            SoundManager.Instance.playerVoiceVolumes[assignedPlayerObjectId] = SoundManagerPatch.defaultPlayerVolume;
        }

        [HarmonyPatch(typeof(StartOfRound), "OnPlayerDC")]
        [HarmonyPostfix]
        private static void OnPlayerDC(StartOfRound __instance, int playerObjectNumber, ulong clientId)
        {
            __instance.allPlayerScripts[playerObjectNumber].gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "OnEnable")]
        [HarmonyPostfix]
        private static void SLM_OnEnable(SteamLobbyManager __instance)
        {
            if (GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam)
            {
                __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter Server IP";
            }
            else
            {
                __instance.serverTagInputField.placeholder.gameObject.GetComponent<TextMeshProUGUI>().text = "Enter tag or id...";
            }
        }

        [HarmonyPatch(typeof(SteamLobbyManager), "RefreshServerListButton")]
        [HarmonyPrefix]
        private static bool RefreshServerListButton(SteamLobbyManager __instance)
        {
            if (__instance.serverTagInputField.text != string.Empty)
            {
                if (GameNetworkManager.Instance.disableSteam)
                {
                    if (LANMenu.TryParseIpAndPort(__instance.serverTagInputField.text, out IPAddress ipAddress, out int port))
                    {
                        LANMenu.JoinLobbyByIP(ipAddress.ToString(), (ushort)port);
                    }
                }
                else if (ulong.TryParse(__instance.serverTagInputField.text, out ulong lobbyId) && lobbyId.ToString().Length >= 15 && lobbyId.ToString().Length <= 20)
                {
                    __instance.StartCoroutine(JoinLobby(__instance, lobbyId));
                    return false;
                }
            }

            return true;
        }

        internal static IEnumerator JoinLobby(SteamLobbyManager __instance, ulong lobbyId)
        {
            if (GameNetworkManager.Instance.waitingForLobbyDataRefresh)
            {
                yield break;
            }

            MainClass.StaticLogger.LogWarning("[JoinLobby] Attempting to find lobby by id: " + lobbyId);
            Task<Lobby?> joinTask = SteamMatchmaking.JoinLobbyAsync(lobbyId);
            yield return new WaitUntil(() => joinTask.IsCompleted);
            if (!joinTask.Result.HasValue)
            {
                MainClass.StaticLogger.LogWarning("[JoinLobby] Failed to find lobby by id: " + lobbyId);
                yield break;
            }
            Lobby lobby = joinTask.Result.Value;
            if (lobby.GetData("vers").IsNullOrWhiteSpace())
            {
                MainClass.StaticLogger.LogWarning("[JoinLobby] Failed to join lobby by id: " + lobbyId);
                yield break;
            }
            LobbySlot.JoinLobbyAfterVerifying(lobby, lobby.Id);
            MainClass.StaticLogger.LogWarning("[JoinLobby] Successfully found lobby by id: " + lobbyId);
            __instance.serverTagInputField.text = "";
        }

        private static string GetGlobalIPAddress(bool external = false)
        {
            if (!external)
            {
                return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
            }

            var url = "https://api.ipify.org/";
            var ip = "0.0.0.0";

            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 2000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                using StreamReader reader = new StreamReader(dataStream);
                ip = reader.ReadToEnd();
                reader.Close();
            }
            catch (Exception ex)
            {
                MainClass.StaticLogger.LogError(ex);
            }

            return ip;
        }

        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPostfix]
        private static void OpenQuickMenu(QuickMenuManager __instance)
        {
            TextMeshProUGUI CrewHeaderText = __instance.menuContainer.transform.Find("PlayerList/Image/Header").GetComponentInChildren<TextMeshProUGUI>();
            if (CrewHeaderText != null)
            {
                CrewHeaderText.text = $"CREW ({(StartOfRound.Instance?.connectedPlayersAmount ?? 0) + 1}):";
            }

            GameObject ResumeObj = __instance.menuContainer.transform.Find("MainButtons/Resume/")?.gameObject;
            if (ResumeObj != null)
            {
                GameObject LobbyCodeObj = GameObject.Find("CopyCurrentLobbyCode");
                if (LobbyCodeObj == null)
                {
                    LobbyCodeObj = UnityEngine.Object.Instantiate(ResumeObj.gameObject, ResumeObj.transform.parent);
                    LobbyCodeObj.name = "CopyCurrentLobbyCode";

                    TextMeshProUGUI LobbyCodeTextMesh = LobbyCodeObj.GetComponentInChildren<TextMeshProUGUI>();
                    string defaultText = GameNetworkManager.Instance.disableSteam ? "> Copy IP Address" : "> Copy Lobby Code";
                    LobbyCodeTextMesh.text = defaultText;

                    Button LobbyCodeButton = LobbyCodeObj.GetComponent<Button>();
                    LobbyCodeButton.onClick = new Button.ButtonClickedEvent();
                    LobbyCodeButton.onClick.AddListener(() => {
                        string lobbyId = "";
                        if (GameNetworkManager.Instance.disableSteam)
                        {
                            if (GameNetworkManager.Instance.isHostingGame)
                            {
                                lobbyId = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.ServerListenAddress;
                                if (lobbyId == "0.0.0.0")
                                {
                                    lobbyId = GetGlobalIPAddress();
                                }
                            }
                            else
                            {
                                lobbyId = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
                            }
                        }
                        else if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.currentLobby.HasValue)
                        {
                            lobbyId = GameNetworkManager.Instance.currentLobby.Value.Id.Value.ToString();
                        }
                        LoadLobbyListAndFilterPatch.CopyLobbyCodeToClipboard(lobbyId, LobbyCodeTextMesh, [defaultText, "Copied To Clipboard", "Invalid Code"]);
                    });
                }

                RectTransform rect = LobbyCodeObj.GetComponent<RectTransform>();
                if (rect == null)
                {
                    return;
                }

                GameObject DebugMenu = __instance.menuContainer.transform.Find("DebugMenu")?.gameObject;
                if (DebugMenu != null && DebugMenu.activeSelf)
                {
                    LobbyCodeObj.transform.SetParent(DebugMenu.transform);
                    rect.localPosition = new Vector3(125f, 185f, 0f);
                    rect.localScale = new Vector3(1f, 1f, 1f);
                }
                else
                {
                    LobbyCodeObj.transform.SetParent(ResumeObj.transform.parent);
                    RectTransform resumeRect = ResumeObj.GetComponent<RectTransform>();
                    rect.localPosition = resumeRect.localPosition + new Vector3(0f, 182f, 0f);
                    rect.localScale = resumeRect.localScale;
                }
            }
        }

    }
}
