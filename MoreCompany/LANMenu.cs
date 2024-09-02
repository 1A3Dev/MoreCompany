using HarmonyLib;
using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace MoreCompany
{
    public class LANMenu : MonoBehaviour
    {
        public static void InitializeMenu()
        {
            var startLAN_button = GameObject.Find("Canvas/MenuContainer/MainButtons/StartLAN");
            if (startLAN_button != null)
            {
                MainClass.StaticLogger.LogInfo("LANMenu startLAN Patched");
                startLAN_button.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
                startLAN_button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    GameObject.Find("Canvas/MenuContainer/LobbyJoinSettings").gameObject.SetActive(true);
                });
            }


            if (GameObject.Find("Canvas/MenuContainer/LobbyJoinSettings") != null) return;

            var menuContainer = GameObject.Find("Canvas/MenuContainer");
            if (menuContainer == null) return;
            var LobbyHostSettings = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings");
            if (LobbyHostSettings == null) return;

            // Clone LobbyHostSettings
            GameObject menu = Instantiate(LobbyHostSettings, LobbyHostSettings.transform.position, LobbyHostSettings.transform.rotation, menuContainer.transform);
            menu.name = "LobbyJoinSettings";

            var lanSubMenu = menu.transform.Find("HostSettingsContainer");
            if (lanSubMenu != null)
            {
                lanSubMenu.name = "JoinSettingsContainer";
                lanSubMenu.transform.Find("LobbyHostOptions").name = "LobbyJoinOptions";

                Destroy(menu.transform.Find("ChallengeLeaderboard").gameObject);
                Destroy(menu.transform.Find("FilesPanel").gameObject);
                Destroy(lanSubMenu.transform.Find("LobbyJoinOptions/OptionsNormal").gameObject);
                Destroy(lanSubMenu.transform.Find("LobbyJoinOptions/LANOptions/AllowRemote").gameObject);
                Destroy(lanSubMenu.transform.Find("LobbyJoinOptions/LANOptions/Local").gameObject);

                var headerText = lanSubMenu.transform.Find("LobbyJoinOptions/LANOptions/Header");
                if (headerText != null)
                    headerText.GetComponent<TextMeshProUGUI>().text = "Join LAN Server:";

                var addressField = lanSubMenu.transform.Find("LobbyJoinOptions/LANOptions/ServerNameField");
                if (addressField != null)
                {
                    addressField.transform.localPosition = new Vector3(0f, 15f, -6.5f);
                    addressField.gameObject.SetActive(true);
                }

                TMP_InputField ip_field = addressField.GetComponent<TMP_InputField>();
                if (ip_field != null)
                {
                    TextMeshProUGUI ip_placeholder = ip_field.placeholder.GetComponent<TextMeshProUGUI>();
                    ip_placeholder.text = ES3.Load("LANIPAddress", "LCGeneralSaveData", "127.0.0.1");

                    Button confirmBut = lanSubMenu.transform.Find("Confirm")?.GetComponent<Button>();
                    if (confirmBut != null)
                    {
                        confirmBut.onClick = new Button.ButtonClickedEvent();
                        confirmBut.onClick.AddListener(() =>
                        {
                            string IP_Address = "127.0.0.1";
                            if (ip_field.text != "")
                                IP_Address = ip_field.text;
                            else
                                IP_Address = ip_placeholder.text;
                            ES3.Save("LANIPAddress", IP_Address, "LCGeneralSaveData");
                            GameObject.Find("Canvas/MenuContainer/LobbyJoinSettings").gameObject.SetActive(false);
                            MainClass.newPlayerCount = 4;
                            NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address = IP_Address;
                            MainClass.StaticLogger.LogInfo($"Listening to LAN server: {IP_Address}");
                            GameObject.Find("MenuManager").GetComponent<MenuManager>().StartAClient();
                        });
                    }
                }

                lanSubMenu.transform.Find("LobbyJoinOptions/LANOptions").gameObject.SetActive(true);
            }
        }
    }

    [HarmonyPatch(typeof(GameNetworkManager), "OnLocalClientConnectionDisapproved")]
    public static class ConnectionDisapprovedPatch
    {
        private static int crewSizeMismatch = 0;
        private static IEnumerator delayedReconnect()
        {
            yield return new WaitForSeconds(0.5f);
            GameObject.Find("MenuManager").GetComponent<MenuManager>().StartAClient();
            yield break;
        }

        private static void Prefix(ref GameNetworkManager __instance, ulong clientId)
        {
            crewSizeMismatch = 0;
            if (__instance.disableSteam)
            {
                try
                {
                    if (!string.IsNullOrEmpty(NetworkManager.Singleton.DisconnectReason) && NetworkManager.Singleton.DisconnectReason.StartsWith("Crew size mismatch!"))
                    {
                        crewSizeMismatch = int.Parse(NetworkManager.Singleton.DisconnectReason.Split("Their size: ")[1].Split(". ")[0]);
                    }
                }
                catch { }
            }
        }
        private static void Postfix(ref GameNetworkManager __instance, ulong clientId)
        {
            if (__instance.disableSteam && crewSizeMismatch != 0)
            {
                if (MainClass.newPlayerCount != crewSizeMismatch)
                {
                    GameObject.Find("MenuManager").GetComponent<MenuManager>().menuNotification.SetActive(false);

                    // Automatic Reconnect
                    Object.FindObjectOfType<MenuManager>().SetLoadingScreen(isLoading: true);
                    MainClass.newPlayerCount = crewSizeMismatch;
                    __instance.StartCoroutine(delayedReconnect());
                }

                crewSizeMismatch = 0;
            }
        }
    }
}
