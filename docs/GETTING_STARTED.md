# Getting Started

This guide will help you set up your development environment and run the BaristaNotes application.

## Prerequisites

### Required Software

1. **.NET 10 SDK**
   - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
   - Verify installation: `dotnet --version` (should be 10.0.x or later)

2. **IDE** (choose one):
   - **Visual Studio 2022** (17.8 or later)
     - Workloads: ".NET Multi-platform App UI development"
     - Available for Windows and macOS
   - **Visual Studio Code**
     - Extensions: C# Dev Kit, .NET MAUI extension
     - Works on Windows, macOS, and Linux (Linux cannot build iOS/macOS)

### Platform-Specific Requirements

#### iOS Development (macOS only)

- **Xcode 15** or later
- **Xcode Command Line Tools**
  ```bash
  xcode-select --install
  ```
- **iOS Simulator** (included with Xcode)

#### Android Development

- **Android SDK** (API Level 21 or higher)
  - Installed via Visual Studio installer, or
  - Android Studio with SDK Manager
- **Android Emulator** or physical device
- **Java JDK 17** (included with Visual Studio/Android Studio)

#### Windows Development (Windows only)

- **Windows 10.0.19041.0** (version 2004) or later
- Windows App SDK installed via Visual Studio

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/BaristaNotes.git
cd BaristaNotes
```

### 2. Restore Dependencies

```bash
dotnet restore
```

This downloads all NuGet packages specified in the project files.

### 3. Verify Project Structure

Your directory should look like this:

```
BaristaNotes/
├── BaristaNotes/              # Main MAUI app
├── BaristaNotes.Core/         # Business logic
├── BaristaNotes.Tests/        # Unit tests
├── BaristaNotes.sln           # Solution file
└── docs/                      # Documentation
```

## Building the Application

### Command Line

#### iOS (macOS only)

```bash
# Build
dotnet build -f net10.0-ios

# Build and run on simulator
dotnet build -t:Run -f net10.0-ios
```

#### Android

```bash
# Build
dotnet build -f net10.0-android

# Build and run on emulator
dotnet build -t:Run -f net10.0-android
```

#### Windows (Windows only)

```bash
# Build
dotnet build -f net10.0-windows10.0.19041.0

# Build and run
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

### Visual Studio

1. Open `BaristaNotes.sln`
2. Select target platform from dropdown (iOS, Android, Windows)
3. Select device/simulator from device dropdown
4. Press **F5** to build and run, or **Ctrl+Shift+B** to build only

### Visual Studio Code

1. Open the `BaristaNotes` folder
2. Install recommended extensions if prompted
3. Open Command Palette (Ctrl+Shift+P / Cmd+Shift+P)
4. Run **.NET MAUI: Pick Android Device** or **iOS Device**
5. Press **F5** to debug

## Running on Devices

### iOS Simulator

Simulators are available after installing Xcode:

```bash
# List available simulators
xcrun simctl list devices

# Run on specific simulator
dotnet build -t:Run -f net10.0-ios -p:_DeviceName="iPhone 15"
```

### iOS Physical Device

1. Connect iPhone/iPad via USB
2. Enable Developer Mode on device (Settings > Privacy & Security > Developer Mode)
3. Trust your Mac when prompted on device
4. In Visual Studio, select your device from the device dropdown
5. You may need to configure signing in the project properties

### Android Emulator

Create an emulator in Android Studio AVD Manager or Visual Studio:

```bash
# List emulators
emulator -list-avds

# Start emulator
emulator -avd <emulator_name>

# Run app
dotnet build -t:Run -f net10.0-android
```

### Android Physical Device

1. Enable Developer Options on device (tap Build Number 7 times in Settings)
2. Enable USB Debugging in Developer Options
3. Connect device via USB
4. Accept "Allow USB debugging" prompt on device
5. Run `adb devices` to verify connection
6. Select device in Visual Studio and run

## Database Setup

The SQLite database is created automatically on first launch. Location by platform:

- **iOS**: `/var/mobile/Containers/Data/Application/{GUID}/Library/baristas.db`
- **Android**: `/data/data/com.yourcompany.baristanotes/files/baristas.db`
- **Windows**: `%LocalAppData%\BaristaNotes\baristas.db`

### Resetting the Database

Delete the database file and restart the app:

```bash
# iOS Simulator
rm ~/Library/Developer/CoreSimulator/Devices/*/data/Containers/Data/Application/*/Library/baristas.db

# Android Emulator
adb shell
run-as com.yourcompany.baristanotes
rm files/baristas.db
```

## Running Tests

### All Tests

```bash
dotnet test
```

### Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~ShotServiceTests"
```

### With Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Development Workflow

### Hot Reload

MauiReactor supports hot reload for UI changes:

1. Start the app with debugger attached
2. Modify a component's `Render()` method
3. Save the file
4. UI updates automatically (no rebuild needed)

Note: Hot reload doesn't work for:
- Adding/removing properties
- Changing method signatures
- Adding new files

### Debugging

#### Visual Studio

- Set breakpoints by clicking left margin in code editor
- Press **F5** to start debugging
- Use Debug windows (Locals, Watch, Call Stack)

#### VS Code

- Set breakpoints by clicking left margin
- Press **F5** to start debugging
- Use Debug Console for expressions

### Logging

The app uses `System.Diagnostics.Debug` for logging:

```csharp
Debug.WriteLine($"Shot created: {shot.Id}");
```

View logs in:
- **Visual Studio**: Output window
- **VS Code**: Debug Console
- **Xcode**: Console app (iOS)
- **Android Studio**: Logcat

## Troubleshooting

### Build Errors

#### "Workload not found"

Install MAUI workload:

```bash
dotnet workload install maui
```

#### "SDK not found"

Ensure .NET 10 SDK is installed:

```bash
dotnet --list-sdks
```

#### Android build fails

Update Android SDK tools in SDK Manager.

### Runtime Errors

#### App crashes on launch

Check Output/Logcat for exception details. Common causes:
- Missing database file (delete and recreate)
- Service registration issue (check `MauiProgram.cs`)

#### Images not loading

Ensure images are in the correct resource folder and marked as `MauiImage` in build action.

### Performance Issues

#### Slow database queries

Add indexes to frequently queried columns (see [DATA_LAYER.md](DATA_LAYER.md)).

#### UI lag

- Avoid heavy operations in `Render()` method
- Use async/await for all I/O operations
- Consider pagination for long lists

## IDE Configuration

### Visual Studio

Recommended extensions:
- MauiReactor Hot Reload (if available)
- .NET MAUI Check

Settings:
- Tools > Options > MAUI > Enable XAML Hot Reload
- Tools > Options > Debugging > Enable Just My Code

### VS Code

Required extensions:
- C# Dev Kit
- .NET MAUI

Recommended extensions:
- SQLite Viewer (for database inspection)
- C# Extensions

## Next Steps

- Read the [Architecture Overview](MAUIREACTOR_PATTERNS.md)
- Explore the [Data Layer](DATA_LAYER.md)
- Review [Service Architecture](SERVICES.md)
- Check [Contributing Guidelines](CONTRIBUTING.md)

## Additional Resources

- [.NET MAUI Documentation](https://learn.microsoft.com/dotnet/maui/)
- [MauiReactor GitHub](https://github.com/adospace/reactorui-maui)
- [Entity Framework Core Docs](https://learn.microsoft.com/ef/core/)

## Getting Help

- Check existing [GitHub Issues](https://github.com/yourusername/BaristaNotes/issues)
- Read the documentation in `/docs` folder
- Review code examples in the codebase
