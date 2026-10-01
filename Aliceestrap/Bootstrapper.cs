#if DEBUG_UPDATER
#warning "Automatic updater debugging is enabled"
#endif

using Aliceestrap.AppData;
using Aliceestrap.RobloxInterfaces;
using Aliceestrap.UI.Elements.Bootstrapper.Base;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
using System.Web;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Shell;

namespace Aliceestrap
{
    public class Bootstrapper
    {
        #region Properties
        private const int ProgressBarMaximum = 10000;

        private const double TaskbarProgressMaximumWpf = 1;
        private const int TaskbarProgressMaximumWinForms = WinFormsDialogBase.TaskbarProgressMaximum;

        private const string AppSettings =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
            "<Settings>\r\n" +
            "	<ContentFolder>content</ContentFolder>\r\n" +
            "	<BaseUrl>http://www.roblox.com</BaseUrl>\r\n" +
            "</Settings>\r\n";

        private readonly FastZipEvents _fastZipEvents = new();
        private readonly CancellationTokenSource _cancelTokenSource = new();

        private IAppData AppData = default!;
        private Dictionary<string, string> PackageDirectoryMap = null!;
        private LaunchMode _launchMode;

        private string _launchCommandLine = App.LaunchSettings.RobloxLaunchArgs;
        private Version? _latestVersion = null;
        private string _latestVersionGuid = null!;
        private string _latestVersionDirectory = null!;
        private PackageManifest _versionPackageManifest = null!;
        private GameJoinData _joinData = null!;
        public static bool _staticDirectory => App.Settings.Prop.StaticDirectory;

        private bool _isInstalling = false;
        private double _progressIncrement;
        private double _taskbarProgressIncrement;
        private double _taskbarProgressMaximum;
        private long _totalDownloadedBytes = 0;
        private long _totalPackagedBytes = 0;
        private bool _packageExtractionSuccess = true;

        private bool _mustUpgrade => App.LaunchSettings.ForceFlag.Active || App.State.Prop.ForceReinstall || String.IsNullOrEmpty(AppData.State.VersionGuid) || !File.Exists(AppData.ExecutablePath);
        private bool _noConnection = false;

        private AsyncMutex? _mutex;

        private volatile int _appPid = 0;
        private IntPtr _appWindowHandle = IntPtr.Zero;

        public IBootstrapperDialog? Dialog = null;

        public bool IsStudioLaunch => _launchMode != LaunchMode.Player;

        public string MutexName { get; set; } = $"{App.ProjectName}-Bootstrapper";
        public bool QuitIfMutexExists { get; set; } = false;
        #endregion

        #region Core
        public Bootstrapper(LaunchMode launchMode)
        {
            _launchMode = launchMode;

            _fastZipEvents.FileFailure += (_, e) =>
            {
                if (!e.Name.EndsWith(".ttf"))
                    throw e.Exception;

                App.Logger.WriteLine("FastZipEvents::OnFileFailure", $"Failed to extract {e.Name}");
                _packageExtractionSuccess = false;
            };
            _fastZipEvents.DirectoryFailure += (_, e) => throw e.Exception;
            _fastZipEvents.ProcessFile += (_, e) => e.ContinueRunning = !_cancelTokenSource.IsCancellationRequested;

            SetupAppData();
        }

        private void SetupAppData()
        {
            AppData = IsStudioLaunch ? new RobloxStudioData() : new RobloxPlayerData();
            Deployment.BinaryType = AppData.BinaryType;
        }

        private async Task SetupPackageDictionaries()
        {
            await App.RemoteData.WaitUntilDataFetched();

            var localData = App.RemoteData.Prop.PackageMaps[IsStudioLaunch ? "studio" : "player"];
            var commonData = App.RemoteData.Prop.PackageMaps.CommonPackageMap;

            PackageDirectoryMap = new(commonData);

            foreach (var package in localData)
                PackageDirectoryMap[package.Key] = package.Value;
        }

        private void SetStatus(string message)
        {
            message = message.Replace("{product}", AppData.ProductName);

            if (Dialog is not null)
                Dialog.Message = message;
        }

        private async Task<bool> CheckForUpdates()
        {
            const string LOG_IDENT = "Bootstrapper::CheckForUpdates";

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Another instance is running, skipping update check");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, "Checking for updates");

            var release = await App.GetLatestRelease();

            if (release is null)
                return false;

            Version latestVersion;

            try
            {
                latestVersion = Utilities.GetVersionFromString(release.TagName);
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Release tag '{release.TagName}' is not a version, skipping");
                return false;
            }

            if (latestVersion <= Utilities.GetVersionFromString(App.Version))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Up to date (latest release is {release.TagName})");
                return false;
            }

            var asset = release.Assets!.FirstOrDefault(x => x.Name.Equals($"{App.ProjectName}.exe", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets!.FirstOrDefault(x => x.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Release {release.TagName} has no exe attached, skipping");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Updating from {App.Version} to {release.TagName}");

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            SetStatus(String.Format(Strings.Bootstrapper_Status_UpdatingApp, release.TagName));

            try
            {
                Directory.CreateDirectory(Paths.TempUpdates);

                string downloadLocation = Path.Combine(Paths.TempUpdates, $"{App.ProjectName}-{latestVersion}.exe");
                string partialLocation = downloadLocation + ".part";

                if (!File.Exists(downloadLocation))
                {
                    using (var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        await using var fileStream = new FileStream(partialLocation, FileMode.Create, FileAccess.Write);
                        await response.Content.CopyToAsync(fileStream);
                    }

                    VerifyUpdate(partialLocation, asset);

                    File.Move(partialLocation, downloadLocation, true);
                }

                string? downloadedVersion = FileVersionInfo.GetVersionInfo(downloadLocation).ProductVersion;

                if (downloadedVersion is null || Utilities.GetVersionFromString(downloadedVersion) <= Utilities.GetVersionFromString(App.Version))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Downloaded exe reports version {downloadedVersion}, which is not newer. Not installing it");
                    File.Delete(downloadLocation);
                    return false;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = downloadLocation
                };

                startInfo.ArgumentList.Add("-upgrade");

                foreach (string arg in App.LaunchSettings.Args)
                    startInfo.ArgumentList.Add(arg);

                if (_launchMode == LaunchMode.Player && !startInfo.ArgumentList.Contains("-player"))
                    startInfo.ArgumentList.Add("-player");
                else if (_launchMode == LaunchMode.Studio && !startInfo.ArgumentList.Contains("-studio"))
                    startInfo.ArgumentList.Add("-studio");

                App.Settings.Save();

                new InterProcessLock("AutoUpdater");

                App.Logger.WriteLine(LOG_IDENT, $"Starting {release.TagName}");

                Process.Start(startInfo);

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Update failed");
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(String.Format(Strings.Bootstrapper_AutoUpdateFailed, release.TagName), MessageBoxImage.Information);
            }

            return false;
        }

        private static void VerifyUpdate(string path, GithubReleaseAsset asset)
        {
            long length = new FileInfo(path).Length;

            if (asset.Size > 0 && length != asset.Size)
                throw new InvalidDataException($"Downloaded {length} bytes, expected {asset.Size}");

            if (asset.Digest is not null && asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));

                if (!hash.Equals(asset.Digest[7..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Downloaded file does not match the release checksum");
            }
        }

        private void UpdateProgressBar()
        {
            if (Dialog is null)
                return;

            SetStatus(string.Format(
                Strings.Bootstrapper_Status_DownloadingPackages,
                FileSize.ByteSize(_totalDownloadedBytes),
                FileSize.ByteSize(_totalPackagedBytes)
                ));

            int progressValue = (int)Math.Floor(_progressIncrement * _totalDownloadedBytes);

            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);

            Dialog.ProgressValue = progressValue;

            double taskbarProgressValue = _taskbarProgressIncrement * _totalDownloadedBytes;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);

            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }

        private void HandleConnectionError(Exception exception)
        {
            const string LOG_IDENT = "Bootstrapper::HandleConnectionError";

            _noConnection = true;

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check failed");
            App.Logger.WriteException(LOG_IDENT, exception);

            string message = Strings.Dialog_Connectivity_BadConnection;

            if (exception is AggregateException)
                exception = exception.InnerException!;

            if (exception is HttpRequestException && exception.InnerException is null)
                message = String.Format(Strings.Dialog_Connectivity_RobloxDown, "[status.roblox.com](https://status.roblox.com)");

            if (_mustUpgrade)
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeNeeded}\n\n{Strings.Dialog_Connectivity_TryAgainLater}";
            else
                message += $"\n\n{Strings.Dialog_Connectivity_RobloxUpgradeSkip}";

            Frontend.ShowConnectivityDialog(
                String.Format(Strings.Dialog_Connectivity_UnableToConnect, "Roblox"),
                message,
                _mustUpgrade ? MessageBoxImage.Error : MessageBoxImage.Warning,
                exception);

            if (_mustUpgrade)
                App.Terminate(ErrorCode.ERROR_CANCELLED);
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Bootstrapper::Run";

            App.Logger.WriteLine(LOG_IDENT, "Running bootstrapper");

            if (Dialog is not null)
                Dialog.CancelEnabled = true;

            SetStatus(Strings.Bootstrapper_Status_Connecting);

            var connectionResult = await Deployment.InitializeConnectivity();

            App.Logger.WriteLine(LOG_IDENT, "Connectivity check finished");

            if (connectionResult is not null)
                HandleConnectionError(connectionResult);

#if !DEBUG && !QA_BUILD
            if (App.Settings.Prop.CheckForUpdates && !App.LaunchSettings.UpgradeFlag.Active)
            {
                if (await CheckForUpdates())
                    return;
            }
#endif

            bool mutexExists = Utilities.DoesMutexExist(MutexName);

            if (mutexExists)
            {
                if (!QuitIfMutexExists)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, waiting...");
                    SetStatus(Strings.Bootstrapper_Status_WaitingOtherInstances);
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{MutexName} mutex exists, exiting!");
                    return;
                }
            }

            await using var mutex = new AsyncMutex(false, MutexName);
            await mutex.AcquireAsync(_cancelTokenSource.Token);

            _mutex = mutex;

            if (mutexExists)
            {
                App.Settings.Load();
                App.State.Load();
                App.RobloxState.Load();
            }

            if (!_noConnection)
            {
                try
                {
                    await GetLatestVersionInfo();
                }
                catch (Exception ex)
                {
                    HandleConnectionError(ex);
                }
            }

            CleanupVersionsFolder();

            bool allModificationsApplied = true;

            if (!_noConnection)
            {
                if (App.RemoteData.LoadedState == GenericTriState.Unknown)
                    SetStatus(Strings.Bootstrapper_Status_WaitingForData);

                await SetupPackageDictionaries();

                if (AppData.State.VersionGuid != _latestVersionGuid || _mustUpgrade)
                {
                    bool backgroundUpdaterMutexOpen = Utilities.DoesMutexExist($"{App.ProjectName}-BackgroundUpdater");
                    if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
                        backgroundUpdaterMutexOpen = false;

                    App.Logger.WriteLine(LOG_IDENT, $"Background updater running: {backgroundUpdaterMutexOpen}");

                    if (backgroundUpdaterMutexOpen && _mustUpgrade)
                    {
                        Utilities.KillBackgroundUpdater();
                        backgroundUpdaterMutexOpen = false;
                    }

                    if (!backgroundUpdaterMutexOpen)
                    {
                        if (IsEligibleForBackgroundUpdate())
                            StartBackgroundUpdater();
                        else
                            await UpgradeRoblox();
                    }
                }

                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                allModificationsApplied = await ApplyModifications();
            }

            if (IsStudioLaunch)
                WindowsRegistry.RegisterStudio();
            else
                WindowsRegistry.RegisterPlayer();

            WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory);

            if (_launchMode != LaunchMode.Player)
                await mutex.ReleaseAsync();

            if (!App.LaunchSettings.NoLaunchFlag.Active && !_cancelTokenSource.IsCancellationRequested)
            {
                if (!App.LaunchSettings.QuietFlag.Active)
                {
                    if (!_packageExtractionSuccess)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ExtractionFailed_Title, Strings.Bootstrapper_ExtractionFailed_Message, ToolTipIcon.Warning);
                    else if (!allModificationsApplied)
                        Frontend.ShowBalloonTip(Strings.Bootstrapper_ModificationsFailed_Title, Strings.Bootstrapper_ModificationsFailed_Message, ToolTipIcon.Warning);
                }

                await StartRoblox();
            }

            await mutex.ReleaseAsync();

            Dialog?.CloseBootstrapper();
        }

        private async Task GetLatestVersionInfo()
        {
            const string LOG_IDENT = "Bootstrapper::GetLatestVersionInfo";

            using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\ROBLOX Corporation\\Environments\\{AppData.RegistryName}\\Channel");

            var match = Regex.Match(
                App.LaunchSettings.RobloxLaunchArgs,
                "channel:([a-zA-Z0-9-_]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            bool ChannelFlag = App.LaunchSettings.ChannelFlag.Active && !string.IsNullOrEmpty(App.LaunchSettings.ChannelFlag.Data);

            void EnrollChannel(string Channel = "production") => Deployment.Channel = Channel;
            void RevertChannel() => Deployment.Channel = Deployment.DefaultChannel;

            string EnrolledChannel = match.Groups.Count == 2 ? match.Groups[1].Value.ToLowerInvariant() : Deployment.DefaultChannel;
            bool behindProductionCheck = App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt;

            if (App.Cookies.Loaded)
            {
                UserChannel? userChannel = await Deployment.GetUserChannel(Deployment.BinaryType);

                if (
                    userChannel?.Token is not null &&
                    userChannel.AssignmentType != 1
                    )
                {
                    if (!string.IsNullOrEmpty(EnrolledChannel))
                        _launchCommandLine = _launchCommandLine.Replace(
                            $"channel:{EnrolledChannel}",
                            $"channel:{userChannel.Channel}",
                            StringComparison.OrdinalIgnoreCase);

                    Deployment.ChannelToken = userChannel.Token;
                    EnrolledChannel = userChannel.Channel;
                }
            }

            if (!ChannelFlag)
            {
                switch (App.Settings.Prop.ChannelChangeMode)
                {
                    case ChannelChangeMode.Automatic:
                        App.Logger.WriteLine(LOG_IDENT, "Enrolling into channel");

                        EnrollChannel(EnrolledChannel);
                        break;
                    case ChannelChangeMode.Prompt:
                        App.Logger.WriteLine(LOG_IDENT, "Prompting channel enrollment");

                        if
                        (
                        !match.Success ||
                        match.Groups.Count != 2 ||
                        match.Groups[1].Value.ToLowerInvariant() == Deployment.Channel
                        )
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Channel is either equal or incorrectly formatted");
                            break;
                        }

                        string DisplayChannel = !String.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : Deployment.DefaultChannel;

                        var Result = Frontend.ShowMessageBox(
                        String.Format(Strings.Bootstrapper_Bootstrapper_Dialog_PromptChannelChange,
                        DisplayChannel, App.Settings.Prop.Channel),
                        MessageBoxImage.Question,
                        MessageBoxButton.YesNo
                        );

                        if (Result == MessageBoxResult.Yes)
                            EnrollChannel(EnrolledChannel);
                        break;
                    case ChannelChangeMode.Ignore:
                        App.Logger.WriteLine(LOG_IDENT, "Ignoring channel enrollment");
                        break;
                }
            }
            else
            {
                string ChannelFlagData = App.LaunchSettings.ChannelFlag.Data!;

                if (!String.IsNullOrEmpty(ChannelFlagData))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Forcing channel {ChannelFlagData}");
                    EnrollChannel(ChannelFlagData);
                }
            }

            if (!App.LaunchSettings.VersionFlag.Active || string.IsNullOrEmpty(App.LaunchSettings.VersionFlag.Data))
            {
                ClientVersion clientVersion;

                try
                {
                    clientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck);
                }
                catch (InvalidChannelException ex)
                {
                    if (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because a WindowsPlayer build does not exist for {App.Settings.Prop.Channel}");
                    }
                    else if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Reverting enrolled channel to {Deployment.DefaultChannel} because {App.Settings.Prop.Channel} is restricted for public use.");

                        if (App.Settings.Prop.ChannelChangeMode != ChannelChangeMode.Automatic)
                        {
                            Frontend.ShowMessageBox(
                                String.Format(
                                    Strings.Boostrapper_Dialog_UnauthorizedChannel,
                                    Deployment.Channel,
                                    Deployment.DefaultChannel
                                ),
                                MessageBoxImage.Information
                            );
                        }
                    }
                    else
                    {
                        throw;
                    }

                    RevertChannel();
                    clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, behindProductionCheck);
                }

                if (clientVersion.IsBehindDefaultChannel && App.Settings.Prop.ChannelChangeMode == ChannelChangeMode.Prompt)
                {
                    MessageBoxResult action = Frontend.ShowMessageBox(
                            String.Format(Strings.Bootstrapper_Dialog_ChannelOutOfDate, Deployment.Channel, Deployment.DefaultChannel),
                            MessageBoxImage.Warning,
                            MessageBoxButton.YesNo
                        );

                    if (action == MessageBoxResult.Yes)
                    {
                        App.Logger.WriteLine("Bootstrapper::CheckLatestVersion", $"Changed Roblox channel from {App.Settings.Prop.Channel} to {Deployment.DefaultChannel}");

                        RevertChannel();
                        clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel);
                    }
                }

                key.SetValueSafe("www." + Deployment.RobloxDomain, Deployment.IsDefaultChannel ? "" : Deployment.Channel);

                _latestVersionGuid = clientVersion.VersionGuid;
                _latestVersion = Utilities.ParseVersionSafe(clientVersion.Version);
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Version set to {App.LaunchSettings.VersionFlag.Data} from arguments");
                _latestVersionGuid = App.LaunchSettings.VersionFlag.Data;
            }

            if (_staticDirectory)
                _latestVersionDirectory = AppData.StaticDirectory;
            else
                _latestVersionDirectory = Path.Combine(Paths.Versions, _latestVersionGuid);

            string pkgManifestUrl = Deployment.GetLocation($"/{_latestVersionGuid}-rbxPkgManifest.txt");
            var pkgManifestData = await App.HttpClient.GetStringAsync(pkgManifestUrl);

            _versionPackageManifest = new(pkgManifestData);

            if (_launchMode == LaunchMode.Unknown)
            {
                App.Logger.WriteLine(LOG_IDENT, "Identifying launch mode from package manifest");

                bool isPlayer = _versionPackageManifest.Exists(x => x.Name == "RobloxApp.zip");
                App.Logger.WriteLine(LOG_IDENT, $"isPlayer: {isPlayer}");

                _launchMode = isPlayer ? LaunchMode.Player : LaunchMode.Studio;
                SetupAppData();
            }
        }

        private bool IsEligibleForBackgroundUpdate()
        {
            const string LOG_IDENT = "Bootstrapper::IsEligibleForBackgroundUpdate";

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Is the background updater process");
                return false;
            }

            if (!App.Settings.Prop.BackgroundUpdatesEnabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Background updates disabled");
                return false;
            }

            if (IsStudioLaunch)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Studio launch");
                return false;
            }

            if (_mustUpgrade)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Must upgrade is true");
                return false;
            }

            if (!string.IsNullOrEmpty(Deployment.ChannelToken))
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Private channel enrollment");
                return false;
            }

            const long minimumFreeSpace = 3_000_000_000;
            long space = Filesystem.GetFreeDiskSpace(Paths.Base);
            if (space < minimumFreeSpace)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: User has {space} free space, at least {minimumFreeSpace} is required");
                return false;
            }

            if (_latestVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Latest version is undefined");
                return false;
            }

            Version? currentVersion = Utilities.GetRobloxVersion(AppData);
            if (currentVersion == default)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Current version is undefined");
                return false;
            }

            if (currentVersion.Minor > _latestVersion.Minor)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not eligible: Downgrade");
                return false;
            }

            int diff = _latestVersion.Minor - currentVersion.Minor;
            if (diff == 0 || diff == 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Eligible");
                return true;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Not eligible: Major version diff is {diff}");
                return false;
            }
        }

        private async Task StartRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::StartRoblox";

            SetStatus(Strings.Bootstrapper_Status_Starting);

            if (_launchMode == LaunchMode.Player)
            {
                GameJoin gameJoin = new();

                _joinData = gameJoin.GetJoinDataByLaunchCommand(_launchCommandLine);
                if (_joinData.JoinType == GameJoinType.Unknown)
                    App.Logger.WriteLine(LOG_IDENT, "Unable to get join data");

                if (App.Settings.Prop.ForceRobloxLanguage)
                {
                    var match = Regex.Match(_launchCommandLine, "gameLocale:([a-z_]+)", RegexOptions.CultureInvariant);

                    if (match.Groups.Count == 2)
                        _launchCommandLine = _launchCommandLine.Replace(
                            "robloxLocale:en_us",
                            $"robloxLocale:{match.Groups[1].Value}",
                            StringComparison.OrdinalIgnoreCase);
                }

                if (!Deployment.IsDefaultRobloxDomain && string.IsNullOrEmpty(_launchCommandLine))
                    _launchCommandLine = "roblox://navigation/home";
            }

            var startInfo = new ProcessStartInfo()
            {
                FileName = AppData.ExecutablePath,
                Arguments = _launchCommandLine,
                WorkingDirectory = AppData.Directory
            };

            if (_launchMode == LaunchMode.Player && ShouldRunAsAdmin())
            {
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
            }
            else if (_launchMode == LaunchMode.StudioAuth)
            {
                Process.Start(startInfo);
                return;
            }

            string? logFileName = null;

            string rbxDir = Path.Combine(Paths.LocalAppData, "Roblox");
            if (!Directory.Exists(rbxDir))
                Directory.CreateDirectory(rbxDir);

            string rbxLogDir = Path.Combine(rbxDir, "logs");
            if (!Directory.Exists(rbxLogDir))
                Directory.CreateDirectory(rbxLogDir);

            var logWatcher = new FileSystemWatcher()
            {
                Path = rbxLogDir,
                Filter = "*.log",
                EnableRaisingEvents = true
            };

            var logCreatedEvent = new AutoResetEvent(false);

            logWatcher.Created += (_, e) =>
            {
                logWatcher.EnableRaisingEvents = false;
                logFileName = e.FullPath;
                logCreatedEvent.Set();
            };

            var autoclosePids = new List<int>();

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (integration?.PreLaunch == true)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Pre-Launching custom integration '{integration.Name}' ({integration.Location} {integration.LaunchArgs} - autoclose is {integration.AutoClose})");
                    int pid = 0;
                    try
                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = integration.Location,
                            Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                            WorkingDirectory = Path.GetDirectoryName(integration.Location),
                            UseShellExecute = true
                        })!;
                        pid = process.Id;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to pre-launch integration '{integration.Name}'!");
                        App.Logger.WriteLine(LOG_IDENT, ex.Message);
                    }

                    if (integration?.AutoClose == true && pid != 0)
                        autoclosePids.Add(pid);

                    if (integration?.Delay != null)
                        Thread.Sleep(integration.Delay);
                }
            }

            if (_cancelTokenSource.IsCancellationRequested)
            {
                App.Logger.WriteLine(LOG_IDENT, "Launch was cancelled, not starting Roblox");
                KillProcesses(autoclosePids);
                return;
            }

            try
            {
                using var process = Process.Start(startInfo)!;

                _appPid = process.Id;

                while (process.MainWindowHandle == IntPtr.Zero && !process.HasExited)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Launch was cancelled while Roblox was starting, closing it");
                        KillProcesses(autoclosePids.Append(process.Id));
                        return;
                    }

                    Thread.Sleep(100);
                }

                _appWindowHandle = process.MainWindowHandle;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return;
            }
            catch (Exception)
            {
                File.Delete(AppData.ExecutablePath);
                throw;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Started Roblox (PID {_appPid}), waiting for log file");

            logCreatedEvent.WaitOne(TimeSpan.FromSeconds(15));

            if (_cancelTokenSource.IsCancellationRequested)
            {
                KillProcesses(autoclosePids);
                return;
            }

            if (String.IsNullOrEmpty(logFileName))
            {
                App.Logger.WriteLine(LOG_IDENT, "Unable to identify log file");
                return;
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, $"Got log file as {logFileName}");
            }

            _mutex?.ReleaseAsync();

            if (IsStudioLaunch)
                return;

            foreach (var integration in App.Settings.Prop.CustomIntegrations)
            {
                if (integration == null)
                    continue;

                if (integration?.PreLaunch == true)
                    continue;

                App.Logger.WriteLine(LOG_IDENT, $"Launching custom integration '{integration!.Name}' ({integration.Location} {integration?.LaunchArgs} - autoclose is {integration!.AutoClose})");

                int pid = 0;

                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = integration.Location,
                        Arguments = integration.LaunchArgs.Replace("\r\n", " "),
                        WorkingDirectory = Path.GetDirectoryName(integration.Location),
                        UseShellExecute = true
                    })!;

                    pid = process.Id;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to launch integration '{integration.Name}'!");
                    App.Logger.WriteLine(LOG_IDENT, ex.Message);
                }

                if (integration.AutoClose && pid != 0)
                    autoclosePids.Add(pid);
            }

            if (App.Settings.Prop.EnableActivityTracking || App.Settings.Prop.EnableWindowManipulation || App.LaunchSettings.TestModeFlag.Active || autoclosePids.Any())
            {
                var watcherData = new WatcherData
                {
                    ProcessId = _appPid,
                    LogFile = logFileName,
                    AutoclosePids = autoclosePids,
                    Handle = _appWindowHandle.ToInt64()
                };

                string watcherDataArg = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(watcherData)));

                string args = $"-watcher \"{watcherDataArg}\"";

                if (App.LaunchSettings.TestModeFlag.Active)
                    args += " -testmode";

                Process.Start(Paths.Process, args);
            }

            Thread.Sleep(1000);
        }

        private static void KillProcesses(IEnumerable<int> pids)
        {
            foreach (int pid in pids)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    process.Kill(true);
                }
                catch (Exception) { }
            }
        }

        private bool ShouldRunAsAdmin()
        {
            foreach (var root in WindowsRegistry.Roots)
            {
                using var key = root.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");

                if (key is null)
                    continue;

                string? flags = (string?)key.GetValue(AppData.ExecutablePath);

                if (flags is not null && flags.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Cancel()
        {
            const string LOG_IDENT = "Bootstrapper::Cancel";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            App.Logger.WriteLine(LOG_IDENT, "Cancelling launch...");

            _cancelTokenSource.Cancel();

            if (Dialog is not null)
                Dialog.CancelEnabled = false;

            if (_isInstalling)
            {
                try
                {
                    WindowsRegistry.RegisterClientLocation(IsStudioLaunch, null);

                    if (Directory.Exists(_latestVersionDirectory))
                        Directory.Delete(_latestVersionDirectory, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Could not fully clean up installation!");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
            else if (_appPid != 0)
            {
                KillProcesses(new[] { _appPid });
            }

            Dialog?.CloseBootstrapper();

            App.SoftTerminate(ErrorCode.ERROR_CANCELLED);
        }
        #endregion

        #region Roblox Install

        private static bool TryDeleteRobloxInDirectory(string dir)
        {
            string clientPath = Path.Combine(dir, App.RobloxPlayerAppName);
            if (!File.Exists(clientPath))
            {
                clientPath = Path.Combine(dir, App.RobloxStudioAppName);
                if (!File.Exists(clientPath))
                    return true;
            }

            try
            {
                File.Delete(clientPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void CleanupVersionsFolder()
        {
            const string LOG_IDENT = "Bootstrapper::CleanupVersionsFolder";

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater tried to cleanup, stopping!");
                return;
            }

            if (!Directory.Exists(Paths.Versions))
            {
                App.Logger.WriteLine(LOG_IDENT, "Versions directory does not exist, skipping cleanup.");
                return;
            }

            foreach (string dir in Directory.GetDirectories(Paths.Versions))
            {
                string dirName = Path.GetFileName(dir);

                if (
                    !_staticDirectory && (dirName != App.RobloxState.Prop.Player.VersionGuid && dirName != App.RobloxState.Prop.Studio.VersionGuid) ||
                    _staticDirectory && (dirName != "WindowsPlayer" && dirName != "WindowsStudio64")
                    )
                {
                    if (!TryDeleteRobloxInDirectory(dir))
                        continue;

                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                    catch (IOException ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {dir}");
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        private void MigrateCompatibilityFlags()
        {
            const string LOG_IDENT = "Bootstrapper::MigrateCompatibilityFlags";

            string oldClientLocation = Path.Combine(Paths.Versions, AppData.State.VersionGuid, AppData.ExecutableName);
            string newClientLocation = Path.Combine(_latestVersionDirectory, AppData.ExecutableName);

            using RegistryKey appFlagsKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
            string? appFlags = appFlagsKey.GetValue(oldClientLocation) as string;

            if (appFlags is not null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Migrating app compatibility flags from {oldClientLocation} to {newClientLocation}...");
                appFlagsKey.SetValueSafe(newClientLocation, appFlags);
                appFlagsKey.DeleteValueSafe(oldClientLocation);
            }
        }
        private static void KillRobloxPlayers()
        {
            const string LOG_IDENT = "Bootstrapper::KillRobloxPlayers";

            List<Process> processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("RobloxPlayerBeta"));
            processes.AddRange(Process.GetProcessesByName("RobloxCrashHandler"));

            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to close process {process.Id}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }


        private async Task UpgradeRoblox()
        {
            const string LOG_IDENT = "Bootstrapper::UpgradeRoblox";
            const int THREAD_LIMIT = 5;

            if (String.IsNullOrEmpty(AppData.State.VersionGuid))
                SetStatus(Strings.Bootstrapper_Status_Installing);
            else
                SetStatus(Strings.Bootstrapper_Status_Upgrading);

            Directory.CreateDirectory(Paths.Base);
            Directory.CreateDirectory(Paths.Downloads);
            Directory.CreateDirectory(Paths.Versions);

            _isInstalling = true;

            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && !IsStudioLaunch)
                KillRobloxPlayers();

            if (!App.LaunchSettings.BackgroundUpdaterFlag.Active && Directory.Exists(_latestVersionDirectory))
            {
                try
                {
                    Directory.Delete(_latestVersionDirectory, true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to delete the latest version directory");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            Directory.CreateDirectory(_latestVersionDirectory);

            var cachedPackageHashes = Directory.GetFiles(Paths.Downloads).Select(x => Path.GetFileName(x));

            int totalSizeRequired = 0;

            totalSizeRequired += _versionPackageManifest.Where(x => !cachedPackageHashes.Contains(x.Signature)).Sum(x => x.PackedSize);
            totalSizeRequired += _versionPackageManifest.Sum(x => x.Size);

            if (Filesystem.GetFreeDiskSpace(Paths.Base) < totalSizeRequired)
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_NotEnoughSpace, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
                return;
            }

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Continuous;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

                Dialog.ProgressMaximum = ProgressBarMaximum;

                _totalPackagedBytes = _versionPackageManifest.Sum(package => package.PackedSize);
                _progressIncrement = (double)ProgressBarMaximum / _totalPackagedBytes;

                if (Dialog is WinFormsDialogBase)
                    _taskbarProgressMaximum = (double)TaskbarProgressMaximumWinForms;
                else
                    _taskbarProgressMaximum = (double)TaskbarProgressMaximumWpf;

                _taskbarProgressIncrement = _taskbarProgressMaximum / (double)_totalPackagedBytes;
            }

            var packageTasks = new List<Task>();

            var ignoredPackages = App.RemoteData.Prop.IgnoredPackages.ToArray();

            var packages = _versionPackageManifest.Where(p => !ignoredPackages.Contains(p.Name)).OrderBy(p => -p.PackedSize);

            SemaphoreSlim downloadSemaphore = new(THREAD_LIMIT);
            foreach (var package in packages)
            {
                await downloadSemaphore.WaitAsync(_cancelTokenSource.Token);


                var task = Task.Run(async () => {
                    await DownloadPackage(package);

                    if (package.Name != "WebView2RuntimeInstaller.zip")
                        ExtractPackage(package);

                    downloadSemaphore.Release();
                }, _cancelTokenSource.Token);

                packageTasks.Add(task);
            }
            await Task.WhenAll(packageTasks);

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            if (Dialog is not null)
            {
                Dialog.ProgressStyle = ProgressBarStyle.Marquee;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
                SetStatus(Strings.Bootstrapper_Status_Configuring);
            }

            if (App.State.Prop.PromptWebView2Install)
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                using var hkcuKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");

                if (hklmKey is not null || hkcuKey is not null)
                {
                    App.State.Prop.PromptWebView2Install = true;
                }
                else
                {
                    var result = Frontend.ShowMessageBox(Strings.Bootstrapper_WebView2NotFound, MessageBoxImage.Warning, MessageBoxButton.YesNo, MessageBoxResult.Yes);

                    if (result != MessageBoxResult.Yes)
                    {
                        App.State.Prop.PromptWebView2Install = false;
                    }
                    else
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Installing WebView2 runtime...");

                        var package = _versionPackageManifest.Find(x => x.Name == "WebView2RuntimeInstaller.zip");

                        if (package is null)
                        {
                            App.Logger.WriteLine(LOG_IDENT, "Aborted runtime install because package does not exist, has WebView2 been added in this Roblox version yet?");
                            return;
                        }

                        string baseDirectory = Path.Combine(_latestVersionDirectory, PackageDirectoryMap[package.Name]);

                        ExtractPackage(package);

                        SetStatus(Strings.Bootstrapper_Status_InstallingWebView2);

                        var startInfo = new ProcessStartInfo()
                        {
                            WorkingDirectory = baseDirectory,
                            FileName = Path.Combine(baseDirectory, "MicrosoftEdgeWebview2Setup.exe"),
                            Arguments = "/silent /install"
                        };

                        await Process.Start(startInfo)!.WaitForExitAsync();

                        App.Logger.WriteLine(LOG_IDENT, "Finished installing runtime");

                        Directory.Delete(baseDirectory, true);
                    }
                }
            }

            MigrateCompatibilityFlags();

            AppData.State.VersionGuid = _latestVersionGuid;

            AppData.State.PackageHashes.Clear();

            foreach (var package in _versionPackageManifest)
                AppData.State.PackageHashes.Add(package.Name, package.Signature);

            CleanupVersionsFolder();

            var allPackageHashes = new List<string>();

            allPackageHashes.AddRange(App.RobloxState.Prop.Player.PackageHashes.Values);
            allPackageHashes.AddRange(App.RobloxState.Prop.Studio.PackageHashes.Values);

            if (!App.Settings.Prop.DebugDisableVersionPackageCleanup)
            {
                foreach (string hash in cachedPackageHashes)
                {
                    if (!allPackageHashes.Contains(hash))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Deleting unused package {hash}");

                        try
                        {
                            File.Delete(Path.Combine(Paths.Downloads, hash));
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Failed to delete {hash}!");
                            App.Logger.WriteException(LOG_IDENT, ex);
                        }
                    }
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Registering approximate program size...");

            int distributionSize = _versionPackageManifest.Sum(x => x.Size + x.PackedSize) / 1024;

            AppData.State.Size = distributionSize;

            int totalSize = App.RobloxState.Prop.Player.Size + App.RobloxState.Prop.Studio.Size;

            using (var uninstallKey = Registry.CurrentUser.CreateSubKey(App.UninstallKey))
            {
                uninstallKey.SetValueSafe("EstimatedSize", totalSize);
            }

            WindowsRegistry.RegisterClientLocation(IsStudioLaunch, _latestVersionDirectory);

            App.Logger.WriteLine(LOG_IDENT, $"Registered as {totalSize} KB");

            App.State.Prop.ForceReinstall = false;

            App.State.Save();
            App.RobloxState.Save();

            _isInstalling = false;
        }

        private static void StartBackgroundUpdater()
        {
            const string LOG_IDENT = "Bootstrapper::StartBackgroundUpdater";

            if (Utilities.DoesMutexExist($"{App.ProjectName}-BackgroundUpdater"))
            {
                App.Logger.WriteLine(LOG_IDENT, "Background updater already running");
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, "Starting background updater");

            Process.Start(Paths.Process, "-backgroundupdater");
        }

        private async Task<bool> ApplyModifications()
        {
            const string LOG_IDENT = "Bootstrapper::ApplyModifications";

            bool success = true;

            SetStatus(Strings.Bootstrapper_Status_ApplyingModifications);

            App.Logger.WriteLine(LOG_IDENT, "Checking file mods...");

            File.Delete(Path.Combine(Paths.Base, "ModManifest.txt"));

            List<string> modFolderFiles = new();

            Directory.CreateDirectory(Paths.Modifications);

            string modFontFamiliesFolder = Path.Combine(Paths.Modifications, "content\\fonts\\families");

            if (File.Exists(Paths.CustomFont))
            {
                App.Logger.WriteLine(LOG_IDENT, "Begin font check");

                Directory.CreateDirectory(modFontFamiliesFolder);

                const string path = "rbxasset://fonts/CustomFont.ttf";

                string contentFolder = Path.Combine(_latestVersionDirectory, "content");
                Directory.CreateDirectory(contentFolder);

                string fontsFolder = Path.Combine(contentFolder, "fonts");
                Directory.CreateDirectory(fontsFolder);

                string familiesFolder = Path.Combine(fontsFolder, "families");
                Directory.CreateDirectory(familiesFolder);

                foreach (string jsonFilePath in Directory.GetFiles(familiesFolder))
                {
                    string jsonFilename = Path.GetFileName(jsonFilePath);
                    string modFilepath = Path.Combine(modFontFamiliesFolder, jsonFilename);

                    if (File.Exists(modFilepath))
                        continue;

                    App.Logger.WriteLine(LOG_IDENT, $"Setting font for {jsonFilename}");

                    var fontFamilyData = JsonSerializer.Deserialize<FontFamily>(File.ReadAllText(jsonFilePath));

                    if (fontFamilyData is null)
                        continue;

                    bool shouldWrite = false;

                    foreach (var fontFace in fontFamilyData.Faces)
                    {
                        if (fontFace.AssetId != path)
                        {
                            fontFace.AssetId = path;
                            shouldWrite = true;
                        }
                    }

                    if (shouldWrite)
                        File.WriteAllText(modFilepath, JsonSerializer.Serialize(fontFamilyData, new JsonSerializerOptions { WriteIndented = true }));
                }

                App.Logger.WriteLine(LOG_IDENT, "End font check");
            } else if (Directory.Exists(modFontFamiliesFolder))
            {
                Directory.Delete(modFontFamiliesFolder, true);
            }

            App.Logger.WriteLine(LOG_IDENT, "Writing AppSettings.xml...");
            if (!File.Exists(Paths.Modifications + "\\AppSettings.xml"))
            {
                Directory.CreateDirectory(_latestVersionDirectory);

                await File.WriteAllTextAsync(
                    Path.Combine(_latestVersionDirectory, "AppSettings.xml"),
                    AppSettings.Replace("roblox.com", Deployment.RobloxDomain)
                );
            }

            foreach (string file in Directory.GetFiles(Paths.Modifications, "*.*", SearchOption.AllDirectories))
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return true;

                string relativeFile = file.Substring(Paths.Modifications.Length + 1);

                if (relativeFile == "README.txt")
                {
                    File.Delete(file);
                    continue;
                }

                if (!App.Settings.Prop.UseFastFlagManager && String.Equals(relativeFile, "ClientSettings\\ClientAppSettings.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (relativeFile.EndsWith(".lock"))
                    continue;

                bool isBlacklisted = relativeFile.Contains("content\\avatar\\heads") || relativeFile.Contains("content\\avatar\\compositing") || relativeFile.Contains("content\\avatar\\meshes");

                if (relativeFile.EndsWith(".mesh") && isBlacklisted)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping file: {relativeFile}");
                    continue;
                }

                modFolderFiles.Add(relativeFile);

                string fileModFolder = Path.Combine(Paths.Modifications, relativeFile);
                string fileVersionFolder = Path.Combine(_latestVersionDirectory, relativeFile);

                if (File.Exists(fileVersionFolder) && MD5Hash.FromFile(fileModFolder) == MD5Hash.FromFile(fileVersionFolder))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} already exists in the version folder, and is a match");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fileVersionFolder)!);

                Filesystem.AssertReadOnly(fileVersionFolder);
                try
                {
                    File.Copy(fileModFolder, fileVersionFolder, true);
                    Filesystem.AssertReadOnly(fileVersionFolder);
                    App.Logger.WriteLine(LOG_IDENT, $"{relativeFile} has been copied to the version folder");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to apply modification ({relativeFile})");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    success = false;
                }
            }

            var fileRestoreMap = new Dictionary<string, List<string>>();

            foreach (string fileLocation in App.RobloxState.Prop.ModManifest)
            {
                if (modFolderFiles.Contains(fileLocation))
                    continue;

                var packageMapEntry = PackageDirectoryMap.SingleOrDefault(x => !String.IsNullOrEmpty(x.Value) && fileLocation.StartsWith(x.Value));
                string packageName = packageMapEntry.Key;

                if (String.IsNullOrEmpty(packageName))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod but does not belong to a package");

                    string versionFileLocation = Path.Combine(_latestVersionDirectory, fileLocation);

                    if (File.Exists(versionFileLocation))
                        File.Delete(versionFileLocation);

                    continue;
                }

                string fileName = fileLocation.Substring(packageMapEntry.Value.Length);

                if (!fileRestoreMap.ContainsKey(packageName))
                    fileRestoreMap[packageName] = new();

                fileRestoreMap[packageName].Add(fileName);

                App.Logger.WriteLine(LOG_IDENT, $"{fileLocation} was removed as a mod, restoring from {packageName}");
            }

            foreach (var entry in fileRestoreMap)
            {
                var package = _versionPackageManifest.Find(x => x.Name == entry.Key);

                if (package is not null)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        return true;

                    await DownloadPackage(package);
                    ExtractPackage(package, entry.Value);
                }
            }

            if (App.LaunchSettings.BackgroundUpdaterFlag.Active || !App.RobloxState.HasFileOnDiskChanged())
            {
                App.RobloxState.Prop.ModManifest = modFolderFiles;
                App.RobloxState.Save();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "RobloxState disk mismatch, not saving ModManifest");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Finished checking file mods");

            if (!success)
                App.Logger.WriteLine(LOG_IDENT, "Failed to apply all modifications");

            return success;
        }

        private async Task DownloadPackage(Package package)
        {
            string LOG_IDENT = $"Bootstrapper::DownloadPackage.{package.Name}";

            if (_cancelTokenSource.IsCancellationRequested)
                return;

            Directory.CreateDirectory(Paths.Downloads);

            string packageUrl = Deployment.GetLocation($"/{_latestVersionGuid}-{package.Name}");
            string robloxPackageLocation = Path.Combine(Paths.LocalAppData, "Roblox", "Downloads", package.Signature);

            if (File.Exists(package.DownloadPath))
            {
                var file = new FileInfo(package.DownloadPath);

                string calculatedMD5 = MD5Hash.FromFile(package.DownloadPath);

                if (calculatedMD5 != package.Signature)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is corrupted ({calculatedMD5} != {package.Signature})! Deleting and re-downloading...");
                    file.Delete();
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Package is already downloaded, skipping...");

                    _totalDownloadedBytes += package.PackedSize;
                    UpdateProgressBar();

                    return;
                }
            }
            else if (File.Exists(robloxPackageLocation))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Found existing copy at '{robloxPackageLocation}'! Copying to Downloads folder...");
                File.Copy(robloxPackageLocation, package.DownloadPath);

                _totalDownloadedBytes += package.PackedSize;
                UpdateProgressBar();

                return;
            }

            if (File.Exists(package.DownloadPath))
                return;

            const int maxTries = 5;

            App.Logger.WriteLine(LOG_IDENT, "Downloading...");

            var buffer = new byte[4096];

            for (int i = 1; i <= maxTries; i++)
            {
                if (_cancelTokenSource.IsCancellationRequested)
                    return;

                int totalBytesRead = 0;

                try
                {
                    var response = await App.HttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, _cancelTokenSource.Token);
                    await using var stream = await response.Content.ReadAsStreamAsync(_cancelTokenSource.Token);
                    await using var fileStream = new FileStream(package.DownloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);

                    while (true)
                    {
                        if (_cancelTokenSource.IsCancellationRequested)
                        {
                            stream.Close();
                            fileStream.Close();
                            return;
                        }

                        int bytesRead = await stream.ReadAsync(buffer, _cancelTokenSource.Token);

                        if (bytesRead == 0)
                            break;

                        totalBytesRead += bytesRead;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancelTokenSource.Token);

                        _totalDownloadedBytes += bytesRead;
                        UpdateProgressBar();
                    }

                    string hash = MD5Hash.FromStream(fileStream);

                    if (hash != package.Signature)
                        throw new ChecksumFailedException($"Failed to verify download of {packageUrl}\n\nExpected hash: {package.Signature}\nGot hash: {hash}");

                    App.Logger.WriteLine(LOG_IDENT, $"Finished downloading! ({totalBytesRead} bytes total)");
                    break;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"An exception occurred after downloading {totalBytesRead} bytes. ({i}/{maxTries})");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    if (ex.GetType() == typeof(ChecksumFailedException))
                    {
                        Frontend.ShowConnectivityDialog(
                            Strings.Dialog_Connectivity_UnableToDownload,
                            Strings.Dialog_Connectivity_UnableToDownloadReason,
                            MessageBoxImage.Error,
                            ex
                        );

                        App.Terminate(ErrorCode.ERROR_CANCELLED);
                    }
                    else if (i >= maxTries)
                        throw;

                    if (File.Exists(package.DownloadPath))
                        File.Delete(package.DownloadPath);

                    _totalDownloadedBytes -= totalBytesRead;
                    UpdateProgressBar();

                    if (ex.GetType() == typeof(IOException) && !packageUrl.StartsWith("http://"))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Retrying download over HTTP...");
                        packageUrl = packageUrl.Replace("https://", "http://");
                    }
                }
            }
        }

        private void ExtractPackage(Package package, List<string>? files = null)
        {
            const string LOG_IDENT = "Bootstrapper::ExtractPackage";

            string? packageDir = PackageDirectoryMap.GetValueOrDefault(package.Name);

            if (packageDir is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"WARNING: {package.Name} was not found in the package map!");
                return;
            }

            string packageFolder = Path.Combine(_latestVersionDirectory, packageDir);
            string? fileFilter = null;

            if (files is not null)
            {
                var regexList = new List<string>();

                foreach (string file in files)
                    regexList.Add("^" + file.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + "$");

                fileFilter = String.Join(';', regexList);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Extracting {package.Name}...");

            var fastZip = new FastZip(_fastZipEvents);

            fastZip.ExtractZip(package.DownloadPath, packageFolder, fileFilter);

            App.Logger.WriteLine(LOG_IDENT, $"Finished extracting {package.Name}");
        }
        #endregion
    }
}
