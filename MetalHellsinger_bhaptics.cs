using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using MyBhapticsTactsuit;
using System.Threading;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MetalHellsinger_bhaptics
{
    [BepInPlugin("org.bepinex.plugins.MetalHellsinger_bhaptics", "MetalHellsinger bhaptics integration", "1.0")]
    public class Plugin : BasePlugin
    {
        public static Plugin _instance;
        public static TactsuitVR? tactsuitVr;
        public static string configPath = Directory.GetCurrentDirectory() + "\\UserData\\";
        public static bool dualWield = false;


        public override void Load()
        {
            _instance = this;
            // Plugin startup logic
            Log.LogMessage("Plugin MetalHellsinger_bhaptics is loaded!");
            tactsuitVr = new TactsuitVR();
            // one startup heartbeat so you know the vest works correctly
            tactsuitVr.PlaybackHaptics("HeartBeat");
            //init protube if any
            //InitializeProTube();
            // patch all functions
            var harmony = new Harmony("bhaptics.patch.metalhellsinger");
            harmony.PatchAll();
        }

        public static void saveChannel(string channelName, string proTubeName)
        {
            string fileName = configPath + channelName + ".pro";
            File.WriteAllText(fileName, proTubeName, Encoding.UTF8);
        }

        public static string readChannel(string channelName)
        {
            string fileName = configPath + channelName + ".pro";
            if (!File.Exists(fileName)) return "";
            return File.ReadAllText(fileName, Encoding.UTF8);
        }

        public static void dualWieldSort()
        {
            ForceTubeVRInterface.FTChannelFile myChannels = JsonConvert.DeserializeObject<ForceTubeVRInterface.FTChannelFile>(ForceTubeVRInterface.ListChannels());
            var pistol1 = myChannels.channels.pistol1;
            var pistol2 = myChannels.channels.pistol2;
            if ((pistol1.Count > 0) && (pistol2.Count > 0))
            {
                dualWield = true;
                _instance.Log.LogMessage("Two ProTube devices detected, player is dual wielding.");
                if ((readChannel("rightHand") == "") || (readChannel("leftHand") == ""))
                {
                    _instance.Log.LogMessage("No configuration files found, saving current right and left hand pistols.");
                    saveChannel("rightHand", pistol1[0].name);
                    saveChannel("leftHand", pistol2[0].name);
                }
                else
                {
                    string rightHand = readChannel("rightHand");
                    string leftHand = readChannel("leftHand");
                    _instance.Log.LogMessage("Found and loaded configuration. Right hand: " + rightHand + ", Left hand: " + leftHand);
                    // Channels 4 and 5 are ForceTubeVRChannel.pistol1 and pistol2
                    ForceTubeVRInterface.ClearChannel(4);
                    ForceTubeVRInterface.ClearChannel(5);
                    ForceTubeVRInterface.AddToChannel(4, rightHand);
                    ForceTubeVRInterface.AddToChannel(5, leftHand);
                }
            }
            else
            {
                _instance.Log.LogMessage("SINGLE WIELD");
            }
        }

        private async void InitializeProTube()
        {
            Log.LogMessage("Initializing ProTube gear...");
            await ForceTubeVRInterface.InitAsync(true);
            Thread.Sleep(10000);
            dualWieldSort();
        }
    }

    [HarmonyPatch(typeof(Player), "DieSoftDeath")]
    public class bhaptics_OnDeath
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            Plugin.tactsuitVr.PlaybackHaptics("Death");
        }
    }
    /*
    [HarmonyPatch(typeof(Player), "OnHurt", new Type[] { })]
    public class bhaptics_OnHurt
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            var angleShift = TactsuitVR.getAngleAndShift(__instance.HitTransforms.parent, __instance.DamageableComponent.HitObject.transform.position);
            Plugin.tactsuitVr.PlayBackHit("BulletHit", angleShift.Key, angleShift.Value);
        }
    }
    */
    [HarmonyPatch(typeof(Player), "PickUpHealth")]
    public class bhaptics_heal
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            Plugin.tactsuitVr.PlaybackHaptics("Heal");
        }
    }
}

