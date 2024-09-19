using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using MoreCompany.Cosmetics;
using UnityEngine;

namespace MoreCompany
{
    [HarmonyPatch]
    public class CosmeticPatches
    {
        public static bool CloneCosmeticsToNonPlayer(Transform cosmeticRoot, int playerClientId, bool detachedHead = false, bool startEnabled = true)
        {
            if (MainClass.playerIdsAndCosmetics.ContainsKey(playerClientId))
            {
                List<string> cosmetics = MainClass.playerIdsAndCosmetics[playerClientId];
                CosmeticApplication cosmeticApplication = cosmeticRoot.GetComponent<CosmeticApplication>();
                if (cosmeticApplication)
                {
                    cosmeticApplication.ClearCosmetics();
                    GameObject.Destroy(cosmeticApplication);
                }

                cosmeticApplication = cosmeticRoot.gameObject.AddComponent<CosmeticApplication>();
                cosmeticApplication.detachedHead = detachedHead;
                foreach (var cosmetic in cosmetics)
                {
                    cosmeticApplication.ApplyCosmetic(cosmetic, startEnabled);
                }

                foreach (var cosmetic in cosmeticApplication.spawnedCosmetics)
                {
                    cosmetic.transform.localScale *= CosmeticRegistry.COSMETIC_PLAYER_SCALE_MULT;
                }

                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "DisablePlayerModel")]
        [HarmonyPostfix]
        public static void SpawnDeadBody(PlayerControllerB __instance, bool enable = false)
        {
            if (!enable)
            {
                Transform cosmeticRoot = __instance.DeadPlayerRagdoll.transform;
                if (cosmeticRoot == null) return;
                bool detachedHead = false;
                CloneCosmeticsToNonPlayer(cosmeticRoot, (int)__instance.playerClientId, detachedHead: detachedHead, startEnabled: MainClass.cosmeticsDeadBodies.Value);
            }
        }

        [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
        [HarmonyPatch(typeof(QuickMenuManager), "CloseQuickMenu")]
        [HarmonyPostfix]
        public static void ToggleQuickMenu(QuickMenuManager __instance)
        {
            if (CosmeticRegistry.menuIsInGame && CosmeticRegistry.cosmeticGUIGlobalScale != null)
            {
                CosmeticRegistry.cosmeticGUIGlobalScale.Find("ActivateButton").gameObject.SetActive(__instance.isMenuOpen);
                if (!__instance.isMenuOpen)
                {
                    CosmeticRegistry.cosmeticGUIGlobalScale.Find("CosmeticsScreen").gameObject.SetActive(false);
                }
            }
        }
    }
}
