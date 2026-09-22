# R.E.P.O. Mods

Source for my [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) mods (BepInEx plugins). Thunderstore namespace: `vibez`.

- **[SpecialOrders](SpecialOrders/)** - order any shop item from the shopkeeper for a deposit; it is waiting in the next shop, at a markup. Tested in the game. On [Thunderstore](https://thunderstore.io/c/repo/p/vibez/SpecialOrders/).
- **[UsableValuables](UsableValuables/)** - hold a valuable that does something (flashlight, boombox, candle, ice saw, blender, jackhammer, scream doll, fan, radio, gramophone, television, toy monkey, grandfather clock, flamethrower, fire extinguisher, star wand, wizard staff, camera, levitation potion) and press Interact (E) to use it, and makes the banana bow (lights a fuse and blows up), the handface (laughs and shakes when held; turns to face and is flung, once, at players who keep moving near it when not), the horse (whinnies the first time it is picked up) and the piano (plays a Liszt rhapsody when held by anything but its keys) act up by themselves, and adds a valuable of its own, a dish sponge that blows bubbles and drips when moved (needs [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/), and every player in the room to have the mod). On [Thunderstore](https://thunderstore.io/c/repo/p/vibez/UsableValuables/).
- **[DevTools](DevTools/)** - my testing shortcuts (not published, no Thunderstore package). **F9**: adds $100K and, when you are inside a level, wins it and goes to the shop. **F10**: spawns a random valuable that UsableValuables works on in front of you. **F8**: spawns the dish sponge. Host/singleplayer only; keys and amount are in `BepInEx/config/vibez.DevTools.cfg`.

## Building

Needs the .NET SDK, the game installed, and a Thunderstore Mod Manager profile that already has BepInEx, [MenuLib](https://thunderstore.io/c/repo/p/nickklmao/MenuLib/) and [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) (the build references their DLLs). A portable SDK lives in `.tools/` (gitignored):

```powershell
.\.tools\dotnet-install.ps1 -Channel 8.0 -InstallDir .\.tools\dotnet -NoPath   # once; get the script from https://dot.net/v1/dotnet-install.ps1
.\build.ps1
```

The game is expected in the default Steam library and the profile is named `Default`. If yours differ, copy `local.props.example` to `local.props` (untracked) and edit it, or pass `-p:GameDir=... -p:ProfileName=...` to the build.

## Testing in the game

`.\Link-DevMod.ps1 -Mod SpecialOrders [-ProfileName MyProfile]` creates a junction from the mod's `package/` folder into the Mod Manager profile's `BepInEx/plugins`, so a rebuild is picked up on the next game launch - no reinstall.

## Publishing

Bump the version in `Plugin.cs`, `package/manifest.json`, `thunderstore.toml` and `package/CHANGELOG.md`, build, then from the mod folder run `tcli build` and `tcli publish --file build/<zip>` (token in the `TCLI_AUTH_TOKEN` environment variable). `tcli` can live in `.tools/` next to the SDK: with `DOTNET_ROOT` set to `.tools\dotnet`, run `.\.tools\dotnet\dotnet.exe tool install tcli --version 0.2.4 --tool-path .\.tools\tcli`. In `thunderstore.toml` a copy into the zip root needs `target = "/"` (an empty target makes `tcli build` fail). Check the live version first: `https://thunderstore.io/api/experimental/package/vibez/<Mod>/`.

## Repo layout

Each mod is a folder: `src/` and a `.csproj`, plus `package/` (the Thunderstore package contents: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`; the built DLL is copied in and is gitignored) and `thunderstore.toml` (local publish config, not part of the package). Target framework, game/profile paths, the base DLL references and the copy-to-`package/` step are shared through `Directory.Build.props` / `Directory.Build.targets` in the repo root, so a new mod's `.csproj` only needs its name and any extra references.

## License

[PolyForm Noncommercial 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0) - see [LICENSE](LICENSE). Free for noncommercial use; contact me for anything commercial.
