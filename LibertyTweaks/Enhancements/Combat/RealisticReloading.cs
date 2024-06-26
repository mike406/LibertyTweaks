﻿using CCL.GTAIV;

using IVSDKDotNet;
using IVSDKDotNet.Enums;
using IVSDKDotNet.Native;
using System;
using System.Numerics;
using System.Windows.Forms;
using static IVSDKDotNet.Native.Natives;

namespace LibertyTweaks
{
    internal class RealisticReloading
    {
        private static bool enable;

        public static void Init(SettingsFile settings)
        {
            enable = settings.GetBoolean("Realistic Reloading", "Enable", true);

            if (enable)
                Main.Log("script initialized...");
        }

        public static void Tick()
        {
            if (!enable)
                return;

            if (NativeControls.IsGameKeyPressed(0, GameKey.Reload))
            {
                int playerId;
                IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());
                playerId = IVPedExtensions.GetHandle(playerPed);

                // Get current weapon
                GET_CURRENT_CHAR_WEAPON(playerPed.GetHandle(), out int currentWeapon);

                // Get total ammo for weapon
                GET_AMMO_IN_CHAR_WEAPON(playerPed.GetHandle(), currentWeapon, out int weaponAmmo);

                // Get ammo in current clip
                GET_AMMO_IN_CLIP(playerPed.GetHandle(), currentWeapon, out int clipAmmo);

                // Get max ammo that can be in weapon clip
                GET_MAX_AMMO_IN_CLIP(playerPed.GetHandle(), currentWeapon, out int clipAmmoMax);

                if (clipAmmo == 0)
                    return;

                if (clipAmmo < clipAmmoMax && weaponAmmo - clipAmmo > 0)
                {
                    if (currentWeapon == (int)eWeaponType.WEAPON_SHOTGUN || currentWeapon == (int)eWeaponType.WEAPON_BARETTA
                        || currentWeapon == (int)eWeaponType.WEAPON_EPISODIC_11 || currentWeapon == (int)eWeaponType.WEAPON_EPISODIC_10
                        || currentWeapon == (int)eWeaponType.WEAPON_EPISODIC_2 || currentWeapon == (int)eWeaponType.WEAPON_EPISODIC_6)
                    {
                        return;
                    }
                    else if (!IS_MOUSE_BUTTON_PRESSED(1))
                    {
                        SET_AMMO_IN_CLIP(playerPed.GetHandle(), (int)currentWeapon, 0);
                    }
                }
            }
        }
    }
}
