### TL/DR:
- Campaign is mostly playable, but there are a few heavily bugged encounters
- There are no changes to content or balance or how mythic paths/companions work.
- Game Version/DLC/Mods should match across players. **The mod does not lock or unlock discrepant content for you**


# Table of contents
1. [Basics](#basics)
1. [Networking](#networking)
1. [Interface](#interface)
1. [Game Settings](#game-settings)
1. [Localization](#localization)
1. [Pausing](#pausing)
1. [Dialogs](#dialogs)
1. [Vendor](#vendor)
1. [Group Changer](#group-changer)
1. [Skip Time](#skip-time)
1. [Rest](#rest)
1. [Area Transitions](#area-transitions)
1. [Cutscenes](#cutscenes)
1. [Inventory](#inventory)
1. [Loot](#loot)
1. [Spellbook](#spellbook)
1. [Leveling](#leveling)
1. [Rolls](#rolls)
1. [Combat](#combat)
1. [Global Map](#global-map)
1. [Alushenyrra Isles (Act 4)](#alushenyrra-isles)
1. [Midnight isles DLC](#midnight-isles-dlc)
1. [Ping system](#ping-system)
1. [Entity Id generation](#entity-id-generation)
1. [Other Mods](#other-mods)
1. [Known Issues](#known-issues)
1. [Long Term Plans](#long-term-plans)


## Basics
It's safe to use this mod for a dedicated multiplayer save. Mod doesn't affect other save games, nor does it write additional information to the current save. Also, all multiplayer changes are disabled in single-player mode.

Multiplayer window loads all available saves. You can use any save to host a multiplayer game.

There is a special `New Campaign` save slot that can be used to start fresh campaign. Player assigned to control main character will be in control of Character generation screen. 

Worth noting, such way to start game is very limited as of now:
- no save import
- no pregen characters
- no custom difficulty

However, you can create a save game in single-player using those features and then host the game from that save.

The latest received save game (either when joining a lobby or when another player loads a game during a session) is stored at `%LOCALAPPDATA%low\Owlcat Games\Pathfinder Wrath Of The Righteous\Multiplayer\Saved Games\latest received save.zks`.

Load/Quickload is available for **everyone** during multiplayer game. It will force other players in the lobby to load the same saved game. However, it skips all synchronization checks, so the game will load even if someone failed to receive or store the save file.

A stable 60+ FPS is strongly recommended. Some game logic is FPS dependent. Abysmal performance in the late game makes it worse.

There is no hard player limit. Extra players can even join as spectators with 0 units, though the player list UI might look a bit weird. That said, most testing was done with 2 players (sometimes 3), so the more people you add, the more likely things are to break.

It is possible to join when the game has already started, you just need to ask someone to load save game and it will load for you as well

### Logs
- Mod writes logs to `./logs` folder.
- Log files are never cleaned by the mod itself

## Networking

### Direct IP Connect
It works fine with local network emulators or a public/static ('white') IP from your ISP.

### Game Codes
P2P connection via selected server on the Host tab

`AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Multiplayer\servers.json` contains servers configuration. Deleting this file force mod to recreate it with a default config.

You can always host/configure your own P2P server in case of any issues. Sources/Container image is available [here](https://github.com/fl01/pathfinder-wotr-multiplayer-beacon)


## Interface

- Photo Mode is disabled for now since it pauses the game and doesn’t add much in multiplayer.
- Various interface buttons contain counter to see how many players are ready for the action
- Opening the Esc menu or a fullscreen window will not pause the game anymore. You can freely change settings/expore inventory/etc without affecting anyone.

### Action Bars
Action bars stay synced. Any changes you make are instantly reflected for everyone in the multiplayer session.

## Overtips
The name of the player controlling a character shows up after the character's name. For example: "Seelah" -> "Seelah (player-name)". However, this does not update immediately, it takes some time (or area reload) to update(recreate) overtips

## Game settings

Multiplayer needs some settings to be locked in and synced across players. When you host, the required settings get applied automatically. If you join someone else's game, you will just get the host's settings.

Some options can't be changed mid-game - you will see those grayed out in the settings menu.

## Localization
Custom-made localization that is fully integrated with native `LocalizationManager/LocalizedStrings`. Though `enGb` is the only option available by default.

Look at [localization](/docs/localization/localization.md) for more details

## Pausing
### Manual Pause
Manual pause can be initiated by anyone, but once paused, it can only be unpaused if everyone else has paused too. It works fine in general case, but can be buggy sometimes as it's not guaranteed that the game will allow to pause for everyone (i.e. already started combat/cutscene/etc), so there is a hotkey to remove 'network pause' state and unpause your local game. Use it in case of any issues.

### Forced Pause
Sometimes the game will hit a `forced pause` that you can't unpause yourself normally (via Spacebar), but it will go away automatically once everyone is synced up again

Forced pause occurrences:
- loading area
- loading random REST encounter

### Autopause
Autopause is disabled for everything, but trap detection

## Dialogs

All dialogs are handled by the host. Clients can vote on responses, but only the host makes the final choice. The voting system works for every type of dialog (book events/ interchapter/etc).

Everyone in the session has to see the dialog line before the host can pick an answer, which keeps things in sync. As a client, you don't technically start a dialog yourself - you just ask the host to start it. This way, everything happens on the host side first, preventing desyncs.

## Vendor

Certain vendor actions are host-only, like finalizing a deal or closing the shop window. Everything else is fully synced and open to everyone - like moving items to buy/sell, bulk selling, or removing items in bulk.

## Group Changer
##### Note: Group Changer data is stored within the UI itself, so it must be opened for everyone before Host is allowed to make any changes (just for the sake of simplified sync implementation)

## Skip Time
works the same way as Group Changer due to same data storage reasons

## Rest
Only the host can set up camp, including managing roles and picking crafting recipes, but the Camp UI updates for everyone. Host can do changes even if UI is not opened for everyone since Rest configuration (roles/recipes/etc) is not tied to UI, so it can be updated in background

Rest can't start until everyone is in 'rest mode' (aka opened the rest window). The Start button now shows a counter for how many players are ready to go.

Banter (small talk) is randomly picked in a deterministic way, so everyone sees the same dialogs. Skipping banter lines is synced for all players.

### Random encounter
The host rolls encounters. Clients communicate with the host after each sleep phase to receive rolled results.

## Area Transitions
Area transition movement now affects only the characters you directly control, but host still follows the default area transition requirement: entire party must be near the exit. As a result, every player has to click either the area transition icon (overtip) or manually move their characters to the exit.

Clients cannot initiate an actual area transition themselves, but they are automatically transferred once the host triggers it. This makes it impossible to be in a desynced state where only one player is moved to the next area.

## Cutscenes
Cutscene skip is synchronized, but, as of now, there are no additional checks to make sure it started/ended for everyone.

## Inventory
Inventory item positions are not synced, so you can sort or split items however you want - but keep in mind that loading a save will reset everything to the save owner version.

## Loot
Items are never created, but rather transferred from X to Y container which makes it impossible to recieve duplicate items. It's completely safe to loot same container/item, but item verification is very basic. It might produce false positive warnings about missing loot (since it's already looted) if you loot same item at the same time.

### Area Looting screen: 
Same as regular looting. However, 'Collect All' / 'Leave' / 'Destroy Uncollected Loot' buttons are disabled for host until everyone is ready to leave the area.

### small bugfix
The base game contains a bug where you could get your items destroyed by moving items into a disposed lootbag object. Lootbags are transient. They are deleted from the game once every item has been looted. "Zone Looting" screen allows you to loot items from lootbags, but it also enables you to put items back, even though that lootbag had been destroyed previously. As a result, every item you left in that disposed lootbag will be destroyed once you close the "Zone Looting" UI.

Mod explicitly denies you from moving items into an already destroyed lootbag. You will see a notification message once this happens.

## Spellbook

The Spellbook UI updates in real time, so you can see when other players are forgetting/memorizing spells. This makes it easier to chat about which spells to pick. Plus, a combat log notification pops up whenever someone changes their spells.

### Changing spells
You can only change spells for characters you control. Trying to change someone else's spells will show a warning.

### Metamagic
You are free to create metamagic for any character (even if you don't control it)

## Leveling
Leveling is synced via UI interactions. Host has to confirm opening the leveling screen before everyone else sees it (same system as dialogs for consistency).

Leveling UI is controlled by the character owner. Everyone else is just a watcher who can't neither interact with UI nor close it.

Simultaneous leveling (i.e. different characters by different players at the same time) is not planned for the near future.

### Mythic leveling
same rules apply for everything (including path selection).

### Respec
Character selection for respec is always controlled by host, but respec/leveling windows themselves follow the default leveling rules

#### Toybox (Party => Respec)
Works fine since it starts default respec process (same as via Hilor on easy difficulty). However, everyone still needs to press that button to start respec process locally as there is no automated startup in this case.

As a side note, you can do whatever you want via Toybox (add items/change a quest state/etc), just need to save/load the game afterwards.

#### RespecWrath
While it lets you fully respec companions (ignoring their original blueprint), it doesn't work by default in multiplayer. Do the respec in single-player first, then load the save in multiplayer.

### Hiring(Creating) Mercenary
same rules apply, but host always controls the leveling (CharGen) screen

## Rolls
Every roll is rolled deterministically based on the numerous seeds (e.g. AreaSeed/Combat Turn Seed/etc.) and roll context (RuleName / Total modifers / Target / etc)

### Perception check rolls (map objects)
Perception checks to reveal stuff don't auto-trigger on clients. Instead, they only go off once the host triggers them. This is to prevent desyncs, like different characters trying to run the same check because of movement lag or other hiccups.

### Inspection Unit knowledge check rolls
Works the same way as Perception checks - unit info only gets revealed once the host triggers it.

### Stealth Unit perception checks
Game spams this 'rule' every tick, but actually relies on cached perception check roll. Changed to work the same way as regular perception checks.

### Inspection Buff check rolls
Automatically passed on both host/client

## Combat

Turn-Based only. Real-Time with Pause (RTwP) combat is completely disabled.

You can switch character owner at any time during combat.

### Random enemy spawns
Sometimes game triggers enemy spawns in a different turns. Enemy units in combat are synced across the players, any new units are not allowed to join combat (and become untargetable) until they are spawned for every player. Combat joins happen in-between turns.

### Players

The source of truth is the host. Position/buff duration/HP of units in combat is synchronized with host on every combat start/turn start/turn end event. Next turn will be started only once host synchronizes info with the clients. Host is also able to detect when clients want to start different character turn (e.g. due to desync issues), but there is an automatic recovery for this situation where host forces clients to start correct turn.

### AI
The game uses `AI action score` calculations that don't have any randomness (AFAIK), but result is heavily affected by factors like low FPS / stutters or similar. In the end, sometimes those calculations result in different AI actions.

To reduce this desync, a simple workaround is used: on the client, the AI turn begins with a slight delay (configurable in settings). This allows the host to determine AI actions first and send them to clients. If a client's locally calculated actions don't match, they are overridden by the host's results.

**Note:** This delay is highly dependent on game performance. If the host's game frequently stutters (for example, during late-game stages), you may need to increase this delay or restart the game more frequently.

### Buffs
Buff timers (duration/tick time) are synced in combat only, but desynced buffs are never created apart from `Fighting defensively`. A lot of buffs do have an underlying `UnitPart` that holds buff state/logic, so simple recreation/removal will not work without syncing that state. This will be addressed sometime later.

### Crusade Army Battles
Combat is fully controlled by the host.

AI sync is not enabled for this combat as it seems impossible to make different AI action in such limited zone.

## Global Map
Global map movement is controlled by the host. Client retains the ability to click different locations to see pop-ups/descriptions.

Random encounters are never rolled on the client side, but rather are sent by the host once they occur.

Although the base game has no support for pausing while you are in the global map, we still need to sync loading times. As a result of this, host simply can't trigger movement until everyone is loaded.

### Crusade
Crusade is fully controlled by the host (army movement/recruiting/battles/etc...)

### Crusade automode
Never checked if it contains any randomness. Also, there are no plans to support it

## Alushenyrra Isles
Dynamic parts (isles/houses/platforms/etc) are controlled by the host camera direction. It might be a bit clunky, but make sure characters don't get stuck as there is no additional position or isle state sync.

## Midnight isles DLC
Partially synced, but it's never been finished to know for sure

## Ping system
There is a configurable hotkey you can use to send pings (alerts) to other players. This includes position, unit and map object pings.

More options are planned later, like pinging at different UI elements (should be useful during leveling or vendoring).

## Entity ID Generation 
The game originally used a single counter to generate new entity IDs (for characters, area effects, facts, map objects, etc.), however this approach didn't work well in network environment. Now it uses a "consistent" generator that considers the current game state (gameId, location, etc.), so IDs should match across all multiplayer players.

The tricky part is `Item.UniqueId` generation - stacking or splitting items creates new IDs locally. Fully syncing that (plus inventory positions) would be a lot of work for almost no benefit. Instead, any network item action (like dropping an item or looting a container) just falls back to matching the item by everything except its `UniqueId`. In short, the mod just looks for 'the exact same item' and applies the action to it.

## Other Mods
Most of the syncing relies on the blueprint asset ID (or something similiar) to replicate action for other players, so in **THEORY** things should work fine as long as those IDs are the same for abilities, spells, etc. This also includes any extra rolls (e.g. Skill Check or Attack Roll) added by mods, but those should use existing rules from a base game. **There is no support for custom-made rules as of now**.
That said, compatibility with other mods hasn't really been tested at all and there are no plans to support it yet.

### Adding Multiplayer Compatibility to your mod

See the [general information](/docs/dev/mods.md) about mod integration, as well as a basic [example](https://github.com/fl01/pathfinder-wotr-multiplayer-bubblebuffs) demonstrating how to utilize the existing networking layer in a separate mod.

### Verified mods:
- Bubble Tweaks - works fine if you use it purely for non-interactive UI changes (Character statistics / Show AoE range / Loot icons / etc)
  - additional interactive UI elements ('Jump to siege' button / Dismiss army / etc) are not synced
  - animation speed changes are not synced

Here is the list of mods which were used during full campaign playthrough (Angel path) in multiplayer:

- 0ToyBox0 - no exta features enabled, just to alter game state when needed
- BubbleBuffs
- BubbleBuffs.Multiplayer
- RespecWrath
- Visual Adjustments

## Known Issues
See the list [here](/docs/issues.md)

## Long term plans

### UI Control
Every UI window/element that is not tied to a specific character is controlled by the host, but there are no restrictions on changing this behavior. The only reason for this is the simplicity of implementation compared to dynamic configuration. 

Eventually, the Lobby window will serve as a central place to configure control over dialogs, vendors, the global map, and other shared interactions.