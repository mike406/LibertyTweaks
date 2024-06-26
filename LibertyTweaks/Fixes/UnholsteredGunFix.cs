﻿using System;
using System.Numerics;

using CCL.GTAIV;

using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;

// Credit: catsmackaroo

namespace LibertyTweaks
{
    internal class UnholsteredGunFix
    {
        private static bool enable;

        public static void Init(SettingsFile settings)
        {
            enable = settings.GetBoolean("Improved Police", "Unholstered Wanted Fix", true);

            if (enable)
                Main.Log("script initialized...");
        }
        public static void Tick()
        {
            if (!enable)
            {
                return;
            }

            // Grab player 
            IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());

            // Get current weapon
            GET_CURRENT_CHAR_WEAPON(playerPed.GetHandle(), out int currentWeap);

            // If player is holding any weapon
            if (currentWeap != 0 && currentWeap != 46)
            {

                // If player is out of car
                if (!IS_CHAR_IN_ANY_CAR(playerPed.GetHandle()))
                {
                    // Grab all peds
                    IVPool pedPool = IVPools.GetPedPool();
                    for (int i = 0; i < pedPool.Count; i++)
                    {
                        UIntPtr ptr = pedPool.Get(i);
                        if (ptr != UIntPtr.Zero)
                        {
                            // Get ped handles
                            int pedHandle = (int)pedPool.GetIndex(ptr);

                            // Get ped models
                            GET_CHAR_MODEL(pedHandle, out uint pedModel);

                            // Get policia
                            GET_CURRENT_BASIC_COP_MODEL(out uint copModel);

                            // Check if anyone nearby is in a cop car or if any cops are nearby
                            if (IS_CHAR_IN_ANY_POLICE_VEHICLE(pedHandle) || pedModel == copModel)
                            {
                                // Get ped coords
                                GET_CHAR_COORDINATES(pedHandle, out Vector3 pedCoords);

                                // Check distance between police & player
                                if (Vector3.Distance(playerPed.Matrix.Pos, pedCoords) < 10f)
                                {
                                    // Grab player ID
                                    uint playerId = GET_PLAYER_ID();

                                    // Check player's wanted level
                                    STORE_WANTED_LEVEL((int)playerId, out uint currentWantedLevel);

                                    // Check if the player is not wanted
                                    if (currentWantedLevel == 0)
                                    {
                                        Main.TheDelayedCaller.Add(TimeSpan.FromSeconds(2), "Main", () =>
                                        {
                                            ALTER_WANTED_LEVEL((int)playerId, 1);
                                            APPLY_WANTED_LEVEL_CHANGE_NOW((int)playerId);
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
