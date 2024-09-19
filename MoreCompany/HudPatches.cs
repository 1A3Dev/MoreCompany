using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MoreCompany.Cosmetics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MoreCompany
{
	[HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
	public static class HudChatPatch
	{
		public static void Prefix(HUDManager __instance, ref string chatMessage, string nameOfUserWhoTyped = "")
		{
			if (__instance.lastChatMessage == chatMessage)
			{
				return;
			}
            StartOfRound startOfRound = Object.FindObjectOfType<StartOfRound>();
			StringBuilder stringBuilder = new StringBuilder(chatMessage);
			for (int i = 0; i < MainClass.newPlayerCount; i++)
			{
				string targetReplacement = $"[playerNum{i}]";
				string replacement = startOfRound.allPlayerScripts[i].playerUsername;
				stringBuilder.Replace(targetReplacement, replacement);
			}
			chatMessage = stringBuilder.ToString();
		}
	}

    [HarmonyPatch]
    public static class MenuManagerLogoOverridePatch
    {
        public static List<TMP_InputField> inputFields = new List<TMP_InputField>();

        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(MenuManager __instance)
        {
            MainClass.ReadSettingsFromFile();

            // Add the MoreCompany logo
            try
            {
                Sprite logoImage = Sprite.Create(MainClass.mainLogo, new Rect(0, 0, MainClass.mainLogo.width, MainClass.mainLogo.height), new Vector2(0.5f, 0.5f));

                GameObject parent = __instance.transform.parent.gameObject;
                Transform mainLogo = parent.transform.Find("MenuContainer/MainButtons/HeaderImage");
                if (mainLogo != null)
                {
                    mainLogo.gameObject.GetComponent<Image>().sprite = logoImage;
                }
                Transform loadingScreen = parent.transform.Find("MenuContainer/LoadingScreen");
                if (loadingScreen != null)
                {
                    loadingScreen.localScale = new Vector3(1.02f, 1.06f, 1.02f);
                    Transform loadingLogo = loadingScreen.Find("Image");
                    if (loadingLogo != null)
                    {
                        loadingLogo.GetComponent<Image>().sprite = logoImage;
                    }
                }
            }
			catch (Exception e)
			{
                //MainClass.StaticLogger.LogError(e);
			}

            CosmeticRegistry.SpawnCosmeticGUI(true);
        }

        private static void CreateCrewCountInput(Transform parent)
        {
            GameObject createdCrewUI = GameObject.Instantiate(MainClass.crewCountUI, parent);
            RectTransform rectTransform = createdCrewUI.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(96.9f, -70f, -6.7f);

            TMP_InputField inputField = createdCrewUI.transform.Find("InputField (TMP)").GetComponent<TMP_InputField>();
            inputField.characterLimit = 3;
            inputField.text = MainClass.newPlayerCount.ToString();
            inputFields.Add(inputField);
            inputField.onSubmit.AddListener(s => {
                UpdateTextBox(inputField, s);
            });
            inputField.onDeselect.AddListener(s => {
                UpdateTextBox(inputField, s);
            });
        }

        public static void UpdateTextBox(TMP_InputField inputField, string s)
        {
            if (inputField.text == MainClass.newPlayerCount.ToString())
                return;

            if (int.TryParse(s, out int result))
            {
                int originalCount = MainClass.newPlayerCount;
                MainClass.newPlayerCount = Mathf.Clamp(result, MainClass.minPlayerCount, MainClass.maxPlayerCount);
                foreach (TMP_InputField field in inputFields)
                    field.text = MainClass.newPlayerCount.ToString();
                MainClass.SaveSettingsToFile();
                if (MainClass.newPlayerCount != originalCount)
                    MainClass.StaticLogger.LogInfo($"Changed Crew Count: {MainClass.newPlayerCount}");
            }
            else if (s.Length != 0)
            {
                foreach (TMP_InputField field in inputFields)
                {
                    field.text = MainClass.newPlayerCount.ToString();
                    field.caretPosition = 1;
                }
            }
        }
    }


    [HarmonyPatch(typeof(QuickMenuManager), "Start")]
    public static class QuickmenuVisualInjectPatch
    {
        public static void Postfix(QuickMenuManager __instance)
        {
            CosmeticRegistry.SpawnCosmeticGUI(false);
        }
    }
}
