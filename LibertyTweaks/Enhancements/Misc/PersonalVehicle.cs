﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Windows.Input;
using CCL.GTAIV;
using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;
using System.Diagnostics;
using IVSDKDotNet.Enums;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Linq;

// Credits: catsmackaroo

namespace LibertyTweaks
{

    internal class PersonalVehicle
    {
        // Main
        public static bool enable;

        // Personal Vehicle
        private static NativeBlip vehBlip;
        private static IVVehicle savedVehicle;
        private static string savedVehicleName;
        private static uint savedVehicleModelId;
        private static byte savedVehiclePrimaryColor;
        private static byte savedVehicleSecondaryColor;
        private static byte savedVehicleQuaternaryColor;
        private static byte savedVehicleTertiaryColor;
        private static float savedVehicleEngineHealth;
        private static float savedVehiclePetrolTankHealth;
        private static float savedVehicleHeading;
        private static float savedVehicleDirt;
        private static bool[] savedVehicleExtras = new bool[11];
        private static Vector3 savedVehiclePosition;
        private static bool firstFrame = true;
        private static bool isBlipAttached;
        private static bool hasSaved;
        private static bool newGameCleanup;
        private static bool hasTeleportedVehicle;

        // Tracker Service
        private static bool blipsSpawned = false;
        private static IVVehicle checkVehicle;
        private static NativeBlip trackerBlip;
        private static List<Vector3> serviceLocations = new List<Vector3>();
        private static Vector3 northAlgonquinPNS = new Vector3(-335, 1531, 19);
        private static Vector3 southAlgonquinPNS = new Vector3(-481, 350, 6);
        private static Vector3 dukesPNS = new Vector3(1065, -286, 20);
        private static Vector3 northAlderneyPNS = new Vector3(-1125, 1185, 16);
        private static Vector3 southAlderneyPNS = new Vector3(-1308, 272, 10);
        private static Vector3 stevieLocation = new Vector3(722, 1392, 14);
        private static bool canBeTracked = false;
        private static bool canShowGuide = false;
        private static bool canFade = false;
        private static uint priceForTracking = 750;
        private static Dictionary<Vector3, bool> messageShown = new Dictionary<Vector3, bool>();

        // Impound Stuff
        private static bool isVehicleImpounded;
        private static List<Vector3> policeStations = new List<Vector3>();
        private static Vector3 algonquinImpound = new Vector3(68, 1248, 15);
        private static Vector3 airportImpound = new Vector3(2138, 465, 5);
        private static Vector3 northAlderneyImpound = new Vector3(-845, 1314, 21);
        private static Vector3 southAlderneyImpound = new Vector3(-1251, -252, 2);
        private static Vector3 bohanImpound = new Vector3(993, 1894, 23);

        // Player Stats
        private static uint islandsUnlocked; 
        private static uint islandsUnlockedInitial = islandsUnlocked; 
        private static uint romanMissionProgress;
        private static uint romanMissionProgressAfter;
        private static uint brucieMissionProgress;
        private static uint brucieMissionProgressAfter; 
        private static uint missionsCompleted;
        private static uint currentEpisode;

        public static void Init(SettingsFile settings)
        {
            enable = settings.GetBoolean("Personal Vehicle", "Enable", true);


            if (enable)
                Main.Log("script initialized...");
        }
        public static void IngameStartup()
        {
            if (!enable)
                return;

            Cleanup();
            islandsUnlockedInitial = GET_INT_STAT(363);
            romanMissionProgress = GET_INT_STAT(3);
            brucieMissionProgress = GET_INT_STAT(16);
            newGameCleanup = false;
            firstFrame = true;
        }
        public static void Process()
        {
            if (!enable)
                return;

            IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());
            uint playerMoney = IVPlayerInfoExtensions.GetMoney(playerPed.PlayerInfo);

            if (!IS_CHAR_IN_ANY_CAR(playerPed.GetHandle()))
                return;

            if (canBeTracked == false)
                return;

            if (playerMoney < priceForTracking)
                return;

            if (IS_CHAR_IN_CAR(playerPed.GetHandle(), savedVehicle.GetHandle()))
            {
                IVGame.ShowSubtitleMessage("This vehicle already has a tracker.");
                return;
            }

            Cleanup();

            savedVehicle = IVVehicle.FromUIntPtr(playerPed.GetVehicle());

            canFade = true;

            Main.Log("Set Unsaved Vehicle: " + savedVehicle.Handling.Name);

            GET_TIME_OF_DAY(out int beforeTime, out int beforeTimeMinute);
            uint afterTime = (uint)(beforeTime + 3);
            uint afterTimeMinute = (uint)(beforeTimeMinute + 30);
            IVGame.ShowSubtitleMessage("");
            Main.TheDelayedCaller.Add(TimeSpan.FromSeconds(2), "Main", () =>
            {
                canShowGuide = true;
                SET_TIME_OF_DAY(afterTime, afterTimeMinute);
                SKIP_RADIO_FORWARD();
                IVPlayerInfoExtensions.RemoveMoney(playerPed.PlayerInfo, (int)priceForTracking);
            });
        }
        public static void Tick()
        {
            if (!enable)
                return;

            IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());
            uint playerIndex = GET_PLAYER_ID();
            uint playerMoney = IVPlayerInfoExtensions.GetMoney(playerPed.PlayerInfo);

            STORE_WANTED_LEVEL((int)playerIndex, out uint playerWantedLevel);

            islandsUnlocked = GET_INT_STAT(363);
            romanMissionProgressAfter = GET_INT_STAT(3);
            brucieMissionProgressAfter = GET_INT_STAT(16);
            currentEpisode = GET_CURRENT_EPISODE();
            missionsCompleted = GET_INT_STAT(253);

            if (firstFrame) 
            {
                SpawnSavedVehicle();
                firstFrame = false;
            }

            // Added this if statement to prevent new game bugs
            if (missionsCompleted == 0 && newGameCleanup == false)
            {
                Cleanup();
                newGameCleanup = true;
            }

            if (islandsUnlockedInitial != islandsUnlocked || romanMissionProgress != romanMissionProgressAfter || brucieMissionProgress != brucieMissionProgressAfter)
            {
                AddServiceLocations();
                ManageServiceBlips();
                islandsUnlockedInitial = islandsUnlocked;
                romanMissionProgress = romanMissionProgressAfter;
                brucieMissionProgress = brucieMissionProgressAfter;
            }

            if (savedVehicle != null)
            {
                ManageVehicleBlip(playerPed);

                if (IS_CAR_DEAD(savedVehicle.GetHandle()))
                    Cleanup();
            }

            if (GET_IS_DISPLAYINGSAVEMESSAGE() && hasSaved == false)
            {
                SaveVehicleData();
                hasSaved = true;
            }
            else if (!GET_IS_DISPLAYINGSAVEMESSAGE())
            {
                hasSaved = false;
            }

            if (!blipsSpawned)
            {
                AddServiceLocations();
                ManageServiceBlips();
            }

            if (IS_CHAR_IN_ANY_CAR(playerPed.GetHandle()))
            {
                HandleTrackerService(playerPed, serviceLocations, playerMoney, playerIndex);
            }

            if (canFade)
            {
                DO_SCREEN_FADE_OUT(2000);
                SET_PLAYER_CONTROL((int)playerIndex, false);
                if (IS_SCREEN_FADING())
                {
                    Main.TheDelayedCaller.Add(TimeSpan.FromSeconds(2), "Main", () =>
                    {
                        DO_SCREEN_FADE_IN(2000);
                        SET_PLAYER_CONTROL((int)playerIndex, true);
                    });
                }
                canFade = false;
            }

            if (savedVehicle != null)
            {
                if (Vector3.Distance(playerPed.Matrix.Pos, savedVehicle.Matrix.Pos) > 200f)
                    return;

                if (isVehicleImpounded == true && Vector3.Distance(playerPed.Matrix.Pos, savedVehicle.Matrix.Pos) < 5f && playerWantedLevel < 4)
                {
                    Main.Log("Player is near impounded car; giving 4 stars.");
                    ALTER_WANTED_LEVEL((int)playerIndex, 4);
                    APPLY_WANTED_LEVEL_CHANGE_NOW((int)playerIndex);
                    isVehicleImpounded = false;
                }


                if (isVehicleImpounded == false)
                {
                    if (playerWantedLevel >= 1 && IS_PLAYER_DEAD((int)playerIndex) || IS_PLAYER_BEING_ARRESTED())
                    {
                        ImpoundVehicle(playerPed);
                    }
                }
            }
        }
        private static void ImpoundVehicle(IVPed playerPed)
        {
            if (!isVehicleImpounded && savedVehicle != null)
            {
                Main.Log("Vehicle has been impounded.");

                if (policeStations.Count == 0)
                {
                    policeStations.Add(airportImpound);
                    policeStations.Add(algonquinImpound);
                    policeStations.Add(northAlderneyImpound);
                    policeStations.Add(southAlderneyImpound);
                    policeStations.Add(bohanImpound);
                }

                var (nearestImpoundLocation, nearestIndex) = FindNearestLocation(savedVehicle.Matrix.Pos, policeStations);

                Main.TheDelayedCaller.Add(TimeSpan.FromSeconds(10), "Main", () =>
                {
                    savedVehicle.Teleport(nearestImpoundLocation, true, true);
                    LOCK_CAR_DOORS(savedVehicle.GetHandle(), 7);
                    SET_CAR_ENGINE_ON(savedVehicle.GetHandle(), false, false);
                    CLOSE_ALL_CAR_DOORS(savedVehicle.GetHandle());

                    string impoundMessage;
                    switch (nearestIndex)
                    {
                        case 0:
                            impoundMessage = "Your tracked vehicle has been impounded at the Francis International Airport Police Station.";
                            SET_CAR_HEADING(savedVehicle.GetHandle(), 268);
                            break;
                        case 1:
                            impoundMessage = "Your tracked vehicle has been impounded at the East Holland Police Station.";
                            SET_CAR_HEADING(savedVehicle.GetHandle(), 89);
                            break;
                        case 2:
                            impoundMessage = "Your tracked vehicle has been impounded at the Leftwood Police Station.";
                            SET_CAR_HEADING(savedVehicle.GetHandle(), 268);
                            break;
                        case 3:
                            impoundMessage = "Your tracked vehicle has been impounded at the Acter Industrial Park Police Station.";
                            SET_CAR_HEADING(savedVehicle.GetHandle(), 268);
                            break;
                        case 4:
                            impoundMessage = "Your tracked vehicle has been impounded at the Northern Gardens Police Station.";
                            SET_CAR_HEADING(savedVehicle.GetHandle(), 359);
                            break;
                        default:
                            impoundMessage = "Your tracked vehicle has been impounded.";
                            break;
                    }

                    IVText.TheIVText.ReplaceTextOfTextLabel("PLACEHOLDER_1", impoundMessage);
                    PRINT_HELP("PLACEHOLDER_1");
                });

                isVehicleImpounded = true;
            }
        }
        private static (Vector3 nearestLocation, int nearestIndex) FindNearestLocation(Vector3 currentPos, List<Vector3> locations)
        {
            float minDistance = float.MaxValue;
            Vector3 nearestLocation = Vector3.Zero;
            int nearestIndex = -1;

            for (int i = 0; i < locations.Count; i++)
            {
                float distance = Vector3.Distance(currentPos, locations[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestLocation = locations[i];
                    nearestIndex = i;
                }
            }

            return (nearestLocation, nearestIndex);
        }
        private static void AddServiceLocations()
        {
            Main.Log("Entering AddServiceLocations method...");

            serviceLocations.Clear();

            if (romanMissionProgress >= 33 || currentEpisode != 0)
            {
                serviceLocations.Clear();
                serviceLocations.Add(dukesPNS);
            }
            if (brucieMissionProgress == 100 || currentEpisode != 0)
            {
                serviceLocations.Clear();
                serviceLocations.Add(dukesPNS);
                serviceLocations.Add(stevieLocation);
            }
            if (islandsUnlocked > 1 || currentEpisode != 0)
            {
                serviceLocations.Clear();
                serviceLocations.Add(dukesPNS);
                serviceLocations.Add(stevieLocation);
                serviceLocations.Add(northAlgonquinPNS);
                serviceLocations.Add(southAlgonquinPNS);
            }
            if (islandsUnlocked > 2 || currentEpisode != 0)
            {
                serviceLocations.Clear();
                serviceLocations.Add(dukesPNS);
                serviceLocations.Add(stevieLocation);
                serviceLocations.Add(northAlgonquinPNS);
                serviceLocations.Add(southAlgonquinPNS);
                serviceLocations.Add(northAlderneyPNS);
                serviceLocations.Add(southAlderneyPNS);
            }
        }
        private static void ManageServiceBlips()
        {
            Main.Log("Entering ManageServiceBlips method...");

            if (trackerBlip != null)
            {
                trackerBlip.Dispose();
                trackerBlip = null;
                blipsSpawned = false;
            }

            foreach (Vector3 location in serviceLocations)
            {
                NativeBlip trackerBlip = NativeBlip.AddBlip(location);
                trackerBlip.ShowOnlyWhenNear = true;
                trackerBlip.Icon = BlipIcon.Building_Garage;
                trackerBlip.Name = "Tracker Service";
                trackerBlip.Scale = 0.8f;
            }
            blipsSpawned = true;
        }
        private static void DisplayTrackerServiceTutorial()
        {
            if (canShowGuide)
            {
                IVText.TheIVText.ReplaceTextOfTextLabel("PLACEHOLDER_1", "You have tracked this vehicle. When tracking a vehicle, you can find it on the map. It will be saved, similar to vehicles parked in-front of safehouses. You may only have one tracked vehicle at a time.");
                PRINT_HELP("PLACEHOLDER_1");
                canShowGuide = false;
            }
        }
        private static void HandleTrackerService(IVPed playerPed, List<Vector3> locations, uint playerMoney, uint playerIndex)
        {
            string[] emergencyVehicles = { "POL", "FBI", "NOOSE", "AMBUL", "FIRE"};

            if (messageShown == null)
            {
                messageShown = new Dictionary<Vector3, bool>();
                foreach (var location in locations)
                {
                    messageShown[location] = false;
                }
            }

            foreach (Vector3 location in locations)
            {
                if (Vector3.Distance(playerPed.Matrix.Pos, location) < 5f)
                {
                    STORE_WANTED_LEVEL((int)playerIndex, out uint currentWantedLevel);

                    if (currentWantedLevel > 0)
                        return;

                    if (canShowGuide)
                    {
                        DisplayTrackerServiceTutorial();
                        return;
                    }

                    if (!IS_CHAR_IN_CAR(playerPed.GetHandle(), savedVehicle.GetHandle()))
                    {
                        if (messageShown[location])
                            return;

                        GET_CAR_CHAR_IS_USING(playerPed.GetHandle(), out int currentCar);
                        GET_CAR_HEALTH(currentCar, out uint currentCarHealth);
                        checkVehicle = IVVehicle.FromUIntPtr(playerPed.GetVehicle());
                        if (emergencyVehicles.Any(identifier => checkVehicle.Handling.Name.Contains(identifier)))
                        {
                            priceForTracking = (uint)(checkVehicle.Handling.MonetaryValue * 0.5);
                        }
                        else
                        {
                            priceForTracking = (uint)(checkVehicle.Handling.MonetaryValue * 0.05);
                        }

                        if (Vector3.Distance(playerPed.Matrix.Pos, stevieLocation) < 5f)
                        {
                            priceForTracking = (uint)(priceForTracking * 0.5);
                        }

                        if (currentCarHealth < 800)
                        {
                            IVGame.ShowSubtitleMessage("This vehicle is too damaged to be given a tracker.");
                            messageShown[location] = true;
                            return;
                        }

                        if (IS_CAR_A_MISSION_CAR(currentCar))
                        {
                            IVGame.ShowSubtitleMessage("You cannot add trackers to mission vehicles.");
                            messageShown[location] = true;
                            return;
                        }

                        if (playerMoney < priceForTracking)
                        {
                            IVGame.ShowSubtitleMessage("You have insufficient funds, a tracker costs $" + priceForTracking + ".");
                            messageShown[location] = true;
                            return;
                        }

                        IVGame.ShowSubtitleMessage("Press " + Main.personalVehicleKeyString + " to add a tracker to this vehicle. " + "Price: $" + priceForTracking);
                        messageShown[location] = true;
                    }

                    canBeTracked = true;
                    return;
                }
                else
                {
                    messageShown[location] = false; 
                }
            }

            canBeTracked = false;
        }
        private static void ManageVehicleBlip(IVPed playerPed)
        {

            if (IS_CHAR_IN_CAR(playerPed.GetHandle(), savedVehicle.GetHandle()))
            {
                if (vehBlip != null)
                {
                    MARK_CAR_AS_NO_LONGER_NEEDED(savedVehicle.GetHandle());
                    vehBlip.Dispose();
                    vehBlip = null;
                    isBlipAttached = false;
                    Main.Log("Blip detached; player in saved vehicle.");
                }
            }
            else if (!isBlipAttached && savedVehicle != null)
            {
                SET_CAR_AS_MISSION_CAR(savedVehicle.GetHandle());
                vehBlip = savedVehicle.AttachBlip();
                vehBlip.Icon = BlipIcon.Building_Garage;
                vehBlip.Name = "Personal Vehicle";
                isBlipAttached = true;
                Main.Log("Blip attached; player not in saved vehicle.");
            }
        }
        private static void SaveVehicleData()
        {
            Main.Log("Entering SaveVehicleData method...");

            try
            {
                if (savedVehicle  != null)
                {
                    GET_CAR_MODEL(savedVehicle.GetHandle(), out uint savedVehicleModel);
                    savedVehicleModelId = savedVehicleModel;
                    savedVehicleName = savedVehicle.Handling.Name;
                    savedVehiclePrimaryColor = savedVehicle.PrimaryColor;
                    savedVehicleSecondaryColor = savedVehicle.SecondaryColor;
                    savedVehicleQuaternaryColor = savedVehicle.QuaternaryColor;
                    savedVehicleTertiaryColor = savedVehicle.TertiaryColor;
                    savedVehicleEngineHealth = savedVehicle.EngineHealth;
                    savedVehiclePetrolTankHealth = savedVehicle.PetrolTankHealth;
                    savedVehicleHeading = savedVehicle.GetHeading();
                    savedVehiclePosition = savedVehicle.Matrix.Pos;
                    savedVehicleDirt = savedVehicle.DirtLevel;

                    for (int i = 1; i < savedVehicleExtras.Length; i++)
                    {
                        savedVehicleExtras[i] = IS_VEHICLE_EXTRA_TURNED_ON(savedVehicle.GetHandle(), (uint)i); 
                    }
                }
            }
            catch (Exception ex)
            {
                Cleanup();
                Main.LogError("Error updating vehicle data: " + ex.Message);
            }

            Main.GetTheSaveGame().SetValue("VehicleName", savedVehicleName);
            Main.GetTheSaveGame().SetInteger("VehicleModel", (int)savedVehicleModelId);
            Main.GetTheSaveGame().SetInteger("VehicleColor1", savedVehiclePrimaryColor);
            Main.GetTheSaveGame().SetInteger("VehicleColor2", savedVehicleSecondaryColor);
            Main.GetTheSaveGame().SetInteger("VehicleColor3", savedVehicleQuaternaryColor);
            Main.GetTheSaveGame().SetInteger("VehicleColor4", savedVehicleTertiaryColor);
            Main.GetTheSaveGame().SetFloat("VehicleEngineHealth", savedVehicleEngineHealth);
            Main.GetTheSaveGame().SetFloat("VehiclePetrolTankHealth", savedVehiclePetrolTankHealth);
            Main.GetTheSaveGame().SetFloat("VehicleHeading", savedVehicleHeading);
            Main.GetTheSaveGame().SetVector3("VehiclePosition", savedVehiclePosition);
            Main.GetTheSaveGame().SetFloat("VehicleDirt", savedVehicleDirt);

            for (int i = 1; i < savedVehicleExtras.Length; i++)
            {
                Main.GetTheSaveGame().SetBoolean($"VehicleExtra{i}", savedVehicleExtras[i]);
            }

            Main.GetTheSaveGame().Save();
        }
        private static void SpawnSavedVehicle()
        {
            string lastSavedVehicleName = Main.GetTheSaveGame().GetValue("VehicleName");
            uint lastSavedVehicleModel = (uint)Main.GetTheSaveGame().GetInteger("VehicleModel");
            Vector3 lastSavedVehiclePosition = Main.GetTheSaveGame().GetVector3("VehiclePosition");
            byte lastSavedVehiclePrimaryColor = (byte)Main.GetTheSaveGame().GetInteger("VehicleColor1");
            byte lastSavedVehicleSecondaryColor = (byte)Main.GetTheSaveGame().GetInteger("VehicleColor2");
            byte lastSavedVehicleQuaternaryColor = (byte)Main.GetTheSaveGame().GetInteger("VehicleColor3");
            byte lastSavedVehicleTertiaryColor = (byte)Main.GetTheSaveGame().GetInteger("VehicleColor4");
            float lastSavedVehicleEngineHealth = Main.GetTheSaveGame().GetFloat("VehicleEngineHealth");
            float lastSavedVehiclePetrolTankHealth = Main.GetTheSaveGame().GetFloat("VehiclePetrolTankHealth");
            float lastSavedVehicleHeading = Main.GetTheSaveGame().GetFloat("VehicleHeading");
            float lastSavedVehicleDirt = Main.GetTheSaveGame().GetFloat("VehicleDirt");
            bool[] lastSavedVehicleExtras = new bool[savedVehicleExtras.Length];
            for (int i = 0; i < lastSavedVehicleExtras.Length; i++)
            {
                lastSavedVehicleExtras[i] = Main.GetTheSaveGame().GetBoolean($"VehicleExtra{i}");
            }

            if (lastSavedVehicleName != "")
            {
                savedVehicle = NativeWorld.SpawnVehicle(lastSavedVehicleModel, lastSavedVehiclePosition, out int savedVehicleHandle, true, true);
                CHANGE_CAR_COLOUR(savedVehicleHandle, lastSavedVehiclePrimaryColor, lastSavedVehicleSecondaryColor);
                SET_EXTRA_CAR_COLOURS(savedVehicleHandle, lastSavedVehicleQuaternaryColor, lastSavedVehicleTertiaryColor);
                SET_CAR_ON_GROUND_PROPERLY(savedVehicleHandle);
                SET_CAR_HEADING(savedVehicleHandle, lastSavedVehicleHeading);
                SET_ENGINE_HEALTH(savedVehicleHandle, (uint)lastSavedVehicleEngineHealth);
                SET_PETROL_TANK_HEALTH(savedVehicleHandle, (uint)lastSavedVehiclePetrolTankHealth);
                SET_VEHICLE_DIRT_LEVEL(savedVehicleHandle, lastSavedVehicleDirt);
                SET_HAS_BEEN_OWNED_BY_PLAYER(savedVehicleHandle, true);
                isBlipAttached = false;

                for (int i = 0; i < lastSavedVehicleExtras.Length; i++)
                {
                    if (lastSavedVehicleExtras[i])
                    {
                        TURN_OFF_VEHICLE_EXTRA(savedVehicleHandle, i, false); 
                    }
                    else
                    {
                        TURN_OFF_VEHICLE_EXTRA(savedVehicleHandle, i, true);
                    }
                }
            }
        }
        private static void Cleanup()
        {
            Main.Log("Entering Cleanup method...");

            if (vehBlip != null)
            {
                vehBlip.Dispose();
                vehBlip = null;
                isBlipAttached = false;
            }

            ResetSavedVehicleState();
        }
        private static void ResetSavedVehicleState()
        {
            if (savedVehicle != null)
            {
                savedVehicleName = "";
                savedVehiclePrimaryColor = 0;
                savedVehicleSecondaryColor = 0;
                savedVehicleQuaternaryColor = 0;
                savedVehicleTertiaryColor = 0;
                savedVehicleEngineHealth = 0;
                savedVehiclePetrolTankHealth = 0;
                savedVehicleHeading = 0;
                savedVehiclePosition = Vector3.Zero;
                savedVehicleDirt = 0;
                savedVehicle.MarkAsNoLongerNeeded();
                Array.Clear(savedVehicleExtras, 0, savedVehicleExtras.Length);
                savedVehicle.Delete();
                savedVehicle = null;
                isVehicleImpounded = false;
            }
        }
    }
}
