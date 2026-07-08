# Multiplayer Mod for Pathfinder: Wrath of the Righteous

Showcase: https://www.youtube.com/watch?v=_YZSPrPy9XI

## How to
1. Download latest `wotr-multiplayer-x.x.x.zip` from releases
2. Install via [UnityModManager](https://www.nexusmods.com/site/mods/21). The latest 0.32.5 version has some issues, use 0.32.4a or lower
3. Go to in-game `Settings -> Multiplayer` to configure your name or other multiplayer settings.
4. Use `Multiplayer` main menu to host or join an existing game.
5. Enjoy

## Changes

Look [here](/docs/about.md) for more details

### TL/DR:
- Campaign is mostly playable, but there are a few heavily bugged encounters
- There are no changes to content, balance or how mythic paths/companions work.
- Game Version/DLC/Mods should match across players.

## How to connect
The mod adds a "Multiplayer" menu to the Main Menu. Use it to access multiplayer features.

It works with a `Direct IP` connection by default. This means you either need a public IP or have to use network emulators (Hamachi, ZeroTier, Radmin VPN, or anything you like) to be able to connect. The default networking configuration covers the most common needs, but you are free to configure specific settings in **Settings -> Multiplayer**.

Additionally, you can select the **Share Game Online** option when hosting a game. This uses the selected server to provide a `Game Code` connection. The host will receive a Game Code (visible on the lobby screen) that can be used by others to connect. A Game Code allows you to establish a `P2P connection` (direct connection) without the need to be on the same network. The downside is that this may not work for everyone due to network equipment or configuration.

There is currently one public server, hosted in the EU. It is only used to help players connect, so its location doesn't affect actual in-game latency.

If you encounter issues with P2P itself or the public server (it could be bugged, overloaded, offline, etc.), your only option is to use **Direct IP** together with network emulators.

It is also possible to play in a mixed mode, where some players connect via Game Code and others via Direct IP

More details regarding p2p server/hosting your own server are available [here](/docs/about.md#game-codes)

## Troubleshooting
Refer to [troubleshooting](/docs/troubleshooting.md) if you are having any problems launching the mod

## Misc
If you want to use [BubbleBuffs](https://github.com/factubsio/BubbleBuffs), a multiplayer compatibility mod is available [here](https://github.com/fl01/pathfinder-wotr-multiplayer-bubblebuffs)
