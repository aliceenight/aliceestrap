using System.Runtime.InteropServices;
using System.Windows.Shell;

namespace Aliceestrap.UI.Utility
{
    internal static class TaskbarProgress
    {
        private enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8,
        }

        [ComImport()]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            [PreserveSig]
            int HrInit();

            [PreserveSig]
            int AddTab(IntPtr hwnd);

            [PreserveSig]
            int DeleteTab(IntPtr hwnd);

            [PreserveSig]
            int ActivateTab(IntPtr hwnd);

            [PreserveSig]
            int SetActiveAlt(IntPtr hwnd);

            [PreserveSig]
            int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            [PreserveSig]
            int SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);

            [PreserveSig]
            int SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [ComImport()]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance
        {
        }

        private static Lazy<ITaskbarList3> _taskbarInstance = new Lazy<ITaskbarList3>(() => (ITaskbarList3)new TaskbarInstance());

        private static TaskbarStates ConvertEnum(TaskbarItemProgressState state)
        {
            return state switch
            {
                TaskbarItemProgressState.None => TaskbarStates.NoProgress,
                TaskbarItemProgressState.Indeterminate => TaskbarStates.Indeterminate,
                TaskbarItemProgressState.Normal => TaskbarStates.Normal,
                TaskbarItemProgressState.Error => TaskbarStates.Error,
                TaskbarItemProgressState.Paused => TaskbarStates.Paused,
                _ => throw new Exception($"Unrecognised TaskbarItemProgressState: {state}")
            };
        }

        private static int SetProgressState(IntPtr windowHandle, TaskbarStates taskbarState)
        {
            return _taskbarInstance.Value.SetProgressState(windowHandle, taskbarState);
        }

        public static int SetProgressState(IntPtr windowHandle, TaskbarItemProgressState taskbarState)
        {
            return SetProgressState(windowHandle, ConvertEnum(taskbarState));
        }

        public static int SetProgressValue(IntPtr windowHandle, int progressValue, int progressMax)
        {
            return _taskbarInstance.Value.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }
    }
}
