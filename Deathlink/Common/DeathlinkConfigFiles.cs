using Jotunn.Managers;
using System.Collections.Generic;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Common {

    // Deathlink's yaml config files. Everything about loading, watching, validating and syncing them is
    // handled by Common/Config; this is the whole of the mod-specific half.
    internal static partial class YamlConfigManager {
        internal static YamlConfigFile<Dictionary<string, DeathChoiceLevel>> DeathChoices;
        internal static YamlConfigFile<Dictionary<long, DeathConfiguration>> CharacterSettings;

        private static void RegisterConfigFiles() {
            DeathChoices = Register(new YamlConfigFile<Dictionary<string, DeathChoiceLevel>>("DeathChoices.yaml") {
                Header = DeathChoicesHeader,
                Defaults = () => DeathConfigurationData.defaultDeathLevels,
                Apply = parsed => {
                    DeathConfigurationData.DeathLevels = parsed;

                    // New levels mean new prefab names to check -- but only the loads that ran before the
                    // prefab table existed left them unchecked, and only those need the deferred pass.
                    DeathConfigurationData.ArmPrefabValidationIfUnchecked();

                    // playerDeathConfiguration holds a REFERENCE into the dictionary we just replaced, and
                    // is otherwise only re-resolved at Player.Load or when the player picks a level.
                    // Without re-resolving here, a reload -- hand edit, server broadcast or in-game edit --
                    // leaves the local player running the previous level object: stale lazy caches and
                    // stale damage modifiers on their ZDO, with nothing on screen to say so.
                    if (Player.m_localPlayer != null) {
                        DeathConfigurationData.CheckAndSetPlayerDeathConfig(Player.m_localPlayer);
                    }
                    Death.DeathChoices.DeathChoiceUI.RefreshIfBuilt();
                },
                Validate = DeathConfigurationData.ValidateDeathLevels,
                // Lets a connected admin push an edited copy to the server. The server still admin-gates
                // and validates before accepting; see Common/Config/ConfigNetwork.
                AllowAdminEdit = true,
                // Clears the skillInfluence flags that older versions wrote into every modifier without
                // being asked, so upgrading does not quietly weaken an existing config. Runs once: the
                // rewrite it triggers removes the marker it keys off.
                MigrateInPlace = DeathConfigurationData.MigrateLegacySkillInfluence,
                // A broken edit leaves the levels that last loaded cleanly in play and does not touch the
                // file, so an admin can fix their mistake in place rather than finding it overwritten.
                OnFailure = ConfigFailurePolicy.KeepLastGood,
                UnknownKeys = UnknownKeyPolicy.WarnAndContinue,
                // Clients keep a copy of the server's levels on disk, matching how this has always
                // behaved. The framework writes the bytes the server sent rather than re-serializing,
                // which is what stops a round trip through the object model quietly dropping settings.
                ClientWritesToDisk = true,
                // The validator resolves prefab names, which do not exist yet during Awake. This is what
                // RevalidatePrefabDependent picks the file out by when the deferred pass runs.
                NeedsPrefabs = true,
            });

            // Save data, not configuration. The game writes it whenever a player picks a level, and
            // MergePlayerSettings is additive by design, so a reload could never honour a deletion -- a
            // watcher here would look like hot-reload while being structurally unable to deliver it.
            CharacterSettings = Register(new YamlConfigFile<Dictionary<long, DeathConfiguration>>("CharacterSettings.yaml") {
                Header = CharacterSettingsHeader,
                Defaults = () => new Dictionary<long, DeathConfiguration>(),
                Apply = DeathConfigurationData.MergePlayerSettings,
                Watch = false,
            });

            // Prefab names cannot be checked during Awake, so the startup pass skips them and this arms
            // the deferred half. OnPrefabsRegistered is a ZNetScene.Awake postfix, so a new scene means a
            // new prefab table and any previous verdict is stale.
            //
            // It only RUNS the pass headless. On a client the check belongs on the player path -- loading
            // or picking a choice -- which is where the levels first get used and where ObjectDB is
            // guaranteed to be up as well. A dedicated server never sets Player.m_localPlayer, so nothing
            // would ever reach EnsurePrefabsValidated there and an admin would see no warnings at all.
            PrefabManager.OnPrefabsRegistered += () => {
                DeathConfigurationData.ArmPrefabValidation();
                if (GUIManager.IsHeadless()) { DeathConfigurationData.EnsurePrefabsValidated(); }
            };

            // DefaultDeathChoice is a BepInEx setting the validator cross-checks against this file, so
            // re-run validation when it settles. Not prefab-related, hence the full pass.
            if (ValConfig.DefaultDeathChoice != null) {
                ValConfig.DefaultDeathChoice.SettingChanged += (sender, args) => RevalidateAll();
            }

            // leaderboard.yaml deliberately stays outside this framework: it is server-owned save data
            // with its own sync loop, its own tolerant deserializer and its own size cap.
        }

        private const string CharacterSettingsHeader = @"#################################################
# Deathlink - Character settings
#
# This file is SAVE DATA, not configuration. It maps a player id to the death level they picked and
# how many changes they have used. The game rewrites it whenever a player makes a choice, so edit it
# with the server stopped -- changes made while it is running will be overwritten.
#
# Only used when UsePrivateKeysForDeathChoice is false in the BepInEx config. When it is true (the
# default) a player's choice is stored on their character instead and this file is not consulted.
#################################################";

        private const string DeathChoicesHeader = @"#################################################
# Deathlink - Death Choice Configuration
#
# Each top-level entry is one death level a player can choose. Add, remove and edit them freely.
#
#   MyLevel:              <- the KEY. This is the identity stored on the player.
#     DisplayName: ...    <- what the player actually sees.
#
# Renaming a key moves every player who had it selected onto the fallback level (see Fallback below)
# and is announced in the log. DisplayName is only a label and is safe to change at any time.
#
# A mistake here costs you one setting, not the file: an unknown key or a misspelled enum value is
# reported in the log with its line number and skipped, and everything else still loads. If the file
# cannot be parsed at all, the levels that last loaded cleanly stay in use and the file is left
# exactly as you wrote it so you can fix it in place.
#
# Edits are picked up while the game is running. On a server they are pushed to every connected
# client automatically.
#
# ---------------------------------------------------------------------------------------------
# LEVEL SETTINGS
# ---------------------------------------------------------------------------------------------
#
#   DisplayName          text     Shown in the selection popup and the compendium.
#   Fallback             bool     Marks this as the level players land on when the one they had
#                                 stored no longer exists. Set it on exactly one level. If none is
#                                 marked, the first level in the file is used and reordering the
#                                 file would change which one that is.
#   DeathSkillRate       number   Multiplies how fast the Deathlink skill itself climbs. 1 is normal,
#                                 2 is twice as fast, 0 stops it entirely.
#   DamageTakenModifier  number   Multiplies damage this player takes. 1 is unchanged.
#   DamageDoneModifier   number   Multiplies damage this player deals. 1 is unchanged.
#
# ---------------------------------------------------------------------------------------------
# DeathStyle - what actually happens when you die
# ---------------------------------------------------------------------------------------------
#
#   ItemLossStyle                 None | DestroyNonWeaponArmor | DeathlinkBased | DestroyAll
#                                 None keeps everything. DeathlinkBased is the interesting one: how
#                                 much you keep scales with your Deathlink skill, between the Min
#                                 and Max numbers below.
#   ItemSavedStyle                OnCharacter | Tombstone   Where the items you keep end up.
#   NonSkillCheckedItemAction     Destroy | Tombstone | Save
#                                 What happens to items on the ItemsNotSkillChecked list in the
#                                 BepInEx config, which never take part in the skill roll.
#   EnableItemSavingChoices       bool    Let the player pick which items to rescue after dying,
#                                 rather than the survivors being chosen at random.
#
#   MinItemsKept / MaxItemsKept              whole numbers  Non-equipment kept, at 0% and 100%
#                                                           Deathlink skill respectively.
#   MinEquipmentKept / MaxEquipmentKept      whole numbers  Equipment kept, same scaling.
#   MinItemsKeptChoices / MaxItemsKeptChoices whole numbers How many of those the player gets to
#                                                           choose, same scaling.
#
#   SkillLossOnDeath              bool    Whether other skills drop on death at all.
#   MinSkillLossPercentage        0-1     Skill lost at FULL Deathlink skill. 0.05 is 5%.
#   MaxSkillLossPercentage        0-1     Skill lost at ZERO Deathlink skill.
#                                 Note the direction: high Deathlink skill moves you toward the Min.
#
#   FoodLossOnDeath               bool    Whether food buffs are lost.
#   FoodLossUsesDeathlink         bool    Scale how much food is lost with Deathlink skill instead
#                                 of clearing all of it.
#
# ---------------------------------------------------------------------------------------------
# ResourceModifiers - more (or less) from harvesting
# ---------------------------------------------------------------------------------------------
#
#   Each entry has a name of your choosing, then:
#     Prefabs         list     Prefab names this applies to. An unknown name is warned about in the
#                              log and skipped; it does not break anything.
#     BonusModifier   number   1.5 means 50% more. Below 1 means less.
#     BonusActions    list     Kills | Harvesting
#     SkillInfluence  bool     Off by default, so the bonus applies in full from the start. Turn it
#                              on to scale the bonus in with Deathlink skill instead: nothing at 0%,
#                              the full amount at 100%.
#
#   Do not list the same prefab in two entries of one level -- only the first will apply, and the
#   log will tell you which.
#
# ---------------------------------------------------------------------------------------------
# SkillModifiers - faster skill gain
# ---------------------------------------------------------------------------------------------
#
#     Skill           name     'All', or any vanilla skill name (Swords, Bows, WoodCutting, ...).
#                              A misspelling is reported in the log along with every valid name.
#     BonusModifier   number   1.2 means 20% more XP.
#     SkillInfluence  bool     As above -- off by default, turn it on to scale with Deathlink skill.
#
#   Entries that match are ADDED TOGETHER, not multiplied. Two entries of 1.05 give 2.10 -- a 110%
#   bonus -- not 1.1025. One entry per skill is usually what you want. An empty block means no
#   change rather than no XP.
#
# ---------------------------------------------------------------------------------------------
# DeathLootModifiers - chance-based bonus drops
# ---------------------------------------------------------------------------------------------
#
#     Prefab          name     What drops. An unknown name is warned about and skipped.
#     Chance          0-1      0.05 is a 5% chance, rolled independently per entry.
#     Amount          number   Stack size when it drops. Defaults to 1.
#     BonusActions    list     Kills | Harvesting. An entry can be in both.
#
# ---------------------------------------------------------------------------------------------
# EXAMPLE - adding a level
# ---------------------------------------------------------------------------------------------
#
# Gatherer:
#   DisplayName: Gatherer
#   DeathSkillRate: 1.5
#   DeathStyle:
#     ItemLossStyle: DeathlinkBased
#     ItemSavedStyle: Tombstone
#     MinEquipmentKept: 2
#     MaxEquipmentKept: 8
#     MinSkillLossPercentage: 0.02
#     MaxSkillLossPercentage: 0.10
#   ResourceModifiers:
#     Wood:
#       Prefabs: [Wood, FineWood, RoundLog]
#       BonusModifier: 1.5
#       BonusActions: [Harvesting]
#
# Related settings live in the BepInEx config file for this mod: DefaultDeathChoice (skip the
# selection popup for new players), AllowedDeathChoiceChanges (how many times a player may switch),
# and ItemsNotSkillChecked (items that never take part in the death roll).
#################################################";
    }
}
