using Deathlink.Death;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Deathlink.Common;

public class DataObjects
{
    // Aliases onto the shared formats in Common/Config, so the call sites throughout this mod did not
    // all have to change at once. The deserializer carries no naming convention and matches
    // case-insensitively, so every file written by an older build -- which emitted camelCase -- still
    // loads unchanged; only newly written files switch to PascalCase.
    public static IDeserializer yamldeserializer = YamlFormat.Default.Deserializer;
    public static ISerializer yamlserializer = YamlFormat.Default.Serializer;

    // Leaderboard payloads travel over the network and are persisted to leaderboard.yaml, so they
    // must tolerate keys the current type no longer maps (e.g. the computed averageLifeSeconds that
    // older builds serialized). Hand-edited config files go through YamlConfigFile instead, which
    // tries strict first so a typo is reported with its line number before falling back to this.
    public static IDeserializer leaderboardDeserializer = YamlFormat.Default.TolerantDeserializer;

    public static readonly string DeathChoiceKey = "DL_DeathChoice";
    // Tracks how many times a player has changed their death choice. Distinct prefix from
    // DeathChoiceKey so PlayerHasUniqueKey's StartsWith check doesn't cross-match.
    public static readonly string DeathChoiceChangesKey = "DL_ChoiceChanges";

    // Damage take/deal multipliers for the player's selected death choice, written onto their
    // own character ZDO so any client that processes a hit (as attacker or target) can read the
    // networked value and apply the correct multiplier. This keeps damage scaling consistent in
    // multiplayer no matter which machine owns the target.
    public static readonly string DamageTakenModifierKey = "DL_DmgTaken";
    public static readonly string DamageDoneModifierKey = "DL_DmgDone";

    // Leaderboard per-character accumulators, persisted as player unique keys so they survive
    // relogs independently of the server sync timer. Prefixes are distinct so PlayerHasUniqueKey's
    // StartsWith check never cross-matches between them (or with the death choice keys above).
    public static readonly string LeaderboardFirstLifeKey = "DL_LBFirst";
    public static readonly string LeaderboardLongestLifeKey = "DL_LBLongest";
    public static readonly string LeaderboardTotalLifeKey = "DL_LBTotal";
    public static readonly string LeaderboardDeathCountKey = "DL_LBDeaths";
    public static readonly string LeaderboardDamageKey = "DL_LBDamage";
    public enum ItemLossStyle
    {
        None,
        DestroyNonWeaponArmor,
        DeathlinkBased,
        DestroyAll
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

    // The original misspelling of BonusModifier, kept as a read-only alias so every DeathChoices.yaml
    // written before the rename still loads. The shim property returns null so OmitDefaults drops it on
    // write: files are read under either spelling, but only ever written under the corrected one.
    const string LegacyBonusModifier = "bonusModifer";

    // Scales a multiplier towards "no effect" as the player's Deathlink skill falls, which is what
    // skillInfluence has always claimed to do in the README. At full skill the configured bonus applies
    // in full; at zero skill it collapses to 1.0 (no change). A bonus at or below 1 is left alone --
    // there is nothing to scale, and a penalty should not get weaker with skill.
    internal static float ScaleTowardsOne(float bonus, float deathSkillPercent) {
        if (bonus <= 1f) { return bonus; }
        return 1f + ((bonus - 1f) * Mathf.Clamp01(deathSkillPercent));
    }

    // Every member below whose initializer is not default(T) carries [DefaultValue] with that same
    // value. Without it the serializer omits anything equal to default(T), so an admin writing
    // `foodLossOnDeath: false` had it dropped on the next rewrite and the initializer put it straight
    // back to true -- their "off" silently became "on". See Common/Config/YamlFormat.cs.
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
        [DefaultValue(true)]
        public bool EnableItemSavingChoices = true;
        [DefaultValue(NonSkillCheckedItemAction.Tombstone)]
        public NonSkillCheckedItemAction nonSkillCheckedItemAction = NonSkillCheckedItemAction.Tombstone;
    }

    public class DeathResourceModifier
    {
        // Opt-in. A bonus applies in full from the start unless this is turned on, which keeps the
        // behaviour every existing config was actually getting while the flag was inert. Deliberately
        // left on default(bool) with no [DefaultValue], so the serializer writes it out only when an
        // admin has enabled it.
        public bool skillInfluence { get; set; }
        public List<string> prefabs { get; set; }
        public float BonusModifier { get; set; }
        [YamlMember(Alias = LegacyBonusModifier)]
        public float? bonusModifer { get { return null; } set { if (value.HasValue) { BonusModifier = value.Value; ReadWithLegacySpelling = true; } } }

        // Set when this entry was read under the old misspelling, which only a file written by 0.10.x or
        // earlier can contain. That makes it a reliable "this file predates 0.11" marker, which the
        // skillInfluence migration keys off. Internal, so YamlDotNet never sees it.
        internal bool ReadWithLegacySpelling;
        public List<ResourceGainTypes> bonusActions { get; set; } = new List<ResourceGainTypes>();
    }

    public class DeathSkillModifier
    {
        // Opt-in. A bonus applies in full from the start unless this is turned on, which keeps the
        // behaviour every existing config was actually getting while the flag was inert. Deliberately
        // left on default(bool) with no [DefaultValue], so the serializer writes it out only when an
        // admin has enabled it.
        public bool skillInfluence { get; set; }
        public Skills.SkillType skill { get; set; }
        public float BonusModifier { get; set; }
        [YamlMember(Alias = LegacyBonusModifier)]
        public float? bonusModifer { get { return null; } set { if (value.HasValue) { BonusModifier = value.Value; ReadWithLegacySpelling = true; } } }

        // Set when this entry was read under the old misspelling, which only a file written by 0.10.x or
        // earlier can contain. That makes it a reliable "this file predates 0.11" marker, which the
        // skillInfluence migration keys off. Internal, so YamlDotNet never sees it.
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
        // Marks the level a player falls back to when their stored choice no longer exists. Exactly one
        // level should set it; without it the resolver has to guess from file order, which means simply
        // reordering the file would change which level orphaned players land on.
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

        // Skill-influenced and flat contributions are cached separately, because the influenced half
        // depends on the player's current Deathlink skill and so cannot be baked into the cache.
        private Dictionary<Skills.SkillType, Tuple<float, float>> CalculatedSkillMods = new Dictionary<Skills.SkillType, Tuple<float, float>>();
        private Dictionary<string, Tuple<float, bool>> CalculatedResourceMods = new Dictionary<string, Tuple<float, bool>>();
        private bool CalculatedResourceModsCached = false;
        private Dictionary<GameObject, Tuple<float, int>> KillLootModifiers = new Dictionary<GameObject, Tuple<float, int>>();
        private bool CalculatedKillLootModifiersCached = false;
        private Dictionary<GameObject, Tuple<float, int>> ResourceLootModifiers = new Dictionary<GameObject, Tuple<float, int>>();
        private bool CalculatedHarvestLootModifiersCached = false;

        public List<KeyValuePair<GameObject, int>> RollKillLoot() {
            if (CalculatedKillLootModifiersCached == false) {
                if (DeathLootModifiers != null && DeathLootModifiers.Count > 0) {
                    foreach (var kvp in DeathLootModifiers) {
                        if (kvp.Value.bonusActions.Contains(ResourceGainTypes.Kills)) {
                            if (string.IsNullOrEmpty(kvp.Value.prefab)) {
                                Logger.LogWarning($"Kill loot modifier '{kvp.Key}' has no prefab set, it will be skipped.");
                                continue;
                            }
                            GameObject lootGO = PrefabManager.Instance.GetPrefab(kvp.Value.prefab);
                            if (lootGO == null) {
                                Logger.LogWarning($"Could not find prefab {kvp.Value.prefab} while building kill loot table, it will be skipped.");
                                continue;
                            }
                            // Indexer, not Add: two entries naming the same prefab would throw, and the
                            // cached flag is only set after this loop, so it would throw on every kill.
                            KillLootModifiers[lootGO] = new Tuple<float, int>(kvp.Value.chance, kvp.Value.amount);
                        }
                    }
                }
                CalculatedKillLootModifiersCached = true;
            }
            List<KeyValuePair<GameObject, int>> lootresults = new List<KeyValuePair<GameObject, int>>();
            foreach (var kvp in KillLootModifiers) {
                float chanceroll = UnityEngine.Random.value;
                Logger.LogDebug($"Rolling chance loot for: {kvp.Key.gameObject.name} {chanceroll} < {kvp.Value.Item1}");
                if (chanceroll < kvp.Value.Item1) {
                    lootresults.Add(new KeyValuePair<GameObject, int>(kvp.Key, kvp.Value.Item2));
                }
            }
            return lootresults;
        }

        public List<KeyValuePair<GameObject, int>> RollHarvestLoot() {
            if (CalculatedHarvestLootModifiersCached == false) {
                if (DeathLootModifiers != null && DeathLootModifiers.Count > 0) {
                    foreach (var kvp in DeathLootModifiers) {
                        if (kvp.Value.bonusActions.Contains(ResourceGainTypes.Harvesting)) {
                            if (string.IsNullOrEmpty(kvp.Value.prefab)) {
                                Logger.LogWarning($"Harvest loot modifier '{kvp.Key}' has no prefab set, it will be skipped.");
                                continue;
                            }
                            GameObject lootGO = PrefabManager.Instance.GetPrefab(kvp.Value.prefab);
                            if (lootGO == null) {
                                Logger.LogWarning($"Could not find prefab {kvp.Value.prefab} while building harvest loot table, it will be skipped.");
                                continue;
                            }
                            // Indexer, not Add: see RollKillLoot above.
                            ResourceLootModifiers[lootGO] = new Tuple<float, int>(kvp.Value.chance, kvp.Value.amount);
                        }
                    }
                }
                CalculatedHarvestLootModifiersCached = true;
            }
            List<KeyValuePair<GameObject, int>> lootresults = new List<KeyValuePair<GameObject, int>>();
            foreach (var kvp in ResourceLootModifiers) {
                if (UnityEngine.Random.value < kvp.Value.Item1) {
                    lootresults.Add(new KeyValuePair<GameObject, int>(kvp.Key, kvp.Value.Item2));
                }
            }
            return lootresults;
        }

        public float GetResouceEarlyCache(string prefab) {
            if (CalculatedResourceModsCached == false) {
                Logger.LogDebug($"Building cache entry for {prefab}");
                if (ResourceModifiers != null) {
                    foreach (var entry in ResourceModifiers) {
                        // Logger.LogDebug($"Checking resource modifiers {entry.Value.prefabs}");
                        if (entry.Value.prefabs != null) {
                            foreach (string pnam in entry.Value.prefabs) {
                                Logger.LogDebug($"Building cache entry for {pnam} - {entry.Value.BonusModifier}");
                                // Indexer, not Add: a prefab named by two entries of the same level would
                                // throw here, and because the cached flag is only set after this loop it
                                // would throw again on every single harvest, forever. First entry wins;
                                // the config validator warns about the overlap at load time.
                                CalculatedResourceMods[pnam] = new Tuple<float, bool>(entry.Value.BonusModifier, entry.Value.skillInfluence);
                            }
                        }
                    }
                }
                CalculatedResourceModsCached = true;
            }
            if (CalculatedResourceMods.TryGetValue(prefab, out Tuple<float, bool> mod)) {
                return mod.Item2
                    ? ScaleTowardsOne(mod.Item1, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus())
                    : mod.Item1;
            }
            return 1f;
        }

        public float GetResouceEarlyCache(GameObject prefab) {
            if (prefab == null) { return 1f; }
            return GetResouceEarlyCache(prefab.name);
        }

        // Returns the multiplier applied to XP gained for skilltype, where 0 means "leave it alone".
        // Matching entries are SUMMED, not multiplied: two entries of 1.05 give 2.10, not 1.1025.
        public float GetSkillBonusLazyCache(Skills.SkillType skilltype) {
            if (CalculatedSkillMods.TryGetValue(skilltype, out Tuple<float, float> cached) == false) {
                float flat_sum = 0;
                float influenced_sum = 0;
                if (SkillModifiers != null && SkillModifiers.Count > 0) {
                    foreach (var skillMod in SkillModifiers) {
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

            // Scale the influenced total as one multiplier rather than per entry, so a level with a
            // single skill-influenced bonus collapses to exactly 1.0 (no change) at zero Deathlink
            // skill instead of stacking a 1.0 per entry.
            if (cached.Item2 == 0f) { return cached.Item1; }
            return cached.Item1 + ScaleTowardsOne(cached.Item2, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
        }

        public string GetLootModifiersDescription() {
            StringBuilder sb = new StringBuilder();
            if (DeathLootModifiers == null) { return sb.ToString(); }
            foreach (var entry in DeathLootModifiers) {
                sb.AppendLine(Localization.instance.Localize($"<color={color_good}>{entry.Value.chance*100}%</color> $loot_desc_pt1 {entry.Key} $loot_desc_pt2 {string.Join(",", entry.Value.bonusActions)}"));
            }
            return sb.ToString();
        }

        public string GetSkillModiferDescription() {
            StringBuilder sb = new StringBuilder();
            if (SkillModifiers == null) { return sb.ToString(); }
            foreach (var entry in SkillModifiers) {
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
                if (entry.Value.BonusModifier > 1f) {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} $drops <color={color_good}>{(entry.Value.BonusModifier - 1) * 100}%</color> $more {string.Join(",", entry.Value.bonusActions)}"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"{entry.Key} $drops <color={color_bad}>{(1 - entry.Value.BonusModifier) * 100}%</color> $less {string.Join(",", entry.Value.bonusActions)}"));
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

            switch (DeathStyle.itemLossStyle)
            {
                case ItemLossStyle.None:
                    sb.AppendLine(Localization.instance.Localize($"$no_item_loss"));
                    break;
                case ItemLossStyle.DestroyNonWeaponArmor:
                    sb.AppendLine(Localization.instance.Localize($"$no_equipment_loss"));
                    break;
                case ItemLossStyle.DestroyAll:
                    sb.AppendLine(Localization.instance.Localize($"$all_item_loss"));
                    break;
                case ItemLossStyle.DeathlinkBased:
                    sb.AppendLine(Localization.instance.Localize($"$limited_saved_deathlink"));
                    sb.AppendLine(Localization.instance.Localize($"$equipment_kept <color={color_good}>{DeathStyle.minEquipmentKept}</color> - <color={color_good}>{DeathStyle.maxEquipmentKept}</color>"));
                    sb.AppendLine(Localization.instance.Localize($"$items_kept <color={color_good}>{DeathStyle.minItemsKept}</color> - <color={color_good}>{DeathStyle.maxItemsKept}</color>"));
                    break;
            }
            //sb.AppendLine();
            if (DeathStyle.itemLossStyle != ItemLossStyle.DestroyAll) {
                if (DeathStyle.itemSavedStyle == ItemSavedStyle.OnCharacter) {
                    sb.AppendLine(Localization.instance.Localize($"$saved_to_character"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$saved_to_tombstone"));
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Tombstone) {
                    sb.AppendLine(Localization.instance.Localize($"$non_skill_items_tombstone"));
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Save) {
                    sb.AppendLine(Localization.instance.Localize($"$non_skill_items_character"));
                }
                if (DeathStyle.nonSkillCheckedItemAction == NonSkillCheckedItemAction.Destroy) {
                    sb.AppendLine(Localization.instance.Localize($"$non_skill_items_destroy"));
                }
            }

            if (DeathStyle.foodLossOnDeath) {
                if (DeathStyle.foodLossUsesDeathlink) {
                    sb.AppendLine(Localization.instance.Localize($"$food_loss_deathlink"));
                } else {
                    sb.AppendLine(Localization.instance.Localize($"$food_loss"));
                }
            }

            //sb.AppendLine();
            if (DeathStyle.maxSkillLossPercentage == DeathStyle.minSkillLossPercentage) {
                sb.AppendLine(Localization.instance.Localize($"$skill_loss_desc <color={color_bad}>{DeathStyle.maxSkillLossPercentage * 100f}%</color>"));
            } else {
                sb.AppendLine(Localization.instance.Localize($"$skill_loss_desc <color={color_bad}>{DeathStyle.maxSkillLossPercentage * 100f}%</color> - <color={color_bad}>{DeathStyle.minSkillLossPercentage * 100f}%</color> $influenced_by_deathlink"));
            }
                

            return sb.ToString();
        }
    }

    public class DeathConfiguration
    {
        public string DeathChoiceLevel { get; set; }
        public int ChangesUsed { get; set; }
    }

    public class PlayerDeathConfiguration {
        public Dictionary<long, DeathConfiguration> selectedDeathStyle { get; set; }
    }

    // One player's all-time leaderboard stats. Serialized to leaderboard.yaml on the server and
    // sent over the wire (the whole board server->client, a single entry client->server).
    public class LeaderboardEntry
    {
        public long PlayerID { get; set; }
        public string PlayerName { get; set; }
        public string DeathChoice { get; set; }

        // Survival, stored in seconds of played time alive; the UI renders these as minutes.
        public float FirstLifeSeconds { get; set; }
        public float LongestLifeSeconds { get; set; }
        public float TotalLifeSeconds { get; set; }
        public int DeathCount { get; set; }

        // Combat
        public float TotalDamage { get; set; }
        public int BossKills { get; set; }

        // Gathering
        public int TreeChops { get; set; }
        public int Mines { get; set; }
        public int CraftsAndBuilds { get; set; }

        // Average played time alive per completed life, in seconds. Computed from the fields above,
        // so it is never serialized: the serializer would emit it, but it has no setter and would
        // throw on deserialization. The UI recomputes it locally from the transmitted values.
        [YamlIgnore]
        public float AverageLifeSeconds => DeathCount > 0 ? TotalLifeSeconds / DeathCount : 0f;
    }

    public abstract class ZNetProperty<T>
    {
        public string Key { get; private set; }
        public T DefaultValue { get; private set; }
        protected readonly ZNetView zNetView;

        protected ZNetProperty(string key, ZNetView zNetView, T defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
            this.zNetView = zNetView;
        }

        private void ClaimOwnership()
        {
            if (!zNetView.IsOwner())
            {
                zNetView.ClaimOwnership();
            }
        }

        public void Set(T value)
        {
            SetValue(value);
        }

        public void ForceSet(T value)
        {
            ClaimOwnership();
            Set(value);
        }

        public abstract T Get();

        protected abstract void SetValue(T value);
    }

    public class BoolZNetProperty : ZNetProperty<bool>
    {
        public BoolZNetProperty(string key, ZNetView zNetView, bool defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override bool Get()
        {
            return zNetView.GetZDO().GetBool(Key, DefaultValue);
        }

        protected override void SetValue(bool value)
        {
            zNetView.GetZDO().Set(Key, value);
        }
    }

    public class IntZNetProperty : ZNetProperty<int>
    {
        public IntZNetProperty(string key, ZNetView zNetView, int defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override int Get()
        {
            return zNetView.GetZDO().GetInt(Key, DefaultValue);
        }

        protected override void SetValue(int value)
        {
            zNetView.GetZDO().Set(Key, value);
        }
    }

    public class StringZNetProperty : ZNetProperty<string>
    {
        public StringZNetProperty(string key, ZNetView zNetView, string defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override string Get()
        {
            return zNetView.GetZDO().GetString(Key, DefaultValue);
        }

        protected override void SetValue(string value)
        {
            zNetView.GetZDO().Set(Key, value);
        }
    }

    public class Vector3ZNetProperty : ZNetProperty<Vector3>
    {
        public Vector3ZNetProperty(string key, ZNetView zNetView, Vector3 defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override Vector3 Get()
        {
            return zNetView.GetZDO().GetVec3(Key, DefaultValue);
        }

        protected override void SetValue(Vector3 value)
        {
            zNetView.GetZDO().Set(Key, value);
        }
    }

    public class DictionaryZNetProperty : ZNetProperty<Dictionary<Skills.SkillType, float>>
    {
        BinaryFormatter binFormatter = new BinaryFormatter();
        public DictionaryZNetProperty(string key, ZNetView zNetView, Dictionary<Skills.SkillType, float> defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override Dictionary<Skills.SkillType, float> Get()
        {
            var stored = zNetView.GetZDO().GetByteArray(Key);
            // we can't deserialize a null buffer
            if (stored == null) { return new Dictionary<Skills.SkillType, float>(); }
            var mStream = new MemoryStream(stored);
            var deserializedDictionary = (Dictionary<Skills.SkillType, float>)binFormatter.Deserialize(mStream);
            return deserializedDictionary;
        }

        protected override void SetValue(Dictionary<Skills.SkillType, float> value)
        {
            
            var mStream = new MemoryStream();
            binFormatter.Serialize(mStream, value);

            zNetView.GetZDO().Set(Key, mStream.ToArray());
        }

        public void UpdateDictionary()
        {
            
        }
    }

    public class ZDOIDZNetProperty : ZNetProperty<ZDOID>
    {
        public ZDOIDZNetProperty(string key, ZNetView zNetView, ZDOID defaultValue) : base(key, zNetView, defaultValue)
        {
        }

        public override ZDOID Get()
        {
            return zNetView.GetZDO().GetZDOID(Key);
        }

        protected override void SetValue(ZDOID value)
        {
            zNetView.GetZDO().Set(Key, value);
        }
    }
}