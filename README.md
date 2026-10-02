<div align="center">

![][banner-light]
![][banner-dark]

</div>

aliceestrap is a custom launcher for Roblox on Windows 10 and above. It
installs and updates Roblox for you, and adds the things the stock launcher
doesn't have.

<div align="center">

![][showcase-light]
![][showcase-dark]

</div>

## Features

- Presets tab: save Roblox settings configs, fast flag presets and channels,
  then apply, edit, rename or delete them from one place
- Launch options: an optional popup right before the game starts where you
  pick the channel, config, fast flag preset and VR mode for that launch
- Its own loading window, plus classic styles and fully custom themes
- Redesigned settings window with light and dark themes
- Discord Rich Presence showing the game you're in
- Detailed server information using [RoValra][rovalra]'s API
- Support for Roblox Studio
- FastFlags editor
  - You cannot apply FastFlags not present in Roblox's allowlist. This does not
    affect Roblox Studio. [Learn more][devforum-fflags]
- Global Basic Settings editor
  - Ability to increase frame rate cap, toggle quality levels and more
- Mods: old cursors and avatar editor background, or your own files
- Tells you when there's a new version, shows what changed, and updates when you say so
- Cache cleaner, channel switcher and more

## Requirements

- Windows 10 or newer, 64-bit
- [.NET 10 Desktop Runtime][dotnet] (Windows offers the download link if it's
  missing)

## Privacy

aliceestrap has no analytics and no remote config. The only places it connects
to are:

- **Roblox** and its download mirrors, to install, update and launch the game
- **RoValra**, only when server details are turned on, to look up where the
  server you joined is located
- **GitHub**, to check this repository for a newer release before Roblox
  starts. You can turn this off with "Check for updates" in the Bootstrapper
  tab

For Discord Rich Presence, aliceestrap hands the status to the Discord app
running on your PC; it never contacts Discord's servers itself. The build
machine's name is never compiled into the binary.

## Building

See [BUILDING.md](BUILDING.md). Short version: clone with `--recursive`, install
the .NET 10 SDK, then run `build.cmd`.

## Licence

aliceestrap is MIT licensed. It contains code from other MIT licensed projects,
whose notices are kept in `LICENSE`, `LICENSE.Bloxstrap` and on the Licenses
list in the app's About window, as their licences require.

Thanks to [Valra](https://github.com/NotValra) for the server location API.

[banner-light]: Images/Aliceestrap-Light.png#gh-light-mode-only
[banner-dark]:  Images/Aliceestrap-Dark.png#gh-dark-mode-only
[showcase-light]: Images/Showcase-Light.png#gh-light-mode-only
[showcase-dark]:  Images/Showcase-Dark.png#gh-dark-mode-only

[rovalra]:   https://www.rovalra.com
[dotnet]:    https://dotnet.microsoft.com/download/dotnet/10.0

[devforum-fflags]: https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
