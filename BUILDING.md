# Building aliceestrap

aliceestrap is a WPF app targeting `net10.0-windows`. It only builds on Windows.

## What you need

| | |
|---|---|
| Windows | 10 or newer |
| .NET SDK | 10.0.100 or newer 10.x |
| .NET Desktop Runtime | 10.0.x, needed to *run* the result |
| git | to clone the repository and the wpfui submodule |

Visual Studio is optional. If you use it, install the **.NET desktop development** workload and open `Aliceestrap.sln`.

### About the SDK version

`global.json` asks for SDK `10.0.100` with `"rollForward": "latestFeature"`, so any 10.x SDK builds it. Install it with:

```
winget install Microsoft.DotNet.SDK.10
```

The runtime is a separate matter. The publish below is framework-dependent, so whoever runs `aliceestrap.exe` needs the **.NET 10 Desktop Runtime** installed. Check with `dotnet --list-runtimes` and look for `Microsoft.WindowsDesktop.App 10.0.x`. If you would rather not depend on that, publish self-contained instead (see below).

## First-time setup

The WPF UI library the app uses lives in its own repository, [aliceenight/wpfui](https://github.com/aliceenight/wpfui), and is pulled in as a git submodule under `wpfui\`:

```
Aliceestrap.csproj  ->  ..\wpfui\src\Wpf.Ui\Wpf.Ui.csproj
```

Clone with submodules:

```
git clone --recursive https://github.com/aliceenight/aliceestrap
```

If you already cloned without `--recursive`, fetch it with:

```
git submodule update --init
```

GitHub's "Download ZIP" does not include submodules, so a zip download will not build.

## Build

From the repository root:

```
build
```

`build.cmd` runs the publish below and puts the finished exe somewhere you can
actually find it:

```
build\aliceestrap.exe
```

`build open` does the same and opens that folder afterwards. It also checks the
`wpfui` folder is present first, rather than letting you discover that through a wall
of MSBuild errors.

### Doing it by hand

If you would rather run the commands yourself:

```
dotnet restore
dotnet build -c Release --no-restore
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Aliceestrap\Aliceestrap.csproj
```

The first restore takes a while. It pulls the net10.0 reference packs and the CsWin32 Windows SDK metadata, which is a large download.

The result is a single file:

```
Aliceestrap\bin\Release\net10.0-windows\win-x64\publish\aliceestrap.exe
```

> [!IMPORTANT]
> Use the one in `publish\`. The build step also leaves an `aliceestrap.exe` in
> `bin\Release\net10.0-windows\`, but that one is only the .NET launcher stub,
> around 250 KB. The actual code lives beside it in `aliceestrap.dll` and twenty
> or so dependency DLLs.
>
> The stub runs fine from that folder, so it looks like a working build, and it
> will happily install itself. But the installer copies a single exe to the
> install folder, and the stub cannot find its DLLs once it is there. It then
> exits instantly with no window and no log file.
>
> The published exe is around 15 MB because everything is bundled inside it.
> If the file you are about to run is small, it is the wrong one.
>
> `build.cmd` avoids this entirely, since the only exe it leaves in `build\` is
> the published one.

To produce a build that runs without the .NET 10 Desktop Runtime installed, swap `--self-contained false` for `--self-contained true`. The exe grows to roughly 150 MB.

For a debug build, use `-c Debug` and read from `bin\Debug\...` instead.

## Running it

The first launch does not find an existing install and runs the installer: a language picker, then the install wizard. It installs to `%LocalAppData%\aliceestrap` with its own registry keys under `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\aliceestrap`.

It also registers itself as the handler for `roblox://` and `roblox-player://`, which means Roblox links from your browser start going through aliceestrap instead of whatever handles them now. That is the point of a bootstrapper, but it is worth knowing before you click Install.

Windows treats paths and registry keys case-insensitively, so if you previously installed a build from before the lowercase rename, this one upgrades it in place rather than installing alongside it.

## Layout

The project folder and the root namespace are called `Aliceestrap`; the assembly and the exe are lowercase `aliceestrap`.

```
Aliceestrap.sln          solution
Aliceestrap\            the application
  App.xaml.cs            entry point, project constants, startup
  Bootstrapper.cs        downloads and installs Roblox
  LaunchHandler.cs       decides what to do with the command line
  Installer.cs           installs and uninstalls aliceestrap itself
  Models\Persistable\    settings, state, anything saved as json
  Resources\Strings.resx source strings, one .resx per language
  UI\Elements\           windows and pages
  UI\ViewModels\         their view models
wpfui\                   the UI library (submodule)
Images\                  banners for this readme
```

Adding a string means editing both `Resources\Strings.resx` and `Resources\Strings.Designer.cs`. Visual Studio regenerates the designer file from the resx; if you are not using it, add the property by hand.

## Things worth knowing

- **Updater.** Before launching Roblox, the app checks the latest release of `aliceenight/aliceestrap` on GitHub and installs a newer `aliceestrap.exe` if there is one. The download is checked against the release's size and SHA-256 digest, and an exe that isn't newer is never installed. "Check for updates" in the Bootstrapper tab turns it off. Release tags must be `v` plus the `<Version>` in `Aliceestrap.csproj`; the release workflow refuses anything else.
- **No machine name in the binary.** The csproj used to bake `$(COMPUTERNAME)\$(USERNAME)` into an assembly attribute, which would otherwise go out in the `User-Agent` header. It bakes the literal `local` instead.
- **No remote config.** `RemoteDataManager` always reads the bundled `Data.json`. The package maps in `Models\APIs\Config\PackageMaps.cs` are therefore only as current as the source. If Roblox renames or adds a package and downloads start failing, that file is where to fix it.

## Troubleshooting

**`MSB1011: Specify which project or solution file to use`**
More than one `.sln` in the root. Delete the stale one, or name the solution explicitly: `dotnet restore Aliceestrap.sln`.

**`The application 'restore' does not exist`**
This is the SDK resolution error wearing a disguise. Read the lines below it; it means `global.json` asked for an SDK you do not have.

**`Wpf.Ui.csproj not found`**
The `wpfui` submodule is missing. Run `git submodule update --init`.

**CsWin32 complains about Windows SDK metadata**
`Aliceestrap.csproj` has a `FixMds` target that hardcodes `microsoft.windows.sdk.win32metadata` version `55.0.45-preview` under `%UserProfile%\.nuget\packages`. It only fires when the normal resolution finds nothing. If the version there has moved on, update the path in the csproj.

**The app runs but immediately closes**
Check the log in `%LocalAppData%\aliceestrap\Logs`. Exceptions get their own dialog, and that dialog has a button to open the log file.
