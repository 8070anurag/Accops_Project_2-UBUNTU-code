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
