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
using BepInEx.Configuration;

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
        public static bool hasJumped = false;
        public static ConfigEntry<bool> leftHanded;


        public override void Load()
        {
            _instance = this;
            Log = base.Log;
            //config
            leftHanded = Config.Bind("bhaptics", "leftHanded", false);
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

        /*
         * There is not real dual wield in this game so I decided not to
         * manage dual wield
         **/
        private async void InitializeProTube()
        {
            Log.LogMessage("Initializing ProTube gear...");
            await ForceTubeVRInterface.InitAsync(true);
            Thread.Sleep(10000);
        }
    }

    [HarmonyPatch(typeof(WeaponAbilityController), "AttackTriggered")]
    public class bhaptics_OnShooting
    {
        [HarmonyPostfix]
        public static void Postfix(WeaponAbilityController __instance, bool onBeat)
        {
            //bHaptics
            if (!Plugin.tactsuitVr.suitDisabled)
            {                
                Plugin.tactsuitVr.PlaybackHaptics(
                    (Plugin.leftHanded.Value) ? "RecoilVest_L" : "RecoilVest_R", true, 
                    (onBeat) ? 2f : 1f, (onBeat) ? 1.5f : 1f);
                Plugin.tactsuitVr.PlaybackHaptics(
                    (Plugin.leftHanded.Value) ? "RecoilArm_L" : "RecoilArm_R", true, 
                    (onBeat) ? 2f : 1f, (onBeat) ? 1.5f : 1f);
            }
            switch (__instance.m_activeWeaponType)
            {
                case PlayerWeaponType.RhythmWeapon:
                    ForceTubeVRInterface.Shoot(210, 126, 50f, ForceTubeVRChannel.all);
                    break;
                case PlayerWeaponType.Shotgun:
                    ForceTubeVRInterface.Shoot(255, 200, 100f, ForceTubeVRChannel.all);
                    break;
                case PlayerWeaponType.Falx:
                    ForceTubeVRInterface.Rumble(126, 50f, ForceTubeVRChannel.all);
                    break;
                case PlayerWeaponType.Crossbow:
                    ForceTubeVRInterface.Shoot(255, 255, 50f, ForceTubeVRChannel.all);
                    break;
                case PlayerWeaponType.Pistols:
                    ForceTubeVRInterface.Kick(210, ForceTubeVRChannel.all);
                    break;
                case PlayerWeaponType.Boomerang:
                    ForceTubeVRInterface.Rumble(100, 50f, ForceTubeVRChannel.all);
                    break;
                default:
                    ForceTubeVRInterface.Kick(210, ForceTubeVRChannel.all);
                    break;
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

    [HarmonyPatch(typeof(JumpMovementState), "TriggerJump")]
    public class bhaptics_OnJump
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            Plugin.hasJumped = true;
            Plugin.tactsuitVr.PlaybackHaptics("OnJump");
        }
    }
    
    [HarmonyPatch(typeof(FirstPersonController), "OnControllerColliderHit")]
    public class bhaptics_OnJumpLanding
    {
        [HarmonyPostfix]
        public static void Postfix(FirstPersonController __instance)
        {
            if (Plugin.tactsuitVr.suitDisabled)
            {
                return;
            }
            if (__instance.IsGrounded && Plugin.hasJumped)
            {
                Plugin.tactsuitVr.PlaybackHaptics("LandAfterJump");
                Plugin.hasJumped = false;
            }
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

    [HarmonyPatch(typeof(InGameState), "OnResurrectionMenuOptionSelected")]
    public class bhaptics_OnRessurect
    {
        [HarmonyPostfix]
        public static void Postfix(ResurrectionMenuOption option)
        {
            if (Plugin.tactsuitVr.suitDisabled || option != ResurrectionMenuOption.Resurrect)
            {
                return;
            }
            Plugin.tactsuitVr.PlaybackHaptics("Resurect");
            Plugin.tactsuitVr.PlaybackHaptics("Resurect_L");
            Plugin.tactsuitVr.PlaybackHaptics("Resurect_R");
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
            Plugin.tactsuitVr.PlayBackHit("Impact", angleShift.Key, angleShift.Value, 4f); 
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

