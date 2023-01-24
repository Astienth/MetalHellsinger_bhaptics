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
        public static ManualLogSource Log;
        public static bool forceTubeConnected = false;


        public override void Load()
        {
            _instance = this;
            Log = base.Log;
            // Plugin startup logic
            Log.LogMessage("Plugin MetalHellsinger_bhaptics is loaded!");
            tactsuitVr = new TactsuitVR();
            // one startup heartbeat so you know the vest works correctly
            tactsuitVr.PlaybackHaptics("HeartBeat");
            
            //init protube if any
            InitializeProTube();
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
            if(pistol1.Count > 0)
            {
                forceTubeConnected = true;
            }
            if ((pistol1.Count > 0) && (pistol2.Count > 0))
            {
                dualWield = true;
                Log.LogMessage("Two ProTube devices detected, player is dual wielding.");
                if ((readChannel("rightHand") == "") || (readChannel("leftHand") == ""))
                {
                    Log.LogMessage("No configuration files found, saving current right and left hand pistols.");
                    saveChannel("rightHand", pistol1[0].name);
                    saveChannel("leftHand", pistol2[0].name);
                }
                else
                {
                    string rightHand = readChannel("rightHand");
                    string leftHand = readChannel("leftHand");
                    Log.LogMessage("Found and loaded configuration. Right hand: " + rightHand + ", Left hand: " + leftHand);
                    // Channels 4 and 5 are ForceTubeVRChannel.pistol1 and pistol2
                    ForceTubeVRInterface.ClearChannel(4);
                    ForceTubeVRInterface.ClearChannel(5);
                    ForceTubeVRInterface.AddToChannel(4, rightHand);
                    ForceTubeVRInterface.AddToChannel(5, leftHand);
                }
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

    [HarmonyPatch(typeof(WeaponAbilityController), "AttackTriggered")]
    public class bhaptics_OnShooting
    {
        [HarmonyPostfix]
        public static void Postfix(WeaponAbilityController __instance)
        {
            //bHaptics
            if (!Plugin.tactsuitVr.suitDisabled)
            {
                Plugin.tactsuitVr.PlaybackHaptics("RecoilVest_R");
                Plugin.tactsuitVr.PlaybackHaptics("RecoilArm_R");
            }
            if (Plugin.forceTubeConnected)
            {
                switch (__instance.m_activeWeaponType)
                {
                    case PlayerWeaponType.RhythmWeapon:
                        ForceTubeVRInterface.Shoot(210, 126, 50f, ForceTubeVRChannel.pistol1);
                        break;
                    case PlayerWeaponType.Shotgun:
                        ForceTubeVRInterface.Shoot(255, 200, 100f, ForceTubeVRChannel.pistol1);
                        break;
                    case PlayerWeaponType.Falx:
                        ForceTubeVRInterface.Rumble(126, 50f, ForceTubeVRChannel.pistol1);
                        break;
                    default:
                        ForceTubeVRInterface.Kick(210, ForceTubeVRChannel.pistol1);
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(DodgeMovementState), "Enter")]
    public class bhaptics_OnDash
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            Plugin.tactsuitVr.PlaybackHaptics("Dash");
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
            Plugin.tactsuitVr.StopHeartBeat();
        }
    }

    [HarmonyPatch(typeof(Player), "ResurrectPlayer")]
    public class bhaptics_OnRessurect
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

    [HarmonyPatch(typeof(Player), "TakeDamage")]
    public class bhaptics_OnHurt
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, AttackBase attack)
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            var angleShift = TactsuitVR.getAngleAndShift(__instance.PlayerTransform, attack.Position);
            Plugin.tactsuitVr.PlayBackHit("Impact", angleShift.Key, angleShift.Value); 
        }
    }

    [HarmonyPatch(typeof(Player), "IsAtLowHealth")]
    public class bhaptics_lowhealth
    {
        public static bool started = false;
        [HarmonyPostfix]
        public static void Postfix(bool __result)
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            if(__result && !started)
            {
                Plugin.tactsuitVr.StartHeartBeat();
                started = true;
            }
            if(!__result && started)
            {
                Plugin.tactsuitVr.StopHeartBeat();
                started = false;
            }
        }
    }

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

