using System;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Object = System.Object;

namespace MoreCompany
{
    public class DebugCommandRegistry
    {
        public static bool commandEnabled = false;
        
        public static void HandleCommand(String[] args)
        {
            if (!commandEnabled)
            {
                return;
            }
            StartOfRound startOfRound = UnityEngine.Object.FindObjectOfType<StartOfRound>();
        }
    }
}
