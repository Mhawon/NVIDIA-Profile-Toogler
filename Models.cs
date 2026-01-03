using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;

namespace NVIDIA_Profil_Toogler
{
    /// <summary>
    /// Represents a single, named display profile with all required settings.
    /// </summary>
    public class Profile : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _name = "New Profile";
        private int _vibrance = 50;
        private float _brightness = 0.5f;
        private float _contrast = 0.5f;
        private float _gamma = 1.0f;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Guid Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    DebugLogger.Log($"Profile '{_name}': Name changed from '{_name}' to '{value}'");
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Vibrance
        {
            get => _vibrance;
            set
            {
                if (_vibrance != value)
                {
                    DebugLogger.Log($"Profile '{Name}': Vibrance changed from '{_vibrance}' to '{value}'");
                    _vibrance = value;
                    OnPropertyChanged(); OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public float Brightness
        {
            get => _brightness;
            set
            {
                if (!MathF.Equals(_brightness, value))
                {
                    DebugLogger.Log($"Profile '{Name}': Brightness changed from '{_brightness}' to '{value}'");
                    _brightness = value;
                    OnPropertyChanged(); OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public float Contrast
        {
            get => _contrast;
            set
            {
                if (!MathF.Equals(_contrast, value))
                {
                    DebugLogger.Log($"Profile '{Name}': Contrast changed from '{_contrast}' to '{value}'");
                    _contrast = value;
                    OnPropertyChanged(); OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public float Gamma
        {
            get => _gamma;
            set
            {
                if (!MathF.Equals(_gamma, value))
                {
                    DebugLogger.Log($"Profile '{Name}': Gamma changed from '{_gamma}' to '{value}'");
                    _gamma = value;
                    OnPropertyChanged(); OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public string Summary => $"🌈{Vibrance}  ☀️{Brightness:F2}  🌗{Contrast:F2}  ✨{Gamma:F2}";

        public override string ToString()
        {
            return Name;
        }

        public Profile Clone()
        {
            return new Profile
            {
                Id = this.Id,
                Name = this.Name,
                Vibrance = this.Vibrance,
                Brightness = this.Brightness,
                Contrast = this.Contrast,
                Gamma = this.Gamma
            };
        }

        public void UpdateFrom(Profile other)
        {
            this.Name = other.Name;
            this.Vibrance = other.Vibrance;
            this.Brightness = other.Brightness;
            this.Contrast = other.Contrast;
            this.Gamma = other.Gamma;
        }
    }

    /// <summary>
    /// Defines the different behaviors a hotkey can have.
    /// </summary>
    public enum HotkeyLogicType
    {
        /// <summary>
        /// Switches directly to a single specified profile.
        /// </summary>
        DirectSwitch,
        /// <summary>
        /// Toggles between two specified profiles.
        /// </summary>
        TwoProfileToggle
    }

    /// <summary>
    /// Defines a single hotkey, its trigger combination, its logic, and associated profiles.
    /// </summary>
    public class HotkeyBinding
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Key Key { get; set; } = Key.None;
        public KeyModifiers Modifiers { get; set; } = KeyModifiers.None;
        public HotkeyLogicType LogicType { get; set; } = HotkeyLogicType.DirectSwitch;
        
        /// <summary>
        /// The list of profile IDs this hotkey interacts with.
        /// For DirectSwitch, this will have 1 ID.
        /// For TwoProfileToggle, this will have 2 IDs.
        /// </summary>
        public List<Guid> ProfileIds { get; set; } = new List<Guid>();

        public List<string> TargetDisplays { get; set; } = new List<string>();

        public string DisplayText
        {
            get
            {
                string modifierString = Modifiers.ToString().Replace("Control", "Ctrl").Replace("Windows", "Win").Replace("Meta", "Win");
                if (Modifiers == KeyModifiers.None)
                {
                    return Key.ToString();
                }
                return $"{modifierString} + {Key}";
            }
        }
    }

    /// <summary>
    /// The main application settings container.
    /// </summary>
    public class AppSettings
    {
        public List<Profile> Profiles { get; set; } = new List<Profile>();
        public List<HotkeyBinding> Hotkeys { get; set; } = new List<HotkeyBinding>();
        public List<string> SelectedDisplays { get; set; } = new List<string>();
        public bool StartMinimized { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public bool HideOnClose { get; set; } = false;
    }

    // This class is still useful for binding to the UI ListBox.
    public class DisplayItem
    {
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; } = false;
        public string DeviceName { get; set; } = "";

        public override string ToString()
        {
            return Name;
        }
    }

    public class ProfileViewModel : INotifyPropertyChanged
    {
        public Profile Profile { get; }
        private bool _isApplied;

        public bool IsApplied
        {
            get => _isApplied;
            set
            {
                if (_isApplied != value)
                {
                    _isApplied = value;
                    OnPropertyChanged();
                }
            }
        }

        public ProfileViewModel(Profile profile)
        {
            Profile = profile;
        }

        // Add a parameterless constructor for XAML design-time support or other scenarios
        public ProfileViewModel() : this(new Profile())
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}