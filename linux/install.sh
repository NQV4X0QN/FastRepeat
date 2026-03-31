#!/usr/bin/env bash
set -euo pipefail

BINARY="target/release/fastrepeat"
INSTALL_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
SERVICE_DIR="$HOME/.config/systemd/user"

echo "Fast Repeat — Linux Installer"
echo ""

# Build if needed
if [ ! -f "$BINARY" ]; then
    echo "Building release binary..."
    cargo build --release
fi

# Install binary
mkdir -p "$INSTALL_DIR"
cp "$BINARY" "$INSTALL_DIR/fastrepeat"
chmod +x "$INSTALL_DIR/fastrepeat"
echo "✓ Installed binary to $INSTALL_DIR/fastrepeat"

# Install desktop file
mkdir -p "$DESKTOP_DIR"
cp fastrepeat.desktop "$DESKTOP_DIR/"
echo "✓ Installed desktop entry"

# Install systemd service
mkdir -p "$SERVICE_DIR"
cp fastrepeat.service "$SERVICE_DIR/"
systemctl --user daemon-reload
echo "✓ Installed systemd user service"

echo ""
echo "Setup complete! Next steps:"
echo ""
echo "  1. Make sure you're in the 'input' group:"
echo "     sudo usermod -aG input \$USER"
echo "     (log out and back in for this to take effect)"
echo ""
echo "  2. Add key bindings:"
echo "     fastrepeat add"
echo ""
echo "  3. Start the daemon:"
echo "     fastrepeat run           # foreground"
echo "     systemctl --user start fastrepeat   # background service"
echo ""
echo "  4. Enable auto-start on login:"
echo "     systemctl --user enable fastrepeat"
echo ""
echo "  5. Check status:"
echo "     fastrepeat status"
echo "     fastrepeat list"
