# Troubleshooting

This document contains known issues, causes, and fixes for the mod.

## Table of contents
- [UnityModManager & Harmony Issues](#umm--harmony-issues)

## UMM & Harmony Issues

### Newer UMM version or using `Assembly` installation method for UMM < 0.32.4a
  
The core issue is that the `Assembly` method (and newer UMM versions) end up loading an outdated `0Harmony.dll` from the game's `Managed` folder instead of the newer version supplied by UMM.

This makes this approach unusable, since the mod relies on newer Harmony features.

A possible workaround is to move/delete `Pathfinder Second Adventure\Wrath_Data\Managed\0Harmony.dll`, which forces the game to fall back to the updated version. However, this may break compatibility with other mods that depend on the original DLL.

---