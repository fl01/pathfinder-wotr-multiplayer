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

### How to connect
It works with a `Direct IP` connection by default. This means you either have a public IP or use network emulators (Hamachi, ZeroTier/Radmin VPN/anything you like) to be able to connect. Default networking configuration covers the most common needs, but you are free to configure specifics in the Settings -> Multiplayer tab.

However, you could select the `Share Game Online` option when hosting a game. That uses the selected server to provide a "Game Code" connection. The host will receive a Game Code (visible on the lobby screen) that can be used to connect. Game Code allows you to establish `P2P connection` aka direct connection without the need to be in the same network. The downside is that this might not be available for everyone simply because of network equipment/configuration.
There is only one "official" server available as of now, which is hosted in the EU, but it could be used anywhere as it doesn't affect your "final" in-game latency whatsoever

Anyway, in case of any issues with P2P itself or official server (it could be bugged/overloaded/shutdown/etc) your only option is to rely on `Direct IP` + network emulators option.

More details regarding p2p server/hosting your own server is available [here](/docs/about.md#game-codes)

### Troubleshooting
Refer to [troubleshooting](/docs/troubleshooting.md) if you are having any problems launching the mod
