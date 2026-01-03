using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Key = Avalonia.Input.Key;
using System.Collections.ObjectModel;
using System.Text;

namespace NVIDIA_Profil_Toogler
{
    public class HotkeyDisplayItem
    {
        public HotkeyBinding Binding { get; }
        public string Profile1Name { get; }
        public string Profile2Name { get; }
        public bool IsTwoProfileToggle { get; }
        public bool IsDirectSwitch { get; }

        public HotkeyDisplayItem(HotkeyBinding binding, List<Profile> allProfiles)
        {
            Binding = binding;
            IsTwoProfileToggle = binding.LogicType == HotkeyLogicType.TwoProfileToggle;
            IsDirectSwitch = binding.LogicType == HotkeyLogicType.DirectSwitch;

            var profile1 = allProfiles.FirstOrDefault(p => p.Id == binding.ProfileIds.FirstOrDefault());
            Profile1Name = profile1?.Name ?? "Unassigned";

            if (IsTwoProfileToggle)
            {
                var profile2 = allProfiles.FirstOrDefault(p => p.Id == binding.ProfileIds.LastOrDefault());
                Profile2Name = profile2?.Name ?? "Unassigned";
            }
            else
            {
                Profile2Name = string.Empty;
            }
        }
    }


    public partial class HotkeySettingsWindow : Window
    {
        // Use an ObservableCollection to automatically update the UI when items are added/removed.
        public ObservableCollection<HotkeyDisplayItem> Hotkeys { get; set; }
        public List<Profile> AllProfiles { get; set; }
        public List<string> AllDisplays { get; set; }

        private HotkeyBinding? _editingHotkey;
        private bool _isAddingNewHotkey;
        private Key _capturedKey = Key.None;
        private KeyModifiers _capturedModifiers = KeyModifiers.None;

        public HotkeySettingsWindow()
        {
            InitializeComponent();
            Hotkeys = new ObservableCollection<HotkeyDisplayItem>();
            AllProfiles = new List<Profile>();
            AllDisplays = new List<string>();
        }

        public HotkeySettingsWindow(List<HotkeyBinding> hotkeys, List<Profile> allProfiles, List<string> allDisplays)
        {
            InitializeComponent();

            // Initialize collections
            AllProfiles = allProfiles;
            AllDisplays = allDisplays;
            Hotkeys = new ObservableCollection<HotkeyDisplayItem>(
                hotkeys.Select(h => new HotkeyDisplayItem(h, AllProfiles))
            );

            // Find controls once
            HotkeyListBox = this.FindControl<ListBox>("HotkeyListBox")!;
            HotkeyEditorPanel = this.FindControl<Border>("HotkeyEditorPanel")!;
            HotkeyInputTextBox = this.FindControl<TextBox>("HotkeyInputTextBox")!;
            LogicTypeComboBox = this.FindControl<ComboBox>("LogicTypeComboBox")!;
            Profile1ComboBox = this.FindControl<ComboBox>("Profile1ComboBox")!;
            Profile2ComboBox = this.FindControl<ComboBox>("Profile2ComboBox")!;
            var displayListBox = this.FindControl<ListBox>("DisplayListBox");
            if (displayListBox != null)
            {
                displayListBox.ItemsSource = AllDisplays.Select(d => new DisplayItem { Name = d, DeviceName = d }).ToList();
            }
            

            // Wire up events
            this.FindControl<Button>("SaveButton")!.Click += (s, e) => this.Close(Hotkeys.Select(h => h.Binding).ToList());
            this.FindControl<Button>("CancelButton")!.Click += (s, e) => this.Close(null);

            this.FindControl<Button>("AddHotkeyButton")!.Click += AddHotkeyButton_Click;
            this.FindControl<Button>("SaveHotkeyButton")!.Click += SaveHotkeyButton_Click;
            this.FindControl<Button>("CancelHotkeyEditButton")!.Click += CancelHotkeyEditButton_Click;

            this.KeyDown += HotkeySettingsWindow_KeyDown;
            LogicTypeComboBox.SelectionChanged += LogicTypeComboBox_SelectionChanged;
            HotkeyListBox.SelectionChanged += HotkeyListBox_SelectionChanged;

            // Populate controls
            HotkeyListBox.ItemsSource = Hotkeys;
            LogicTypeComboBox.ItemsSource = Enum.GetValues(typeof(HotkeyLogicType)).Cast<HotkeyLogicType>();
            Profile1ComboBox.ItemsSource = AllProfiles;
            Profile2ComboBox.ItemsSource = AllProfiles;
        }

        private void HotkeyListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            HotkeyListBox.SelectedItem = null;
        }

        private void ShowHotkeyEditor(bool show)
        {
            HotkeyEditorPanel.IsVisible = show;
        }

        private void LogicTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (LogicTypeComboBox.SelectedItem is HotkeyLogicType selectedLogic)
            {
                Profile2ComboBox.IsEnabled = (selectedLogic == HotkeyLogicType.TwoProfileToggle);
            }
        }

        private void AddHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            _editingHotkey = new HotkeyBinding();
            _isAddingNewHotkey = true;
            ClearHotkeyEditorFields();
            ShowHotkeyEditor(true);
        }

        private void EditHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: HotkeyDisplayItem selectedItem })
            {
                var selectedHotkey = selectedItem.Binding;
                // Create a temporary copy for editing, so we can cancel changes.
                _editingHotkey = new HotkeyBinding
                {
                    Id = selectedHotkey.Id,
                    Key = selectedHotkey.Key,
                    Modifiers = selectedHotkey.Modifiers,
                    LogicType = selectedHotkey.LogicType,
                    ProfileIds = new List<Guid>(selectedHotkey.ProfileIds),
                    TargetDisplays = new List<string>(selectedHotkey.TargetDisplays)
                };
                _isAddingNewHotkey = false;
                PopulateHotkeyEditorFields(_editingHotkey);
                ShowHotkeyEditor(true);
            }
        }

        private void DeleteHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: HotkeyDisplayItem selectedItem })
            {
                Hotkeys.Remove(selectedItem);
            }
        }

        private void ClearHotkeyEditorFields()
        {
            _capturedKey = Key.None;
            _capturedModifiers = KeyModifiers.None;
            HotkeyInputTextBox.Text = "Press a key combination...";
            LogicTypeComboBox.SelectedItem = HotkeyLogicType.DirectSwitch;
            Profile1ComboBox.SelectedItem = null;
            Profile2ComboBox.SelectedItem = null;
        }

        private void PopulateHotkeyEditorFields(HotkeyBinding hotkey)
        {
            _capturedKey = hotkey.Key;
            _capturedModifiers = hotkey.Modifiers;
            HotkeyInputTextBox.Text = hotkey.DisplayText;
            LogicTypeComboBox.SelectedItem = hotkey.LogicType;
            Profile1ComboBox.SelectedItem = AllProfiles.FirstOrDefault(p => p.Id == hotkey.ProfileIds.FirstOrDefault());
            Profile2ComboBox.SelectedItem = AllProfiles.FirstOrDefault(p => p.Id == hotkey.ProfileIds.LastOrDefault());

            var displayListBox = this.FindControl<ListBox>("DisplayListBox");
            if (displayListBox != null)
            {
                var displayItems = AllDisplays
                    .Select(name => new DisplayItem
                    {
                        Name = name,
                        DeviceName = name,
                        IsSelected = hotkey.TargetDisplays.Contains(name)
                    })
                    .ToList();
                displayListBox.ItemsSource = displayItems;
            }
        }

        private void HotkeySettingsWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (HotkeyEditorPanel.IsVisible && HotkeyInputTextBox.IsFocused)
            {
                e.Handled = true;
                _capturedKey = e.Key;
                _capturedModifiers = e.KeyModifiers;

                string modifierString = e.KeyModifiers.ToString().Replace("Control", "Ctrl");
                HotkeyInputTextBox.Text = (e.KeyModifiers == KeyModifiers.None) ? e.Key.ToString() : $"{modifierString} + {e.Key}";
            }
        }
        
        private async void SaveHotkeyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_editingHotkey == null) return;

            // Update the hotkey object from UI controls
            _editingHotkey.Key = _capturedKey;
            _editingHotkey.Modifiers = _capturedModifiers;
            _editingHotkey.LogicType = (HotkeyLogicType)LogicTypeComboBox.SelectedItem!;
            
            _editingHotkey.ProfileIds.Clear();
            if (Profile1ComboBox.SelectedItem is Profile profile1)
            {
                _editingHotkey.ProfileIds.Add(profile1.Id);
            }
            if (_editingHotkey.LogicType == HotkeyLogicType.TwoProfileToggle && Profile2ComboBox.SelectedItem is Profile profile2)
            {
                _editingHotkey.ProfileIds.Add(profile2.Id);
            }

            var displayListBox = this.FindControl<ListBox>("DisplayListBox");
            if (displayListBox != null && displayListBox.ItemsSource is List<DisplayItem> displayItems)
            {
                _editingHotkey.TargetDisplays = displayItems
                    .Where(item => item.IsSelected)
                    .Select(item => item.DeviceName)
                    .ToList();
            }

            if (!_editingHotkey.TargetDisplays.Any())
            {
                await MessageBoxWindow.Show(this, "Validation Error", "You must select at least one target display for the hotkey.");
                return;
            }

            if (_isAddingNewHotkey)
            {
                Hotkeys.Add(new HotkeyDisplayItem(_editingHotkey, AllProfiles));
            }
            else
            {
                // Find the original in the collection and replace it to trigger UI update
                var originalItem = Hotkeys.FirstOrDefault(h => h.Binding.Id == _editingHotkey.Id);
                if (originalItem != null)
                {
                    int index = Hotkeys.IndexOf(originalItem);
                    Hotkeys[index] = new HotkeyDisplayItem(_editingHotkey, AllProfiles);
                }
            }
            
            ShowHotkeyEditor(false);
        }

        private void CancelHotkeyEditButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowHotkeyEditor(false);
        }
    }
}