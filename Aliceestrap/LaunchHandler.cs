﻿using System.Windows;

using Windows.Win32;
using Windows.Win32.Foundation;

using Aliceestrap.UI.Elements.Dialogs;
using Aliceestrap.Integrations;
using Aliceestrap.RobloxInterfaces;

namespace Aliceestrap
{
    public static class LaunchHandler
    {
        public static void ProcessNextAction(NextAction action, bool isUnfinishedInstall = false)
        {
            const string LOG_IDENT = "LaunchHandler::ProcessNextAction";

            switch (action)
            {
                case NextAction.LaunchSettings:
                    App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                    LaunchSettings();
                    break;

                case NextAction.LaunchRoblox:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox");
                    LaunchRoblox(LaunchMode.Player);
                    break;

                case NextAction.LaunchRobloxStudio:
                    App.Logger.WriteLine(LOG_IDENT, "Opening Roblox Studio");
                    LaunchRoblox(LaunchMode.Studio);
                    break;

                default:
                    App.Logger.WriteLine(LOG_IDENT, "Closing");
                    App.Terminate(isUnfinishedInstall ? ErrorCode.ERROR_INSTALL_USEREXIT : ErrorCode.ERROR_SUCCESS);
                    break;
            }
        }

        public static void ProcessLaunchArgs()
        {
            const string LOG_IDENT = "LaunchHandler::ProcessLaunchArgs";

            if (App.LaunchSettings.UninstallFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening uninstaller");
                LaunchUninstaller();
            }
            else if (App.LaunchSettings.MenuFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening settings");
                LaunchSettings();
            }
            else if (App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening watcher");
                LaunchWatcher();
            }
            else if (App.LaunchSettings.BackgroundUpdaterFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening background updater");
                LaunchBackgroundUpdater();
            }
            else if (App.LaunchSettings.RobloxLaunchMode != LaunchMode.None)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Opening bootstrapper ({App.LaunchSettings.RobloxLaunchMode})");
                LaunchRoblox(App.LaunchSettings.RobloxLaunchMode);
            }
            else if (App.LaunchSettings.BloxshadeFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening Bloxshade");
                LaunchBloxshadeConfig();
            }
            else if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Opening menu");
                LaunchMenu();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Closing - quiet flag active");
                App.Terminate();
            }
        }

        public static void LaunchInstaller()
        {
            using var interlock = new InterProcessLock("Installer");

            if (!interlock.IsAcquired)
            {
                Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Installer, MessageBoxImage.Stop);
                App.Terminate();
                return;
            }

            if (App.LaunchSettings.UninstallFlag.Active)
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_FirstRunUninstall, MessageBoxImage.Error);
                App.Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
                return;
            }

            if (App.LaunchSettings.QuietFlag.Active)
            {
                var installer = new Installer();

                if (!installer.CheckInstallLocation())
                    App.Terminate(ErrorCode.ERROR_INSTALL_FAILURE);

                installer.DoInstall();

                interlock.Dispose();

                ProcessLaunchArgs();
            }
            else
            {
#if QA_BUILD
                Frontend.ShowMessageBox("You are about to install a QA build of aliceestrap. The red window border indicates that this is a QA build.\n\nQA builds are handled completely separately of your standard installation, like a virtual environment.", MessageBoxImage.Information);
#endif

                new LanguageSelectorDialog().ShowDialog();

                var installer = new UI.Elements.Installer.MainWindow();
                installer.ShowDialog();

                interlock.Dispose();

                ProcessNextAction(installer.CloseAction, !installer.Finished);
            }

        }

        public static void LaunchUninstaller()
        {
            using var interlock = new InterProcessLock("Uninstaller");

            if (!interlock.IsAcquired)
            {
                Frontend.ShowMessageBox(Strings.Dialog_AlreadyRunning_Uninstaller, MessageBoxImage.Stop);
                App.Terminate();
                return;
            }

            bool confirmed = false;
            bool keepData = true;

            if (App.LaunchSettings.QuietFlag.Active)
            {
                confirmed = true;
            }
            else
            {
                var dialog = new UninstallerDialog();
                dialog.ShowDialog();

                confirmed = dialog.Confirmed;
                keepData = dialog.KeepData;
            }

            if (!confirmed)
            {
                App.Terminate();
                return;
            }

            Installer.DoUninstall(keepData);

            Frontend.ShowMessageBox(Strings.Bootstrapper_SuccessfullyUninstalled, MessageBoxImage.Information);

            App.Terminate();
        }

        public static void LaunchSettings()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchSettings";

            using var interlock = new InterProcessLock("Settings");

            if (interlock.IsAcquired)
            {
                bool showAlreadyRunningWarning = Process.GetProcessesByName(App.ProjectName).Length > 1;

                var window = new UI.Elements.Settings.MainWindow(showAlreadyRunningWarning);

                window.ShowDialog();
            }
            else
            {
                App.Logger.WriteLine(LOG_IDENT, "Found an already existing menu window");

                var process = Utilities.GetProcessesSafe().Where(x => x.MainWindowTitle == Strings.Menu_Title).FirstOrDefault();

                if (process is not null)
                    PInvoke.SetForegroundWindow((HWND)process.MainWindowHandle);

                App.Terminate();
            }
        }

        public static void LaunchMenu()
        {
            var dialog = new LaunchMenuDialog();
            dialog.ShowDialog();

            ProcessNextAction(dialog.CloseAction);
        }

        public static void LaunchRoblox(LaunchMode launchMode)
        {
            const string LOG_IDENT = "LaunchHandler::LaunchRoblox";

            if (launchMode == LaunchMode.None)
                throw new InvalidOperationException("No Roblox launch mode set");

            if (!File.Exists(Path.Combine(Paths.System, "mfplat.dll")))
            {
                Frontend.ShowMessageBox(Strings.Bootstrapper_WMFNotFound, MessageBoxImage.Error);

                if (!App.LaunchSettings.QuietFlag.Active)
                    Utilities.ShellExecute("https://support.microsoft.com/en-us/topic/media-feature-pack-list-for-windows-n-editions-c1c6fffa-d052-8338-7a79-a4bb980a700a");

                App.Terminate(ErrorCode.ERROR_FILE_NOT_FOUND);
            }

            if (App.Settings.Prop.ConfirmLaunches && Utilities.IsRobloxRunning() && launchMode != LaunchMode.Studio)
            {
                var result = Frontend.ShowMessageBox(Strings.Bootstrapper_ConfirmLaunch, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    App.Terminate();
                    return;
                }
            }

            if (launchMode == LaunchMode.Player && !App.LaunchSettings.QuietFlag.Active)
            {
                if (!PrepareLaunchOptions())
                {
                    App.Logger.WriteLine(LOG_IDENT, "Launch options were cancelled, aborting launch");
                    App.Terminate();
                    return;
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(launchMode);
            IBootstrapperDialog? dialog = null;

            if (!App.LaunchSettings.QuietFlag.Active)
            {
                App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper dialog");
                dialog = App.Settings.Prop.BootstrapperStyle.GetNew();
                App.Bootstrapper.Dialog = dialog;
                dialog.Bootstrapper = App.Bootstrapper;
            }

            Task.Run(App.Bootstrapper.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            dialog?.ShowBootstrapper();

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }

        private static bool PrepareLaunchOptions()
        {
            const string LOG_IDENT = "LaunchHandler::PrepareLaunchOptions";

            bool askChannel = App.Settings.Prop.PromptChannelOnLaunch;
            bool askConfig = App.Settings.Prop.PromptConfigOnLaunch;
            bool askVR = App.Settings.Prop.PromptVRModeOnLaunch;
            bool askFlags = App.Settings.Prop.PromptFlagPresetOnLaunch;
            string? preset = App.Settings.Prop.GlobalSettingsPreset;

            if (!askChannel && !askConfig && !askVR && !askFlags && String.IsNullOrEmpty(preset))
                return true;

            bool needsGlobalSettings = askConfig || askVR || !String.IsNullOrEmpty(preset);

            if (needsGlobalSettings && !App.GlobalSettings.Loaded)
            {
                App.Logger.WriteLine(LOG_IDENT, "Global settings aren't loaded, trying again");
                App.GlobalSettings.Load();
            }

            if (needsGlobalSettings && !App.GlobalSettings.Loaded)
            {
                App.Logger.WriteLine(LOG_IDENT, "Global settings still aren't loaded, skipping everything that needs them");

                askConfig = false;
                askVR = false;
                preset = null;

                if (!askChannel && !askFlags)
                    return true;
            }

            bool? vr = null;
            string? channel = null;
            string? flagPreset = null;

            if (askChannel || askConfig || askVR || askFlags)
            {
                var dialog = new LaunchOptionsDialog(
                    askChannel,
                    askConfig,
                    askVR,
                    ChannelPresetManager.GetDefault(),
                    preset,
                    askVR && App.GlobalSettings.GetVREnabled(),
                    askFlags
                );

                dialog.ShowDialog();

                if (!dialog.Confirmed)
                    return false;

                if (askConfig)
                    preset = dialog.SelectedPreset;

                vr = dialog.LaunchInVR;
                channel = dialog.SelectedChannel;
                flagPreset = dialog.SelectedFlagPreset;
            }

            if (!String.IsNullOrEmpty(flagPreset))
            {
                App.Logger.WriteLine(LOG_IDENT, $"User picked fast flag preset {flagPreset}");

                if (FastFlagPresetManager.Apply(flagPreset))
                    App.FastFlags.Save();
            }

            if (!String.IsNullOrEmpty(channel) && !channel.Equals(Deployment.Channel, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Switching channel from {Deployment.Channel} to {channel}");

                Deployment.Channel = channel;
            }

            if (!String.IsNullOrEmpty(preset))
                GlobalSettingsPresetManager.Apply(preset);

            if (vr is not null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"User picked {(vr.Value ? "VR" : "non-VR")}");

                App.GlobalSettings.SetVREnabled(vr.Value);
                App.GlobalSettings.Save();
            }

            return true;
        }

        public static void LaunchWatcher()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchWatcher";

            var watcher = new Watcher();

            Task.Run(watcher.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher task has finished");

                watcher.Dispose();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the watcher");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                if (App.Settings.Prop.CleanerOptions != CleanerOptions.Never)
                    Cleaner.DoCleaning();

                App.Terminate();
            });
        }

        public static void LaunchBloxshadeConfig()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBloxshade";

            App.Logger.WriteLine(LOG_IDENT, "Showing unsupported warning");

            new BloxshadeDialog().ShowDialog();
            App.SoftTerminate();
        }

        public static void LaunchBackgroundUpdater()
        {
            const string LOG_IDENT = "LaunchHandler::LaunchBackgroundUpdater";

            App.LaunchSettings.QuietFlag.Active = true;
            App.LaunchSettings.NoLaunchFlag.Active = true;

            App.Logger.WriteLine(LOG_IDENT, "Initializing bootstrapper");
            App.Bootstrapper = new Bootstrapper(LaunchMode.Player)
            {
                MutexName = $"{App.ProjectName}-BackgroundUpdater",
                QuitIfMutexExists = true
            };

            CancellationTokenSource cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Started event waiter");
                using (EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.AutoReset, $"{App.ProjectName}-BackgroundUpdaterKillEvent"))
                    handle.WaitOne();

                App.Logger.WriteLine(LOG_IDENT, "Received close event, killing it all!");
                App.Bootstrapper.Cancel();
            }, cts.Token);

            Task.Run(App.Bootstrapper.Run).ContinueWith(t =>
            {
                App.Logger.WriteLine(LOG_IDENT, "Bootstrapper task has finished");
                cts.Cancel();

                if (t.IsFaulted)
                {
                    App.Logger.WriteLine(LOG_IDENT, "An exception occurred when running the bootstrapper");

                    if (t.Exception is not null)
                        App.FinalizeExceptionHandling(t.Exception);
                }

                App.Terminate();
            });

            App.Logger.WriteLine(LOG_IDENT, "Exiting");
        }
    }
}
