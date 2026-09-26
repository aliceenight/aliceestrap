using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using Windows.Win32.Foundation;
using Windows.Win32;

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Mvvm.Contracts;
using Wpf.Ui.Mvvm.Services;

namespace Aliceestrap.UI.Elements.Base
{
    public abstract class WpfUiWindow : UiWindow
    {
        #region Drag Variables
        private bool _isManualDrag;
        private System.Drawing.Point _dragStartMousePos;
        private System.Drawing.Point _dragStartWindowPos;
        private DateTime _hitTime = DateTime.Now;
        private readonly int _dragDelay = 10;
        #endregion

        private readonly IThemeService _themeService = new ThemeService();

        public WpfUiWindow()
        {
            ApplyTheme();
        }

        public void ApplyTheme(bool useAcrylic = true)
        {
            const int customThemeIndex = 2;

            _themeService.SetTheme(App.Settings.Prop.Theme.GetFinal() == Enums.Theme.Dark ? ThemeType.Dark : ThemeType.Light);
            ApplyAccent(App.Settings.Prop.Theme.GetFinal());

            var dict = new ResourceDictionary { Source = new Uri($"pack://application:,,,/UI/Style/{Enum.GetName(App.Settings.Prop.Theme.GetFinal())}.xaml") };

            this.Resources["MainWindowBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

            Application.Current.Resources.MergedDictionaries[customThemeIndex] = dict;

#if QA_BUILD
            this.BorderBrush = System.Windows.Media.Brushes.Red;
            this.BorderThickness = new Thickness(4);
#endif
        }

        public static void ApplyAccent(Enums.Theme theme)
        {
            if (theme == Enums.Theme.Dark)
                Accent.Apply(
                    Color.FromRgb(0x9D, 0x6B, 0xFF),
                    Color.FromRgb(0xB0, 0x8A, 0xFF),
                    Color.FromRgb(0xC3, 0xA6, 0xFF),
                    Color.FromRgb(0xD6, 0xC2, 0xFF));
            else
                Accent.Apply(
                    Color.FromRgb(0x7B, 0x4F, 0xE0),
                    Color.FromRgb(0x6A, 0x3F, 0xD0),
                    Color.FromRgb(0x5B, 0x33, 0xBD),
                    Color.FromRgb(0x4C, 0x29, 0xA3));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            if (App.Settings.Prop.WPFSoftwareRender || App.LaunchSettings.NoGPUFlag.Active)
            {
                if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                    hwndSource.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }

        #region Acrylic Drag Logic
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!App.Settings.Prop.UseAcrylicBackground)
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            if (e.ClickCount > 1)
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            var clickedElement = e.OriginalSource as DependencyObject;
            bool isTitleBarClick = false;

            while (clickedElement != null)
            {
                if (clickedElement is System.Windows.Controls.Button || clickedElement is Button)
                {
                    base.OnPreviewMouseLeftButtonDown(e);
                    return;
                }

                if (clickedElement is TitleBar)
                {
                    isTitleBarClick = true;
                    break;
                }
                clickedElement = VisualTreeHelper.GetParent(clickedElement);
            }

            if (isTitleBarClick)
            {
                _isManualDrag = true;

                PInvoke.GetCursorPos(out _dragStartMousePos);

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                PInvoke.GetWindowRect((HWND)hwnd, out RECT rect);
                _dragStartWindowPos = new System.Drawing.Point { X = rect.left, Y = rect.top };

                this.CaptureMouse();
                e.Handled = true;
                return;
            }

            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_isManualDrag)
            {
                _isManualDrag = false;
                this.ReleaseMouseCapture();
                e.Handled = true;
            }
            base.OnPreviewMouseLeftButtonUp(e);
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (_isManualDrag && this.IsMouseCaptured)
            {
                if ((DateTime.Now - _hitTime).TotalMilliseconds < _dragDelay)
                    return;

                _hitTime = DateTime.Now;

                PInvoke.GetCursorPos(out System.Drawing.Point pt);

                int deltaX = pt.X - _dragStartMousePos.X;
                int deltaY = pt.Y - _dragStartMousePos.Y;

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                PInvoke.GetWindowRect((HWND)hwnd, out Windows.Win32.Foundation.RECT rect);

                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                PInvoke.MoveWindow((HWND)hwnd, _dragStartWindowPos.X + deltaX, _dragStartWindowPos.Y + deltaY, width, height, true);
            }
            base.OnPreviewMouseMove(e);
        }
        #endregion
    }
}
