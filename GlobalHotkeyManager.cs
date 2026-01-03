using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NVIDIA_Profil_Toogler
{
    public sealed class GlobalHotkeyManager : IDisposable
    {
        private readonly Thread _listenerThread;
        private readonly Action<int> _onHotkeyPressed;
        private volatile MessageWindow? _window;
        private volatile SynchronizationContext? _syncContext;

        public GlobalHotkeyManager(Action<int> onHotkeyPressed)
        {
            _onHotkeyPressed = onHotkeyPressed;
            var readyEvent = new ManualResetEvent(false);

            _listenerThread = new Thread(() =>
            {
                _window = new MessageWindow(this);
                _syncContext = new WindowsFormsSynchronizationContext();
                readyEvent.Set();
                Application.Run(); // This starts a message loop on this thread
            })
            {
                Name = "GlobalHotkeyManagerThread",
                IsBackground = true
            };

            _listenerThread.Start();
            readyEvent.WaitOne(); // Wait for the window and context to be ready
        }

        public void Register(int id, uint modifiers, uint vk)
        {
            // Post the registration to the listener thread
            _syncContext?.Post(_ =>
            {
                if (_window?.Register(id, modifiers, vk) == false)
                {
                    DebugLogger.Log($"Failed to register hotkey ID {id}. Win32 Error: {Marshal.GetLastWin32Error()}");
                }
            }, null);
        }

        public void Unregister(int id)
        {
            // Post the unregistration to the listener thread
            _syncContext?.Post(_ => _window?.Unregister(id), null);
        }

        private void ProcessHotkey(int id)
        {
            // The callback is invoked from the listener thread
            _onHotkeyPressed(id);
        }

        public void Dispose()
        {
            // Post the shutdown message to the listener thread
            _syncContext?.Post(_ => Application.ExitThread(), null);
        }

        // This class is a message-only window that listens for hotkey messages
        private class MessageWindow : NativeWindow
        {
            private readonly GlobalHotkeyManager _manager;
            private const int WM_HOTKEY = 0x0312;

            public MessageWindow(GlobalHotkeyManager manager)
            {
                _manager = manager;
                // Create a message-only window
                CreateHandle(new CreateParams { Parent = (IntPtr)(-3) }); // HWND_MESSAGE
            }

            public bool Register(int id, uint modifiers, uint vk)
            {
                return RegisterHotKey(this.Handle, id, modifiers, vk);
            }

            public void Unregister(int id)
            {
                UnregisterHotKey(this.Handle, id);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    _manager.ProcessHotkey(m.WParam.ToInt32());
                }
                base.WndProc(ref m);
            }

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        }
    }
}
