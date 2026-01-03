using System;
using System.Collections.Concurrent;
using System.Threading;
using Avalonia.Threading;

namespace NVIDIA_Profil_Toogler
{
    public sealed class HotkeyService : IDisposable
    {
        private readonly GlobalHotkeyManager _manager;
        private readonly ConcurrentDictionary<int, Action> _actions = new ConcurrentDictionary<int, Action>();
        private int _idCounter = 0;

        public HotkeyService()
        {
            _manager = new GlobalHotkeyManager(OnHotkeyPressed);
        }

        public int Register(uint modifiers, uint vk, Action action)
        {
            var id = Interlocked.Increment(ref _idCounter);
            _actions[id] = action;
            _manager.Register(id, modifiers, vk);
            return id;
        }

        public void Unregister(int id)
        {
            if (_actions.TryRemove(id, out _))
            {
                _manager.Unregister(id);
            }
        }

        private void OnHotkeyPressed(int id)
        {
            if (_actions.TryGetValue(id, out var action))
            {
                // Ensure the action is executed on the UI thread
                Dispatcher.UIThread.Post(action);
            }
        }

        public void Dispose()
        {
            _manager.Dispose();
        }
    }
}