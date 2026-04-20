# Context Menu App

## Technical Design and Implementation Documentation

**Project Name:** Context Menu App  
**Document Type:** Technical Design and Implementation Documentation  
**Prepared From:** Source code and configuration in this repository  
**Organization Style Reference:** Based on the attached Accops project document format  
**Platform:** Ubuntu Linux with Nautilus (GNOME Files) integration  
**Programming Language:** C#, Python bridge script, Bash  
**Framework:** .NET 8  
**GUI Utility:** Zenity  
**Primary OS Integration:** Nautilus Python extension API, Linux process execution, filesystem operations  
**Date:** 15 April 2026

---

## Table of Contents

1. Introduction  
2. Project Objectives  
3. System Requirements  
4. Development Environment Setup  
5. Installation of Required Packages  
6. Installation of .NET SDK  
7. Nautilus Python Extension Setup  
8. Zenity Requirement  
9. Project Creation and Configuration  
10. Project Structure and File Description  
11. Application Architecture  
12. Command Detection and Routing Mechanism  
13. Upload Implementation  
14. Download Implementation  
15. Dialog Handling Using Zenity  
16. Configuration Management  
17. Nautilus Extension Registration and Deployment  
18. Installation and Uninstallation Scripts  
19. Logging and Debugging Support  
20. Testing and Demonstration Procedure  
21. Error Handling and Exception Management  
22. Security and Safety Considerations  
23. Troubleshooting Guide  
24. Conclusion

---

## 1. Introduction

The **Context Menu App** is a Linux desktop integration project that extends the **Nautilus file manager** with custom right-click actions for **Upload** and **Download**. The application is implemented primarily in **C# using .NET 8**, while a small **Python Nautilus bridge** is used only to register menu items and forward click events to the C# binary.

The solution supports two interaction models:

- **GUI integration mode** through Nautilus right-click context menus.
- **CLI mode** for testing, configuration, registration, and troubleshooting.

This design keeps business logic in one place while allowing the operating system to expose the functionality through the native file manager experience.

---

## 2. Project Objectives

The main objectives of this project are:

- To integrate custom actions into the Nautilus right-click context menu.
- To provide a clean separation between UI integration, application logic, and configuration handling.
- To implement file and folder callbacks using C# rather than Python-heavy logic.
- To support both **file-based actions** and **background folder actions**.
- To provide native Linux popup dialogs through Zenity.
- To allow menu items to be enabled or disabled without changing code.
- To support automated installation, registration, and removal of the extension.

---

## 3. System Requirements

The application is intended for Linux desktop environments where Nautilus is available.

### Minimum requirements

- Ubuntu or another Linux distribution with Nautilus support
- .NET 8 SDK for development
- Nautilus Python bindings
- Zenity
- Bash shell

### Runtime requirements

- Nautilus file manager
- Python support for Nautilus extensions
- Access to the user-local directories:
  - `~/.local/share/context-menu-app/`
  - `~/.local/share/nautilus-python/extensions/`

---

## 4. Development Environment Setup

The repository is structured as a standard .NET console application with supporting folders for interfaces, services, models, a Nautilus bridge, and shell scripts.

Typical development setup:

1. Install the .NET 8 SDK.
2. Install Nautilus Python bindings.
3. Install Zenity.
4. Clone or copy the project to the development machine.
5. Run and test using CLI mode before registering the Nautilus extension.

The project uses `appsettings.json` for feature toggles and supports operation in both development and installed locations.

---

## 5. Installation of Required Packages

The project depends on Linux desktop packages outside of the .NET runtime.

### On Ubuntu/Debian

```bash
sudo apt-get update
sudo apt-get install -y python3-nautilus zenity
```

### On RHEL/Fedora-like systems

The provided install script attempts to use `dnf` and install the Nautilus Python package and Zenity where available.

---

## 6. Installation of .NET SDK

The project file targets **`net8.0`**, so the .NET 8 SDK is required for development and publishing.

### Verify installation

```bash
dotnet --version
```

### Build the project

```bash
dotnet build
```

### Publish a Linux executable

```bash
dotnet publish -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true
```

The published binary is used by the Nautilus extension during registration.

---

## 7. Nautilus Python Extension Setup

Nautilus does not directly load C# assemblies as extensions, so this project uses a small Python bridge:

- File: `NautilusExtension/context_menu_extension.py`
- Target install directory: `~/.local/share/nautilus-python/extensions/`

The Python bridge:

- Reads the runtime config file on each right-click.
- Adds `Upload` to folder background menus when enabled.
- Adds `Download` to file menus when enabled.
- Calls the installed C# binary using `subprocess.Popen()`.

This keeps application logic in C# while using the officially supported Nautilus extension mechanism for menu registration.

---

## 8. Zenity Requirement

Zenity is used to display native informational, error, and file-selection dialogs.

The application uses Zenity for:

- Upload file selection
- Upload success/error dialogs
- Download file information dialogs
- General user feedback in GUI mode

If Zenity is missing, the `DialogService` falls back to console output and error logging where possible.

---

## 9. Project Creation and Configuration

The project is defined by `ContextMenuApp.csproj` and uses:

- `OutputType` = `Exe`
- `TargetFramework` = `net8.0`
- Nullable reference types enabled
- Implicit usings enabled

Configuration is stored in `appsettings.json`:

```json
{
  "ContextMenuOptions": {
    "Upload": true,
    "Download": true
  }
}
```

These flags control whether each menu item is shown and whether the application allows the action at runtime.

---

## 10. Project Structure and File Description

### Root files

- `ContextMenuApp.csproj`  
  Main .NET project definition.

- `Program.cs`  
  Application entry point. Handles mode detection, command routing, configuration loading, and service creation.

- `appsettings.json`  
  Feature flags for Upload and Download menu visibility.

- `README.md`  
  General project overview and quick usage instructions.

### Interfaces

- `Interfaces/IUploadService.cs`  
  Contract for upload callback handling.

- `Interfaces/IDownloadService.cs`  
  Contract for download callback handling.

- `Interfaces/IDialogService.cs`  
  Contract for native popup dialogs and file selection dialogs.

- `Interfaces/IExtensionService.cs`  
  Contract for extension registration and unregistration.

- `Interfaces/IConfigurationService.cs`  
  Contract for reading and writing application settings.

### Models

- `Models/ContextMenuConfig.cs`  
  Represents the `ContextMenuOptions` configuration section.

- `Models/OperationResult.cs`  
  Standard result object returned by service methods.

### Services

- `Services/UploadService.cs`  
  Handles folder path normalization, validation, file selection, and upload callback flow.

- `Services/DownloadService.cs`  
  Handles file path normalization, metadata extraction, validation, and download callback flow.

- `Services/DialogService.cs`  
  Launches Zenity dialogs and writes debug logs.

- `Services/ConfigurationService.cs`  
  Loads and saves feature flags from `appsettings.json`.

- `Services/ExtensionService.cs`  
  Publishes the binary, deploys configuration, installs the Python bridge, cleans older extension copies, and restarts Nautilus.

### Extension and scripts

- `NautilusExtension/context_menu_extension.py`  
  Python bridge for Nautilus menu integration.

- `Scripts/install.sh`  
  Installation and deployment automation script.

- `Scripts/uninstall.sh`  
  Full cleanup and removal script.

---

## 11. Application Architecture

The project follows a service-based architecture with clear separation of responsibilities.

### High-level flow

1. Nautilus or the terminal launches the application.
2. `Program.cs` determines the command and the target path.
3. Configuration is loaded through `ConfigurationService`.
4. The command is routed to the correct service:
   - `UploadService`
   - `DownloadService`
   - `ExtensionService`
5. `DialogService` provides native visual feedback.
6. A structured `OperationResult` is returned.

### Architectural characteristics

- Separation of concerns
- Interface-based service design
- Configuration-driven feature visibility
- Support for both GUI-triggered and CLI-triggered execution
- Graceful degradation if configuration or GUI tools are unavailable

---

## 12. Command Detection and Routing Mechanism

`Program.cs` supports the following commands:

- `upload <path>`
- `download <path>`
- `register`
- `unregister`
- `config`
- `enable upload`
- `disable upload`
- `enable download`
- `disable download`
- `help`

### Mode detection

#### CLI mode

When arguments are provided, the application treats the run as a command-line request and routes directly to the correct service.

#### Nautilus mode

When launched without arguments, the application:

1. Inspects `argv[0]` to determine whether the process was launched as Upload or Download.
2. Falls back to environment variable inspection if needed.
3. Reads:
   - `NAUTILUS_SCRIPT_SELECTED_FILE_PATHS` for file selection
   - `NAUTILUS_SCRIPT_CURRENT_URI` for folder background clicks

This lets the same binary support both file and folder context menu actions.

---

## 13. Upload Implementation

Upload functionality is implemented in `Services/UploadService.cs`.

### Upload workflow

1. Receive the folder path from CLI or Nautilus.
2. Normalize the path:
   - Trim input
   - Convert `file://` URI to local path if needed
   - Resolve to absolute path
3. Validate the directory exists.
4. Open a native file-selection dialog with Zenity.
5. If the user selects a file, show a success dialog.
6. Return an `OperationResult`.

### Current behavior

The current project demonstrates upload flow by letting the user select a file from the folder and then showing a confirmation dialog. It does not yet upload the file to a remote API or backend service.

### Important runtime details

- Folder validation uses `Directory.Exists()`.
- The file picker starts in the clicked folder.
- Canceling the dialog is treated as a successful user-driven cancel result rather than a crash.

---

## 14. Download Implementation

Download functionality is implemented in `Services/DownloadService.cs`.

### Download workflow

1. Receive the selected file path.
2. Normalize the path.
3. Extract the filename.
4. Validate that the file exists.
5. Read metadata using `FileInfo`.
6. Show a Zenity dialog with:
   - Filename
   - Extension
   - Size
   - Absolute path
7. Return an `OperationResult`.

### Current behavior

The current project demonstrates a download callback by displaying file information for the selected file. It does not download from a server; instead, it proves that the correct file context is captured from Nautilus.

---

## 15. Dialog Handling Using Zenity

`Services/DialogService.cs` centralizes GUI feedback behavior.

### Supported dialog types

- `ShowInfoDialog()` using `zenity --info`
- `ShowErrorDialog()` using `zenity --error`
- `ShowFileOpenDialog()` using `zenity --file-selection`

### Implementation details

- Uses `ProcessStartInfo` with `UseShellExecute = false`
- Redirects standard error to capture Zenity failures
- Waits for dialog processes to exit
- Falls back to console output if dialog execution fails

### Logging

The dialog service also contains an internal static logging helper used throughout the app, especially for GUI-triggered runs where terminal output may not be visible.

---

## 16. Configuration Management

Configuration is handled through `Services/ConfigurationService.cs`.

### Configuration behavior

- Searches for `appsettings.json` in:
  1. The executable directory
  2. The current working directory

- If no config file is found or parsing fails:
  - Upload defaults to enabled
  - Download defaults to enabled

### Save behavior

When commands like `enable upload` or `disable download` are used, the configuration service writes updates to:

1. The current working directory copy
2. The installed application directory copy, if available

This ensures the change is reflected both during development and after installation.

---

## 17. Nautilus Extension Registration and Deployment

Extension registration is handled by `Services/ExtensionService.cs`.

### Registration flow

1. Remove older extension copies and script remnants.
2. Publish the C# app as a self-contained Linux binary.
3. Install the binary into `~/.local/share/context-menu-app/`.
4. Deploy `appsettings.json` beside the binary.
5. Write the Python bridge into the Nautilus extension directory.
6. Restart Nautilus.

### Unregistration flow

1. Run cleanup logic against known extension locations.
2. Remove user-level and system-level remnants where possible.
3. Restart Nautilus.

### Why cleanup is important

Nautilus can load extensions from more than one path. If old files remain, duplicate menu items may appear. The project explicitly removes old Python bridge files, old scripts, some compiled extension names, and `__pycache__` folders to avoid duplicate registrations.

---

## 18. Installation and Uninstallation Scripts

The `Scripts` folder contains Linux shell scripts for lifecycle management.

### `Scripts/install.sh`

This script:

1. Checks required dependencies.
2. Publishes the .NET project as a self-contained binary.
3. Installs the binary to the user-local app directory.
4. Installs the Python Nautilus bridge.
5. Cleans old script-based menu entries.
6. Restarts Nautilus.

### `Scripts/uninstall.sh`

This script:

1. Scans for matching extension files.
2. Removes user-level Python extension files.
3. Removes old Nautilus script entries.
4. Attempts to remove system-level Python and compiled extension files.
5. Removes Python cache folders.
6. Removes the installed app directory.
7. Restarts Nautilus.

These scripts are useful for repeatable installation and troubleshooting in Linux desktop environments.

---

## 19. Logging and Debugging Support

The project includes lightweight logging support through `DialogService.Log()`.

### Log file behavior

- The log file is written to the user’s home directory when possible.
- It captures startup information, dialog execution details, configuration load/save information, cleanup actions, and unhandled exceptions.

### Why logging is needed

When the app is launched from Nautilus, users typically do not see a terminal window. File-based logging provides a practical way to inspect runtime problems.

---

## 20. Testing and Demonstration Procedure

The application supports direct CLI testing without Nautilus registration.

### Build test

```bash
dotnet build
```

### Upload flow test

```bash
dotnet run -- upload "/home/$USER/Documents"
```

Expected result:

- The folder path is normalized and validated.
- A file picker dialog opens in the target folder.
- Selecting a file shows an upload success dialog.

### Download flow test

```bash
dotnet run -- download "/home/$USER/Documents/test.txt"
```

Expected result:

- The selected file is validated.
- File name and metadata are shown in an info dialog.

### Configuration test

```bash
dotnet run -- config
dotnet run -- disable upload
dotnet run -- enable upload
dotnet run -- disable download
dotnet run -- enable download
```

### Extension test

```bash
dotnet run -- register
```

After registration:

1. Open Nautilus.
2. Right-click on empty space and check for `Upload`.
3. Right-click on a file and check for `Download`.

---

## 21. Error Handling and Exception Management

The application includes layered error handling.

### Covered error scenarios

- Invalid command-line usage
- Missing or empty Nautilus environment variables
- Missing directory for upload flow
- Missing file for download flow
- Missing or invalid configuration file
- Zenity execution failures
- Publish or install failures during registration
- Unhandled application exceptions

### Strategy

- Service-level validation returns structured `OperationResult` values.
- Dialog-based error messages are shown to the user where possible.
- A global `try/catch` in `Main()` logs fatal exceptions.
- Configuration falls back to safe defaults when reading fails.

This helps the application remain usable even when parts of the environment are misconfigured.

---

## 22. Security and Safety Considerations

Although the project is not a security product, it includes several safety-focused design decisions.

### Safety measures in the current implementation

- Upload and Download visibility is controlled by configuration flags.
- Input paths are normalized before use.
- File and directory existence checks are performed before operations continue.
- The Python bridge contains minimal logic and delegates behavior to the C# binary.
- Configuration reading in the Nautilus extension uses defaults when parsing fails.
- Dialog command arguments escape quotes before launching Zenity.

### Operational considerations

- Registration and cleanup may invoke privileged commands through shell scripts or `sudo`, depending on system state.
- The uninstall and cleanup routines intentionally remove old extension files from known locations to prevent duplicate menu entries.

---

## 23. Troubleshooting Guide

### Problem: Upload or Download menu item is not visible

Possible checks:

- Verify `python3-nautilus` is installed.
- Verify the extension exists in `~/.local/share/nautilus-python/extensions/`.
- Check `appsettings.json` and confirm the feature is enabled.
- Restart Nautilus with:

```bash
nautilus -q
```

### Problem: Duplicate menu items appear

Possible cause:

- Old extension files still exist in user-level or system-level Nautilus extension paths.

Recommended action:

```bash
dotnet run -- unregister
```

Or use the provided uninstall script to remove older copies and cache files.

### Problem: Dialogs do not open

Possible checks:

- Verify Zenity is installed:

```bash
which zenity
```

- Confirm the Linux desktop session has GUI access and `DISPLAY` is set.

### Problem: Configuration changes do not apply

Possible checks:

- Ensure the installed copy of `appsettings.json` exists beside the installed binary.
- Use `dotnet run -- config` to verify current flags.
- Re-register the extension if deployment files are out of sync.

### Problem: Register command fails

Possible checks:

- Confirm the .NET SDK is installed.
- Confirm Linux publish for `linux-x64` is supported in the current environment.
- Check the log file for publish or filesystem errors.

---

## 24. Conclusion

The **Context Menu App** successfully demonstrates how a Linux desktop file manager can be extended using a hybrid design where **Python is used only for Nautilus integration** and **C# contains the core application logic**.

The project provides:

- A clean service-based architecture
- Config-driven menu visibility
- Native GUI feedback through Zenity
- CLI support for testing and administration
- Automated registration and cleanup workflows
- A practical bridge between Linux desktop events and managed C# code

From a technical design perspective, the project is a good example of combining:

- Linux desktop extension points
- .NET console application design
- interface-based service abstraction
- configuration-driven behavior
- shell automation for deployment

Overall, the implementation achieves its primary goal of adding **Upload** and **Download** actions to the Nautilus context menu while keeping the design modular, understandable, and maintainable.
