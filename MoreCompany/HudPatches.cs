using System.Text;
using HarmonyLib;
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
}
