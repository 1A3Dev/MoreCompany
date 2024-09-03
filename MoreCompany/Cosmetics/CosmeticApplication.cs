using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreCompany.Cosmetics
{
    public enum ParentType
    {
        Player,
        DeadBody,
        MaskedEnemy,
    }

    public class CosmeticApplication : MonoBehaviour
    {
        public bool detachedHead = false;

        public ParentType parentType;

        public Transform head;
        public Transform hip;
        public Transform lowerArmRight;
        public Transform shinLeft;
        public Transform shinRight;
        public Transform chest;
        public List<CosmeticInstance> spawnedCosmetics = new List<CosmeticInstance>();
        public List<string> spawnedCosmeticsIds = new List<string>();

        public void Awake()
        {
            Transform spine = transform.Find("spine") ?? transform;
            head = spine.Find("spine.001").Find("spine.002").Find("spine.003").Find("spine.004");
            chest = spine.Find("spine.001").Find("spine.002").Find("spine.003");
            lowerArmRight = spine.Find("spine.001").Find("spine.002").Find("spine.003").Find("shoulder.R").Find("arm.R_upper").Find("arm.R_lower");
            hip = spine;
            shinLeft = spine.Find("thigh.L").Find("shin.L");
            shinRight = spine.Find("thigh.R").Find("shin.R");

            RefreshAllCosmeticPositions();
        }

        private void OnDisable()
        {
            foreach (var spawnedCosmetic in spawnedCosmetics)
            {
                spawnedCosmetic.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (spawnedCosmetics.Count > 0)
            {
                if (parentType == ParentType.Player)
                {
                    PlayerControllerB playerController = transform.GetComponentInParent<PlayerControllerB>();
                    UpdateAllCosmeticVisibilities(playerController != null && (int)playerController.playerClientId == StartOfRound.Instance.thisClientPlayerId);
                }
                else if (parentType == ParentType.MaskedEnemy)
                {
                    UpdateAllCosmeticVisibilities();

                    MaskedPlayerEnemy maskedEnemy = transform.GetComponentInParent<MaskedPlayerEnemy>();
                    if (maskedEnemy != null)
                    {
                        maskedEnemy.skinnedMeshRenderers = maskedEnemy.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                        maskedEnemy.meshRenderers = maskedEnemy.gameObject.GetComponentsInChildren<MeshRenderer>();
                    }
                }
                else
                {
                    UpdateAllCosmeticVisibilities();
                }
            }
        }

        public void ClearCosmetics()
        {
            foreach (var spawnedCosmetic in spawnedCosmetics)
            {
                GameObject.Destroy(spawnedCosmetic.gameObject);
            }
            spawnedCosmetics.Clear();
            spawnedCosmeticsIds.Clear();
        }
        
        public bool ApplyCosmetic(string cosmeticId, bool startEnabled)
        {
            if (CosmeticRegistry.cosmeticInstances.ContainsKey(cosmeticId) && !spawnedCosmeticsIds.Contains(cosmeticId))
            {
                CosmeticInstance cosmeticInstance = CosmeticRegistry.cosmeticInstances[cosmeticId];

                if (startEnabled && cosmeticInstance.cosmeticType == CosmeticType.HAT && detachedHead) return false;

                GameObject cosmeticInstanceGameObject = GameObject.Instantiate(cosmeticInstance.gameObject);
                cosmeticInstanceGameObject.SetActive(startEnabled);
                CosmeticInstance cosmeticInstanceBehavior = cosmeticInstanceGameObject.GetComponent<CosmeticInstance>();
                spawnedCosmetics.Add(cosmeticInstanceBehavior);
                ParentCosmetic(cosmeticInstanceBehavior);
                spawnedCosmeticsIds.Add(cosmeticId);
                return true;
            }

            return false;
        }

        public void UpdateAllCosmeticVisibilities(bool isLocalPlayer = false)
        {
            bool isActive = false;
            if (parentType == ParentType.Player)
            {
                isActive = MainClass.cosmeticsSyncOther.Value && !isLocalPlayer;
                MainClass.StaticLogger.LogInfo("UpdateAllCosmeticVisibilities: PlayerControllerB");
            }
            else if (parentType == ParentType.DeadBody)
            {
                isActive = MainClass.cosmeticsDeadBodies.Value;
                MainClass.StaticLogger.LogInfo("UpdateAllCosmeticVisibilities: DeadBodyInfo");
            }
            else if (parentType == ParentType.MaskedEnemy)
            {
                isActive = MainClass.cosmeticsMaskedEnemy.Value;
                MainClass.StaticLogger.LogInfo("UpdateAllCosmeticVisibilities: MaskedPlayerEnemy");
            }

            foreach (var spawnedCosmetic in spawnedCosmetics)
            {
                if (spawnedCosmetic.cosmeticType == CosmeticType.HAT && detachedHead) continue;
                spawnedCosmetic.gameObject.SetActive(isActive);
            }
        }

        public void RefreshAllCosmeticPositions()
        {
            foreach (var spawnedCosmetic in spawnedCosmetics)
            {
                ParentCosmetic(spawnedCosmetic);
            }
        }

        private bool ParentCosmetic(CosmeticInstance cosmeticInstance)
        {
            Transform targetTransform = null;
            switch (cosmeticInstance.cosmeticType)
            {
                case CosmeticType.HAT:
                    targetTransform = head;
                    break;
                case CosmeticType.R_LOWER_ARM:
                    targetTransform = lowerArmRight;
                    break;
                case CosmeticType.HIP:
                    targetTransform = hip;
                    break;
                case CosmeticType.L_SHIN:
                    targetTransform = shinLeft;
                    break;
                case CosmeticType.R_SHIN:
                    targetTransform = shinRight;
                    break;
                case CosmeticType.CHEST:
                    targetTransform = chest;
                    break;
            }

            if (targetTransform == null)
            {
                MainClass.StaticLogger.LogError("Failed to find transform of type: " + cosmeticInstance.cosmeticType);
                return false;
            }

            cosmeticInstance.transform.position = targetTransform.position;
            cosmeticInstance.transform.rotation = targetTransform.rotation;
            cosmeticInstance.transform.parent = targetTransform;
            return true;
        }
    }
}
