### Env config
Set `WOTR_PATH` environment var to target Wrath game folder, e.g. `D:\Games\Steam\steamapps\common\Pathfinder Second Adventure`

### Debugging
1. Get Unity debug DLLs - `UnityPlayer.dll` and `WinPixEventRuntime.dll` in a preferred way
    - Download Unity `2020.3.48f1` https://unity.com/releases/editor/whats-new/2020.3.48. Look for required DLLs in `Unity 2020.3.48f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_development_mono`
    - use DLLs from `/tools/debugging`

2. Copy both DLLs into `Wrath_Data` (look inside your game folder)
3. Install VS Unity tools https://learn.microsoft.com/en-us/visualstudio/gamedev/unity/get-started/visual-studio-tools-for-unity
4. Update `Wrath_Data/boot.config`
```
player-connection-mode=Listen
player-connection-guid=3060108046
player-connection-debug=1
player-connection-ip=127.0.0.1
```
5. Launch Game
6. Visual Studio -> Debug -> Attach Unity Debugger
