  **0.10.1**
---
```
- Improves dedicated server parsing of leaderboard data
- Imrpoves refresh of existing leaderboard data experiance
```

  **0.10.0**
---
```
- Improves responsive tracking of leadership board data
- Ensures player data is synced to the server before disconnects
- Adds a death choice option, which scales with deathlink level
	- Death choice allows selecting which items are saved (or a portion of items saved)
	- Only applies for deathlink progression based deaths (not hardcore or vanilla)
	- New configurations added for the default deathlink profiles
```

  **0.9.3**
---
```
- Improves resiliance of a number of configurations which if unset could result in unexpected behavior
- Moves the deathlink leadership board to the deathlink tab, defaults it to hidden
- Fixes an issue where the deathlink selection would select the wrong entry, if configuration was broken
```

  **0.9.2**
---
```
- Adds per death choice damage taken/done configuration
	- This allows players having unique damage amplification or reduction based on their death choice
	- Player A could take 25% more damage while player B could take 5% less damage, both on the same server
```

  **0.9.1**
---
```
- Adds a server-tracked leaderboard, shown in the Compendium, with Survival, Combat and Gathering views
- Leaderboard data is persisted on the server and synced to clients every 30 minutes (configurable)
- Note: total damage tracking is approximate in multiplayer (counts damage processed on the attacker's client)
- Adds a configuration to set the default deathlink choice for players (removes the players choice)
- Also added 1 (configurable) chance to reset deathlink choice from the compendium
```

  **0.8.7**
---
```
- Improves fallback when loading player profile data
```

  **0.8.6**
---
```
- command support for RCON by tristan
```

  **0.8.5**
---
```
- Improve dl-reset-choice now supports:
	- Matching player name
	- Player platform account name
	- Player platform ID
```

  **0.8.4**
---
```
- Fixes AzuExtendedInventory integration
```

  **0.8.3**
---
```
- Removed Repacker
- Improves server relay for reset-choice command
```

  **0.8.2**
---
```
- Fixes scroll visibility of death choices
- Adds more failsafes for invalid death choice configurations
- Improves deahtlink reset command matching to player names or ids
- Adds backpack integration to avoid deathlink destroying player backpacks (just items in the pack)
- Adds missing wood types to the default list of wood harvestables
```

  **0.8.1**
---
```
- Adds a Deathlink choice reset command `dl-reset-choice`
	- Can be used locally or on a server (only by admins)
	- Requires that the target player is online
```

  **0.8.0**
---
```
- Adds support for storing death configuration as a player private key, this is now the default method
- Syncs player deathlink key on server join
- Optionally falls back to yaml configuration if no key is found
- Fixes a bug with AzuExtendedInventory integration where items might not be saved correctly
- Fixes an issue with remote deathlink configuration not being applied if it was synced after the players preferences were loaded
- Adds an integration to WackyMMO
	- Allows WackyMMO XP loss on death
	- Allows WackyMMO XP gain from deathlink progression
	- Adds configuration to disable WackyMMO XP loss on death
	- Adds configuration to tune XP loss/ XP gain rates
```

  **0.7.5**
---
```
- Improves Death transpiler compatibility with other mods
```

  **0.7.4**
---
```
- Fixes an issue re-adding remote deathlink configurations
```

  **0.7.3**
---
```
- Kill drops rewards now require the player has hit the target creature
- Death XP Penalty is scaled correctly
- Added an integration for Almanac Classes, which allows gaining and loosing Almanac XP
```


  **0.7.2**
---
```
- Fixes probability comparisons for low chance kill drops
```

  **0.7.1**
---
```
- Fixes divide by zero for rarity based loot rolls
- Adds harvest multipliers support to pickable items
```

  **0.7.0**
---
```
- Multiplayer Overhaul
- Death configuration is now managed by profiles
- Players on a server can choose which deathlink profile they want to use
- Selected choice and bonuses/penalties are visible in the compendium
- Death profiles can influence many aspects
	- Increase/decreased gathering yields
	- Drops from enemies
	- Skill gains/penalties
	- What happens to your items on death
	- How many items you can keep on death
	- How much food you keep on death
- Dependencies have been updated to current version of Jotun
```

  **0.5.5**
---
```
- Fixes equipped items not being saved to tombstone when they are removed
```

  **0.5.4**
---
```
- Prevent shuffling of items on death
- Fix some items appearing as duplicated ghosts
```

  **0.5.3**
---
```
- Refresh inventory state to prevent ghost items
```

  **0.5.2**
---
```
- Added localization and external localization support (Bepinex/config/Deathlink/localization)
```

  **0.5.1**
---
```
- Fixes an issue that would prevent saving any equipment
- Changes the default maximum equipment saved style to be an absolute value (this will not change existing configs)
```

  **0.5.0**
---
```
- Adds a system to allow configuring skill reduction rates on death
	- Skills can now be configured to be reduced by a percentage, like vanilla
	- Skills GAINS since the last death can also be reduced by a configurable percentage
	- Skills which can lose XP can be configured
		- Configure which skills do not loose gained XP
		- Configure which skills do not loose XP on death
- 
```

  **0.4.1**
---
```
- Fix minimap marker on death NPE
```

  **0.4.0**
---
```
- Added MaxPercentResourcesRetainedOnDeath which provides an alternative to MaximumEquipmentRetainedOnDeath and can the behavior between the two can be toggled with MaximumEquipmentRetainedStyle
	- This allows for scaling of the amount of equipment saved for a variety of different playstyles, and the option for more linear progression between the start of the game to the end of the game
- Fixes an edgecase for retaining food on death
- Fixes an edgecase for retaining items on death that would result in the player retaining less items than intended
- Added DeathSkillPercentageStyle which allows for scaling of saved items based on total player inventory size and not just items in the players inventory
- Lowered the skill floor for item loss, particularly impactful for extremely large inventories (80+)
- Increased base XP rate for all activities
```

  **0.3.4**
---
```
- Fix non-skill checked items being processed incorrectly
```

  **0.3.3**
---
```
- Update default frequency of skill gains
```

  **0.3.2**
---
```
- Fixes AzuExtendedInventory integration not shuffling saved equipment
- Fixes MaxItemsSaved resulting in the same items regularly being saved
```

  **0.3.1**
---
```
- Adds a max number of equipment items saved option
- Adds the option to save all items to the tomb stone
```

  **0.3.0**
---
```
- Fixes old binary being included
- Adds AzuEPI integration
	- Supports saving items from quickslots added by any mods that use AzuEPI
	- Saving of items is not ordered and may not result in the item staying in the slot (unless it is equipment)
- Logo fixes
```

  **0.2.0**
---
```
- Add configurable food clearing on death
- Add configurable food clearing based on skill level on death
- Added configuration to add or not add a map marker on death
```

  **0.1.0**
---
```
- Initial release
- Add death resistance skill
- Add configurable death item loss/destruction
- Add seperate configurable tier for items which are either dropped, or destroyed
- Add skill tracker and skill xp reduction control
```