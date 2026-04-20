using System;
using System.Diagnostics;
using System.IO;
using ContextMenuApp.Interfaces;

namespace ContextMenuApp.Services
{
    /// <summary>
    /// Service responsible for managing the OS integration of the application.
    /// Handles installing and uninstalling the Nautilus Python bridge extension.
    ///
    /// === WHY DUPLICATES HAPPEN ===
    ///
    /// Nautilus loads extensions from MULTIPLE locations simultaneously:
    ///   1. ~/.local/share/nautilus-python/extensions/*.py  (user-level Python)
    ///   2. /usr/share/nautilus-python/extensions/*.py      (system-level Python)
    ///   3. /usr/lib/.../nautilus/extensions-4/*.so         (system-level compiled C)
    ///   4. ~/.local/share/nautilus/scripts/*               (Nautilus Scripts)
    ///
    /// If the same menu items are registered from more than one location,
    /// they appear as duplicates in the right-click menu.
    ///
    /// This service ensures ALL old copies are removed BEFORE installing
    /// a single clean copy, preventing any duplicates.
    /// </summary>
    public class ExtensionService : IExtensionService
    {
        private readonly IDialogService _dialogService;
        private readonly string _pythonBridgeFileName = "context_menu_extension.py";

        public ExtensionService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public OperationResult RegisterExtension()
        {
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string extensionDir = Path.Combine(homeDir, ".local", "share", "nautilus-python", "extensions");
                string destinationPath = Path.Combine(extensionDir, _pythonBridgeFileName);
                string appDir = Path.Combine(homeDir, ".local", "share", "context-menu-app");

                // Step 1: Remove ALL old extensions from EVERY location first
                RunCleanupScript(homeDir);

                // Step 2: Publish and install the C# binary
                PublishAndInstallBinary(appDir);

                // Step 3: Deploy appsettings.json to the install directory
                DeployConfigFile(appDir);

                // Step 4: Create extension directory if needed
                if (!Directory.Exists(extensionDir))
                {
                    Directory.CreateDirectory(extensionDir);
                }

                // Step 5: Write our single Python bridge extension
                File.WriteAllText(destinationPath, GetPythonExtensionCode());

                // Step 6: Kill and restart Nautilus
                KillNautilus();

                return new OperationResult
                {
                    Success = true,
                    OperationType = "Register",
                    CallbackData = destinationPath,
                    Message = "Successfully published binary, deployed config, registered extension, and restarted Nautilus.",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new OperationResult
                {
                    Success = false,
                    OperationType = "Register",
                    CallbackData = "Error",
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                };
            }
        }

        public OperationResult UnregisterExtension()
        {
            try
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Remove ALL extensions from EVERY location
                RunCleanupScript(homeDir);

                // Kill and restart Nautilus aggressively
                KillNautilus();

                return new OperationResult
                {
                    Success = true,
                    OperationType = "Unregister",
                    CallbackData = "All locations",
                    Message = "Successfully unregistered all Nautilus extensions and restarted file manager.",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new OperationResult
                {
                    Success = false,
                    OperationType = "Unregister",
                    CallbackData = "Error",
                    Message = ex.Message,
                    Timestamp = DateTime.Now
                };
            }
        }
        /// <summary>
        /// Publishes the C# project as a self-contained linux-x64 binary
        /// and installs it to the specified app directory.
        ///
        /// This is the binary that the Python bridge calls when the user
        /// clicks Upload or Download in the Nautilus context menu.
        ///
        /// Uses /tmp for publish output because VirtualBox shared folders
        /// (vboxsf) do not support memory-mapped file I/O required by
        /// .NET's single-file bundler.
        /// </summary>
        private void PublishAndInstallBinary(string appDir)
        {
            // Find the project directory (where .csproj is located)
            // We look relative to the current working directory
            string projectDir = Directory.GetCurrentDirectory();
            string publishDir = "/tmp/contextmenu-publish";

            // Step A: Publish the .NET project
            DialogService.Log($"[Register] Publishing project from: {projectDir}");
            using (var publish = new Process())
            {
                publish.StartInfo.FileName = "dotnet";
                publish.StartInfo.Arguments = $"publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o \"{publishDir}\"";
                publish.StartInfo.WorkingDirectory = projectDir;
                publish.StartInfo.UseShellExecute = false;
                publish.Start();
                publish.WaitForExit(120000); // Allow up to 2 minutes for publish

                if (publish.ExitCode != 0)
                {
                    throw new Exception($"dotnet publish failed with exit code {publish.ExitCode}");
                }
            }
            DialogService.Log($"[Register] Published to: {publishDir}");

            // Step B: Copy the binary to the install location
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }

            string sourceBinary = Path.Combine(publishDir, "ContextMenuApp");
            string destBinary = Path.Combine(appDir, "ContextMenuApp");
            File.Copy(sourceBinary, destBinary, overwrite: true);

            // Step C: Make it executable
            using (var chmod = new Process())
            {
                chmod.StartInfo.FileName = "chmod";
                chmod.StartInfo.Arguments = $"+x \"{destBinary}\"";
                chmod.StartInfo.UseShellExecute = false;
                chmod.Start();
                chmod.WaitForExit(3000);
            }
            DialogService.Log($"[Register] Binary installed to: {destBinary}");
        }

        /// <summary>
        /// Runs a comprehensive bash cleanup script that removes ALL known
        /// extension files from every location Nautilus loads from.
        ///
        /// Writes the script to a temp file and executes it so that sudo
        /// can properly prompt for a password in the terminal.
        /// </summary>
        private void RunCleanupScript(string homeDir)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "contextmenu_cleanup.sh");

            string scriptContent = $@"#!/bin/bash
# --- 1. Clean user-level Python extensions ---
rm -f '{homeDir}/.local/share/nautilus-python/extensions/context_menu_extension.py'

# --- 2. Clean old Nautilus Script symlinks ---
rm -f '{homeDir}/.local/share/nautilus/scripts/ContextMenuApp'
rm -f '{homeDir}/.local/share/nautilus/scripts/Upload'
rm -f '{homeDir}/.local/share/nautilus/scripts/Download'
rm -f '{homeDir}/.local/share/nautilus/scripts/📤 Upload'
rm -f '{homeDir}/.local/share/nautilus/scripts/📥 Download'
rm -f '{homeDir}/.local/share/nautilus/scripts/Context Menu'
rm -f '{homeDir}/.local/share/nautilus/scripts/test.sh'
rm -f '{homeDir}/.local/share/nautilus/scripts/debug.sh'
rm -rf '{homeDir}/.local/share/nautilus/scripts/.bin'

# --- 3. Clean system-level Python extensions (sudo) ---
sudo rm -f /usr/share/nautilus-python/extensions/context_menu_extension.py 2>/dev/null || true

# --- 4. Clean compiled C .so extensions (all known names and locations) ---
sudo rm -f /usr/lib/x86_64-linux-gnu/nautilus/extensions-4/libcontextmenu-extension.so 2>/dev/null || true
sudo rm -f /usr/lib/x86_64-linux-gnu/nautilus/extensions-4/libcontext_menu_extension.so 2>/dev/null || true
sudo rm -f /usr/lib/x86_64-linux-gnu/nautilus/extensions-4/context_menu_extension.so 2>/dev/null || true
sudo rm -f /usr/lib/x86_64-linux-gnu/nautilus/extensions-4/context-menu-extension.so 2>/dev/null || true
rm -f '{homeDir}/.local/lib/nautilus/extensions-4/libcontextmenu-extension.so' 2>/dev/null || true
rm -f '{homeDir}/.local/lib/nautilus/extensions-4/libcontext_menu_extension.so' 2>/dev/null || true

# --- 5. Clean Python __pycache__ (compiled .pyc bytecode) ---
rm -rf '{homeDir}/.local/share/nautilus-python/extensions/__pycache__'
sudo rm -rf /usr/share/nautilus-python/extensions/__pycache__ 2>/dev/null || true

echo 'CLEANUP_DONE'
";

            try
            {
                File.WriteAllText(scriptPath, scriptContent);

                using var chmod = new Process();
                chmod.StartInfo.FileName = "/bin/bash";
                chmod.StartInfo.Arguments = $"-c \"chmod +x '{scriptPath}'\"";
                chmod.StartInfo.UseShellExecute = false;
                chmod.Start();
                chmod.WaitForExit(3000);

                using var process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = scriptPath;
                // Do NOT redirect stdin — allow sudo to prompt for password in terminal
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit(30000);

                DialogService.Log($"[Cleanup] Script exited with code: {process.ExitCode}");
            }
            catch (Exception ex)
            {
                DialogService.Log($"[Cleanup] Script failed: {ex.Message}");
            }
            finally
            {
                // Clean up temp script
                try { File.Delete(scriptPath); } catch { }
            }
        }

        /// <summary>
        /// Kills Nautilus aggressively using killall (not just nautilus -q).
        /// nautilus -q sends a polite quit signal, but GNOME session may
        /// immediately respawn it with cached extensions still loaded.
        /// killall -9 forces an immediate kill, and GNOME will respawn
        /// a fresh Nautilus that re-reads extension directories from scratch.
        /// </summary>
        private void KillNautilus()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = "-c \"nautilus -q 2>/dev/null; sleep 1; killall nautilus 2>/dev/null; true\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                process.WaitForExit(10000);
            }
            catch (Exception ex)
            {
                DialogService.Log($"[ExtensionService] Failed to kill nautilus: {ex.Message}");
            }
        }

        /// <summary>
        /// Deploys appsettings.json to the install directory alongside the binary.
        /// This config file is read by the Python bridge to show/hide menu items
        /// and by the C# app as a safety guard.
        ///
        /// Searches for the source config file in:
        ///   1. Current working directory (project root during development)
        ///   2. Same directory as the running executable
        /// </summary>
        private void DeployConfigFile(string appDir)
        {
            string configFileName = "appsettings.json";
            string destConfig = Path.Combine(appDir, configFileName);

            // Try to find the source config file
            string? sourceConfig = null;

            // Location 1: Current working directory (project root)
            string cwdConfig = Path.Combine(Directory.GetCurrentDirectory(), configFileName);
            if (File.Exists(cwdConfig))
            {
                sourceConfig = cwdConfig;
            }
            else
            {
                // Location 2: Same directory as the running executable
                string exeConfig = Path.Combine(AppContext.BaseDirectory, configFileName);
                if (File.Exists(exeConfig))
                {
                    sourceConfig = exeConfig;
                }
            }

            if (sourceConfig != null)
            {
                File.Copy(sourceConfig, destConfig, overwrite: true);
                DialogService.Log($"[Register] Config deployed from {sourceConfig} to {destConfig}");
            }
            else
            {
                // If no config file found, create a default one
                string defaultConfig = """
                {
                  "ContextMenuOptions": {
                    "Upload": true,
                    "Download": true
                  }
                }
                """;
                File.WriteAllText(destConfig, defaultConfig);
                DialogService.Log($"[Register] No source config found. Created default config at {destConfig}");
            }
        }

        private string GetPythonExtensionCode()
        {
            return """"
"""
Nautilus Extension — Python Bridge for C# ContextMenuApp

This file is a thin bridge between Ubuntu's Nautilus file manager and our
C# application. It contains ZERO business logic — it only registers menu
items and calls the C# binary when clicked.

All application logic (Upload, Download, Dialogs) is in C# (100%).

Configuration-Driven Visibility:
  The extension synchronously calls the C# binary to ask for visibility 
  permission using `check-visibility-upload` and `check-visibility-download`.
  This guarantees that all complex permission logic (database queries, 
  extension formatting, etc.) runs inside compiled C#.
"""
import subprocess
import os
from gi.repository import Nautilus, GObject  # type: ignore

# Path to the C# self-contained binary
BINARY_PATH = os.path.expanduser("~/.local/share/context-menu-app/ContextMenuApp")

class ContextMenuExtension(GObject.GObject, Nautilus.MenuProvider):
    def get_background_items(self, *args):
        folder = args[-1] if args else None
        if folder is None:
            return []

        folder_path = folder.get_location().get_path()
        if not folder_path:
            folder_path = folder.get_uri()

        # Ask C# app if we should show Upload
        # return code 0 = True (Show), 1 = False (Hide)
        result = subprocess.run([BINARY_PATH, "check-visibility-upload", folder_path], capture_output=True)
        if result.returncode != 0:
            return []

        item = Nautilus.MenuItem(
            name="ContextMenuApp::Upload",
            label="📤 Upload",
            tip="Upload — show folder location via C# app"
        )
        item.connect("activate", self._on_upload, folder_path)
        return [item]

    def get_file_items(self, *args):
        files = args[-1] if args else []
        if not files:
            return []

        file_path = files[0].get_location().get_path()
        if not file_path:
            return []

        # Ask C# app if we should show Download
        # return code 0 = True (Show), 1 = False (Hide)
        result = subprocess.run([BINARY_PATH, "check-visibility-download", file_path], capture_output=True)
        if result.returncode != 0:
            return []

        item = Nautilus.MenuItem(
            name="ContextMenuApp::Download",
            label="📥 Download",
            tip="Download — show file info via C# app"
        )
        item.connect("activate", self._on_download, file_path)
        return [item]

    def _on_upload(self, menu, folder_path):
        subprocess.Popen([BINARY_PATH, "upload", folder_path])

    def _on_download(self, menu, file_path):
        subprocess.Popen([BINARY_PATH, "download", file_path])
"""";
        }
    }
}
