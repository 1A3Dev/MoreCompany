using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace MoreCompany
{
    [HarmonyPatch(typeof(AudioMixer), "SetFloat")]
    public static class AudioMixerSetFloatPatch
    {
        public static bool Prefix(string name, ref float value)
        {
            return true;
        }
    }
}
