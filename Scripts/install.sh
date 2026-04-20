#!/bin/bash
# ============================================================
#  Context Menu App — Installation Script (C# + Python Bridge)
# ============================================================
#
# This script installs the Context Menu App on Ubuntu.
#
# Architecture:
#   - C# (100% of app logic): Upload, Download, Dialogs, APIs
#   - Python (10-line bridge): Registers menu items in Nautilus
#
# The Python file contains ZERO business logic. It just tells
# Nautilus "when user clicks Upload/Download, call our C# app."
#
# What this script does:
#   1. Installs python3-nautilus (if not present)
#   2. Checks zenity is available
#   3. Publishes C# app as self-contained binary
#   4. Installs C# binary to ~/.local/share/context-menu-app/
#   5. Installs Python bridge to ~/.local/share/nautilus-python/extensions/
#   6. Cleans up old files from previous installs
#   7. Restarts Nautilus
# ============================================================

set -e  # Exit on error

echo "============================================="
echo "  Context Menu App — Installation"
echo "============================================="
echo ""

# Determine project directory (where install.sh is located)
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
echo "[install] Project directory: $PROJECT_DIR"
echo ""

# --- Step 1 & 2: Install dependencies ---
echo "[Step 1 & 2] Checking dependencies (Python wrapper & zenity)..."

install_dependency() {
    local DEB_PKG=$1
    local RPM_PKG=$2

    if command -v apt-get &> /dev/null; then
        if ! dpkg -s "$DEB_PKG" &>/dev/null; then
            echo "  📦 Installing $DEB_PKG via apt-get..."
            sudo apt-get install -y "$DEB_PKG"
            echo "  ✅ $DEB_PKG installed."
        else
            echo "  ✅ $DEB_PKG already installed."
        fi
    elif command -v dnf &> /dev/null; then
        if ! rpm -q "$RPM_PKG" &>/dev/null; then
            if [ "$RPM_PKG" = "nautilus-python" ]; then
                if ! rpm -q epel-release &>/dev/null; then
                    echo "  📦 Enabling CRB repository..."
                    sudo subscription-manager repos --enable "codeready-builder-for-rhel-9-$(arch)-rpms" || true
                    echo "  📦 Installing EPEL repository..."
                    sudo dnf install -y https://dl.fedoraproject.org/pub/epel/epel-release-latest-9.noarch.rpm || true
                fi
            fi
            echo "  📦 Installing $RPM_PKG via dnf..."
            sudo dnf install -y "$RPM_PKG"
            echo "  ✅ $RPM_PKG installed."
        else
            echo "  ✅ $RPM_PKG already installed."
        fi
    else
        echo "  ⚠️ Cannot determine package manager (neither apt nor dnf). Please ensure $DEB_PKG / $RPM_PKG is installed."
    fi
}

install_dependency "python3-nautilus" "nautilus-python"
install_dependency "zenity" "zenity"
echo ""

# --- Step 3: Publish C# project ---
echo "[Step 3] Publishing .NET project (self-contained, linux-x64)..."

# Publish to /tmp/ because VirtualBox shared folders (vboxsf) do not
# support memory-mapped file I/O, which .NET's single-file bundler requires.
cd "$PROJECT_DIR"
PUBLISH_DIR="/tmp/contextmenu-publish"
rm -rf "$PUBLISH_DIR"
dotnet publish -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$PUBLISH_DIR"

echo "  ✅ Project published to: $PUBLISH_DIR"
echo ""

# --- Step 4: Install C# binary ---
echo "[Step 4] Installing C# binary..."

APP_DIR="$HOME/.local/share/context-menu-app"
mkdir -p "$APP_DIR"

cp "$PUBLISH_DIR/ContextMenuApp" "$APP_DIR/ContextMenuApp"
chmod +x "$APP_DIR/ContextMenuApp"

echo "  ✅ C# binary installed to: $APP_DIR/ContextMenuApp"
echo "  📏 Size: $(du -h "$APP_DIR/ContextMenuApp" | cut -f1)"
echo ""

# --- Step 5: Install Python bridge extension ---
echo "[Step 5] Installing Nautilus Python bridge..."

EXTENSION_DIR="$HOME/.local/share/nautilus-python/extensions"
mkdir -p "$EXTENSION_DIR"

cp "$PROJECT_DIR/NautilusExtension/context_menu_extension.py" \
   "$EXTENSION_DIR/context_menu_extension.py"

echo "  ✅ Python bridge installed to: $EXTENSION_DIR/"
echo ""

# --- Step 6: Clean up old files from previous installs ---
echo "[Step 6] Cleaning up old files..."

NAUTILUS_SCRIPTS_DIR="$HOME/.local/share/nautilus/scripts"
rm -f "$NAUTILUS_SCRIPTS_DIR/ContextMenuApp" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/Upload" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/Download" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/Context Menu" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/test.sh" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/debug.sh" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/📤 Upload" 2>/dev/null
rm -f "$NAUTILUS_SCRIPTS_DIR/📥 Download" 2>/dev/null
rm -rf "$NAUTILUS_SCRIPTS_DIR/.bin" 2>/dev/null

echo "  ✅ Old files cleaned up."
echo ""

# --- Step 7: Restart Nautilus ---
echo "[Step 7] Restarting Nautilus..."

nautilus -q 2>/dev/null || true

echo "  ✅ Nautilus restarted."
echo ""

# --- Done ---
echo "============================================="
echo "  ✅  Installation Complete!"
echo "============================================="
echo ""
echo "  HOW TO USE:"
echo ""
echo "  1. Open the Files app (Nautilus)"
echo ""
echo "  2. Right-click on EMPTY SPACE:"
echo "     → You'll see '📤 Upload'"
echo "     → Click it → Popup shows folder location"
echo ""
echo "  3. Right-click on a FILE:"
echo "     → You'll see '📥 Download'"
echo "     → Click it → Popup shows filename & file info"
echo ""
echo "  CLI TESTING (from terminal):"
echo "    dotnet run --project $PROJECT_DIR -- upload /home/\$USER/Documents"
echo "    dotnet run --project $PROJECT_DIR -- download /home/\$USER/Documents/test.txt"
echo ""
