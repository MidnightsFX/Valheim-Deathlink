  **0.11.0**
---
```
Death choice configuration is much harder to break, and two settings that were documented but did
nothing now work. Existing DeathChoices.yaml files load unchanged.

Configuration
- DeathChoices.yaml is now written with full documentation of every setting at the top of the file.
- A typo no longer discards your entire configuration. An unrecognised setting or a misspelled value
  is reported in the log with its line number and skipped; everything else still loads. A file that
  cannot be parsed at all leaves the last working configuration in place and your file untouched.
- A misspelled value now lists every valid option in the log.
- Removing or renaming a death level now warns which players will be moved and where to.
- Bad prefab names, duplicate prefabs across entries, a DefaultDeathChoice that does not exist, and
  suspect skill loss ranges are all reported at load time.
- New 'Fallback: true' setting marks which level players move to when their choice disappears.
  Previously this was whichever level happened to be first in the file, so reordering changed it.
- Config files are watched by polling rather than the OS watcher, so one save is one reload.
- New Config Poll Interval and Config Apply Delay settings control how quickly edits are picked up.

Fixes
- Turning a setting OFF is no longer silently lost. Any 'false' you wrote for FoodLossOnDeath,
  SkillLossOnDeath, FoodLossUsesDeathlink or EnableItemSavingChoices was being dropped whenever the
  file was rewritten and reverted to on.
- Clients now store the server's configuration exactly as sent instead of a lossy re-save, which also
  stops the config file header being wiped on connect.
- Fixed a crash on every harvest if one level listed the same prefab in two ResourceModifiers
  entries, and the same crash on kills for duplicate DeathLootModifiers prefabs.

Behaviour changes
- DeathSkillRate now works. It was documented as "rate at which Deathlink skill increases" but was
  never read. If you set it above 1, Deathlink skill will now climb faster than it did.
- SkillInfluence now works, and now defaults to OFF. It was documented as "whether Deathlink skill
  will influence this bonus" but was never read, so bonuses always applied in full. With it off you
  keep exactly that behaviour; turn it on to scale a bonus in with Deathlink skill instead, from
  nothing at 0% to the full amount at 100%.
  Older versions wrote "skillInfluence: true" into every modifier whether you asked for it or not, so
  your existing file almost certainly has it set everywhere. Deathlink clears those once, the first
  time it loads your file, and says so in the log -- your bonuses keep working exactly as they did.
  Turn it back on for any bonus you actually want to scale with skill; that choice is kept.
- 'bonusModifer' is now spelled 'BonusModifier'. The old spelling is still read, so existing files
  keep working, but files written by this version use the corrected name.
- Newly written config files use PascalCase names. Settings are read without regard to case, so
  older files are unaffected.
- Clients and servers must both be on 0.11.0; the config sync format changed.
```

  **0.10.3**
---
```
- Updates required Jotunn version
```

  **0.10.2**
---
```
- Fixes skill gains not always applying
- Fixes non-equipment saving not always giving the correct budget
```

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