# Deathlink - Rougelike Control
Progression and Choice based death control.

Bringing together players from many different experiance levels often means that some players struggle while others are bored.
Deathlink provides a way to challenge and reward players based on their appetite for risk.

Additionally, death is one of the few things that does not progress through the game. 
Deathlink changes that by providing a skill that levels up faster the longer you stay alive,
and scales your death changes based on that skill.


## Example Deathlink Choices
| Vanilla | Minimal | Hardcore |
|:-:|:-:|:-:|
| ![deathlink_selection](https://i.postimg.cc/02RQ74cG/image.png)  | ![deathlink_selection2](https://i.postimg.cc/PrWqyMRg/image.png)  |  ![deathlink_selection3](https://i.postimg.cc/RhmkZwLt/image.png) |


## Configuration
Deathlink has extensive yaml based configuration options. However it can be used with the default configuration.

Each of these configuration sections below can be applied to any number of profiles which users can select.
Deathlink selection is one per character and is stored on the server the character is on,a character can have
different selections in singleplayer or multiplayer.

### Example Configuration

<details>
  <summary>Click for full example</summary>
  
```yaml
Rougelike3:
  displayName: Berserker
  deathStyle:
    foodLossOnDeath: true
    foodLossUsesDeathlink: true
    maxEquipmentKept: 3
    skillLossOnDeath: true
    maxSkillLossPercentage: 0.2
    minSkillLossPercentage: 0.05
    itemLossStyle: DeathlinkBased
    nonSkillCheckedItemAction: Tombstone
  deathSkillRate: 1
  resourceModifiers:
    Wood:
      skillInfluence: true
      prefabs:
      - Wood
      - FineWood
      - RoundLog
      - YggdrasilWood
      - Blackwood
      bonusModifer: 1.5
      bonusActions:
      - Harvesting
    Ore:
      skillInfluence: true
      prefabs:
      - CopperOre
      - TinOre
      - IronScrap
      - SilverOre
      - BlackMetalScrap
      - CopperScrap
      - FlametalOreNew
      bonusModifer: 1.5
      bonusActions:
      - Harvesting
  skillModifiers:
    All:
      skillInfluence: true
      skill: All
      bonusModifer: 1.2
  deathLootModifiers:
    AmberPearl:
      prefab: AmberPearl
      chance: 0.05
      amount: 1
      bonusActions:
      - Kills
```

</details>

### Deathstyle Configuration
Deathstyle configuration governs what happens when you die.

```yaml
  deathStyle:
    foodLossOnDeath: true                  # Player will loose food on death or not
    foodLossUsesDeathlink: true            # Food loss is based on skill level from loosing all foods, to none
    maxEquipmentKept: 3                    # The maximum number of equiped items to keep on death
    skillLossOnDeath: true                 # Whether or not skill loss occurs on death
    maxSkillLossPercentage: 0.2            # The maximum percentage of skill lost on death (you start here, example is 20%)
    minSkillLossPercentage: 0.05           # The minimum skill loss, at max skill level (example is 5%)
    itemLossStyle: DeathlinkBased          # Item loss style, can be None, DestroyNonWeaponArmor, DeathlinkBased, DestroyAll
    nonSkillCheckedItemAction: Tombstone   # If items are set to avoid skillcheck, what happens to them, can be Destroy, Tombstone, Save
    itemSavedStyle: Tombstone              # Items that are saved can be: OnCharacter, Tombstone
  deathSkillRate: 1                        # Rate at which Deathlink skill increases, higher is faster
```

### Resource Configuration
Resource configuration provides a way to get additional resources from kills or harvesting
```yaml
resourceModifiers:
    Wood:                     # Name of the entry, can be anything
      skillInfluence: true    # Whether or not Deathlink skill will influence this bonus
      prefabs:                # List of prefabs that this bonus applies to
      - Wood
      - FineWood
      - RoundLog
      - YggdrasilWood
      - Blackwood
      bonusModifer: 1.5       # The bonus modifier, larger is more, 1.5 is 50% more
      bonusActions:           # List of actions that will trigger this bonus can be Kills or Harvesting
      - Harvesting
    Ore:
      skillInfluence: true
      prefabs:
      - CopperOre
      - TinOre
      - IronScrap
      - SilverOre
      - BlackMetalScrap
      - CopperScrap
      - FlametalOreNew
      bonusModifer: 1.5
      bonusActions:
      - Harvesting
```

### Skill Configuration
Skill configuration proviedes a way to grant additional XP or reduced XP for any or all skills
```yaml
  skillModifiers:
    All:                      # Name of the entry, can be anything
      skillInfluence: true    # Whether or not Deathlink skill will influence this bonus
      skill: All              # The skill that this bonus applies to, can be All or any specific skill name
      bonusModifer: 1.2       # The bonus modifier, larger is more, 1.2 is 20% more
```

### Death Loot Configuration
Death loot configuration provides a way to gain additional items on kills, specific to player Death choice
```yaml
  deathLootModifiers:
    AmberPearl:               # Name of the entry, can be anything
      prefab: AmberPearl      # The prefab name of the item to drop
      chance: 0.05            # The base chance of the item dropping, 0.05 is 5%
      amount: 1               # The amount of the item to drop
      bonusActions:           # List of actions that will trigger this bonus can be Kills or Harvesting
      - Kills
```

### Resetting a players choice
You or someone you play with has made a terrible choice, they thought they were so hardcore and now they can't stay alive.

Well! There is a solution! As an admin you can reset yours or other players death choices, ensure that you have the console enabled by setting your launch paramters in your mod manager to include `-console`

In game run the command `dl-reset-choice [playername]` eg: `dl-reset-choice Midnight` or `dl-reset-choice 91231230123`. This command will clear the players choice from the server, and remove their choice from their character.
They should open their inventory again to select a new choice.

If you are having trouble matching an existing player in the game, be sure to turn logging on for Deathlink, and validate the steam ID or player name you are using matches a player in the game.


### Localization
External localization can be configured at `BepInEx\config\Deathlink\localizations`. This folder and the default localization will be generated and added the first time this mod runs.
New localization keys will be added to localization files as they are added to the mod. Existing localization keys will not be changed, so your localization customizations are safe.

### Questions, Bug reports and feedback

Got a bug to report or just want to chat about the mod? Drop by the discord or github.
[![discord logo](https://i.imgur.com/uE6umQE.png)](https://discord.gg/Dmr9PQTy9m)
[![github logo](https://i.imgur.com/lvbP5OF.png)](https://github.com/MidnightsFX/valheim_rougelite)



