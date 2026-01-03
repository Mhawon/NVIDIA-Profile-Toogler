using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using Avalonia.Input;
using System.Diagnostics;
using NvAPIWrapper; // Added this line
using NvAPIWrapper.Display;

namespace NVIDIA_Profil_Toogler
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        private HotkeyService _hotkeyService = null!;
        private Dictionary<Guid, int> _hotkeyRegistrations = new Dictionary<Guid, int>();
        private Guid? _lastAppliedProfileId;
        private bool _isShuttingDown = false;
        public ObservableCollection<ProfileViewModel> ProfileViewModels { get; set; }

        public MainWindow(AppSettings settings)
        {
            _settings = settings;
            ProfileViewModels = new ObservableCollection<ProfileViewModel>();
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.PropertyChanged += MainWindow_PropertyChanged;
        }

        public MainWindow() : this(new AppSettings()) // For designer
        {
        }

        private void MainWindow_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == WindowStateProperty && e.NewValue is WindowState newState)
            {
                DebugLogger.Log($"MainWindow: WindowState changed to {newState}. HideOnClose is {_settings.HideOnClose}.");
            }
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            DebugLogger.Log("MainWindow: Loaded event started.");

            var mainContent = this.FindControl<DockPanel>("MainDockPanel");
            if (mainContent != null) mainContent.IsEnabled = false;
            this.Cursor = new Cursor(StandardCursorType.Wait);

            try
            {
                // Load configuration at startup
                LoadConfiguration();

                if (_settings.StartMinimized)
                {
                    DebugLogger.Log("MainWindow: StartMinimized is true, hiding window.");
                    this.Hide();
                }

                ApplySettingsToUi();
                WireUpEvents();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _hotkeyService = new HotkeyService();
                    RegisterAllHotkeys();
                }
                else
                {
                    SetHotkeyButton.IsVisible = false;
                    AddProfileButton.IsEnabled = false;
                    await MessageBoxWindow.Show(this, "Limited Functionality", "NVIDIA features are disabled as this does not appear to be a Windows environment.");
                    DebugLogger.Log("Disabled all NVIDIA-dependent controls as platform is not Windows.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"FATAL: An error occurred during startup: {ex}");
                await MessageBoxWindow.Show(this, "Startup Error", $"An unexpected error occurred during startup: {ex.Message}");
            }
            finally
            {
                if (mainContent != null) mainContent.IsEnabled = true;
                this.Cursor = new Cursor(StandardCursorType.Arrow);
                DebugLogger.Log("MainWindow: Loaded event finished.");
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            DebugLogger.Log($"MainWindow: Closing event. HideOnClose is {_settings.HideOnClose}.");
            if (_settings.HideOnClose)
            {
                e.Cancel = true;
                this.Hide();
                DebugLogger.Log("MainWindow: Hiding instead of closing.");
            }
            else
            {
                if (_isShuttingDown) return;
                _isShuttingDown = true;

                DebugLogger.Log("MainWindow: Closing for real.");
                _hotkeyService?.Dispose();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }

        private void RegisterAllHotkeys()
        {
            UnregisterAllHotkeys();

            if (_hotkeyService == null)
            {
                DebugLogger.Log("RegisterAllHotkeys: HotkeyService is not initialized (not on Windows).");
                return;
            }

            DebugLogger.Log($"RegisterAllHotkeys: Attempting to register {_settings.Hotkeys.Count} hotkeys.");

            foreach (var hotkeyBinding in _settings.Hotkeys)
            {
                uint vk = GetVirtualKeyCode(hotkeyBinding.Key);
                uint fsModifiers = (uint)GetHotkeyModifier(hotkeyBinding.Modifiers);

                if (vk == 0)
                {
                    DebugLogger.Log($"WARNING: Skipping hotkey {hotkeyBinding.DisplayText}: unsupported key.");
                    continue;
                }

                Action hotkeyAction = () =>
                {
                    DebugLogger.Log($"Hotkey {hotkeyBinding.DisplayText} triggered logic for Hotkey ID: {hotkeyBinding.Id}");
                    ExecuteHotkeyLogic(hotkeyBinding);
                };

                try
                {
                    int serviceRegistrationId = _hotkeyService.Register(fsModifiers, vk, hotkeyAction);
                    _hotkeyRegistrations[hotkeyBinding.Id] = serviceRegistrationId;
                    DebugLogger.Log($"Hotkey {hotkeyBinding.DisplayText} registered successfully with ID {serviceRegistrationId}.");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ERROR: Failed to register hotkey {hotkeyBinding.DisplayText} via service: {ex.Message}");
                }
            }
        }

        private void UnregisterAllHotkeys()
        {
            if (_hotkeyService == null || _hotkeyRegistrations == null) return;

            foreach (var regId in _hotkeyRegistrations.Values)
            {
                try
                {
                    _hotkeyService.Unregister(regId);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ERROR: Failed to unregister hotkey ID {regId}: {ex.Message}");
                }
            }
            _hotkeyRegistrations.Clear();
            DebugLogger.Log("All hotkeys unregistered.");
        }

        private void UpdateAppliedProfileIndicator(Guid appliedProfileId)
        {
            _lastAppliedProfileId = appliedProfileId;
            foreach (var vm in ProfileViewModels)
            {
                vm.IsApplied = vm.Profile.Id == appliedProfileId;
            }
        }

        private async void ExecuteHotkeyLogic(HotkeyBinding hotkeyBinding)
        {
            if (!hotkeyBinding.TargetDisplays.Any())
            {
                DebugLogger.Log($"ExecuteHotkeyLogic: Hotkey '{hotkeyBinding.DisplayText}' has no target displays configured, aborting hotkey action.");
                return;
            }

            Profile? profileToApply = null;

            if (hotkeyBinding.LogicType == HotkeyLogicType.DirectSwitch)
            {
                var profileId = hotkeyBinding.ProfileIds.FirstOrDefault();
                profileToApply = _settings.Profiles.FirstOrDefault(p => p.Id == profileId);
            }
            else if (hotkeyBinding.LogicType == HotkeyLogicType.TwoProfileToggle)
            {
                if (hotkeyBinding.ProfileIds.Count < 2) return;

                var profileA = _settings.Profiles.FirstOrDefault(p => p.Id == hotkeyBinding.ProfileIds[0]);
                var profileB = _settings.Profiles.FirstOrDefault(p => p.Id == hotkeyBinding.ProfileIds[1]);

                if (profileA == null || profileB == null) return;

                profileToApply = (_lastAppliedProfileId == profileB.Id) ? profileA : profileB;
            }
            
            if (profileToApply != null)
            {
                DebugLogger.Log($"ExecuteHotkeyLogic: Applying profile '{profileToApply.Name}'.");
                await ExecuteColorApplierProcess(profileToApply, hotkeyBinding.TargetDisplays);
                UpdateAppliedProfileIndicator(profileToApply.Id);
            }
        }
        
        private uint GetVirtualKeyCode(Key key)
        {
            return key switch
            {
                Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44, Key.E => 0x45,
                Key.F => 0x46, Key.G => 0x47, Key.H => 0x48, Key.I => 0x49, Key.J => 0x4A,
                Key.K => 0x4B, Key.L => 0x4C, Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F,
                Key.P => 0x50, Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
                Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58, Key.Y => 0x59,
                Key.Z => 0x5A,
                Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33, Key.D4 => 0x34,
                Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37, Key.D8 => 0x38, Key.D9 => 0x39,
                Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73, Key.F5 => 0x74,
                Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77, Key.F9 => 0x78, Key.F10 => 0x79,
                Key.F11 => 0x7A, Key.F12 => 0x7B,
                Key.Space => 0x20,
                _ => 0
            };
        }

        private HotkeyModifier GetHotkeyModifier(KeyModifiers modifiers)
        {
            HotkeyModifier hotkeyModifiers = HotkeyModifier.MOD_NONE;
            if (modifiers.HasFlag(KeyModifiers.Alt)) hotkeyModifiers |= HotkeyModifier.MOD_ALT;
            if (modifiers.HasFlag(KeyModifiers.Control)) hotkeyModifiers |= HotkeyModifier.MOD_CONTROL;
            if (modifiers.HasFlag(KeyModifiers.Shift)) hotkeyModifiers |= HotkeyModifier.MOD_SHIFT;
            if (modifiers.HasFlag(KeyModifiers.Meta)) hotkeyModifiers |= HotkeyModifier.MOD_WIN;
            return hotkeyModifiers;
        }

        [Flags]
        private enum HotkeyModifier : uint
        {
            MOD_NONE = 0x0000,
            MOD_ALT = 0x0001,
            MOD_CONTROL = 0x0002,
            MOD_SHIFT = 0x0004,
            MOD_WIN = 0x0008
        }
        
        private async Task ExecuteColorApplierProcess(Profile profile, List<string> targetDisplayNames)
        {
            var applierPath = Path.Combine(AppContext.BaseDirectory, "ColorApplier", "ColorApplier.exe"); // Adjust path as necessary

            if (!File.Exists(applierPath))
            {
                DebugLogger.Log($"ColorApplier executable not found at '{applierPath}'.");
                await MessageBoxWindow.Show(this, "Error", $"Could not find 'ColorApplier.exe'. Please ensure it's located in a 'ColorApplier' sub-folder next to the main application.");
                return;
            }

            var arguments = new List<string>();
            arguments.Add($"--vibrance {profile.Vibrance}");
            arguments.Add($"--brightness {profile.Brightness.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            arguments.Add($"--contrast {profile.Contrast.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            arguments.Add($"--gamma {profile.Gamma.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            foreach (var display in targetDisplayNames)
            {
                arguments.Add($"--display \"{display}\""); // Quote display names in case they have spaces
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = applierPath,
                Arguments = string.Join(" ", arguments),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            DebugLogger.Log($"Launching ColorApplier: {startInfo.FileName} {startInfo.Arguments}");

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    DebugLogger.Log("Failed to start ColorApplier process.");
                    return;
                }
                await process.WaitForExitAsync();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    DebugLogger.Log($"ColorApplier Output: {output}");
                }
                if (!string.IsNullOrWhiteSpace(error))
                {
                    DebugLogger.Log($"ColorApplier Error: {error}");
                }

                if (process.ExitCode != 0)
                {
                    DebugLogger.Log($"ColorApplier exited with error code: {process.ExitCode}");
                    await MessageBoxWindow.Show(this, "Application Error", $"Failed to apply settings: ColorApplier exited with error code {process.ExitCode}. See debug log for details.");
                }
                else
                {
                    DebugLogger.Log("ColorApplier executed successfully.");
                }
            }
        }

        private Profile? _editingProfile;
        private bool _isAddingNewProfile;

        private void ShowProfileEditor(bool show)
        {
            var editorPanel = this.FindControl<Border>("ProfileEditorPanel");
            var mainContent = this.FindControl<Border>("MainContentBorder");
            if (editorPanel != null) editorPanel.IsVisible = show;
            if (mainContent != null) mainContent.IsVisible = !show;
        }

        private void SaveConfiguration()
        {
            try
            {
                UpdateSettingsFromUi();
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                DebugLogger.Log("MainWindow: Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ERROR: Failed to save config file: {ex.ToString()}.");
            }
        }
        
        private void SaveProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_editingProfile == null) return;

            if (_isAddingNewProfile)
            {
                _settings.Profiles.Add(_editingProfile);
            }
            else
            {
                var originalProfile = _settings.Profiles.FirstOrDefault(p => p.Id == _editingProfile.Id);
                if (originalProfile != null)
                {
                    originalProfile.UpdateFrom(_editingProfile);
                }
            }

            SaveConfiguration();
            RefreshProfileList();
            ShowProfileEditor(false);
        }

        private void CancelProfileEditButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowProfileEditor(false);
        }

        private void WireUpEvents()
        {
            AddProfileButton.Click += AddProfileButton_Click;
            SetHotkeyButton.Click += SetHotkeyButton_Click;
            
            var settingsButton = this.FindControl<Button>("SettingsButton");
            if(settingsButton != null) settingsButton.Click += SettingsButton_Click;
            var closeSettingsButton = this.FindControl<Button>("CloseSettingsButton");
            if(closeSettingsButton != null) closeSettingsButton.Click += CloseSettingsButton_Click;
            var saveSettingsButton = this.FindControl<Button>("SaveSettingsButton");
            if(saveSettingsButton != null) saveSettingsButton.Click += SaveSettingsButton_Click;

            var saveProfileButton = this.FindControl<Button>("SaveProfileButton");
            if(saveProfileButton != null) saveProfileButton.Click += SaveProfileButton_Click;
            var cancelProfileEditButton = this.FindControl<Button>("CancelProfileEditButton");
            if(cancelProfileEditButton != null) cancelProfileEditButton.Click += CancelProfileEditButton_Click;
        }



        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    DebugLogger.Log("MainWindow: Configuration loaded successfully.");
                }
                else
                {
                    _settings = new AppSettings();
                    DebugLogger.Log("MainWindow: No config file found, using default settings.");
                }
            }
            catch (JsonException ex) // Catch specific JSON deserialization errors
            {
                DebugLogger.Log($"ERROR: Failed to deserialize config file: {ex.Message}. Using default settings and overwriting corrupted file.");
                _settings = new AppSettings(); // Reset to default settings
                SaveConfiguration(); // Overwrite corrupted file with default
            }
            catch (Exception ex) // Catch other potential file I/O errors
            {
                DebugLogger.Log($"ERROR: Failed to load config file: {ex.ToString()}. Using default settings.");
                _settings = new AppSettings(); // Reset to default settings
            }
        }
        
        private List<string> GetDisplayNames()
        {
            var displayNames = new List<string>();
            try
            {
                NvAPIWrapper.NVIDIA.Initialize();
                var displays = Display.GetDisplays();
                DebugLogger.Log($"GetDisplayNames: Found {displays.Length} display(s).");
                foreach (var display in displays)
                {
                    displayNames.Add(display.Name);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ERROR: Could not enumerate displays using NvAPIWrapper: {ex}");
            }
            finally
            {
                NvAPIWrapper.NVIDIA.Unload();
            }
            return displayNames;
        }

        private void ApplySettingsToUi()
        {
            ProfileListBox.ItemsSource = ProfileViewModels;
            RefreshProfileList();
        }

        private void RefreshProfileList()
        {
            ProfileViewModels.Clear();
            foreach (var profile in _settings.Profiles)
            {
                if (profile == null)
                {
                    DebugLogger.Log("WARNING: Null profile found in _settings.Profiles during RefreshProfileList. Skipping.");
                    continue;
                }
                ProfileViewModels.Add(new ProfileViewModel(profile));
            }
        }

        private void UpdateSettingsFromUi()
        {
            var startWithWindowsCheckBox = this.FindControl<CheckBox>("StartWithWindowsCheckBox");
            var startHiddenToTrayCheckBox = this.FindControl<CheckBox>("StartHiddenToTrayCheckBox");
            var hideOnCloseCheckBox = this.FindControl<CheckBox>("HideOnCloseCheckBox");
            var displayListBox = this.FindControl<ListBox>("DisplayListBox");

            if (startWithWindowsCheckBox != null)
                _settings.StartWithWindows = startWithWindowsCheckBox.IsChecked ?? false;
            if (startHiddenToTrayCheckBox != null)
                _settings.StartMinimized = startHiddenToTrayCheckBox.IsChecked ?? false;
            if (hideOnCloseCheckBox != null)
                _settings.HideOnClose = hideOnCloseCheckBox.IsChecked ?? false;

            if (displayListBox != null && displayListBox.ItemsSource is List<DisplayItem> displayItems)
            {
                _settings.SelectedDisplays = displayItems
                    .Where(item => item.IsSelected)
                    .Select(item => item.DeviceName)
                    .ToList();
            }
        }

        private void AddProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            _isAddingNewProfile = true;
            _editingProfile = new Profile { Name = "New Profile" };
            
            var editorPanel = this.FindControl<Border>("ProfileEditorPanel");
            if (editorPanel != null)
            {
                editorPanel.DataContext = _editingProfile;
            }
            
            ShowProfileEditor(true);
        }

        private void EditProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ProfileViewModel vm })
            {
                _isAddingNewProfile = false;
                _editingProfile = vm.Profile.Clone();
                
                var editorPanel = this.FindControl<Border>("ProfileEditorPanel");
                if (editorPanel != null)
                {
                    editorPanel.DataContext = _editingProfile;
                }
                
                ShowProfileEditor(true);
            }
        }

        private void DeleteProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ProfileViewModel vm })
            {
                _settings.Profiles.Remove(vm.Profile);
                ProfileViewModels.Remove(vm);
                SaveConfiguration();
            }
        }

        private async void SetHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            var hotkeyEditor = new HotkeySettingsWindow(_settings.Hotkeys, _settings.Profiles, GetDisplayNames());
            var result = await hotkeyEditor.ShowDialog<List<HotkeyBinding>>(this);

            if (result != null)
            {
                _settings.Hotkeys = result;
                SaveConfiguration();
                RegisterAllHotkeys();
            }
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var settingsPopup = this.FindControl<Popup>("SettingsPopup");
            if (settingsPopup != null)
            {
                if (!settingsPopup.IsOpen)
                {
                    // Load settings into the popup controls
                    var startWithWindowsCheckBox = this.FindControl<CheckBox>("StartWithWindowsCheckBox");
                    var startHiddenToTrayCheckBox = this.FindControl<CheckBox>("StartHiddenToTrayCheckBox");
                    var hideOnCloseCheckBox = this.FindControl<CheckBox>("HideOnCloseCheckBox");
                    var displayListBox = this.FindControl<ListBox>("DisplayListBox");

                    if (startWithWindowsCheckBox != null)
                        startWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
                    if (startHiddenToTrayCheckBox != null)
                        startHiddenToTrayCheckBox.IsChecked = _settings.StartMinimized;
                    if (hideOnCloseCheckBox != null)
                        hideOnCloseCheckBox.IsChecked = _settings.HideOnClose;

                    if (displayListBox != null)
                    {
                        var displayItems = GetDisplayNames()
                            .Select(name => new DisplayItem
                            {
                                Name = name,
                                DeviceName = name,
                                IsSelected = _settings.SelectedDisplays.Contains(name)
                            })
                            .ToList();
                        displayListBox.ItemsSource = displayItems;
                    }
                }
                settingsPopup.IsOpen = !settingsPopup.IsOpen;
            }
        }

        private async void SaveSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var displayListBox = this.FindControl<ListBox>("DisplayListBox");
            if (displayListBox != null && displayListBox.ItemsSource is List<DisplayItem> displayItems)
            {
                if (!displayItems.Any(item => item.IsSelected))
                {
                    await MessageBoxWindow.Show(this, "Validation Error", "You must select at least one display.");
                    return;
                }
            }

            SaveConfiguration();
            
            var settingsPopup = this.FindControl<Popup>("SettingsPopup");
            if (settingsPopup != null)
            {
                settingsPopup.IsOpen = false;
            }
        }

        private void CloseSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var settingsPopup = this.FindControl<Popup>("SettingsPopup");
            if (settingsPopup != null)
            {
                settingsPopup.IsOpen = false;
            }
        }

        private async void ApplyProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Profile profileToApply })
            {
                if (_settings.SelectedDisplays.Any())
                {
                    DebugLogger.Log($"ApplyProfileButton: Applying profile '{profileToApply.Name}'.");
                    await ExecuteColorApplierProcess(profileToApply, _settings.SelectedDisplays);
                    UpdateAppliedProfileIndicator(profileToApply.Id);
                }
                else
                {
                    DebugLogger.Log("No displays selected to apply profile to.");
                    await MessageBoxWindow.Show(this, "Information", "Please select at least one display from the list first.");
                }
            }
            else
            {
                DebugLogger.Log("ApplyProfileButton_Click was called by an unexpected sender or with a null Tag.");
            }
        }
    }
}