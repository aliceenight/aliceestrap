using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Windows.Forms;
using System.Drawing;
using Windows.Win32.UI.Accessibility;

using Aliceestrap.Models.RobloxApi;

namespace Aliceestrap.Integrations
{
    public class WindowManipulation
    {
        private WINEVENTPROC? _setTitleHook;

        private HWND _hWnd;
        private uint _robloxPID;

        private readonly string _titleTemplate = App.Settings.Prop.RobloxTitle;
        private string _title = "";
        private long _userId;

        public bool UsesAccountName =>
            _titleTemplate.Contains("{username}", StringComparison.OrdinalIgnoreCase)
            || _titleTemplate.Contains("{displayname}", StringComparison.OrdinalIgnoreCase);

        public WindowManipulation(long windowHandle, long robloxProcessId)
        {
            const string LOG_IDENT = "WindowManipulation";

            App.Logger.WriteLine(LOG_IDENT, $"Got window handle as {windowHandle}");
            _hWnd = (HWND)(IntPtr)windowHandle;
            _robloxPID = (uint)robloxProcessId;
        }

        public void Start()
        {
            if (App.Settings.Prop.FakeBorderlessFullscreen)
                FakeBorderless();

            ApplyWindowModifications();
        }

        public void TrackAccount(ActivityWatcher activityWatcher)
        {
            if (!UsesAccountName)
                return;

            activityWatcher.OnGameJoin += async (_, _) =>
            {
                const string LOG_IDENT = "WindowManipulation::TrackAccount";

                long userId = activityWatcher.Data.UserId;

                if (userId == 0 || userId == _userId)
                    return;

                _userId = userId;

                try
                {
                    var user = await Http.GetJson<GetUserResponse>(new Uri($"https://users.roblox.com/v1/users/{userId}"));

                    _title = BuildTitle(_titleTemplate, user.Name, user.DisplayName);
                    PInvoke.SetWindowText(_hWnd, _title);

                    App.Logger.WriteLine(LOG_IDENT, "Updated Roblox title with the account name");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to get the account name");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            };
        }

        private static string BuildTitle(string template, string? username, string? displayName)
        {
            string title = template
                .Replace("{username}", username ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{displayname}", displayName ?? "", StringComparison.OrdinalIgnoreCase);

            if (username is null)
                title = Regex.Replace(title, @"\s{2,}", " ").Trim(' ', '-', '|', ':', ',', '·', '•', '(', ')', '[', ']', '@');

            return String.IsNullOrWhiteSpace(title) ? "Roblox" : title;
        }

        private void FakeBorderless()
        {
            const string LOG_IDENT = "WindowManipulation::BorderlessFullscreen";
            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox to borderless fullscreen");

            const int GWLSTYLE = -16;

            int style = PInvoke.GetWindowLong(_hWnd, (WINDOW_LONG_PTR_INDEX)GWLSTYLE);

            const int WS_CAPTION = 0x00C00000;
            const int WS_THICKFRAME = 0x00040000;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            const int WS_SYSMENU = 0x00080000;

            style &= ~WS_CAPTION;
            style &= ~WS_THICKFRAME;
            style &= ~WS_MINIMIZEBOX;
            style &= ~WS_MAXIMIZEBOX;
            style &= ~WS_SYSMENU;

            Rectangle resolution = Screen.PrimaryScreen!.Bounds;

            PInvoke.SetWindowLong((HWND)_hWnd, (WINDOW_LONG_PTR_INDEX)GWLSTYLE, style);

            PInvoke.SetWindowPos((HWND)_hWnd, (HWND)IntPtr.Zero, 0, 0, resolution.Width, resolution.Height + 1, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        }

        private void ApplyWindowModifications()
        {
            const string LOG_IDENT = "WindowManipulation::ApplyWindowModifications";
            const int WINEVENT_OUTOFCONTEXT = 0x0;
            const int EVENT_OBJECT_NAMECHANGE = 0x800C;
            const int WM_SETICON = 0x0080;

            App.Logger.WriteLine(LOG_IDENT, "Applying window modifications");

            _setTitleHook = new(SetWindowTitleHook);

            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox icon");
            RobloxIcon robloxIcon = App.Settings.Prop.RobloxIcon;
            if (robloxIcon != RobloxIcon.IconDefault)
                using (var icon = robloxIcon.GetIcon())
                {
                    IntPtr hIconCopy = PInvoke.CopyIcon((HICON)icon.Handle);
                    PInvoke.SendMessage(_hWnd, WM_SETICON, 0, hIconCopy);
                }

            App.Logger.WriteLine(LOG_IDENT, "Setting Roblox title");
            if (_titleTemplate != "Roblox")
            {
                if (String.IsNullOrEmpty(_title))
                    _title = BuildTitle(_titleTemplate, null, null);

                PInvoke.SetWindowText(_hWnd, _title);

                App.Current.Dispatcher.Invoke(() => PInvoke.SetWinEventHook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, null, _setTitleHook, _robloxPID, 0, WINEVENT_OUTOFCONTEXT));
            }
        }

        private void SetWindowTitleHook(HWINEVENTHOOK hWinEventHook, uint iEvent, HWND hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            const string LOG_IDENT = "WindowManipulation::SetWindowTitleHook";

            string title = _title;

            Span<char> titleBuffer = new char[256];
            PInvoke.GetWindowText(_hWnd, titleBuffer);

            string currentTitle = titleBuffer.TrimEnd('\0').ToString();

            if (currentTitle != title)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Setting Roblox title back to {title}");
                PInvoke.SetWindowText(_hWnd, title);
            }
        }
    }
}
