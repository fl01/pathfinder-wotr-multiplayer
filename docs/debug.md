# How to debug
1. Download Unity `2020.3.48f1` https://unity.com/releases/editor/whats-new/2020.3.48
 OR use DLLs in `/tools/debug`
2. Copy `UnityPlayer.dll` and `WinPixEventRuntime.dll` into Wrath_Data
(`Unity 2020.3.48f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_development_mono`)
3. Install VS Unity tools https://learn.microsoft.com/en-us/visualstudio/gamedev/unity/get-started/visual-studio-tools-for-unity
4. Update Wrath_Data/boot.config
```
player-connection-mode=Listen
player-connection-guid=3060108046
player-connection-debug=1
player-connection-ip=127.0.0.1
```
5. Launch Game
6. Attach Unity Debugger
