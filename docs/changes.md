# Multiplayer changes

#### Disclaimer: All of the changes below only kick in when you are playing in Multiplayer (started or joined through the Multiplayer Window). Single-player isn't affected at all, so you can keep playing normally. But if you are curious about what the mod adds, you can always host a solo game to check it out.

## TL/DR:
- Prologue and Act1 are mostly playable. Anything beyond that (including any DLCs) does not work

- DLC/Mods/Content should match across players. 

  You can see a quick rundown of each player’s DLCs/mods once they connect to the host, but it's just for a reference. **The mod does not lock or unlock discrepant content for you**. The most obvious case is extra preorder/DLC items sitting in your stash. If someone doesn't own that DLC or bonus, those items are hidden for them, so anything you try to do with them (equip, drop, etc.) will just fail. One simple workaround is to get rid of them by dropping the items on the global map in single-player.

## Other Mods Compatibility
Most of the syncing relies on the blueprint asset ID (or something similiar) to replicate action for other players, so in **THEORY** things should work fine as long as those IDs are the same for abilities, spells, etc. This also includes any extra rolls (e.g. Skill Check or Attack Roll) added by mods, but those roll types must be available within base game.
That said, compatibility with other mods hasn't really been tested at all and there are no plans to support it.

However, this might be revised later on once multiplayer is stable enough to start focusing on making things play nicely with popular mods.

### Known mod compatibility issues:
- ModMenu
  - global edit of `SaveLoadPcView` causes some minor side-effects for our own copy of that view
  - multiplayer settings are corrupted / not loaded correctly on Settings UI. Didn't look for a reason yet

## UnityModManager settings
- Checkbox to show the debug console (for anyone curious about the behind-the-scenes stuff).
- Checkbox to display the unitId in the name tag above your character, e.g., 'Seelah' → 'Seelah [my-unit-id]'.

Most players won't need to bother with these settings

## Networking
There are two connection options planned, but only one works right now:

- Direct IP Connect – works fine with local network emulators or a public/static ('white') IP from your ISP.
- Game Code (via remote server) – not available yet.

## Interface

Added few elements to the game:
- 'Multiplayer' main menu option - use it to host or join games
- 'Multiplayer' configuration tab within Settings menu - contains bunch of settings like your name or which port to use for hosting
- 'Multipalyer Lobby' menu within Esc menu - see who is playing / change character control

A couple of notes:
- Photo Mode is disabled for now since it pauses the game and doesn’t add much in multiplayer.
- Multiple interface buttons contain counters to see how many players are ready for the action

## Localization
Custom-made localization that is fully integrated with native `LocalizationManager/LocalizedStrings`. Though `enGb` is the only option available by default.

Look at [localization](/docs/localization/localization.md) for more details

## Game settings

Multiplayer needs some settings to be locked in and synced across players. When you host, the required settings get applied automatically. If you join someone else's game, you will just get the host's settings.
Some options can't be changed mid-game - you will see those grayed out in the settings menu.

## Basics
Multiplayer window loads all available saves. You can use any save to host a multiplayer game.

There is a special `New Campaign` save slot that can be used to start fresh campaign. It will start regular new game sequence, but for everyone in multiplayer session. Player assigned to control main character will be in control of leveling (Character generation) screen. 

Worth noting, such way to start game is very limited as of now:
- no save import
- no pregen characters
- no custom difficulty

However, you can create a save game in single-player using those features and then host the game from that save.

Clients store the most recently joined and started multiplayer save game at `%LOCALAPPDATA%low\Owlcat Games\Pathfinder Wrath Of The Righteous\Saved Multiplayer Games\latest joined game.zks`.

Load/Quickload is available for **everyone** during multiplayer game. It will force other players in the lobby to load the same saved game. However, it skips all synchronization checks, so the game will load even if someone failed to receive or store the save file.

A stable 60+ FPS is strongly recommended. If you drop below that, you will probably run into various desync issues (like AI picking different targets, units not attacking after moving, etc.)

There is no hard player limit. Extra players can even join as spectators with 0 units, though the player list UI might look a bit weird. That said, most testing was done with 2 players (sometimes 3), so the more people you add, the more likely things are to break.

It is possible to join when the game has already started (host IP address or game code can be found in the Multiplayer Lobby window), you need to ask host to load save game and it will load for you as well

## Logs
- Mod writes logs to `Mods/WOTRMultiplayer/logs` folder.
- Log files are never cleaned by the mod itself

## Pausing

Opening the Esc menu or a fullscreen window will not pause the game anymore. You can freely change settings/expore inventory/etc without affecting anyone.

### Manual Pause
Only the host can pause the game (with spacebar or whatever key they have set). Once paused, it can only be unpaused if everyone else has paused too.

### Forced Pause

Sometimes the game will hit a `forced pause` that you can't unpause yourself (neither as a host nor a client), but it will go away automatically once everyone is synced up again.

Forced pause occurrences:
- loading area
- loading random REST encounter

### Autopause
The only time autopause kicks in is for trap detection, and it follows the same rules as manual pause

## Overtips
The name of the player controlling a character shows up after the character's name. For example: "Seelah" -> "Seelah (player-name)"

## Dialogs

All dialogs are handled by the host. Clients can vote on responses, but only the host makes the final choice. The voting system works for every type of dialog (regular convos, interchapter scenes, book events).

Everyone in the session has to see the dialog line before the host can pick an answer, which keeps things in sync. As a client, you don't technically start a dialog yourself - you just ask the host to start it. This way, everything happens on the host side first, preventing desyncs.

## Vendor

Certain vendor actions are host-only, like finalizing a deal or closing the shop window. Everything else is fully synced and open to everyone - like moving items to buy/sell, bulk selling, or removing items in bulk.

## Group Changer
##### Note: Group Changer data is stored within the UI itself, so it must be opened for everyone before Host is allowed to make any changes (just for the sake of simplified sync implementation)

### Leaving zone
Client is only a watcher with no permissions to change anything. There is a counter within Accept button to see how many players are ready. Closing screen as a host will result in closing screen for everyone

### Mid Zone (aka recruiting companions)
The same rules apply

## Skip Time
works the same way as Group Changer due to same data storage reasons

## Area Transitions
When someone triggers an area transition, the game tries to move the whole party (even characters you don't control) to the exit. Usually that works fine, but you can still cancel movement for your own characters. Because of network delay, this might not sync instantly for others, which can lead to one player loading the next area while the rest are still behind.

If that happens, the players who already transitioned will be stuck in a forced pause until everyone else loads in. Easiest fix: anyone left behind should just click the transition again to catch up. While that's happening, avoid doing stuff that could cause desync (like using items, abilities, or picking up loot).

## Global Map
Global map movement is controlled by the host. Client retains the ability to click different locations to see pop-ups/descriptions.

Random encounters are never rolled on the client side, but rather are sent by the host once they occur.

Although the base game has no support for pausing while you are in the global map, we still need to sync loading times. As a result of this, host simply can't trigger movement until everyone is loaded.

## Rolls
Most rolls happen on the host, but combat works a little differently. Each turn has a `Turn Owner` - the player controlling the active character. That player's rolls are done locally so their turn feels smooth, and then everyone else gets the results over the network.
Worth noting - In cases with 3+ players (1 host/2+ clients), the host acts as a relay to transfer rolls between clients, resulting in slightly more stutters for one client.

Those results come in through the main game loop, which means if the thread gets blocked, your game will freeze until the roll comes through. (If you ever hit the timeout, that means the game is out of sync and needs a reload.)

During cutscenes, all rolls happen locally since they don't really matter (at least, that's the assumption). In dialogs, only skill checks and saving throws are rolled by the host - everything else just rolls locally for the same "doesn't really matter" reason

### Perception check rolls (map objects)
Perception checks to reveal stuff don't auto-trigger on clients. Instead, they only go off once the host triggers them. This is to prevent desyncs, like different characters trying to run the same check because of movement lag or other hiccups.

### Inspection Unit knowledge check rolls
Works the same way as Perception checks - unit info only gets revealed once the host triggers it.

### Stealth Unit perception checks
Game spams this 'rule' every tick, but actually relies on cached perception check roll. Changed to work the same way as regular perception checks.

### Inspection Buff check rolls
##### Disclaimer: Always passed in Tactical combat by default
Automatically passed on both host/client

## Combat
Turn-Based only. You can't even enable Tactical Combat within MP session

Switching character control after a turn has already started isn't supported. Some calculations break when you do that, and re-running them isn't really in scope right now.

### Players

Combat start is synced for all players. Every unit's position gets synced at the start of each turn, and turn order comes from the host - so no one can start a turn out of order.

Combat flow:
- Client starts combat: waits for the host to confirm Combat Start, then rolls initiatives once confirmed → sends Unit Turn Start request to host → waits for host confirmation → syncs unit positions → turn actually starts.

- Host starts combat: sets up combat (rolls initiatives, etc.) → sends Combat Start confirmation to clients → waits for all clients’ Unit Turn Start requests → syncs unit positions → turn actually starts.

### AI
AI actions don't have any randomness, so their turns play out the same for everyone in multiplayer. AI rolls are handled by the host when out of combat, or by the `Turn Owner` during combat.

In rare cases, AI might attack a different target or make an extra attack after moving - usually because of position desync or low FPS. There's a basic AI sync option that forces AI to attack the same target, but you can turn it off in 'Multiplayer Settings' if it causes issues

## Action Bars
Action bars stay synced. Any changes you make are instantly reflected for everyone in the multiplayer session.

## Inventory
Inventory item positions are not synced, so you can sort or split items however you want - but keep in mind that loading a save will reset everything to the save owner version.

## Loot
Looting is synced for all multiplayer players, so everyone can grab items from the same container at the same time.

### Area Looting screen: 
Same as regular looting. However, 'Collect All' / 'Leave' / 'Destroy Uncollected Loot' buttons are disabled for host until everyone is ready to leave the area.

### small bugfix
The base game contains a bug where you could get your items destroyed by moving items into a disposed lootbag object. Lootbags are transient. They are deleted from the game once every item has been looted. "Zone Looting" screen allows you to loot items from lootbags, but it also enables you to put items back, even though that lootbag had been destroyed previously. As a result, every item you left in that disposed lootbag will be destroyed once you close the "Zone Looting" UI.

Mod explicitly denies you from moving items into an already destroyed lootbag. You will see a notification message once this happens.

## Rest
Only the host can set up camp, including managing roles and picking crafting recipes, but the Camp UI updates for everyone. Host can do changes even if UI is not opened for everyone since Rest configuration (roles/recipes/etc) is not tied to UI, so it can be updated in background

Rest can't start until everyone is in 'rest mode' (aka opened the rest window). The Start button now shows a counter for how many players are ready to go.

Banter (small talk) is randomly picked in a way that stays consistent - host and clients roll separately but still get the same results without extra network syncing. Each multiplayer session has its own "random seed", so rehosting the game gives different banter. Skipping banter lines is synced for all players.

### Random encounter
Everything about random encounters is synced from the host. That synchronization comes to play after banter has ended.

## Memorizing spells
You can only change spells for characters you control. Trying to change someone else's spells will show a warning.

The Spellbook UI updates in real time, so you can see when other players are updating spell. This makes it easier to chat about which spells to pick. Plus, a combat text notification pops up whenever someone changes their spells.

## Leveling
Leveling is synced for all multiplayer players. The host has to confirm opening the leveling screen before everyone else sees it (same system as dialogs for consistency).

Some rules for the leveling screen:
- Only the character owner can make changes (class, skills, abilities, etc.). Everyone else just watches.
- Watchers can't close the leveling screen.
- The character owner has to wait while the screen loads for everyone else. Switching phases (like from class selection to skill points) locks the owner until everyone has loaded the new phase.
- Closing the leveling screen as the owner closes it for everyone.

### Mythic leveling
same rules apply

### Respec
Character selection for respec is always controlled by host, but respec/leveling windows themselves follow the default leveling rules

#### Toybox (Party => Respec)
Synchronization will start working at the moment of opening respec window, but everyone still needs to press that button to start respec process as there is no automated respec startup in this case.

Same leveling/respec rules apply

#### Other mods
Never tried, never checked. Should work if it opens the default respec window too.

### Hiring(Creating) Mercenary
same rules apply, but it's always controlled by the host

## Entity ID Generation 
The game originally used a single counter to generate new entity IDs (for characters, area effects, facts, map objects, etc.), however this approach didn't work well in network environment. Now it uses a "consistent" generator that considers the current game state (gameId, location, etc.), so IDs should match across all multiplayer players.

The tricky part is `Item.UniqueId` generation - stacking or splitting items creates new IDs locally. Fully syncing that (plus inventory positions) would be a lot of work for almost no benefit. Instead, any network item action (like dropping an item or looting a container) just falls back to matching the item by everything except its `UniqueId`. In short, the mod just looks for 'the exact same item' and applies the action to it.

## How to deal with desync
If a roll doesn't come through for some reason, you will get a stutter and a popup warning. In that case, the roll will be rolled locally, which might lead to different results.

You can either ignore it (if the outcome is more or less the same for everyone) or do a quicksave/quickload to resync all players.

## Long term plans

### Ping system
Highlighting a point/unit/map object for everyone in the multiplayer

### Rolls
Current rolls synchronization implementation has an obvious flaw - freezing game while rolls are being retrieved. That's happening because network communication occurs at the moment when game tries to roll dice.

The plan is to move to 'predictable rolls' where both the host and client use the same seed to roll random values. There are good timings when these seeds could be transferred without blocking the game (combat start / round start / area load).

The downside is that incorrect usage of those seeds will break roll sequences for an entire round (i.e., until the next seed update). However, the current sync implementation has helped to isolate the dice rolling process, so it should be relatively easy to make an upgrade now.

Anyway, this will be updated on a roll-type basis, i.e., reworking attack/damage rolls would almost eliminate stutters in combat, as you rarely roll anything else
