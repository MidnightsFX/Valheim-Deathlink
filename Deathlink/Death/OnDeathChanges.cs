using Deathlink.Common;
using HarmonyLib;
using Splatform;
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
            codeMatcher.MatchStartForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Player), nameof(Player.CreateTombStone))))
                .Advance(1)
                .InsertAndAdvance(Transpilers.EmitDelegate(ModifyDeath))
                .CreateLabelOffset(out Label label, offset: 4)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Br, label))
                .ThrowIfNotMatch("Unable to patch Deathlink player death changes.");

            // Vanilla pins every death unconditionally. Route that one call through the
            // ShowDeathMapMarker toggle so it is the only thing deciding whether a death pin appears.
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

        private static void ModifyDeath(Player __instance)
        {
            // Tombstone item dropped modifications
            TombstoneOnDeath(__instance);

            // Set food loss status based on configs & skills
            FoodLossOnDeath(__instance);
        }

        public static void FoodLossOnDeath(Player instance)
        {
            if (Deathlink.pcfg().DeathStyle.foodLossOnDeath)
            {
                if (Deathlink.pcfg().DeathStyle.foodLossUsesDeathlink && instance.m_foods.Count > 0)
                {
                    float skill_level = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
                    if (skill_level >= 0.9f)
                    {
                        // We don't remove any food if the players skill percentage is above 90%
                    }
                    else if (skill_level >= 0.6f && skill_level < 0.9f)
                    {
                        instance.m_foods.Remove(instance.m_foods[0]);
                    }
                    else if (skill_level > 0.3f && skill_level < 0.6f)
                    {
                        instance.m_foods.Remove(instance.m_foods[0]);
                        if (instance.m_foods.Count > 0) { instance.m_foods.Remove(instance.m_foods[0]); }
                    }
                    else
                    {
                        instance.m_foods.Clear();
                    }
                }
                else {
                    instance.m_foods.Clear();
                }
            }
        }

        public static void TombstoneOnDeath(Player instance) {
            List<ItemDrop.ItemData> playerItems = instance.m_inventory.GetAllItems();
            List<ItemDrop.ItemData> playerItemsWithoutNonSkillCheckedItems = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> playerNonSkillCheckItems = new List<ItemDrop.ItemData>();
            Inventory inventory = instance.m_inventory;

            Dictionary<ItemResults, List<ItemDrop.ItemData>> itemResults = new Dictionary<ItemResults, List<ItemDrop.ItemData>>();

            // Only ChooseEquipment fills these. It decides for itself what goes in the tombstone, so it
            // bypasses ItemSavedStyle and EquipmentSavedStyle entirely.
            List<ItemDrop.ItemData> alwaysToTombstone = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> equipmentToChooseFrom = null;
            int equipmentPicks = 0;

            // Filter out the non-skill-checked items, this allows them to act differently than those that are skill checked
            string[] nonSkillCheckedItems = ValConfig.ItemsNotSkillChecked.Value.Split(',');
            foreach (ItemDrop.ItemData item in playerItems) {
                if (item.m_dropPrefab != null && nonSkillCheckedItems.Contains(item.m_dropPrefab.name)) {
                    playerNonSkillCheckItems.Add(item);
                } else {
                    playerItemsWithoutNonSkillCheckedItems.Add(item);
                }
            }
            // AzuEPI quickslots are extra rows of m_inventory, so their items are already in the two lists
            // above and every case below decides them along with the rest of the inventory.

            // Item destruction style
            switch (Deathlink.pcfg().DeathStyle.itemLossStyle) {
                case DataObjects.ItemLossStyle.None:
                    Logger.LogDebug("No items destroyed on death.");
                    // Split so equipment and everything else can be sent to different places.
                    itemResults.SafeInsertOrAppend(ItemResults.EquipmentSaved, playerItemsWithoutNonSkillCheckedItems.GetEquipment());
                    itemResults.SafeInsertOrAppend(ItemResults.ItemSaved, playerItemsWithoutNonSkillCheckedItems.GetNotEquipment());
                    break;
                case DataObjects.ItemLossStyle.DestroyNonWeaponArmor:
                    Logger.LogDebug("Destroying non Equipment items.");
                    itemResults.SafeInsertOrAppend(ItemResults.ItemLost, playerItemsWithoutNonSkillCheckedItems.GetNotEquipment());
                    itemResults.SafeInsertOrAppend(ItemResults.EquipmentSaved, playerItemsWithoutNonSkillCheckedItems.GetEquipment());
                    foreach (var item in playerItemsWithoutNonSkillCheckedItems.GetNotEquipment()) {
                        instance.m_inventory.RemoveItem(item);
                    }
                    break;
                case DataObjects.ItemLossStyle.DestroyAll:
                    Logger.LogDebug("Destroying all non skill checked items.");
                    itemResults.SafeInsertOrAppend(ItemResults.ItemLost, playerItemsWithoutNonSkillCheckedItems);
                    foreach(ItemDrop.ItemData item in playerItemsWithoutNonSkillCheckedItems) {
                        instance.m_inventory.RemoveItem(item);
                    }
                    break;
                case DataObjects.ItemLossStyle.DeathlinkBased:
                    float deathskill = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
                    int totalKept = CalculateItemsSavable(deathskill);
                    int choiceCount = Mathf.Clamp(CalculateItemChoiceCount(deathskill), 0, totalKept);
                    // Player-driven item saving: defer the save/loss decision to a respawn UI when it
                    // is enabled and there is actually a choice to make (the player has picks and some
                    // items would otherwise be lost).
                    if (Deathlink.pcfg().DeathStyle.EnableItemSavingChoices
                        && choiceCount > 0
                        && playerItemsWithoutNonSkillCheckedItems.Count > totalKept) {
                        Logger.LogDebug($"Deferring item saving to respawn choice. keep {totalKept}, pick {choiceCount}, pool {playerItemsWithoutNonSkillCheckedItems.Count}");
                        // Remove every candidate now so the reduced inventory is what gets saved for
                        // respawn; the chosen items are re-added once the player picks them.
                        foreach (var item in playerItemsWithoutNonSkillCheckedItems) {
                            instance.m_inventory.RemoveItem(item);
                        }
                        DeathSavingOptions.CaptureDeathChoice(playerItemsWithoutNonSkillCheckedItems, totalKept, choiceCount);
                        // Skill-checked items are handled on respawn, so nothing is added to
                        // itemResults for them here.
                        break;
                    }
                    Logger.LogDebug("Destroying random items based on deathlink skill");
                    itemResults = DetermineItemResultsByDeathlink(instance, playerItemsWithoutNonSkillCheckedItems);
                    itemResults[ItemResults.EquipmentLost].ForEach(item => instance.m_inventory.RemoveItem(item));
                    itemResults[ItemResults.ItemLost].ForEach(item => instance.m_inventory.RemoveItem(item));
                    break;
                case DataObjects.ItemLossStyle.ChooseEquipment:
                    // Everything goes in the tombstone and the picks are pulled back out after respawn, so
                    // quitting before choosing leaves the player with a normal full tombstone.
                    List<ItemDrop.ItemData> equipment = playerItemsWithoutNonSkillCheckedItems.GetEquipment();
                    equipmentPicks = CalculateEquipmentSavable(DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
                    alwaysToTombstone.AddRange(playerItemsWithoutNonSkillCheckedItems.GetNotEquipment());
                    if (equipment.Count <= equipmentPicks) {
                        // Every piece fits the allowance, so there is nothing to choose: it all stays on the
                        // character, still equipped.
                        Logger.LogDebug($"ChooseEquipment| keeping all {equipment.Count} equipment, allowance is {equipmentPicks}.");
                    } else {
                        alwaysToTombstone.AddRange(equipment);
                        if (equipmentPicks > 0) { equipmentToChooseFrom = equipment; }
                        Logger.LogDebug($"ChooseEquipment| {equipment.Count} equipment to the tombstone, {equipmentPicks} can be taken back.");
                    }
                    break;
            }

            // Determine what happens to the non-skill checked items, these items can also be added to the tombstone
            if (Deathlink.pcfg().DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Destroy) {
                Logger.LogDebug($"Non skill checked being destroyed.");
                itemResults.SafeInsertOrAppend(ItemResults.ItemLost, playerNonSkillCheckItems);
                // Vanilla CreateTombStone is skipped, so anything left in the inventory stays on the character.
                playerNonSkillCheckItems.ForEach(item => instance.m_inventory.RemoveItem(item));
            }
            // Items not skill checked are requested to go to the tombstone
            if (Deathlink.pcfg().DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Save) {
                Logger.LogDebug($"Non skill checked items being left on player.");
            }

            // Collect everything headed for the tombstone first, so one is only spawned when something
            // will actually be in it. An empty one despawns at once and still hands out its loot boost.
            DeathProgressionDetails style = Deathlink.pcfg().DeathStyle;
            List<ItemDrop.ItemData> toTombstone = new List<ItemDrop.ItemData>();
            if (style.EquipmentDestination() == ItemSavedStyle.Tombstone && itemResults.ContainsKey(ItemResults.EquipmentSaved)) {
                toTombstone.AddRange(itemResults[ItemResults.EquipmentSaved]);
            }
            if (style.itemSavedStyle == ItemSavedStyle.Tombstone && itemResults.ContainsKey(ItemResults.ItemSaved)) {
                toTombstone.AddRange(itemResults[ItemResults.ItemSaved]);
            }
            toTombstone.AddRange(alwaysToTombstone);
            // Items not skill checked are requested to go to the tombstone
            if (style.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Tombstone) {
                toTombstone.AddRange(playerNonSkillCheckItems);
            }

            if (toTombstone.Count > 0) {
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

                TombStone tombstone = tombstoneGo.GetComponent<TombStone>();
                PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
                string name = playerProfile.GetName();
                long playerId = playerProfile.GetPlayerID();
                tombstone.Setup(name, playerId);
                inventory.Changed();

                // After the items are in: the choice records each piece's slot in the tombstone.
                if (equipmentToChooseFrom != null) {
                    DeathSavingOptions.CaptureEquipmentChoice(tombstoneGo.GetComponent<ZNetView>().GetZDO().m_uid,
                        playerId, equipmentToChooseFrom, equipmentPicks);
                }
            }
        }

        public static void AddItemsToTombstone(Inventory tombstone, List<ItemDrop.ItemData> transferItems) {
            int size = Mathf.RoundToInt(Mathf.Sqrt(transferItems.Count())) + 1;
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

        /// <summary>
        /// Skill-scaled count of non-equipment items kept on a DeathlinkBased death.
        /// </summary>
        internal static int CalculateItemsToKeep(float deathskillbonus) {
            return Mathf.RoundToInt(((Deathlink.pcfg().DeathStyle.maxItemsKept - Deathlink.pcfg().DeathStyle.minItemsKept) * deathskillbonus) + Deathlink.pcfg().DeathStyle.minItemsKept);
        }

        /// <summary>
        /// Skill-scaled count of equipment items kept on a DeathlinkBased death, and the number of pieces
        /// a ChooseEquipment death lets the player take back from the tombstone.
        /// </summary>
        internal static int CalculateEquipmentSavable(float deathskillbonus) {
            return Mathf.RoundToInt(((Deathlink.pcfg().DeathStyle.maxEquipmentKept - Deathlink.pcfg().DeathStyle.minEquipmentKept) * deathskillbonus) + Deathlink.pcfg().DeathStyle.minEquipmentKept);
        }

        /// <summary>
        /// Total items saved on a DeathlinkBased death (equipment + non-equipment budget). Shared by
        /// the random-save path and the player-choice path so both agree on the keep budget.
        /// </summary>
        internal static int CalculateItemsSavable(float deathskillbonus) {
            return CalculateItemsToKeep(deathskillbonus) + CalculateEquipmentSavable(deathskillbonus);
        }

        /// <summary>
        /// Skill-scaled number of items the player may hand-pick in the death saving choice UI.
        /// </summary>
        internal static int CalculateItemChoiceCount(float deathskillbonus) {
            return Mathf.RoundToInt(((Deathlink.pcfg().DeathStyle.maxItemsKeptChoices - Deathlink.pcfg().DeathStyle.minItemsKeptChoices) * deathskillbonus) + Deathlink.pcfg().DeathStyle.minItemsKeptChoices);
        }

        internal static Dictionary<ItemResults, List<ItemDrop.ItemData>> DetermineItemResultsByDeathlink(Player player, List<ItemDrop.ItemData> playerItemsWithoutNonSkillCheckedItems) {
            float deathskillbonus = DeathProgressionSkill.DeathSkillCalculatePercentWithBonus();
            int items_to_keep = CalculateItemsToKeep(deathskillbonus);
            int max_equipment_savable = CalculateEquipmentSavable(deathskillbonus);
            int numberOfItemsSavable = items_to_keep + max_equipment_savable;

            Dictionary<ItemResults, List<ItemDrop.ItemData>> itemResults = new Dictionary<ItemResults, List<ItemDrop.ItemData>>(){
                { ItemResults.EquipmentSaved, new List<ItemDrop.ItemData>() },
                { ItemResults.EquipmentLost, new List<ItemDrop.ItemData>() },
                { ItemResults.ItemSaved, new List<ItemDrop.ItemData>() },
                { ItemResults.ItemLost, new List<ItemDrop.ItemData>() }
            }
            ;

            // Equipment items are handled differently than resources etc
            int equipment_saved = 0;
            foreach (var equipment in Deathlink.shuffleList(playerItemsWithoutNonSkillCheckedItems.GetEquipment())) {
                if (numberOfItemsSavable > 0) {
                    if (RemoveEquipmentByStyle(equipment_saved, max_equipment_savable, equipment, numberOfItemsSavable, out numberOfItemsSavable, out equipment_saved)) {
                        // If the item is not equipped but is still equipment, it should be saved since we have space for it
                        Logger.LogDebug($"Save: {equipment.m_shared.m_name}");
                        itemResults[ItemResults.EquipmentSaved].Add(equipment);
                    } else {
                        Logger.LogDebug($"Remove by style: {equipment.m_shared.m_name}");
                        itemResults[ItemResults.EquipmentLost].Add(equipment);
                    }
                } else {
                    Logger.LogDebug($"Save limit reached, removing: {equipment.m_shared.m_name}");
                    itemResults[ItemResults.EquipmentLost].Add(equipment);
                }
            }

            // we still have savable space after saving any equipped items
            List<ItemDrop.ItemData> nonEquippableItems = Deathlink.shuffleList(playerItemsWithoutNonSkillCheckedItems.GetNotEquipment());
            if (numberOfItemsSavable > 0) {
                // shuffle inventory items that are not equipment
                foreach (var item in nonEquippableItems) {
                    if (numberOfItemsSavable > 0 && items_to_keep > 0) {
                        Logger.LogDebug($"NonEq| Saving: {item.m_shared.m_name}");
                        itemResults[ItemResults.ItemSaved].Add(item);
                        numberOfItemsSavable -= 1;
                        items_to_keep -= 1;
                    } else {
                        Logger.LogDebug($"NonEq| Removing: {item.m_shared.m_name}");
                        itemResults[ItemResults.ItemLost].Add(item);
                    }
                }
            } else {
                Logger.LogDebug($"NonEq| Removing: {nonEquippableItems.Count} due to no saves remaining.");
                foreach(var item in nonEquippableItems) {
                    itemResults[ItemResults.ItemLost].Add(item);
                }
            }

            return itemResults;
        }

        internal static bool RemoveEquipmentByStyle(int equipment_saved, int max_equipment_savable, ItemDrop.ItemData equipment, int numberOfItemsSavable, out int remainingsaves, out int equipment_saved_count)
        {
            // Default the out params to the caller's *current* state, not zero. These are assigned
            // straight back into the caller's numberOfItemsSavable/equipment_saved, so returning 0
            // on a "don't save this piece" outcome would wipe the shared save budget and destroy
            // every remaining (including non-equipment) item. Preserve the budget instead.
            equipment_saved_count = equipment_saved;
            remainingsaves = numberOfItemsSavable;
            if (numberOfItemsSavable <= 0) {
                return false;
            }

            if (equipment_saved >= max_equipment_savable) {
                Logger.LogDebug($"Max equipment retained ({max_equipment_savable}) reached, removing {equipment.m_dropPrefab.name}");
                return false;
            }

            Logger.LogDebug($"Saving equipment remaining savable?({numberOfItemsSavable}) {equipment.m_dropPrefab.name}");
            equipment_saved_count = equipment_saved + 1;
            remainingsaves = numberOfItemsSavable - 1;
            return true;
        }
    }
}