using System.IO;
using System.Windows;
using System.Xml.Linq;
using Aliceestrap.AppData;
using Microsoft.Win32;

namespace Aliceestrap
{
    internal class Installer
    {
        private const bool OpenReleaseNotes = true;

        private static string DesktopShortcut => Path.Combine(Paths.Desktop, $"{App.ProjectName}.lnk");

        private static string StartMenuShortcut => Path.Combine(Paths.WindowsStartMenu, $"{App.ProjectName}.lnk");

        public string InstallLocation = Path.Combine(Paths.LocalAppData, App.ProjectName);

        public bool ExistingDataPresent => File.Exists(Path.Combine(InstallLocation, "Settings.json"));

        public bool CreateDesktopShortcuts = true;

        public bool CreateStartMenuShortcuts = true;

        public bool IsImplicitInstall = false;

        public string InstallLocationError { get; set; } = "";

        public void DoInstall()
        {
            const string LOG_IDENT = "Installer::DoInstall";

            App.Logger.WriteLine(LOG_IDENT, "Beginning installation");

            Directory.CreateDirectory(InstallLocation);

            Paths.Initialize(InstallLocation);

            if (!IsImplicitInstall)
            {
                Filesystem.AssertReadOnly(Paths.Application);

                try
                {
                    File.Copy(Paths.Process, Paths.Application, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not overwrite executable");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    Frontend.ShowMessageBox(Strings.Installer_Install_CannotOverwrite, MessageBoxImage.Error);
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                }
            }

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("DisplayIcon", $"{Paths.Application},0");
                uninstallKey.SetValueSafe("DisplayName", App.ProjectName);

                uninstallKey.SetValueSafe("DisplayVersion", App.Version);

                if (uninstallKey.GetValue("InstallDate") is null)
                    uninstallKey.SetValueSafe("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

                uninstallKey.SetValueSafe("InstallLocation", Paths.Base);
                uninstallKey.SetValueSafe("NoRepair", 1);
                uninstallKey.SetValueSafe("Publisher", App.ProjectOwner);
                uninstallKey.SetValueSafe("ModifyPath", $"\"{Paths.Application}\" -settings");
                uninstallKey.SetValueSafe("QuietUninstallString", $"\"{Paths.Application}\" -uninstall -quiet");
                uninstallKey.SetValueSafe("UninstallString", $"\"{Paths.Application}\" -uninstall");
            }

            WindowsRegistry.RegisterApis();

            WindowsRegistry.RegisterPlayer();

            if (CreateDesktopShortcuts)
                Shortcut.Create(Paths.Application, "", DesktopShortcut);

            if (CreateStartMenuShortcuts)
                Shortcut.Create(Paths.Application, "", StartMenuShortcut);

            App.Settings.Load(false);
            App.State.Load(false);
            App.FastFlags.Load(false);

            if (App.IsStudioVisible)
                WindowsRegistry.RegisterStudio();

            App.Settings.Save();

            App.Logger.WriteLine(LOG_IDENT, "Installation finished");

        }

        private bool ValidateLocation()
        {
            if (InstallLocation.Length <= 3)
                return false;

            if (InstallLocation.StartsWith("\\\\"))
                return false;

            if (InstallLocation.StartsWith(Path.GetTempPath(), StringComparison.InvariantCultureIgnoreCase)
                || InstallLocation.Contains("\\Temp\\", StringComparison.InvariantCultureIgnoreCase))
                return false;

            if (InstallLocation.Contains("OneDrive", StringComparison.InvariantCultureIgnoreCase))
                return false;

            if (String.Compare(Directory.GetParent(InstallLocation)?.FullName, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase) == 0)
                return false;

            if (InstallLocation.Contains("Program Files"))
                return false;

            if (InstallLocation.Contains("WindowsApps"))
                return false;

            return true;
        }

        public bool CheckInstallLocation()
        {
            if (string.IsNullOrEmpty(InstallLocation))
            {
                InstallLocationError = Strings.Menu_InstallLocation_NotSet;
            }
            else if (!ValidateLocation())
            {
                InstallLocationError = Strings.Menu_InstallLocation_CantInstall;
            }
            else
            {
                if (!IsImplicitInstall 
                    && !InstallLocation.EndsWith(App.ProjectName, StringComparison.InvariantCultureIgnoreCase)
                    && Directory.Exists(InstallLocation)
                    && Directory.EnumerateFileSystemEntries(InstallLocation).Any())
                {
                    string suggestedChange = Path.Combine(InstallLocation, App.ProjectName);

                    MessageBoxResult result = Frontend.ShowMessageBox(
                        String.Format(Strings.Menu_InstallLocation_NotEmpty, suggestedChange),
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNoCancel,
                        MessageBoxResult.Yes
                    );

                    if (result == MessageBoxResult.Yes)
                        InstallLocation = suggestedChange;
                    else if (result == MessageBoxResult.Cancel || result == MessageBoxResult.None)
                        return false;
                }

                try
                {
                    string testFile = Path.Combine(InstallLocation, $"{App.ProjectName}WriteTest.txt");

                    Directory.CreateDirectory(InstallLocation);
                    File.WriteAllText(testFile, "");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    InstallLocationError = Strings.Menu_InstallLocation_NoWritePerms;
                }
                catch (Exception ex)
                {
                    InstallLocationError = ex.Message;
                }
            }

            return String.IsNullOrEmpty(InstallLocationError);
        }

        public static void DoUninstall(bool keepData)
        {
            const string LOG_IDENT = "Installer::DoUninstall";

            var processes = new List<Process>();
            
            if (!String.IsNullOrEmpty(App.RobloxState.Prop.Player.VersionGuid))
                processes.AddRange(Process.GetProcessesByName(App.RobloxPlayerAppName));

            if (App.IsStudioVisible)
                processes.AddRange(Process.GetProcessesByName(App.RobloxStudioAppName));

            if (processes.Any())
            {
                var result = Frontend.ShowMessageBox(
                    Strings.Bootstrapper_Uninstall_RobloxRunning,
                    MessageBoxImage.Information,
                    MessageBoxButton.OKCancel,
                    MessageBoxResult.OK
                );

                if (result != MessageBoxResult.OK)
                {
                    App.Terminate(ErrorCode.ERROR_CANCELLED);
                    return;
                }

                try
                {
                    foreach (var process in processes)
                    {
                        process.Kill();
                        process.Close();
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process! {ex}");
                }
            }

            string robloxFolder = Path.Combine(Paths.LocalAppData, "Roblox");
            bool playerStillInstalled = true;
            bool studioStillInstalled = true;

            using var playerKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\roblox-player");
            var playerFolder = playerKey?.GetValue("InstallLocation");

            if (playerKey is null || playerFolder is not string)
            {
                playerStillInstalled = false;

                WindowsRegistry.Unregister("roblox");
                WindowsRegistry.Unregister("roblox-player");
            }
            else
            {
                string playerPath = Path.Combine((string)playerFolder, App.RobloxPlayerAppName);

                WindowsRegistry.RegisterPlayer(playerPath, "%1");
            }

            using var studioKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\roblox-studio");
            var studioFolder = studioKey?.GetValue("InstallLocation");

            if (studioKey is null || studioFolder is not string)
            {
                studioStillInstalled = false;

                WindowsRegistry.Unregister("roblox-studio");
                WindowsRegistry.Unregister("roblox-studio-auth");

                WindowsRegistry.Unregister("Roblox.Place");
                WindowsRegistry.Unregister(".rbxl");
                WindowsRegistry.Unregister(".rbxlx");
            }
            else
            {
                string studioPath = Path.Combine((string)studioFolder, App.RobloxStudioAppName);
                string studioLauncherPath = Path.Combine((string)studioFolder, "RobloxStudioLauncherBeta.exe");

                WindowsRegistry.RegisterStudioProtocol(studioPath, "%1");
                WindowsRegistry.RegisterStudioFileClass(studioPath, "-ide \"%1\"");
            }

            Registry.CurrentUser.DeleteSubKey(App.ApisKey);

            var cleanupSequence = new List<Action>
            {
                () =>
                {
                    foreach (var file in Directory.GetFiles(Paths.Desktop).Where(x => x.EndsWith("lnk")))
                    {
                        var shortcut = ShellLink.Shortcut.ReadFromFile(file);

                        if (shortcut.ExtraData.EnvironmentVariableDataBlock?.TargetUnicode == Paths.Application)
                            File.Delete(file);
                    }
                },

                () => File.Delete(StartMenuShortcut),

                () => Directory.Delete(Paths.Versions, true),

                () => Directory.Delete(Paths.Downloads, true),

                () => File.Delete(App.State.FileLocation),

                () =>
                {
                if (Paths.Roblox == Path.Combine(Paths.Base, "Roblox"))
                    Directory.Delete(Paths.Roblox, true);
                }
            };


            if (!keepData)
            {
                cleanupSequence.AddRange(new List<Action>
                {
                    () => Directory.Delete(Paths.Modifications, true),
                    () => Directory.Delete(Paths.Logs, true),

                    () => File.Delete(App.Settings.FileLocation)
                });
            }

            bool deleteFolder = Directory.GetFiles(Paths.Base).Length <= 3;

            if (deleteFolder)
                cleanupSequence.Add(() => Directory.Delete(Paths.Base, true));

            if (!playerStillInstalled && !studioStillInstalled && Directory.Exists(robloxFolder))
                cleanupSequence.Add(() => Directory.Delete(robloxFolder, true));

            cleanupSequence.Add(() => Registry.CurrentUser.DeleteSubKey(App.UninstallKey));

            foreach (var process in cleanupSequence)
            {
                try
                {
                    process();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Encountered exception when running cleanup sequence (#{cleanupSequence.IndexOf(process)})");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            if (Directory.Exists(Paths.Base))
            {
                string deleteCommand;

                if (deleteFolder)
                    deleteCommand = $"del /Q \"{Paths.Base}\\*\" && rmdir \"{Paths.Base}\"";
                else
                    deleteCommand = $"del /Q \"{Paths.Application}\"";

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout 5 && {deleteCommand}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
        }

        public static void HandleUpgrade()
        {
            const string LOG_IDENT = "Installer::HandleUpgrade";

            if (!File.Exists(Paths.Application) || Paths.Process == Paths.Application)
                return;

            bool isAutoUpgrade = App.LaunchSettings.UpgradeFlag.Active
                || Paths.Process.StartsWith(Path.Combine(Paths.Base, "Updates"))
                || Paths.Process.StartsWith(Path.Combine(Paths.LocalAppData, "Temp"))
                || Paths.Process.StartsWith(Paths.TempUpdates);

            var existingVer = FileVersionInfo.GetVersionInfo(Paths.Application).ProductVersion;
            var currentVer = FileVersionInfo.GetVersionInfo(Paths.Process).ProductVersion;

            if (MD5Hash.FromFile(Paths.Process) == MD5Hash.FromFile(Paths.Application))
                return;

            if (currentVer is not null && existingVer is not null && Utilities.CompareVersions(currentVer, existingVer) == VersionComparison.LessThan)
            {
                var result = Frontend.ShowMessageBox(
                    Strings.InstallChecker_VersionLessThanInstalled,
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                    return;
            }

            if (!isAutoUpgrade)
            {
                var result = Frontend.ShowMessageBox(
                    Strings.InstallChecker_VersionDifferentThanInstalled,
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo
                );

                if (result != MessageBoxResult.Yes)
                    return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Doing upgrade");

            Filesystem.AssertReadOnly(Paths.Application);

            using (var ipl = new InterProcessLock("AutoUpdater", TimeSpan.FromSeconds(5)))
            {
                if (!ipl.IsAcquired)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to update! (Could not obtain singleton mutex)");
                    return;
                }
            }

            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    File.Copy(Paths.Process, Paths.Application, true);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == 1)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Waiting for write permissions to update version");
                    }
                    else if (i == 10)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Failed to update! (Could not get write permissions after 10 tries/5 seconds)");
                        App.Logger.WriteException(LOG_IDENT, ex);
                        return;
                    }

                    Thread.Sleep(500);
                }
            }

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("DisplayVersion", App.Version);

                uninstallKey.SetValueSafe("Publisher", App.ProjectOwner);
                uninstallKey.DeleteValue("HelpLink", false);
            }

            if (currentVer is null)
                return;

            if (isAutoUpgrade)
            {
            }
            else
            {
                Frontend.ShowMessageBox(
                    string.Format(Strings.InstallChecker_Updated, currentVer),
                    MessageBoxImage.Information,
                    MessageBoxButton.OK
                );
            }
        }
    }
}
