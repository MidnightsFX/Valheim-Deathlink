using Jotunn.Managers;
using System.Collections.Generic;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Common {

    internal static partial class YamlConfigManager {
        internal static YamlConfigFile<Dictionary<string, DeathChoiceLevel>> DeathChoices;
        internal static YamlConfigFile<Dictionary<long, DeathConfiguration>> CharacterSettings;

        private static void RegisterConfigFiles() {
            // A misspelled value falls back to the enum's first member, which here is Destroy.
            TolerantEnumConverter.SetFallback(typeof(NonSkillCheckedItemAction), NonSkillCheckedItemAction.Tombstone);

            DeathChoices = Register(new YamlConfigFile<Dictionary<string, DeathChoiceLevel>>("DeathChoices.yaml") {
                Header = DeathChoicesHeader,
                Defaults = () => DeathConfigurationData.defaultDeathLevels,
                Apply = parsed => {
                    DeathConfigurationData.DeathLevels = parsed;
                    DeathConfigurationData.ArmPrefabValidationIfUnchecked();

                    // playerDeathConfiguration still points into the replaced dictionary; without this a
                    // reload leaves the player on the old level's caches and damage modifiers.
                    if (Player.m_localPlayer != null) {
                        DeathConfigurationData.CheckAndSetPlayerDeathConfig(Player.m_localPlayer);
                    }
                    Death.DeathChoices.DeathChoiceUI.RefreshIfBuilt();
                },
                Validate = DeathConfigurationData.ValidateDeathLevels,
                AllowAdminEdit = true,
                MigrateInPlace = DeathConfigurationData.MigrateLegacySkillInfluence,
                OnFailure = ConfigFailurePolicy.KeepLastGood,
                UnknownKeys = UnknownKeyPolicy.WarnAndContinue,
                ClientWritesToDisk = true,
                NeedsPrefabs = true,
            });

            // Not watched: it is save data the server rewrites whenever a player picks a level.
            CharacterSettings = Register(new YamlConfigFile<Dictionary<long, DeathConfiguration>>("CharacterSettings.yaml") {
                Header = CharacterSettingsHeader,
                Defaults = () => new Dictionary<long, DeathConfiguration>(),
                Apply = DeathConfigurationData.ApplyPlayerSettings,
                Watch = false,
            });

            // Each ZNetScene brings a new prefab table, so the check is re-armed. Clients run it when the player
            // loads, once ObjectDB is up too; a dedicated server never has a local player, so it runs here.
            PrefabManager.OnPrefabsRegistered += () => {
                DeathConfigurationData.ArmPrefabValidation();
                if (GUIManager.IsHeadless()) { DeathConfigurationData.EnsurePrefabsValidated(); }
            };

            // The validator cross-checks DefaultDeathChoice against DeathChoices.yaml.
            if (ValConfig.DefaultDeathChoice != null) {
                ValConfig.DefaultDeathChoice.SettingChanged += (sender, args) => RevalidateAll();
            }

            // leaderboard.yaml stays outside this framework: it is server-owned save data with its own sync loop.
        }

        private const string CharacterSettingsHeader = @"#################################################
# Deathlink - Character settings
#
# This file is SAVE DATA, not configuration. It maps a player id to the death level they picked and
# how many changes they have used. The server rewrites it whenever a player makes a choice, so edit it
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
#   MyLevel:              <- the KEY. This is the identity stored on the player. No spaces.
#     DisplayName: ...    <- what the player actually sees.
#
# Renaming or removing a key moves every player who had it onto DefaultDeathChoice from the BepInEx
# config, or the Fallback level when that is empty, and is announced in the log. DisplayName is only
# a label and is safe to change at any time.
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
#                                 stored no longer exists and DefaultDeathChoice is empty. Players
#                                 who have not chosen yet also play under it. Set it on exactly one
#                                 level. If none is marked, the first level in the file is used and
#                                 reordering the file would change which one that is.
#   DeathSkillRate       number   Multiplies how fast the Deathlink skill itself climbs. 1 is normal,
#                                 2 is twice as fast, 0 stops it entirely.
#   DamageTakenModifier  number   Multiplies damage this player takes. 1 is unchanged.
#   DamageDoneModifier   number   Multiplies damage this player deals. 1 is unchanged.
#
# ---------------------------------------------------------------------------------------------
# DeathStyle - what actually happens when you die
# ---------------------------------------------------------------------------------------------
#
#   ItemLossStyle                 None | DestroyNonWeaponArmor | DeathlinkBased | DestroyAll |
#                                 ChooseEquipment
#                                 None keeps everything. DeathlinkBased is the interesting one: how
#                                 much you keep scales with your Deathlink skill, between the Min
#                                 and Max numbers below.
#                                 ChooseEquipment destroys nothing: everything goes to the tombstone,
#                                 and after respawning the player picks MinEquipmentKept to
#                                 MaxEquipmentKept pieces of equipment (scaled by Deathlink skill) to
#                                 take back out of it. If they carry no more equipment than that, it
#                                 all simply stays on them. ItemSavedStyle, EquipmentSavedStyle and
#                                 EnableItemSavingChoices do nothing on this style.
#   ItemSavedStyle                OnCharacter | Tombstone   Where the items you keep end up.
#   EquipmentSavedStyle           SameAsItems | OnCharacter | Tombstone
#                                 Where kept equipment ends up (weapons, armor, tools, shields,
#                                 utility items and trinkets). SameAsItems, the default, follows
#                                 ItemSavedStyle. To keep your gear on you and drop everything else
#                                 in the tombstone: ItemLossStyle None, ItemSavedStyle Tombstone,
#                                 EquipmentSavedStyle OnCharacter.
#   NonSkillCheckedItemAction     Destroy | Tombstone | Save
#                                 What happens to items on the ItemsNotSkillChecked list in the
#                                 BepInEx config, which never take part in the skill roll.
#   EnableItemSavingChoices       bool    Let the player pick which items to rescue after dying,
#                                 rather than the survivors being chosen at random.
#
#   MinItemsKept / MaxItemsKept              whole numbers  Non-equipment kept, at 0% and 100%
#                                                           Deathlink skill respectively.
#   MinEquipmentKept / MaxEquipmentKept      whole numbers  Equipment kept, same scaling. On
#                                                           ChooseEquipment, the pieces the player
#                                                           may take back from the tombstone.
#   MinItemsKeptChoices / MaxItemsKeptChoices whole numbers How many of those the player gets to
#                                                           choose, same scaling.
#
#   SkillLossOnDeath              bool    Whether other skills drop on death at all.
#   MinSkillLossPercentage        0-1     Skill lost at FULL Deathlink skill. 0.05 is 5%.
#   MaxSkillLossPercentage        0-1     Skill lost at ZERO Deathlink skill.
#                                 Note the direction: high Deathlink skill moves you toward the Min.
#
#   FoodLossOnDeath               bool    Whether food buffs are lost. false keeps all of them.
#   FoodLossUsesDeathlink         bool    Scale how much food is lost with Deathlink skill instead
#                                 of clearing all of it.
#
# ---------------------------------------------------------------------------------------------
# ResourceModifiers - more (or less) from harvesting and kills
# ---------------------------------------------------------------------------------------------
#
#   Each entry has a name of your choosing, then:
#     Prefabs         list     Prefab names this applies to. An unknown name is warned about in the
#                              log and skipped; it does not break anything.
#     BonusModifier   number   1.5 means 50% more. Below 1 means less.
#     BonusActions    list     Kills (creature drops) | Harvesting (trees, rocks, pickables).
#                              Leave it out to apply to both.
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
#     BonusActions    list     Kills | Harvesting. An entry can be in both; leave it out for both.
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
# Quartermaster:
#   DisplayName: Quartermaster
#   DeathStyle:
#     ItemLossStyle: ChooseEquipment
#     MinEquipmentKept: 1
#     MaxEquipmentKept: 5
#     MinSkillLossPercentage: 0.03
#     MaxSkillLossPercentage: 0.10
#
# Related settings live in the BepInEx config file for this mod: DefaultDeathChoice (skip the
# selection popup for new players), AllowedDeathChoiceChanges (how many times a player may switch),
# and ItemsNotSkillChecked (items that never take part in the death roll).
#################################################";
    }
}
