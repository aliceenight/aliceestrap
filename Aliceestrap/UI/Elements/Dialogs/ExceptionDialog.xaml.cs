using System.Media;
using System.Web;
using System.Windows;
using System.Windows.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;

namespace Aliceestrap.UI.Elements.Dialogs
{
    public partial class ExceptionDialog
    {
        const int MAX_GITHUB_URL_LENGTH = 8192;

        public ExceptionDialog(Exception exception)
        {
            InitializeComponent();
            AddException(exception);

            if (!App.Logger.Initialized)
                LocateLogFileButton.Content = Strings.Dialog_Exception_CopyLogContents;

            HelpMessageMDTextBlock.MarkdownText = Strings.Dialog_Exception_Info_2_Alt;
            VersionText.Text = String.Format(Strings.Dialog_Exception_Version, App.DisplayVersion);

            ReportExceptionButton.Visibility = Visibility.Collapsed;

            LocateLogFileButton.Click += delegate
            {
                if (App.Logger.Initialized && !String.IsNullOrEmpty(App.Logger.FileLocation))
                    Utilities.ShellExecute(App.Logger.FileLocation);
                else
                    Clipboard.SetDataObject(App.Logger.AsDocument);
            };

            CloseButton.Click += delegate
            {
                Close();
            };

            SystemSounds.Hand.Play();

            Loaded += delegate
            {
                IntPtr hWnd = new WindowInteropHelper(this).Handle;
                PInvoke.FlashWindow((HWND)hWnd, true);
            };
        }

        private void AddException(Exception exception, bool inner = false)
        {
            if (!inner)
                ErrorRichTextBox.Selection.Text = $"{exception.GetType()}: {exception.Message}";

            if (exception.InnerException is null)
                return;

            ErrorRichTextBox.Selection.Text += $"\n\n[Inner Exception]\n{exception.InnerException.GetType()}: {exception.InnerException.Message}";

            AddException(exception.InnerException, true);
        }
    }
}
