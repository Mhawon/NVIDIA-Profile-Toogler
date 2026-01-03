# NVIDIA Profile Toogler

NVIDIA Profile Toogler is a utility for Windows that allows you to create, manage, and quickly switch between different color profiles for your NVIDIA GPU using global hotkeys.

![image](https://github.com/Mhawon/NVIDIA-Profile-Toogler/assets/62069239/651e065e-2b47-495c-a5b7-5a13349949d0)


## ✨ Features

- **Profile Management**: Create and save custom color profiles with specific settings for:
  - Digital Vibrance
  - Brightness
  - Contrast
  - Gamma
- **Global Hotkeys**: Configure system-wide hotkeys to:
  - Switch directly to a specific profile.
  - Toggle between two different profiles.
- **Multi-Display Support**: Apply color profiles to one or more selected displays.
- **Tray Integration**: The application can run in the system tray for easy access and minimal footprint.
- **Startup Options**: Configure the application to:
  - Start with Windows.
  - Start minimized to the tray.
  - Hide to the tray when the window is closed.

## 🚀 Getting Started

1.  Go to the [Releases page](https://github.com/Mhawon/NVIDIA-Profile-Toogler/releases).
2.  Download the `NVIDIA-Profile-Toogler.zip` file from the latest release.
3.  Unzip the archive to a location of your choice.
4.  Run `NVIDIA Profil Toogler.exe`.

## 🛠️ How to Use

- **Create a Profile**: Click the "➕" button to open the profile editor, give your profile a name, adjust the sliders, and save.
- **Set Hotkeys**: Click the "⌨️" button to open the hotkey manager. You can add a new hotkey, assign it a key combination, and define its logic (switch to a single profile or toggle between two).
- **Apply a Profile**: Click the "▶️" button next to a profile to apply it to your selected displays.
- **Settings**: Click the "⚙️" button to configure startup options and select the displays you want to control.

## 👨‍💻 Building from Source

If you want to build the project yourself, you'll need:

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later.

To build the project, run the following command in the `Profils Toogler/NVCP_Avalonia` directory:

```bash
dotnet build -c Release
```

The output will be in the `bin/Release/net6.0-windows` directory.

## 🤝 Contributing

Contributions are welcome! If you have a feature request, bug report, or want to contribute to the code, please feel free to open an issue or a pull request.

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
