#!/bin/bash
# ============================================================
#  Context Menu App — Full Uninstall Script
# ============================================================
#  Removes ALL traces of the context menu extension from
#  every location Nautilus could possibly load from.
# ============================================================

set -e

echo "============================================="
echo "  Context Menu App — Full Uninstall"
echo "============================================="
echo ""

# --- Step 1: Find ALL extension files on the system ---
echo "[Step 1] Scanning for ALL context_menu_extension files..."
echo ""

echo "  Searching for .py files:"
find / -name "context_menu_extension*" -type f 2>/dev/null | while read f; do
    echo "    FOUND: $f"
done

echo ""
echo "  Searching for .so files:"
find / -name "*context*menu*" -name "*.so" -type f 2>/dev/null | while read f; do
    echo "    FOUND: $f"
done

echo ""

# --- Step 2: Remove user-level Python extension ---
echo "[Step 2] Removing user-level Python extension..."

EXT_FILE="$HOME/.local/share/nautilus-python/extensions/context_menu_extension.py"
if [ -f "$EXT_FILE" ]; then
    rm -f "$EXT_FILE"
    echo "  ✅ Removed: $EXT_FILE"
else
    echo "  ⏭  Not found: $EXT_FILE"
fi
echo ""

# --- Step 3: Remove old Nautilus Script entries ---
echo "[Step 3] Removing old Nautilus Script entries..."

SCRIPTS_DIR="$HOME/.local/share/nautilus/scripts"
rm -f "$SCRIPTS_DIR/ContextMenuApp" 2>/dev/null && echo "  ✅ Removed: ContextMenuApp" || true
rm -f "$SCRIPTS_DIR/Upload" 2>/dev/null && echo "  ✅ Removed: Upload" || true
rm -f "$SCRIPTS_DIR/Download" 2>/dev/null && echo "  ✅ Removed: Download" || true
rm -f "$SCRIPTS_DIR/📤 Upload" 2>/dev/null && echo "  ✅ Removed: 📤 Upload" || true
rm -f "$SCRIPTS_DIR/📥 Download" 2>/dev/null && echo "  ✅ Removed: 📥 Download" || true
rm -f "$SCRIPTS_DIR/Context Menu" 2>/dev/null && echo "  ✅ Removed: Context Menu" || true
rm -f "$SCRIPTS_DIR/test.sh" 2>/dev/null || true
rm -f "$SCRIPTS_DIR/debug.sh" 2>/dev/null || true
rm -rf "$SCRIPTS_DIR/.bin" 2>/dev/null || true
echo ""

# --- Step 4: Remove system-level Python extensions (sudo) ---
echo "[Step 4] Removing system-level Python extensions (needs sudo)..."

SYS_PY="/usr/share/nautilus-python/extensions/context_menu_extension.py"
if [ -f "$SYS_PY" ]; then
    sudo rm -f "$SYS_PY"
    echo "  ✅ Removed: $SYS_PY"
else
    echo "  ⏭  Not found: $SYS_PY"
fi
echo ""

# --- Step 5: Remove compiled .so extensions ---
echo "[Step 5] Removing compiled .so extensions..."

# System-level (needs sudo)
SO_DIRS=(
    "/usr/lib/x86_64-linux-gnu/nautilus/extensions-4"
    "/usr/lib/nautilus/extensions-4"
    "/usr/lib/x86_64-linux-gnu/nautilus/extensions-3.0"
    "/usr/lib/nautilus/extensions-3.0"
)

for dir in "${SO_DIRS[@]}"; do
    for name in libcontextmenu-extension.so libcontext_menu_extension.so context_menu_extension.so context-menu-extension.so; do
        filepath="$dir/$name"
        if [ -f "$filepath" ]; then
            sudo rm -f "$filepath"
            echo "  ✅ Removed: $filepath"
        fi
    done
done

# User-level (~/.local/lib/)
USER_EXT_DIR="$HOME/.local/lib/nautilus/extensions-4"
if [ -d "$USER_EXT_DIR" ]; then
    for name in libcontextmenu-extension.so libcontext_menu_extension.so; do
        filepath="$USER_EXT_DIR/$name"
        if [ -f "$filepath" ]; then
            rm -f "$filepath"
            echo "  ✅ Removed: $filepath"
        fi
    done
fi
echo ""

# --- Step 6: Remove Python __pycache__ (THIS IS THE KEY FIX) ---
# Python compiles .py files into .pyc bytecode and caches them.
# Nautilus loads from __pycache__/ even AFTER the .py file is deleted!
# This is why Upload/Download persist after removing the .py file.
echo "[Step 6] Removing Python __pycache__ directories..."

CACHE_DIRS=(
    "$HOME/.local/share/nautilus-python/extensions/__pycache__"
    "/usr/share/nautilus-python/extensions/__pycache__"
)

for cache_dir in "${CACHE_DIRS[@]}"; do
    if [ -d "$cache_dir" ]; then
        sudo rm -rf "$cache_dir" 2>/dev/null || rm -rf "$cache_dir" 2>/dev/null || true
        echo "  ✅ Removed: $cache_dir"
    else
        echo "  ⏭  Not found: $cache_dir"
    fi
done
echo ""

# --- Step 7: Remove installed C# binary ---
echo "[Step 7] Removing installed C# binary..."

APP_DIR="$HOME/.local/share/context-menu-app"
if [ -d "$APP_DIR" ]; then
    rm -rf "$APP_DIR"
    echo "  ✅ Removed: $APP_DIR"
else
    echo "  ⏭  Not found: $APP_DIR"
fi
echo ""

# --- Step 8: Kill Nautilus ---
echo "[Step 8] Killing Nautilus (GNOME will auto-restart it fresh)..."

nautilus -q 2>/dev/null || true
sleep 2
killall nautilus 2>/dev/null || true

echo "  ✅ Nautilus killed."
echo ""

# --- Step 8: Final scan ---
echo "[Step 8] Final verification — scanning for remaining files..."
echo ""

REMAINING=$(find / -name "context_menu_extension*" -type f 2>/dev/null || true)
if [ -z "$REMAINING" ]; then
    echo "  ✅ CLEAN — No context_menu_extension files found anywhere."
else
    echo "  ⚠️  Still found:"
    echo "$REMAINING" | while read f; do
        echo "    $f"
    done
fi
echo ""

echo "============================================="
echo "  ✅  Full Uninstall Complete!"
echo "============================================="
echo ""
echo "  Right-click in Nautilus now — you should see"
echo "  ZERO Upload/Download entries."
echo ""
echo "  To re-install, run:"
echo "    dotnet run -- register"
echo ""
