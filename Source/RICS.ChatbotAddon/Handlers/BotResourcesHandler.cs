// BotResourcesHandler.cs
// Copyright (c) Captolamia
// Licensed under AGPLv3 — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CAP_RICS_ChatbotAddon.Handlers
{
    internal static class BotResourcesHandler
    {
        public static string Build()
        {
            try
            {
                Map map = BotMapHelper.GetPlayerMap();
                if (map == null)
                    return BotMapHelper.ErrorNoMapJson();

                int meals = 0, meat = 0, fish = 0, milk = 0, vegetables = 0, fruit = 0, eggs = 0, otherRaw = 0;
                int medicineTotal = 0;
                int wood = 0, fabric = 0, leather = 0, wool = 0, metals = 0, stoneBlocks = 0;
                int componentIndustrial = 0, componentSpacer = 0;
                var medicineBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    var counter = map.resourceCounter;
                    if (counter != null)
                    {
                        foreach (var kvp in counter.AllCountedAmounts)
                        {
                            var def = kvp.Key;
                            if (def == null) continue;
                            int count = kvp.Value;

                            if (def.IsMedicine)
                            {
                                string key = def.label?.CapitalizeFirst() ?? def.defName;
                                medicineBreakdown[key] = count;
                                medicineTotal += count;
                            }

                            if (def.ingestible != null)
                            {
                                var foodType = def.ingestible.foodType;
                                var cats = def.thingCategories ?? new List<ThingCategoryDef>();

                                bool isMeal = cats.Any(c => c.defName.Equals("Meals", StringComparison.OrdinalIgnoreCase))
                                              || (foodType & FoodTypeFlags.Meal) != 0
                                              || (foodType & FoodTypeFlags.Processed) != 0;
                                if (isMeal)
                                {
                                    meals += count;
                                    continue;
                                }

                                if (def.IsNutritionGivingIngestible)
                                {
                                    bool isFish = cats.Any(c => c.defName.Equals("Fish", StringComparison.OrdinalIgnoreCase))
                                                  || def.defName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0;
                                    if (isFish) { fish += count; continue; }

                                    bool isMeat = cats.Any(c => c.defName.Equals("Meat", StringComparison.OrdinalIgnoreCase))
                                                  || (foodType & FoodTypeFlags.Meat) != 0;
                                    if (isMeat) { meat += count; continue; }

                                    if (def.defName.Equals("Milk", StringComparison.OrdinalIgnoreCase)
                                        || def.defName.IndexOf("Milk", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        milk += count;
                                        continue;
                                    }

                                    bool isEgg = cats.Any(c => c.defName.StartsWith("Eggs", StringComparison.OrdinalIgnoreCase))
                                                 || def.defName.IndexOf("Egg", StringComparison.OrdinalIgnoreCase) >= 0;
                                    if (isEgg) { eggs += count; continue; }

                                    bool isFruit = cats.Any(c => c.defName.IndexOf("Fruit", StringComparison.OrdinalIgnoreCase) >= 0);
                                    if (isFruit) { fruit += count; continue; }

                                    bool isVeg = (foodType & FoodTypeFlags.VegetableOrFruit) != 0
                                                 || cats.Any(c => c.defName.Equals("Vegetables", StringComparison.OrdinalIgnoreCase)
                                                                  || c.defName.Equals("PlantFoodRaw", StringComparison.OrdinalIgnoreCase));
                                    if (isVeg) { vegetables += count; continue; }

                                    otherRaw += count;
                                }
                            }

                            if (def.defName == "WoodLog") wood += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Textiles" || c.defName == "Fabric") == true) fabric += count;
                            else if (def.thingCategories?.Any(c => c.defName == "Leathers") == true) leather += count;
                            else if (def.defName.StartsWith("Wool")) wool += count;
                            else if (def.defName == "Steel" || def.defName == "Plasteel") metals += count;
                            else if (def.defName.EndsWith("Block") || def.defName.Contains("Blocks")) stoneBlocks += count;

                            if (def.defName == "ComponentIndustrial") componentIndustrial += count;
                            else if (def.defName == "ComponentSpacer") componentSpacer += count;
                        }
                    }
                }
                catch
                {
                    // partial ok
                }

                int rawFood = meat + fish + milk + vegetables + fruit + eggs + otherRaw;

                var payload = new BotResourcesPayload
                {
                    status = "ok",
                    command = "botresources",
                    food = new BotFoodBlock
                    {
                        meals = meals,
                        rawFood = rawFood,
                        meat = meat,
                        fish = fish,
                        milk = milk,
                        vegetables = vegetables,
                        fruit = fruit,
                        eggs = eggs,
                        other = otherRaw
                    },
                    medicine = new BotMedicineBlock
                    {
                        total = medicineTotal,
                        breakdown = medicineBreakdown
                    },
                    materials = new BotMaterialsBlock
                    {
                        wood = wood,
                        fabric = fabric,
                        leather = leather,
                        wool = wool,
                        metals = metals,
                        stoneBlocks = stoneBlocks
                    },
                    components = new BotComponentsBlock
                    {
                        industrial = componentIndustrial,
                        spacer = componentSpacer
                    }
                };

                return BotJson.Serialize(payload);
            }
            catch (Exception ex)
            {
                return BotMapHelper.ErrorExceptionJson(ex.Message);
            }
        }
    }

    internal class BotResourcesPayload
    {
        public string status;
        public string command;
        public BotFoodBlock food;
        public BotMedicineBlock medicine;
        public BotMaterialsBlock materials;
        public BotComponentsBlock components;
    }

    internal class BotFoodBlock
    {
        public int meals;
        public int rawFood;
        public int meat;
        public int fish;
        public int milk;
        public int vegetables;
        public int fruit;
        public int eggs;
        public int other;
    }

    internal class BotMedicineBlock
    {
        public int total;
        public Dictionary<string, int> breakdown;
    }

    internal class BotMaterialsBlock
    {
        public int wood;
        public int fabric;
        public int leather;
        public int wool;
        public int metals;
        public int stoneBlocks;
    }

    internal class BotComponentsBlock
    {
        public int industrial;
        public int spacer;
    }
}
