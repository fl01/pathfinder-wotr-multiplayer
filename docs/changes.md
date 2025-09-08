# Multiplayer changes

#### Disclaimer: All of the changes below only kick in when you’re playing in Multiplayer (started or joined through the Multiplayer Window). Single-player isn’t affected at all, so you can keep playing normally. But if you’re curious about what the mod adds, you can always host a solo game to check it out.

## UnityModManager settings
- Checkbox to show the debug console (for anyone curious about the behind-the-scenes stuff).
- Checkbox to display the unitId in the name tag above your character, e.g., 'Seelah' → 'Seelah [my-unit-id]'.

Most players won't need to bother with these settings.

## Networking
There are two connection options planned, but only one works right now:

- Direct IP Connect – works fine with local network emulators or a public/static ('white') IP from your ISP.
- Game Code (via remote server) – not available yet.

## Interface

The mod adds a new 'Multiplayer' button to the main menu—use it to host or join games.

There's also a 'Multiplayer Lobby' option in the Esc menu, where you can swap around character control during a session.

A couple of notes:
- Photo Mode is disabled for now since it pauses the game and doesn’t add much in multiplayer.
- The Rest window now shows a counter so you can see how many players are ready to rest (or finish resting)."

## Localization
It's mostly just a few menu titles and notification messages, so there's no localization built in. For now (and probably a while), it will stay English-only.

## Game settings

Multiplayer needs some settings to be locked in or synced across players. When you host, the required settings get applied automatically. If you join someone else's game, you will just get the host's settings.
Some options can't be changed mid-game - you will see those grayed out in the settings menu.

## Basics

- You need an existing save file to host a multiplayer game. That save gets shared with everyone in the lobby automatically.
- The most recent multiplayer save is stored at `%APP_DATA%\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Saved Multiplayer Games`
- Mod logs are located in `Mods/WOTRMultiplayer/logs` folder
- Once the game actually starts, a save copy is also stored in your normal save folder.
- If you load or quickload during a session, it forces everyone in the lobby to load the same save (transferred over the network).
- A stable 60+ FPS is strongly recommended. If you drop below that, you'll probably run into various desync issues (like AI picking different targets, units not attacking after moving, etc.).
- There's no hard player limit. Extra players can even join as spectators with 0 units, though the player list UI might look a bit weird. That said, most testing was done with 2 players (sometimes 3), so the more people you add, the more likely things are to break.
- It's possible to join when the game has already started (host IP address or game code can be found in the Multiplayer Lobby window), you need to ask host to load save game and it will load for you as well

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

## Area Transitions
When someone triggers an area transition, the game tries to move the whole party (even characters you don't control) to the exit. Usually that works fine, but you can still cancel movement for your own characters. Because of network delay, this might not sync instantly for others, which can lead to one player loading the next area while the rest are still behind.

If that happens, the players who already transitioned will be stuck in a forced pause until everyone else loads in. Easiest fix: anyone left behind should just click the transition again to catch up. While that's happening, avoid doing stuff that could cause desync (like using items, abilities, or picking up loot).

Area Looting screen: TBD

## Rolls
Most rolls happen on the host, but combat works a little differently. Each turn has a `Turn Owner` - the player controlling the active character. That player's rolls are done locally so their turn feels smooth, and then everyone else gets the results over the network.
Worth noting - In cases with 3+ players (1 host/2+ clients), the host acts as a relay to transfer rolls between clients, resulting in slightly more stutters for one client.

Those results come in through the main game loop, which means if the thread gets blocked, your game will freeze until the roll comes through. (If you ever hit the timeout, that means the game is out of sync and needs a reload.)

During cutscenes, all rolls happen locally since they don't really matter (at least, that's the assumption). In dialogs, only skill checks and saving throws are rolled by the host - everything else just rolls locally for the same "doesn't really matter" reason

### Perception check rolls (map objects)
Perception checks to reveal stuff don't auto-trigger on clients. Instead, they only go off once the host triggers them. This is to prevent desyncs, like different characters trying to run the same check because of movement lag or other hiccups.

### Inspection Unit knowledge check rolls
Works the same way as Perception checks - unit info only gets revealed once the host triggers it.

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

## Rest
Only the host can set up camp, including managing roles and picking crafting recipes, but the Camp UI updates for everyone.

Banter (small talk) is randomly picked in a way that stays consistent - host and clients roll separately but still get the same results without extra network syncing. Each multiplayer session has its own "random seed", so rehosting the game gives different banter. Skipping banter lines is synced for all players.

Rest can't start until everyone is in 'rest mode' (aka opened the rest window). The Start button now shows a counter for how many players are ready to go.

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

## Entity ID Generation 
The game originally used a single counter to generate new entity IDs (for characters, area effects, facts, map objects, etc.), which often caused desyncs. Now it uses a "consistent" generator that considers the current game state (gameId, location, etc.), so IDs mostly match across all multiplayer players.

The tricky part is ItemId generation - stacking or splitting items creates new IDs locally. Fully syncing that (plus inventory positions) would be a lot of work for almost no benefit. Instead, any network item action (like dropping an item or looting a container) just falls back to matching the item by everything except its UniqueId. In short, the mod just looks for 'the exact same item' and applies the action to it.

## How to deal with desync
If a roll doesn't come through for some reason, you will get a stutter and a popup warning. In that case, the roll will be rolled locally, which might lead to different results.

You can either ignore it (if the outcome is more or less the same for everyone) or do a quicksave/quickload to resync all players.

## Not implemented
### Abilities
Only basic usage of abilities is available. Any extra conditions like metamagic will not work properly as those modifications will not be applied for remote players

### Mythic leveling
would work the same as regular leveling

### Hiring Merc
CharGen screen is synced for leveling only. You will need to create initial merc locally and then host multiplayer game. Further merc leveling will be available within MP session
This might be available later, but it has very low priority

### TL/DR
Right now, everything past the prologue isn't supported/implemented. The Tavern is the last safe location - you can't leave it without causing a full desync

## Long term plans
- Roll syncing will get a rework once the rest of multiplayer is stable enough. The plan is to move to 'predictable rolls' for both host and clients, which should reduce stutters by not blocking the main loop as much. This needs proper isolation of the random generation process though, so it might not work for every single roll