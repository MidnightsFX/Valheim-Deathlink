using Deathlink.Death;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Deathlink.Common;

public class DataObjects
{
    public static ISerializer yamlserializer = YamlFormat.Default.Serializer;

    // Tolerant: leaderboard data from older builds carries keys this type no longer maps.
    public static IDeserializer leaderboardDeserializer = YamlFormat.Default.TolerantDeserializer;

    public static readonly string DeathChoiceKey = "DL_DeathChoice";
    public static readonly string DeathChoiceChangesKey = "DL_ChoiceChanges";

    public static readonly string DamageTakenModifierKey = "DL_DmgTaken";
    public static readonly string DamageDoneModifierKey = "DL_DmgDone";

    // Kept as player unique keys so the leaderboard accumulators survive relogs between server syncs.
    public static readonly string LeaderboardFirstLifeKey = "DL_LBFirst";
    public static readonly string LeaderboardLongestLifeKey = "DL_LBLongest";
    public static readonly string LeaderboardTotalLifeKey = "DL_LBTotal";
    public static readonly string LeaderboardDeathCountKey = "DL_LBDeaths";
    public static readonly string LeaderboardDamageKey = "DL_LBDamage";

    // New members go on the END: the editor's cycle button maps a member's position to its value.
    public enum ItemLossStyle
    {
        None,
        DestroyNonWeaponArmor,
        DeathlinkBased,
        DestroyAll,
        // Nothing is destroyed: everything goes to the tombstone and the player takes some equipment back.
        ChooseEquipment
    }

    public enum ItemResults
    {
        EquipmentSaved,
        EquipmentLost,
        ItemSaved,
        ItemLost,
    }

    public enum ItemSavedStyle
    {
        OnCharacter,
        Tombstone
    }

    // SameAsItems must stay the zero member: unset, omitted-on-write and misspelled values all land on it.
    public enum EquipmentSavedStyle
    {
        SameAsItems,
        OnCharacter,
        Tombstone
    }

    public enum ResourceGainTypes
    {
        Kills,
        Harvesting
    }

    public enum NonSkillCheckedItemAction
    {
        Destroy,
        Tombstone,
        Save
    }

    const string color_good = "#b9f2ff";
    const string color_bad = "#ff4040";

    // U+1F480. No Valheim font has it: it comes from the Noto Emoji font at the end of the TextMeshPro
    // fallback chain, so it only renders reliably in TMP text, not in a legacy UI.Text.
    const string skull = "\U0001F480";

    // The old misspelling of BonusModifier: still read, never written, because the shim property
    // returns null and OmitDefaults drops it.
    const string LegacyBonusModifier = "bonusModifer";

    // A bonus at or below 1 is left alone: a penalty should not get weaker with skill.
    internal static float ScaleTowardsOne(float bonus, float deathSkillPercent) {
        if (bonus <= 1f) { return bonus; }
        return 1f + ((bonus - 1f) * Mathf.Clamp01(deathSkillPercent));
    }

    // Any member whose initializer is not default(T) needs a matching [DefaultValue]. The serializer
    // omits values equal to default(T), so `foodLossOnDeath: false` would be dropped on rewrite and
    // read back as true.
    public class DeathProgressionDetails
    {
        [DefaultValue(true)]
        public bool foodLossOnDeath = true;
        [DefaultValue(true)]
        public bool foodLossUsesDeathlink = true;
        public int minItemsKept;
        public int maxItemsKept;
        public int minEquipmentKept;
        public int maxEquipmentKept;
        public int minItemsKeptChoices;
        public int maxItemsKeptChoices;
        [DefaultValue(true)]
        public bool skillLossOnDeath = true;
        public float maxSkillLossPercentage;
        public float minSkillLossPercentage;
        public ItemLossStyle itemLossStyle;
        public ItemSavedStyle itemSavedStyle;
        public EquipmentSavedStyle equipmentSavedStyle;
        [DefaultValue(true)]
        public bool EnableItemSavingChoices = true;
        [DefaultValue(NonSkillCheckedItemAction.Tombstone)]
        public NonSkillCheckedItemAction nonSkillCheckedItemAction = NonSkillCheckedItemAction.Tombstone;

        // A method rather than a property so YamlDotNet never tries to serialize it.
        internal ItemSavedStyle EquipmentDestination() {
            switch (equipmentSavedStyle) {
                case EquipmentSavedStyle.OnCharacter: return ItemSavedStyle.OnCharacter;
                case EquipmentSavedStyle.Tombstone: return ItemSavedStyle.Tombstone;
                default: return itemSavedStyle;
            }
        }
    }

    public class DeathResourceModifier
    {
        // Opt-in, with no [DefaultValue], so it is only written out once an admin turns it on.
        public bool skillInfluence { get; set; }
        public List<string> prefabs { get; set; }
        public float BonusModifier { get; set; }
        [YamlMember(Alias = LegacyBonusModifier)]
        public float? bonusModifer { get { return null; } set { if (value.HasValue) { BonusModifier = value.Value; ReadWithLegacySpelling = true; } } }

        // Only a pre-0.11 file has the old spelling; the skillInfluence migration keys off it.
        internal bool ReadWithLegacySpelling;
        public List<ResourceGainTypes> bonusActions { get; set; } = new List<ResourceGainTypes>();
    }

    public class DeathSkillModifier
    {
        // Opt-in, with no [DefaultValue], so it is only written out once an admin turns it on.
        public bool skillInfluence { get; set; }
        public Skills.SkillType skill { get; set; }
        public float BonusModifier { get; set; }
        [YamlMember(Alias = LegacyBonusModifier)]
        public float? bonusModifer { get { return null; } set { if (value.HasValue) { BonusModifier = value.Value; ReadWithLegacySpelling = true; } } }

        // Only a pre-0.11 file has the old spelling; the skillInfluence migration keys off it.
        internal bool ReadWithLegacySpelling;
    }

    public class DeathLootModifier
    {
        public string prefab { get; set; }
        public float chance { get; set; }
        [DefaultValue(1)]
        public int amount { get; set; } = 1;
        public List<ResourceGainTypes> bonusActions { get; set; } = new List<ResourceGainTypes>();
    }

    public class DeathChoiceLevel
    {
        public string DisplayName { get; set; }
        public DeathProgressionDetails DeathStyle { get; set; } = new DeathProgressionDetails();
        public bool Fallback { get; set; }
        [DefaultValue(1f)]
        public float DeathSkillRate { get; set; } = 1f;
        [DefaultValue(1f)]
        public float DamageTakenModifier { get; set; } = 1f;
        [DefaultValue(1f)]
        public float DamageDoneModifier { get; set; } = 1f;
        public Dictionary<string, DeathResourceModifier> ResourceModifiers { get; set; }
        public Dictionary<string, DeathSkillModifier> SkillModifiers { get; set; }
        public Dictionary<string, DeathLootModifier> DeathLootModifiers { get; set; }

        // Flat and skill-influenced sums are cached apart: the influenced half depends on current Deathlink skill.
        private readonly Dictionary<Skills.SkillType, Tuple<float, float>> CalculatedSkillMods = new Dictionary<Skills.SkillType, Tuple<float, float>>();
        private Dictionary<string, Tuple<float, bool>>[] resourceModsByAction;
        private List<KeyValuePair<GameObject, Tuple<float, int>>>[] lootByAction;
        private static readonly List<KeyValuePair<GameObject, int>> NoLoot = new List<KeyValuePair<GameObject, int>>();

        private static bool AppliesTo(List<ResourceGainTypes> actions, ResourceGainTypes action) {
            return actions == null || actions.Count == 0 || actions.Contains(action);
        }

        public bool HasResourceModifiers(ResourceGainTypes action) {
            return ResourceModsFor(action).Count > 0;
        }

        private Dictionary<string, Tuple<float, bool>> ResourceModsFor(ResourceGainTypes action) {
            if (resourceModsByAction == null) {
                var byAction = new[] { new Dictionary<string, Tuple<float, bool>>(), new Dictionary<string, Tuple<float, bool>>() };
                if (ResourceModifiers != null) {
                    foreach (DeathResourceModifier mod in ResourceModifiers.Values) {
                        if (mod?.prefabs == null) { continue; }
                        Tuple<float, bool> value = new Tuple<float, bool>(mod.BonusModifier, mod.skillInfluence);
                        foreach (ResourceGainTypes kind in new[] { ResourceGainTypes.Kills, ResourceGainTypes.Harvesting }) {
                            if (!AppliesTo(mod.bonusActions, kind)) { continue; }
                            foreach (string prefab in mod.prefabs) {
                                // First entry wins, matching the validator's duplicate warning.
                                if (string.IsNullOrEmpty(prefab) || byAction[(int)kind].ContainsKey(prefab)) { continue; }
                                byAction[(int)kind][prefab] = value;
                            }
                        }
                    }
                }
                resourceModsByAction = byAction;
            }
            return resourceModsByAction[(int)action];
        }

        public float GetResourceModifier(string prefab, ResourceGainTypes action) {
            if (ResourceModsFor(action).TryGetValue(prefab, out Tuple<float, bool> mod)) {
                return mod.Item2
                    ? ScaleTowardsOne(mod.Item1, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus())
                    : mod.Item1;
            }
            return 1f;
        }

        public float GetResourceModifier(GameObject prefab, ResourceGainTypes action) {
            if (prefab == null) { return 1f; }
            return GetResourceModifier(prefab.name, action);
        }

        public bool HasLoot(ResourceGainTypes action) {
            return LootFor(action).Count > 0;
        }

        private List<KeyValuePair<GameObject, Tuple<float, int>>> LootFor(ResourceGainTypes action) {
            if (lootByAction == null) {
                var byAction = new[] { new List<KeyValuePair<GameObject, Tuple<float, int>>>(), new List<KeyValuePair<GameObject, Tuple<float, int>>>() };
                if (DeathLootModifiers != null) {
                    foreach (KeyValuePair<string, DeathLootModifier> entry in DeathLootModifiers) {
                        DeathLootModifier loot = entry.Value;
                        if (loot == null || string.IsNullOrEmpty(loot.prefab)) {
                            Logger.LogWarning($"Loot modifier '{entry.Key}' has no prefab set, it will be skipped.");
                            continue;
                        }
                        GameObject lootGO = PrefabManager.Instance.GetPrefab(loot.prefab);
                        if (lootGO == null) {
                            Logger.LogWarning($"Could not find prefab {loot.prefab} for loot modifier '{entry.Key}', it will be skipped.");
                            continue;
                        }
                        foreach (ResourceGainTypes kind in new[] { ResourceGainTypes.Kills, ResourceGainTypes.Harvesting }) {
                            if (!AppliesTo(loot.bonusActions, kind)) { continue; }
                            byAction[(int)kind].Add(new KeyValuePair<GameObject, Tuple<float, int>>(lootGO, new Tuple<float, int>(loot.chance, loot.amount)));
                        }
                    }
                }
                lootByAction = byAction;
            }
            return lootByAction[(int)action];
        }

        public List<KeyValuePair<GameObject, int>> RollLoot(ResourceGainTypes action) {
            List<KeyValuePair<GameObject, Tuple<float, int>>> table = LootFor(action);
            if (table.Count == 0) { return NoLoot; }
            List<KeyValuePair<GameObject, int>> results = null;
            foreach (var loot in table) {
                if (UnityEngine.Random.value >= loot.Value.Item1) { continue; }
                if (results == null) { results = new List<KeyValuePair<GameObject, int>>(); }
                results.Add(new KeyValuePair<GameObject, int>(loot.Key, loot.Value.Item2));
            }
            return results ?? NoLoot;
        }

        // Returns the multiplier applied to XP gained for skilltype, where 0 means "leave it alone".
        // Matching entries are SUMMED, not multiplied: two entries of 1.05 give 2.10, not 1.1025.
        public float GetSkillBonusLazyCache(Skills.SkillType skilltype) {
            if (CalculatedSkillMods.TryGetValue(skilltype, out Tuple<float, float> cached) == false) {
                float flat_sum = 0;
                float influenced_sum = 0;
                if (SkillModifiers != null && SkillModifiers.Count > 0) {
                    foreach (var skillMod in SkillModifiers) {
                        if (skillMod.Value == null) { continue; }
                        if (skillMod.Value.skill == Skills.SkillType.All || skillMod.Value.skill == skilltype) {
                            if (skillMod.Value.skillInfluence) {
                                influenced_sum += skillMod.Value.BonusModifier;
                            } else {
                                flat_sum += skillMod.Value.BonusModifier;
                            }
                        }
                    }
                }
                cached = new Tuple<float, float>(flat_sum, influenced_sum);
                CalculatedSkillMods.Add(skilltype, cached);
            }

            // Scaled as one total rather than per entry, so zero Deathlink skill gives exactly 1.0
            // instead of stacking a 1.0 per entry.
            if (cached.Item2 == 0f) { return cached.Item1; }
            return cached.Item1 + ScaleTowardsOne(cached.Item2, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
        }

        private static string ActionsText(List<ResourceGainTypes> actions) {
            return actions == null || actions.Count == 0 ? $"{ResourceGainTypes.Kills},{ResourceGainTypes.Harvesting}" : string.Join(",", actions);
        }

        public string GetLootModifiersDescription() {
            StringBuilder sb = new StringBuilder();
            if (DeathLootModifiers == null) { return sb.ToString(); }
            foreach (var entry in DeathLootModifiers) {
                if (entry.Value == null) { continue; }
                sb.AppendLine(Localization.instance.Localize($"<color={color_good}>{entry.Value.chance*100}%</color> $loot_desc_pt1 {entry.Key} $loot_desc_pt2 {ActionsText(entry.Value.bonusActions)}"));
            }
            return sb.ToString();
        }

        public string GetSkillModiferDescription() {
            StringBuilder sb = new StringBuilder();
            if (SkillModifiers == null) { return sb.ToString(); }
            foreach (var entry in SkillModifiers) {
                if (entry.Value == null) { continue; }
                if (entry.Value.BonusModifier > 1f) {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} +<color={color_good}>{Mathf.Round((entry.Value.BonusModifier - 1f)*100)}%</color> $xp"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} -<color={color_bad}>{Mathf.Round((1f - entry.Value.BonusModifier)*100)}%</color> $xp"));
                }
            }
            return sb.ToString();
        }

        public string GetResourceModiferDescription() {
            StringBuilder sb = new StringBuilder();
            if (ResourceModifiers == null) { return sb.ToString(); }
            foreach (var entry in ResourceModifiers) {
                if (entry.Value == null) { continue; }
                if (entry.Value.BonusModifier > 1f) {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} $drops <color={color_good}>{(entry.Value.BonusModifier - 1) * 100}%</color> $more {ActionsText(entry.Value.bonusActions)}"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} $drops <color={color_bad}>{(1 - entry.Value.BonusModifier) * 100}%</color> $less {ActionsText(entry.Value.bonusActions)}"));
                }
            }

            return sb.ToString();
        }

        public string GetDamageModifierDescription() {
            StringBuilder sb = new StringBuilder();
            if (DamageTakenModifier != 1f) {
                if (DamageTakenModifier > 1f) {
                    sb.AppendLine(Localization.instance.Localize($"$damage_taken +<color={color_bad}>{Mathf.Round((DamageTakenModifier - 1f) * 100)}%</color>"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$damage_taken -<color={color_good}>{Mathf.Round((1f - DamageTakenModifier) * 100)}%</color>"));
                }
            }
            if (DamageDoneModifier != 1f) {
                if (DamageDoneModifier > 1f) {
                    sb.AppendLine(Localization.instance.Localize($"$damage_dealt +<color={color_good}>{Mathf.Round((DamageDoneModifier - 1f) * 100)}%</color>"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$damage_dealt -<color={color_bad}>{Mathf.Round((1f - DamageDoneModifier) * 100)}%</color>"));
                }
            }
            return sb.ToString();
        }

        public string GetDeathStyleDescription() {
            StringBuilder sb = new StringBuilder();

            // DeathlinkBased gets skulls too: anything past the kept budget is destroyed.
            switch (DeathStyle.itemLossStyle)
            {
                case ItemLossStyle.None:
                    sb.AppendLine(Localization.instance.Localize($"$no_item_loss"));
                    break;
                case ItemLossStyle.DestroyNonWeaponArmor:
                    sb.AppendLine(Localization.instance.Localize($"<color={color_bad}>{skull} $no_equipment_loss</color>"));
                    break;
                case ItemLossStyle.DestroyAll:
                    sb.AppendLine(Localization.instance.Localize($"<color={color_bad}>{skull} $all_item_loss {skull}</color>"));
                    break;
                case ItemLossStyle.DeathlinkBased:
                    sb.AppendLine(Localization.instance.Localize($"<color={color_bad}>{skull} $limited_saved_deathlink {skull}</color>"));
                    sb.AppendLine(Localization.instance.Localize($"$equipment_kept <color={color_good}>{DeathStyle.minEquipmentKept}</color> - <color={color_good}>{DeathStyle.maxEquipmentKept}</color>"));
                    sb.AppendLine(Localization.instance.Localize($"$items_kept <color={color_good}>{DeathStyle.minItemsKept}</color> - <color={color_good}>{DeathStyle.maxItemsKept}</color>"));
                    break;
                case ItemLossStyle.ChooseEquipment:
                    sb.AppendLine(Localization.instance.Localize($"$choose_equipment_desc"));
                    sb.AppendLine(Localization.instance.Localize($"$equipment_picks <color={color_good}>{DeathStyle.minEquipmentKept}</color> - <color={color_good}>{DeathStyle.maxEquipmentKept}</color>"));
                    break;
            }
            if (DeathStyle.itemLossStyle != ItemLossStyle.DestroyAll) {
                // ChooseEquipment decides where everything goes on its own, so these lines would contradict it.
                if (DeathStyle.itemLossStyle != ItemLossStyle.ChooseEquipment) {
                    if (DeathStyle.itemSavedStyle == ItemSavedStyle.OnCharacter) {
                        sb.AppendLine(Localization.instance.Localize($"$saved_to_character"));
                    } else {
                        sb.AppendLine(Localization.instance.Localize($"$saved_to_tombstone"));
                    }
                    if (DeathStyle.equipmentSavedStyle == EquipmentSavedStyle.OnCharacter) {
                        sb.AppendLine(Localization.instance.Localize($"$equipment_saved_to_character"));
                    } else if (DeathStyle.equipmentSavedStyle == EquipmentSavedStyle.Tombstone) {
                        sb.AppendLine(Localization.instance.Localize($"$equipment_saved_to_tombstone"));
                    }
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Tombstone) {
                    sb.AppendLine(Localization.instance.Localize($"$non_skill_items_tombstone"));
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Save) {
                    sb.AppendLine(Localization.instance.Localize($"$non_skill_items_character"));
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Destroy) {
                    sb.AppendLine(Localization.instance.Localize($"<color={color_bad}>{skull} $non_skill_items_destroy {skull}</color>"));
                }
            }

            if (DeathStyle.foodLossOnDeath) {
                if (DeathStyle.foodLossUsesDeathlink) {
                    sb.AppendLine(Localization.instance.Localize($"$food_loss_deathlink"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$food_loss"));
                }
            } else {
                sb.AppendLine(Localization.instance.Localize($"$food_kept"));
            }

            if (DeathStyle.skillLossOnDeath) {
                if (DeathStyle.maxSkillLossPercentage == DeathStyle.minSkillLossPercentage) {
                    sb.AppendLine(Localization.instance.Localize($"$skill_loss_desc <color={color_bad}>{DeathStyle.maxSkillLossPercentage * 100f}%</color>"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$skill_loss_desc <color={color_bad}>{DeathStyle.maxSkillLossPercentage * 100f}%</color> - <color={color_bad}>{DeathStyle.minSkillLossPercentage * 100f}%</color> $influenced_by_deathlink"));
                }
            }

            return sb.ToString();
        }
    }

    public class DeathConfiguration
    {
        public string DeathChoiceLevel { get; set; }
        public int ChangesUsed { get; set; }
    }

    public class LeaderboardEntry
    {
        public long PlayerID { get; set; }
        public string PlayerName { get; set; }
        public string DeathChoice { get; set; }

        public float FirstLifeSeconds { get; set; }
        public float LongestLifeSeconds { get; set; }
        public float TotalLifeSeconds { get; set; }
        public int DeathCount { get; set; }

        public float TotalDamage { get; set; }
        public int BossKills { get; set; }

        public int TreeChops { get; set; }
        public int Mines { get; set; }
        public int CraftsAndBuilds { get; set; }

        // YamlIgnore: the serializer would emit it, and with no setter deserializing it would throw.
        [YamlIgnore]
        public float AverageLifeSeconds => DeathCount > 0 ? TotalLifeSeconds / DeathCount : 0f;
    }
}