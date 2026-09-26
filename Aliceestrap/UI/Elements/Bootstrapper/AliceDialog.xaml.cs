using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

using Aliceestrap.RobloxInterfaces;
using Aliceestrap.UI.Elements.Bootstrapper.Base;
using Aliceestrap.UI.ViewModels.Bootstrapper;

namespace Aliceestrap.UI.Elements.Bootstrapper
{
    public partial class AliceDialog : IBootstrapperDialog
    {
        private readonly AliceDialogViewModel _viewModel;

        private Aliceestrap.Bootstrapper? _bootstrapper;

        private bool _isClosing;

        public Aliceestrap.Bootstrapper? Bootstrapper
        {
            get => _bootstrapper;
            set
            {
                _bootstrapper = value;
                _viewModel.SetTarget(value?.IsStudioLaunch ?? false);
            }
        }

        #region UI Elements
        public string Message
        {
            get => _viewModel.Message;
            set
            {
                _viewModel.Message = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.Message));
            }
        }

        public ProgressBarStyle ProgressStyle
        {
            get => _viewModel.ProgressIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            set
            {
                _viewModel.ProgressIndeterminate = value == ProgressBarStyle.Marquee;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressIndeterminate));
                _viewModel.RefreshProgress();
            }
        }

        public int ProgressMaximum
        {
            get => _viewModel.ProgressMaximum;
            set
            {
                _viewModel.ProgressMaximum = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressMaximum));
                _viewModel.RefreshProgress();
            }
        }

        public int ProgressValue
        {
            get => _viewModel.ProgressValue;
            set
            {
                _viewModel.ProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.ProgressValue));
                _viewModel.RefreshProgress();
            }
        }

        public TaskbarItemProgressState TaskbarProgressState
        {
            get => _viewModel.TaskbarProgressState;
            set
            {
                _viewModel.TaskbarProgressState = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressState));
            }
        }

        public double TaskbarProgressValue
        {
            get => _viewModel.TaskbarProgressValue;
            set
            {
                _viewModel.TaskbarProgressValue = value;
                _viewModel.OnPropertyChanged(nameof(_viewModel.TaskbarProgressValue));
            }
        }

        public bool CancelEnabled
        {
            get => _viewModel.CancelEnabled;
            set
            {
                _viewModel.CancelEnabled = value;

                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelButtonVisibility));
                _viewModel.OnPropertyChanged(nameof(_viewModel.CancelEnabled));
            }
        }
        #endregion

        public AliceDialog()
        {
            _viewModel = new AliceDialogViewModel(this);
            DataContext = _viewModel;

            InitializeComponent();

            Title = App.Settings.Prop.BootstrapperTitle;
            Icon = App.Settings.Prop.BootstrapperIcon.GetIcon().GetImageSource();

            Deployment.ChannelChanged += OnChannelChanged;
        }

        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Background = System.Windows.Media.Brushes.Transparent;

            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);

            if (source?.CompositionTarget is not null)
                source.CompositionTarget.BackgroundColor = Colors.Transparent;

            if (source is not null)
            {
                int noBorder = DWMWA_COLOR_NONE;
                DwmSetWindowAttribute(source.Handle, DWMWA_BORDER_COLOR, ref noBorder, sizeof(int));
            }
        }

        private void OnChannelChanged(object? sender, string channel) => _viewModel.SetChannel(channel);

        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void UiWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosing)
                Bootstrapper?.Cancel();

            Deployment.ChannelChanged -= OnChannelChanged;
        }

        #region IBootstrapperDialog Methods
        public void ShowBootstrapper() => ShowDialog();

        public void CloseBootstrapper()
        {
            _isClosing = true;
            Dispatcher.BeginInvoke(Close);
        }

        public void ShowSuccess(string message, Action? callback) => BaseFunctions.ShowSuccess(message, callback);
        #endregion
    }
}
