using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

using Aliceestrap.UI.ViewModels.Settings;
using Aliceestrap.UI.Elements.Settings.Pages;
using Aliceestrap.UI.Elements.Controls;

namespace Aliceestrap.UI.Elements.Settings
{
    public partial class MainWindow : INavigationWindow
    {
        private Models.Persistable.WindowState _state => App.State.Prop.SettingsWindow;

        private List<SearchBarItem>? _searchIndex;

        public MainWindow(bool showAlreadyRunningWarning)
        {
            var viewModel = new MainWindowViewModel();

            viewModel.RequestSaveNoticeEvent += (_, _) => SettingsSavedSnackbar.Show();
            viewModel.RequestCloseWindowEvent += (_, _) => Close();

            DataContext = viewModel;

            InitializeComponent();

            App.Logger.WriteLine("MainWindow", "Initializing settings window");

            if (showAlreadyRunningWarning)
                ShowAlreadyRunningSnackbar();

            gbs.IsEnabled = viewModel.GBSEnabled;

            VersionBadge.Text = $"v{App.DisplayVersion}";

            BuildTabs();

            LoadState();

            FitTabsOnOneLine();

            string? lastPageName = App.State.Prop.LastPage;
            Type? lastPage = lastPageName is null ? null : Type.GetType(lastPageName);

            if (lastPage != null)
                SafeNavigate(lastPage);

            RootNavigation.Navigated += OnNavigation!;

            PresetEditSession.Changed += OnEditSessionChanged;

            void OnNavigation(object? sender, RoutedNavigationEventArgs e)
            {
                INavigationItem? currentPage = RootNavigation.Current;

                App.State.Prop.LastPage = currentPage?.PageType.FullName!;

                SyncHeader(currentPage);
            }

            this.Loaded += (s, e) =>
            {
                SyncHeader(RootNavigation.Current);

                FitTabsOnOneLine();

                Dispatcher.InvokeAsync(() =>
                {
                    BuildSearchIndexAutomatically();
                }, System.Windows.Threading.DispatcherPriority.Background);
            };
        }

        #region Preset editing

        private void OnEditSessionChanged(object? sender, EventArgs e)
        {
            bool editing = PresetEditSession.Active;

            EditBanner.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;

            SaveButton.IsEnabled = !editing;
            SaveAndLaunchButton.IsEnabled = !editing;

            if (editing)
            {
                EditBannerTitle.Text = String.Format(Strings.Menu_Presets_Editing, PresetEditSession.Name);
                EditBannerKind.Text = PresetEditSession.Kind == PresetKind.RobloxConfig
                    ? Strings.Menu_Presets_Editing_Config
                    : Strings.Menu_Presets_Editing_Flags;

                Navigate(PresetEditSession.Kind == PresetKind.RobloxConfig ? typeof(GlobalSettingsPage) : typeof(FastFlagsPage));
            }
            else
            {
                Navigate(typeof(PresetsPage));
            }
        }

        private void EditSave_Click(object sender, RoutedEventArgs e)
        {
            if (!PresetEditSession.Commit())
                Frontend.ShowMessageBox(Strings.Menu_Presets_Flags_Failed, MessageBoxImage.Error);
        }

        private void EditCancel_Click(object sender, RoutedEventArgs e) => PresetEditSession.Cancel();

        #endregion

        #region Tabs

        private readonly Dictionary<Type, RadioButton> _tabs = new();

        private void BuildTabs()
        {
            foreach (var item in RootNavigation.Items.OfType<NavigationItem>())
            {
                if (item.Visibility != Visibility.Visible || item.PageType is null)
                    continue;

                var label = new StackPanel { Orientation = Orientation.Horizontal };

                label.Children.Add(new SymbolIcon
                {
                    Symbol = item.Icon,
                    FontSize = 15,
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                label.Children.Add(new TextBlock
                {
                    Text = item.Content as string,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var tab = new RadioButton
                {
                    Style = (Style)FindResource("ShellNavPill"),
                    GroupName = "SettingsTabs",
                    Content = label,
                    IsEnabled = item.IsEnabled,
                    Margin = new Thickness(2, 6, 2, 0)
                };

                Type pageType = item.PageType;

                tab.Checked += (_, _) =>
                {
                    if (RootNavigation.Current?.PageType != pageType)
                        Navigate(pageType);
                };

                _tabs[pageType] = tab;
                TabStrip.Children.Add(tab);
            }
        }

        private void FitTabsOnOneLine()
        {
            TabStrip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double needed = Math.Ceiling(TabStrip.DesiredSize.Width + TabStrip.Margin.Left + TabStrip.Margin.Right + 16);

            if (needed > MinWidth)
                MinWidth = needed;

            if (Width < MinWidth)
                Width = MinWidth;
        }

        private void SyncHeader(INavigationItem? current)
        {
            if (current?.PageType is null)
                return;

            PageTitle.Text = current.Content as string ?? "";

            if (_tabs.TryGetValue(current.PageType, out var tab))
                tab.IsChecked = true;

        }


        #endregion

        public void LoadState()
        {
            if (_state.Left > SystemParameters.VirtualScreenWidth)
                _state.Left = 0;

            if (_state.Top > SystemParameters.VirtualScreenHeight)
                _state.Top = 0;

            if (_state.Width > 0)
                this.Width = _state.Width;

            if (_state.Height > 0)
                this.Height = _state.Height;

            if (_state.Left > 0 && _state.Top > 0)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
        }

        private async void SafeNavigate(Type page)
        {
            await Task.Delay(500);

            if (page == typeof(GlobalSettingsPage) && !App.GlobalSettings.Loaded)
                return;

            Navigate(page);
        }

        private async void ShowAlreadyRunningSnackbar()
        {
            await Task.Delay(500);
            AlreadyRunningSnackbar.Show();
        }

        #region INavigationWindow methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        private void WpfUiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (PresetEditSession.Active || App.FastFlags.Changed || App.PendingSettingTasks.Any())
            {
                var result = Frontend.ShowMessageBox(Strings.Menu_UnsavedChanges, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            PresetEditSession.Changed -= OnEditSessionChanged;
            PresetEditSession.Cancel();

            _state.Width = this.Width;
            _state.Height = this.Height;

            _state.Top = this.Top;
            _state.Left = this.Left;

            App.State.Save();
        }

        private void WpfUiWindow_Closed(object sender, EventArgs e)
        {
            if (App.LaunchSettings.TestModeFlag.Active)
                LaunchHandler.LaunchRoblox(LaunchMode.Player);
            else
                App.SoftTerminate();
        }

        private void BuildSearchIndexAutomatically()
        {
            _searchIndex = new List<SearchBarItem>();

            var navItems = RootNavigation.Items.OfType<NavigationItem>();

            foreach (var item in navItems)
            {
                if (item.PageType == null)
                    continue;

                if (Activator.CreateInstance(item.PageType) is Page pageInstance)
                {
                    var optionControls = FindLogicalChildren<OptionControl>(pageInstance);

                    foreach (var optionControl in optionControls)
                    {
                        if (optionControl.Header is string headerText && !string.IsNullOrWhiteSpace(headerText))
                        {
                            _searchIndex.Add(new SearchBarItem
                            {
                                DisplayName = headerText,
                                PageType = item.PageType
                            });
                        }
                    }
                }
            }
        }

        private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            foreach (object rawChild in LogicalTreeHelper.GetChildren(depObj))
            {
                if (rawChild is DependencyObject child)
                {
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindLogicalChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void AutoSuggestBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is AutoSuggestBox autoSuggestBox)
            {
                var currentText = autoSuggestBox.Text;

                if (string.IsNullOrWhiteSpace(currentText))
                {
                    autoSuggestBox.ItemsSource = null;
                    return;
                }

                var selectedSetting = _searchIndex?.FirstOrDefault(x => x.DisplayName.Equals(currentText, StringComparison.OrdinalIgnoreCase));

                if (selectedSetting is not null)
                {
                    Navigate(selectedSetting.PageType);
                    return;
                }

                var query = currentText.ToLower();
                autoSuggestBox.ItemsSource = _searchIndex?
                    .Where(x => x.DisplayName.ToLower().Contains(query))
                    .Select(x => x.DisplayName)
                    .ToList();
            }
        }
    }
}