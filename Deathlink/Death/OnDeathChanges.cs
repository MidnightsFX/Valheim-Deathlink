using Deathlink.Common;
using HarmonyLib;
using Splatform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death;

public static class OnDeathChanges
{
    [HarmonyPatch(typeof(Player))]
    public static class OnDeath_Tombstone_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(Player.OnDeath))]
        static IEnumerable<CodeInstruction> ConstructorTranspiler( IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcher = new CodeMatcher(instructions, generator);
            // ModifyDeath takes the `this` that was loaded for CreateTombStone, so the branch skips that
            // call and the m_foods.Clear() after it: call, ldarg.0, ldfld, callvirt.
            codeMatcher.MatchStartForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Player), nameof(Player.CreateTombStone))))
                .ThrowIfNotMatch("Unable to patch Deathlink player death changes.")
                .Advance(1)
                .InsertAndAdvance(Transpilers.EmitDelegate(ModifyDeath))
                .CreateLabelOffset(out Label label, offset: 4)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Br, label));

            // Set in place rather than replacing the instruction, so any label on it survives.
            MethodInfo addPin = AccessTools.Method(typeof(Minimap), nameof(Minimap.AddPin));
            codeMatcher.MatchStartForward(new CodeMatch(instruction => instruction.Calls(addPin)))
                .ThrowIfNotMatch("Unable to patch Deathlink death map marker.")
                .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(OnDeath_Tombstone_Patch), nameof(AddDeathPinIfEnabled)));

            return codeMatcher.Instructions();
        }

        // Same arguments as Minimap.AddPin plus the instance, so the stack vanilla built for AddPin is
        // consumed unchanged. Vanilla pops the result, so null is fine when the pin is skipped.
        // Public because it is called directly from the patched method body.
        public static Minimap.PinData AddDeathPinIfEnabled(Minimap minimap, Vector3 pos, Minimap.PinType type, string name, bool save, bool isChecked, long ownerID, PlatformUserID author)
        {
            if (!ValConfig.ShowDeathMapMarker.Value) { return null; }
            return minimap.AddPin(pos, type, name, save, isChecked, ownerID, author);
        }

        // Runs inside the transpiled Player.OnDeath: an exception here would skip the rest of it,
        // RequestRespawn included, and leave the player dead for good.
        private static void ModifyDeath(Player __instance)
        {
            try {
                TombstoneOnDeath(__instance);
            } catch (Exception e) {
                Logger.LogError($"Deathlink failed to handle this death's items: {e}");
            }
            try {
                FoodLossOnDeath(__instance);
            } catch (Exception e) {
                Logger.LogError($"Deathlink failed to apply food loss on death: {e}");
            }
        }

        private static void FoodLossOnDeath(Player instance)
        {
            DeathProgressionDetails style = Deathlink.pcfg().DeathStyle;
            if (!style.foodLossOnDeath) { return; }
            int lost = instance.m_foods.Count;
            if (style.foodLossUsesDeathlink) {
                float skill = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
                lost = skill >= 0.9f ? 0 : skill >= 0.6f ? 1 : skill >= 0.3f ? 2 : lost;
            }
            instance.m_foods.RemoveRange(0, Mathf.Min(lost, instance.m_foods.Count));
        }

        private static void TombstoneOnDeath(Player instance) {
            DeathSavingOptions.ResolveUnchosenKeepItems();
            DeathProgressionDetails style = Deathlink.pcfg().DeathStyle;
            List<ItemDrop.ItemData> skillChecked = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> notSkillChecked = new List<ItemDrop.ItemData>();
            Dictionary<ItemResults, List<ItemDrop.ItemData>> itemResults = new Dictionary<ItemResults, List<ItemDrop.ItemData>>();

            // Only ChooseEquipment fills these. It decides for itself what goes in the tombstone, so it
            // bypasses ItemSavedStyle and EquipmentSavedStyle entirely.
            List<ItemDrop.ItemData> alwaysToTombstone = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> equipmentToChooseFrom = null;
            int equipmentPicks = 0;

            // AzuEPI quickslots are extra rows of m_inventory, so their items land in these two lists and
            // are decided along with the rest of the inventory.
            string[] nonSkillCheckedItems = ValConfig.ItemsNotSkillChecked.Value.Split(',');
            foreach (ItemDrop.ItemData item in instance.m_inventory.GetAllItems()) {
                if (item.m_dropPrefab != null && nonSkillCheckedItems.Contains(item.m_dropPrefab.name)) {
                    notSkillChecked.Add(item);
                } else {
                    skillChecked.Add(item);
                }
            }
            if (Logger.IsDebug) {
                Logger.LogDebug($"Death with {skillChecked.Count + notSkillChecked.Count} items ({notSkillChecked.Count} not skill checked), " +
                    $"{skillChecked.Count(item => item.m_equipped)} equipped, lowest row {(skillChecked.Count > 0 ? skillChecked.Max(item => item.m_gridPos.y) : 0)}.");
            }

            switch (style.itemLossStyle) {
                case ItemLossStyle.None:
                    Logger.LogDebug("No items destroyed on death.");
                    // Split so equipment and everything else can be sent to different places.
                    itemResults.SafeInsertOrAppend(ItemResults.EquipmentSaved, skillChecked.GetEquipment());
                    itemResults.SafeInsertOrAppend(ItemResults.ItemSaved, skillChecked.GetNotEquipment());
                    break;
                case ItemLossStyle.DestroyNonWeaponArmor:
                    Logger.LogDebug("Destroying non Equipment items.");
                    List<ItemDrop.ItemData> notEquipment = skillChecked.GetNotEquipment();
                    itemResults.SafeInsertOrAppend(ItemResults.ItemLost, notEquipment);
                    itemResults.SafeInsertOrAppend(ItemResults.EquipmentSaved, skillChecked.GetEquipment());
                    notEquipment.ForEach(item => instance.m_inventory.RemoveItem(item));
                    break;
                case ItemLossStyle.DestroyAll:
                    Logger.LogDebug("Destroying all non skill checked items.");
                    itemResults.SafeInsertOrAppend(ItemResults.ItemLost, skillChecked);
                    skillChecked.ForEach(item => instance.m_inventory.RemoveItem(item));
                    break;
                case ItemLossStyle.DeathlinkBased:
                    float deathskill = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
                    int totalKept = CalculateItemsToKeep(deathskill) + CalculateEquipmentSavable(deathskill);
                    int choiceCount = Mathf.Clamp(ScaleBySkill(style.minItemsKeptChoices, style.maxItemsKeptChoices, deathskill), 0, totalKept);
                    // Only worth asking when there are picks to make and something would otherwise be lost.
                    if (style.EnableItemSavingChoices && choiceCount > 0 && skillChecked.Count > totalKept) {
                        Logger.LogDebug($"Deferring item saving to respawn choice. keep {totalKept}, pick {choiceCount}, pool {skillChecked.Count}");
                        // Taken out now so the inventory saved for the respawn is the reduced one; the
                        // chosen items are put back once the player picks them.
                        skillChecked.ForEach(item => instance.m_inventory.RemoveItem(item));
                        DeathSavingOptions.CaptureDeathChoice(instance, skillChecked, totalKept, choiceCount);
                        break;
                    }
                    Logger.LogDebug("Destroying random items based on deathlink skill");
                    itemResults = DetermineItemResultsByDeathlink(skillChecked);
                    itemResults[ItemResults.EquipmentLost].ForEach(item => instance.m_inventory.RemoveItem(item));
                    itemResults[ItemResults.ItemLost].ForEach(item => instance.m_inventory.RemoveItem(item));
                    break;
                case ItemLossStyle.ChooseEquipment:
                    // Everything goes in the tombstone and the picks are pulled back out after respawn, so
                    // quitting before choosing leaves the player with a normal full tombstone.
                    List<ItemDrop.ItemData> equipment = skillChecked.GetEquipment();
                    equipmentPicks = CalculateEquipmentSavable(DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
                    alwaysToTombstone.AddRange(skillChecked.GetNotEquipment());
                    if (equipment.Count <= equipmentPicks) {
                        // Every piece fits the allowance, so it all stays on the character, still equipped.
                        Logger.LogDebug($"ChooseEquipment| keeping all {equipment.Count} equipment, allowance is {equipmentPicks}.");
                    } else {
                        alwaysToTombstone.AddRange(equipment);
                        if (equipmentPicks > 0) { equipmentToChooseFrom = equipment; }
                        Logger.LogDebug($"ChooseEquipment| {equipment.Count} equipment to the tombstone, {equipmentPicks} can be taken back.");
                    }
                    break;
            }

            if (style.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Destroy) {
                Logger.LogDebug($"Non skill checked being destroyed.");
                itemResults.SafeInsertOrAppend(ItemResults.ItemLost, notSkillChecked);
                // Vanilla CreateTombStone is skipped, so anything left in the inventory stays on the character.
                notSkillChecked.ForEach(item => instance.m_inventory.RemoveItem(item));
            }

            // Collect everything headed for the tombstone first, so one is only spawned when something
            // will actually be in it. An empty one despawns at once and still hands out its loot boost.
            List<ItemDrop.ItemData> toTombstone = new List<ItemDrop.ItemData>();
            if (style.EquipmentDestination() == ItemSavedStyle.Tombstone && itemResults.ContainsKey(ItemResults.EquipmentSaved)) {
                toTombstone.AddRange(itemResults[ItemResults.EquipmentSaved]);
            }
            if (style.itemSavedStyle == ItemSavedStyle.Tombstone && itemResults.ContainsKey(ItemResults.ItemSaved)) {
                toTombstone.AddRange(itemResults[ItemResults.ItemSaved]);
            }
            toTombstone.AddRange(alwaysToTombstone);
            if (style.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Tombstone) {
                toTombstone.AddRange(notSkillChecked);
            }
            if (toTombstone.Count == 0) { return; }

            Logger.LogDebug($"Tombstone needed for {toTombstone.Count} items.");
            GameObject tombstoneGo = UnityEngine.Object.Instantiate<GameObject>(instance.m_tombstone, instance.GetCenterPoint(), instance.transform.rotation);
            Inventory tombstoneInv = tombstoneGo.GetComponent<Container>().GetInventory();

            // Vanilla unequips before filling the grave. Without this an item keeps its equipped flag
            // in the tombstone and looks worn after it is taken back. Gear that stays on the
            // character is left equipped, so it is still worn after respawn.
            foreach (ItemDrop.ItemData item in toTombstone) {
                if (item.m_equipped) { instance.UnequipItem(item, triggerEquipEffects: false); }
            }
            AddItemsToTombstone(tombstoneInv, toTombstone);
            toTombstone.ForEach(item => instance.m_inventory.RemoveItem(item));

            PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
            long playerId = playerProfile.GetPlayerID();
            tombstoneGo.GetComponent<TombStone>().Setup(playerProfile.GetName(), playerId);
            instance.m_inventory.Changed();

            // After the items are in: the choice records each piece's slot in the tombstone.
            if (equipmentToChooseFrom != null) {
                DeathSavingOptions.CaptureEquipmentChoice(tombstoneGo.GetComponent<ZNetView>().GetZDO().m_uid,
                    playerId, equipmentToChooseFrom, equipmentPicks);
            }
        }

        private static void AddItemsToTombstone(Inventory tombstone, List<ItemDrop.ItemData> transferItems) {
            int size = Mathf.RoundToInt(Mathf.Sqrt(transferItems.Count)) + 1;
            tombstone.m_width += size;
            tombstone.m_height += size;
            foreach (ItemDrop.ItemData item in transferItems) {
                // Items keep their player-inventory slot, which extra inventory rows (AzuEPI) can put
                // outside the grid, so make sure every slot is inside it.
                tombstone.m_width = Mathf.Max(tombstone.m_width, item.m_gridPos.x + 1);
                tombstone.m_height = Mathf.Max(tombstone.m_height, item.m_gridPos.y + 1);
                tombstone.m_inventory.Add(item);
            }
            tombstone.Changed();
        }

        private static int ScaleBySkill(int min, int max, float deathskill) {
            return Mathf.RoundToInt((max - min) * deathskill + min);
        }

        private static int CalculateItemsToKeep(float deathskill) {
            DeathProgressionDetails style = Deathlink.pcfg().DeathStyle;
            return ScaleBySkill(style.minItemsKept, style.maxItemsKept, deathskill);
        }

        // Also the number of pieces a ChooseEquipment death lets the player take back from the tombstone.
        private static int CalculateEquipmentSavable(float deathskill) {
            DeathProgressionDetails style = Deathlink.pcfg().DeathStyle;
            return ScaleBySkill(style.minEquipmentKept, style.maxEquipmentKept, deathskill);
        }

        private static Dictionary<ItemResults, List<ItemDrop.ItemData>> DetermineItemResultsByDeathlink(List<ItemDrop.ItemData> skillChecked) {
            float deathskill = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
            int itemsToKeep = CalculateItemsToKeep(deathskill);
            int equipmentToKeep = CalculateEquipmentSavable(deathskill);
            // One shared budget: kept equipment and kept items both draw from it.
            int savesLeft = itemsToKeep + equipmentToKeep;

            Dictionary<ItemResults, List<ItemDrop.ItemData>> itemResults = new Dictionary<ItemResults, List<ItemDrop.ItemData>>() {
                { ItemResults.EquipmentSaved, new List<ItemDrop.ItemData>() },
                { ItemResults.EquipmentLost, new List<ItemDrop.ItemData>() },
                { ItemResults.ItemSaved, new List<ItemDrop.ItemData>() },
                { ItemResults.ItemLost, new List<ItemDrop.ItemData>() }
            };

            foreach (var equipment in Deathlink.shuffleList(skillChecked.GetEquipment())) {
                if (savesLeft > 0 && equipmentToKeep > 0) {
                    itemResults[ItemResults.EquipmentSaved].Add(equipment);
                    savesLeft--;
                    equipmentToKeep--;
                } else {
                    itemResults[ItemResults.EquipmentLost].Add(equipment);
                }
            }

            foreach (var item in Deathlink.shuffleList(skillChecked.GetNotEquipment())) {
                if (savesLeft > 0 && itemsToKeep > 0) {
                    itemResults[ItemResults.ItemSaved].Add(item);
                    savesLeft--;
                    itemsToKeep--;
                } else {
                    itemResults[ItemResults.ItemLost].Add(item);
                }
            }

            if (Logger.IsDebug) {
                Logger.LogDebug($"Deathlink item rolls| equipment kept {itemResults[ItemResults.EquipmentSaved].Count}, lost {itemResults[ItemResults.EquipmentLost].Count}; " +
                    $"items kept {itemResults[ItemResults.ItemSaved].Count}, lost {itemResults[ItemResults.ItemLost].Count}");
            }
            return itemResults;
        }
    }
}
