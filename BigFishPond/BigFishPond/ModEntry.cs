using StardewModdingAPI;
using StardewValley;
using HarmonyLib;
using StardewValley.Buildings;
using System;
using System.Collections.Generic;
using StardewValley.GameData.FishPonds;
using StardewValley.Extensions;
using Microsoft.Xna.Framework;
using StardewValley.ItemTypeDefinitions;



namespace BigFishPond
{

    internal sealed class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            Harmony harmony = new(ModManifest.UniqueID);
            
            harmony.Patch(
                original: AccessTools.Method(typeof(FishPond), nameof(FishPond.UpdateMaximumOccupancy)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(UpdateMaximumOccupancyForBigFishPond))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(FishPond), nameof(FishPond.dayUpdate)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(IgnoreQuestsForRegularFishPondAtTenPopulation))
                );

            harmony.Patch(
                original: AccessTools.Method(typeof(FishPond), nameof(FishPond.doAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(doActionAddBFPLogic))
                );

            harmony.Patch(
                original: AccessTools.Method(typeof(FishPond), nameof(FishPond.GetRawData)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(GetRawDataBFP))
                );
        }

        [HarmonyPostfix]
        private static void UpdateMaximumOccupancyForBigFishPond(FishPond __instance, int __state)
        {
            try
            {
                if (__instance.fishType.Value == null)
                {
                    return;
                }
                if (__instance.buildingType.Contains("BigFishPond"))
                {

                    FishPondData __fishPondData = FishPond.GetRawData(__instance.fishType.Value);
                    /*
                    Console.WriteLine("Found BigFishPond", LogLevel.Error);
                    Console.WriteLine(__instance.id.Value);
                    Console.WriteLine(__fishPondData.ToString(), LogLevel.Error);
                    */
                    for (int i = 1; i <= 20; i++)
                    {
                        if (i <= __instance.lastUnlockedPopulationGate.Value)
                        {
                            __instance.maxOccupants.Set(i);
                            //Console.WriteLine("Max Occupants increased to: " + i, LogLevel.Error);
                            continue;
                        }
                        if (__fishPondData.PopulationGates == null || !__fishPondData.PopulationGates.ContainsKey(i))
                        {
                            __instance.maxOccupants.Set(i);
                            //Console.WriteLine("Max Occupants set to: " + i, LogLevel.Error);
                            continue;
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed in {nameof(UpdateMaximumOccupancyForBigFishPond)}:\n{e}", LogLevel.Error);
            }
        }

        [HarmonyPostfix]
        private static void IgnoreQuestsForRegularFishPondAtTenPopulation(int dayOfMonth, FishPond __instance, int __state)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                /*
                Console.WriteLine("Building name: " + __instance.buildingType.Value, LogLevel.Error);
                Console.WriteLine("Building fish type: " + __instance.fishType.Value, LogLevel.Error);
                Console.WriteLine("Building fish count: " + __instance.FishCount, LogLevel.Error);
                Console.WriteLine("Building max populations: " + __instance.maxOccupants.Value, LogLevel.Error);
                */
                if (__instance.buildingType.Contains("BigFishPond"))
                {
                    return;
                }

                FishPondData fishPondData = __instance.GetFishPondData();

                if(__instance.maxOccupants.Value < 10)
                {
                    return;
                }

                if (__instance.maxOccupants.Value == FishPond.MAXIMUM_OCCUPANCY)
                {
                    __instance.neededItemCount.Value = -1;
                    __instance.neededItem.Value = null;
                    __instance.hasCompletedRequest.Value = true;
                    __instance.daysSinceSpawn.Value = 0;
                    return;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed in {nameof(IgnoreQuestsForRegularFishPondAtTenPopulation)}:\n{e}", LogLevel.Error);
                return;
            }
        }

        [HarmonyPrefix]
        public static bool doActionAddBFPLogic(Vector2 tileLocation, Farmer who, FishPond __instance)
        {
            try
            {
                
                if (__instance.daysOfConstructionLeft.Value <= 0 && __instance.occupiesTile(tileLocation))
                {
                    if (who.isMoving())
                    {
                        Game1.haltAfterCheck = false;
                    }

                    //back to the original method when we don't want to put fish into pond
                    if (who.ActiveObject != null && __instance.performActiveObjectDropInAction(who, probe: false))
                    {
                        return true;
                    }

                    if (__instance.output.Value != null)
                    {
                        return true;
                    }

                    if (who.ActiveObject != null && __instance.HasUnresolvedNeeds() && who.ActiveObject.QualifiedItemId == __instance.neededItem.Value.QualifiedItemId)
                    {
                        return true;
                    }

                    //put legendary fish in big pond only
                    if (who.ActiveObject != null)
                        if (__instance.buildingType.Contains("BigFishPond"))
                            if (who.ActiveObject.HasContextTag("fish_pond_ignore"))
                                if (__instance.fishType.Value == null || __instance.fishType.Value == who.ActiveObject.ItemId)
                                    if (__instance.currentOccupants.Value < __instance.maxOccupants.Value)
                                    {
                                        AddFishToPond(who, __instance);
                                        return false;
                                    }

                }

                return true;
            }

            catch (Exception e)
            {
                Console.WriteLine($"Failed in {nameof(doActionAddBFPLogic)}:\n{e}", LogLevel.Error);
                return true;
            }
        }

        [HarmonyPrefix]
        public static bool GetRawDataBFP(string itemId, ref FishPondData __result)
        {
            if (itemId == null)
            {
                __result = null;
                return true;
            }
            HashSet<string> contextTags = ItemContextTagManager.GetBaseContextTags(itemId);
            FishPondData selected = null;
            foreach (FishPondData data in DataLoader.FishPondData(Game1.content))
            {
                if (!(selected?.Precedence <= data.Precedence) && ItemContextTagManager.DoAllTagsMatch(data.RequiredTags, contextTags))
                {
                    selected = data;
                }
            }
            __result = selected;
            /*
            Console.WriteLine("Selected data: {0}",selected.Id.ToString());
            Console.WriteLine("Spawn time for selected {0}: {1}", selected.Id.ToString(), selected.SpawnTime);
            */
            return false;
        }

        private static void AddFishToPond(Farmer who, FishPond __instance)
        {
            var fish = who.ActiveObject;
            __instance.fishType.Value = fish.ItemId;
            if (__instance.currentOccupants.Value == 0)
            {
                __instance.UpdateMaximumOccupancy();
            }
            __instance.currentOccupants.Value++;
            showObjectThrownIntoPondAnimation(who, fish, __instance);
            who.reduceActiveItemByOne();
        }

        private static void showObjectThrownIntoPondAnimation(Farmer who, StardewValley.Object whichObject, FishPond __instance, Action callback = null)
        {
            who.faceGeneralDirection(__instance.GetCenterTile() * 64f + new Vector2(32f, 32f));
            float distance;
            float gravity;
            float velocity;
            float t;
            TemporaryAnimatedSpriteList fishTossSprites;
            ParsedItemData itemData;
            if (who.FacingDirection == 1 || who.FacingDirection == 3)
            {
                distance = Vector2.Distance(who.Position, __instance.GetCenterTile() * 64f);
                float verticalDistance = __instance.GetCenterTile().Y * 64f + 32f - who.position.Y;
                distance -= 8f;
                gravity = 0.0025f;
                velocity = (float)((double)distance * Math.Sqrt(gravity / (2f * (distance + 96f))));
                t = 2f * (velocity / gravity) + (float)((Math.Sqrt(velocity * velocity + 2f * gravity * 96f) - (double)velocity) / (double)gravity);
                t += verticalDistance;
                float xVelocityReduction = 0f;
                if (verticalDistance > 0f)
                {
                    xVelocityReduction = verticalDistance / 832f;
                    t += xVelocityReduction * 200f;
                }
                Game1.playSound("throwDownITem");
                fishTossSprites = new TemporaryAnimatedSpriteList();
                itemData = ItemRegistry.GetDataOrErrorItem(whichObject.QualifiedItemId);
                fishTossSprites.Add(new TemporaryAnimatedSprite(itemData.GetTextureName(), itemData.GetSourceRect(), who.Position + new Vector2(0f, -64f), flipped: false, 0f, Color.White)
                {
                    scale = 4f,
                    layerDepth = 1f,
                    totalNumberOfLoops = 1,
                    interval = t,
                    motion = new Vector2((float)((who.FacingDirection != 3) ? 1 : (-1)) * (velocity - xVelocityReduction), (0f - velocity) * 3f / 2f),
                    acceleration = new Vector2(0f, gravity),
                    timeBasedMotion = true
                });
                fishTossSprites.Add(new TemporaryAnimatedSprite(28, 100f, 2, 1, __instance.GetCenterTile() * 64f, flicker: false, flipped: false)
                {
                    delayBeforeAnimationStart = (int)t,
                    layerDepth = (((float)(int)__instance.tileY + 0.5f) * 64f + 2f) / 10000f
                });
                fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 55f, 8, 0, __instance.GetCenterTile() * 64f, flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
                {
                    delayBeforeAnimationStart = (int)t
                });
                fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 65f, 8, 0, __instance.GetCenterTile() * 64f + new Vector2(Game1.random.Next(-32, 32), Game1.random.Next(-16, 32)), flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
                {
                    delayBeforeAnimationStart = (int)t
                });
                fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 75f, 8, 0, __instance.GetCenterTile() * 64f + new Vector2(Game1.random.Next(-32, 32), Game1.random.Next(-16, 32)), flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
                {
                    delayBeforeAnimationStart = (int)t
                });
                if (who.IsLocalPlayer)
                {
                    DelayedAction.playSoundAfterDelay("waterSlosh", (int)t, who.currentLocation);
                    if (callback != null)
                    {
                        DelayedAction.functionAfterDelay(callback, (int)t);
                    }
                }
                return;
            }
            distance = Vector2.Distance(who.Position, __instance.GetCenterTile() * 64f);
            float height = Math.Abs(distance);
            if (who.FacingDirection == 0)
            {
                distance = 0f - distance;
                height += 64f;
            }
            float horizontalDistance = __instance.GetCenterTile().X * 64f - who.position.X;
            gravity = 0.0025f;
            velocity = (float)Math.Sqrt(2f * gravity * height);
            t = (float)(Math.Sqrt(2f * (height - distance) / gravity) + (double)(velocity / gravity));
            t *= 1.05f;
            t = ((who.FacingDirection != 0) ? (t * 2.5f) : (t * 0.7f));
            t -= Math.Abs(horizontalDistance) / ((who.FacingDirection == 0) ? 100f : 2f);
            Game1.playSound("throwDownITem");
            fishTossSprites = new TemporaryAnimatedSpriteList();
            itemData = ItemRegistry.GetDataOrErrorItem(whichObject.QualifiedItemId);
            fishTossSprites.Add(new TemporaryAnimatedSprite(itemData.GetTextureName(), itemData.GetSourceRect(), who.Position + new Vector2(0f, -64f), flipped: false, 0f, Color.White)
            {
                scale = 4f,
                layerDepth = 1f,
                totalNumberOfLoops = 1,
                interval = t,
                motion = new Vector2(horizontalDistance / ((who.FacingDirection == 0) ? 900f : 1000f), 0f - velocity),
                acceleration = new Vector2(0f, gravity),
                timeBasedMotion = true
            });
            fishTossSprites.Add(new TemporaryAnimatedSprite(28, 100f, 2, 1, __instance.GetCenterTile() * 64f, flicker: false, flipped: false)
            {
                delayBeforeAnimationStart = (int)t,
                layerDepth = (((float)(int)__instance.tileY + 0.5f) * 64f + 2f) / 10000f
            });
            fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 55f, 8, 0, __instance.GetCenterTile() * 64f, flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
            {
                delayBeforeAnimationStart = (int)t
            });
            fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 65f, 8, 0, __instance.GetCenterTile() * 64f + new Vector2(Game1.random.Next(-32, 32), Game1.random.Next(-16, 32)), flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
            {
                delayBeforeAnimationStart = (int)t
            });
            fishTossSprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(0, 0, 64, 64), 75f, 8, 0, __instance.GetCenterTile() * 64f + new Vector2(Game1.random.Next(-32, 32), Game1.random.Next(-16, 32)), flicker: false, Game1.random.NextBool(), (((float)(int)__instance.tileY + 0.5f) * 64f + 1f) / 10000f, 0.01f, Color.White, 0.75f, 0.003f, 0f, 0f)
            {
                delayBeforeAnimationStart = (int)t
            });
            if (who.IsLocalPlayer)
            {
                DelayedAction.playSoundAfterDelay("waterSlosh", (int)t, who.currentLocation);
                if (callback != null)
                {
                    DelayedAction.functionAfterDelay(callback, (int)t);
                }
            }
        }
    }
}