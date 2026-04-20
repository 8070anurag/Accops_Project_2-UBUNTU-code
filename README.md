# Context Menu App — Upload & Download API (Pure C#)

A C# .NET 10.0 application that integrates with Ubuntu's **Nautilus (GNOME Files)** file manager to add custom right-click context menu options. **100% C# — no Python required.**

## Features

| Action | Trigger | Callback |
|--------|---------|----------|
| 📤 **Upload** | Right-click on **empty space** → Scripts → 📤 Upload | Shows folder **location** |
| 📥 **Download** | Right-click on a **file** → Scripts → 📥 Download | Shows **filename** |

## How It Works

The app uses Nautilus's built-in **Scripts** feature. Any executable placed in
`~/.local/share/nautilus/scripts/` appears in the right-click → Scripts submenu.

```
Right-click → Scripts → 📤 Upload / 📥 Download
                             │
                             ▼
                   ContextMenuApp (C# binary)
                             │
                   Reads Nautilus environment variables:
                   ├── NAUTILUS_SCRIPT_SELECTED_FILE_PATHS
                   └── NAUTILUS_SCRIPT_CURRENT_URI
                             │
                             ▼
                   Routes to Upload or Download Service
                             │
                             ▼
                   Shows Zenity dialog with result
```

Two **symlinks** (`📤 Upload` and `📥 Download`) point to the same C# binary.
The app detects which symlink launched it by reading its own process name via
`Environment.ProcessPath` (which reads `/proc/self/exe` on Linux).

## Project Structure

```
ContextMenuApp/
├── ContextMenuApp.csproj            # .NET 10.0 project
├── Program.cs                       # Entry point — CLI + Nautilus script mode
├── Models/
│   └── OperationResult.cs           # Shared result model
├── Interfaces/                      # Separate API interfaces
│   ├── IUploadService.cs            # Upload API contract
│   ├── IDownloadService.cs          # Download API contract
│   └── IDialogService.cs            # Dialog API contract
├── Services/                        # API implementations
│   ├── UploadService.cs             # Upload logic + callback
│   ├── DownloadService.cs           # Download logic + callback
│   └── DialogService.cs             # Zenity dialog launcher
└── Scripts/
    └── install.sh                   # Automated deployment
```

## Separate APIs

| API | Interface | Implementation |
|-----|-----------|----------------|
| Upload API | `IUploadService` | `UploadService.cs` |
| Download API | `IDownloadService` | `DownloadService.cs` |
| Dialog API | `IDialogService` | `DialogService.cs` |

## OS Functions Used

| Function | Where Used | Purpose |
|----------|-----------|---------|
| `Environment.ProcessPath` (/proc/self/exe) | Program.cs | Detects symlink name → Upload or Download |
| `Environment.GetEnvironmentVariable()` (getenv) | Program.cs | Reads Nautilus env vars for file/folder paths |
| `NAUTILUS_SCRIPT_SELECTED_FILE_PATHS` | Program.cs | Selected file paths (set by Nautilus) |
| `NAUTILUS_SCRIPT_CURRENT_URI` | Program.cs | Current folder URI (set by Nautilus) |
| `Process.Start()` (fork+execvp) | DialogService.cs | Launches Zenity popup |
| `Path.GetFileName()` | DownloadService.cs | Extracts filename from path |
| `Directory.Exists()` (stat) | UploadService.cs | Validates folder exists |
| `File.Exists()` (stat) | DownloadService.cs | Validates file exists |
| `FileInfo` (stat) | DownloadService.cs | Gets file size & metadata |
| `Uri.LocalPath` | Both services | Converts file:// URI to path |
| `chmod +x` | install.sh | Sets execute permission |
| `ln -sf` (symlink) | install.sh | Creates named symlinks for Nautilus |
| `zenity --info` | DialogService.cs | Native GNOME popup dialog |

## Prerequisites

- Ubuntu 24.04 LTS
- .NET SDK 10.0
- zenity (pre-installed on GNOME)

## Installation

```bash
# Clone the repo
git clone https://github.com/YOUR_USERNAME/ContextMenuApp.git
cd ContextMenuApp

# Run the installer
chmod +x Scripts/install.sh
./Scripts/install.sh
```

The installer will:
1. Build the C# project as a self-contained Linux executable
2. Copy it to `~/.local/share/nautilus/scripts/`
3. Create symlinks: `📤 Upload` and `📥 Download`
4. Restart Nautilus

## Manual Testing (CLI Mode)

```bash
# Test Download API
dotnet run -- download "/home/$USER/Documents/test.txt"

# Test Upload API
dotnet run -- upload "/home/$USER/Documents"
```

## Usage

1. Open **Files** (Nautilus)
2. Right-click on a **file** → **Scripts** → **📥 Download** → Popup shows filename
3. Right-click on **empty space** → **Scripts** → **📤 Upload** → Popup shows location
