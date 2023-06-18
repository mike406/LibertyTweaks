﻿using CCL.GTAIV;

using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;

// Credits: ClonkAndre 

namespace LibertyTweaks.RemoveWeapons
{
    internal class RemoveWeapons
    {
        private static bool enableFix;
        public static void Init(SettingsFile settings)
        {
            enableFix = settings.GetBoolean("Main", "Remove Weapons On Death", true);
        }

        public static void Tick()
        {
            if (!enableFix)
                return;

            CPed playerPed = CPed.FromPointer(CPlayerInfo.FindPlayerPed());

            if (IS_CHAR_DEAD(playerPed.GetHandle()))
                REMOVE_ALL_CHAR_WEAPONS(playerPed.GetHandle()); 
        }
    }
}