#!/usr/bin/env bash
# =============================================================================
# Hermes Game Engine — Deploy Script
# =============================================================================
# Deploys a Unity WebGL build to the production VPS, updates nginx
# permissions, and optionally updates the portal index.html.
#
# Usage:
#   ./deploy.sh --game my-game --build ./Builds/WebGL
#   ./deploy.sh --game my-game --build ./Builds/WebGL --update-portal
#   ./deploy.sh --help
#
# Requirements:
#   - ssh, scp
#   - VPS SSH key at ~/.ssh/dealpulse_key
#   - Network access to 103.40.207.193
# =============================================================================

set -euo pipefail

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
VPS_HOST="103.40.207.193"
VPS_USER="rocky"
SSH_KEY="$HOME/.ssh/dealpulse_key"
REMOTE_BASE="/opt/games/builds"

# ---------------------------------------------------------------------------
# Help text
# ---------------------------------------------------------------------------
show_help() {
    cat <<EOF
Usage: $(basename "$0") [OPTIONS]

Deploy a Unity WebGL build to the production VPS.

Options:
  --game NAME        (Required) Game name — used as the subdirectory on the VPS.
  --build PATH       (Required) Path to the local WebGL build output directory.
  --update-portal    If set, also copies the build's index.html to the portal
                     directory on the VPS so it becomes the default landing page.
  --help             Show this help message and exit.

Examples:
  ./deploy.sh --game space-shooter --build ./Builds/WebGL
  ./deploy.sh --game space-shooter --build ./Builds/WebGL --update-portal

Environment:
  VPS_HOST     VPS IP/hostname (default: $VPS_HOST)
  VPS_USER     SSH user        (default: $VPS_USER)
  SSH_KEY      SSH key path    (default: $SSH_KEY)
EOF
    exit 0
}

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
GAME_NAME=""
BUILD_PATH=""
UPDATE_PORTAL=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --game)
            GAME_NAME="$2"
            shift 2
            ;;
        --build)
            BUILD_PATH="$2"
            shift 2
            ;;
        --update-portal)
            UPDATE_PORTAL=true
            shift
            ;;
        --help|-h)
            show_help
            ;;
        *)
            echo "❌ Unknown option: $1"
            echo "   Use --help to see valid options."
            exit 1
            ;;
    esac
done

# ---------------------------------------------------------------------------
# Pre-flight validation
# ---------------------------------------------------------------------------
if [[ -z "$GAME_NAME" ]]; then
    echo "❌ Missing required argument: --game NAME"
    echo "   Use --help to see usage."
    exit 1
fi

if [[ -z "$BUILD_PATH" ]]; then
    echo "❌ Missing required argument: --build PATH"
    echo "   Use --help to see usage."
    exit 1
fi

if [[ ! -d "$BUILD_PATH" ]]; then
    echo "❌ Build directory does not exist or is not a directory: $BUILD_PATH"
    exit 1
fi

if [[ ! -f "$SSH_KEY" ]]; then
    echo "❌ SSH key not found at: $SSH_KEY"
    echo "   Ensure ~/.ssh/dealpulse_key exists and has proper permissions (600)."
    exit 1
fi

# Ensure SSH key has correct permissions
chmod 600 "$SSH_KEY"

# ---------------------------------------------------------------------------
# Deploy
# ---------------------------------------------------------------------------
REMOTE_DIR="$REMOTE_BASE/$GAME_NAME"
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║  🚀 Deploying '$GAME_NAME' to $VPS_HOST:$REMOTE_DIR"
echo "╚══════════════════════════════════════════════════════════════╝"

# 1. Create remote directory & SCP build files
echo ""
echo "── Step 1/3: Uploading build to VPS ──"
ssh -i "$SSH_KEY" -o StrictHostKeyChecking=no "$VPS_USER@$VPS_HOST" \
    "mkdir -p '$REMOTE_DIR'"

scp -i "$SSH_KEY" -o StrictHostKeyChecking=no -r "$BUILD_PATH"/* \
    "$VPS_USER@$VPS_HOST:$REMOTE_DIR/"

echo "   ✅ Upload complete."

# 2. Fix nginx permissions on the VPS
echo ""
echo "── Step 2/3: Updating nginx permissions ──"
ssh -i "$SSH_KEY" -o StrictHostKeyChecking=no "$VPS_USER@$VPS_HOST" <<'REMOTESHELL'
    set -e
    DIR="/opt/games/builds"
    if [ -d "$DIR" ]; then
        # Set ownership to www-data (or whichever user nginx runs as)
        sudo chown -R www-data:www-data "$DIR" 2>/dev/null || \
            sudo chown -R "$USER:www-data" "$DIR" 2>/dev/null || true

        # Ensure directories are readable and executable
        find "$DIR" -type d -exec chmod 755 {} \;

        # Ensure files are readable
        find "$DIR" -type f -exec chmod 644 {} \;

        echo "   ✅ Permissions fixed."
    else
        echo "   ⚠️  Directory $DIR does not exist on VPS — skipping chown."
    fi
REMOTESHELL

# 3. (Optional) Update portal index.html
if [[ "$UPDATE_PORTAL" == true ]]; then
    echo ""
    echo "── Step 3/3: Updating portal index.html ──"

    PORTAL_DIR="/opt/games/portal"
    # Copy the build's index.html to a portal landing directory
    # This makes the game appear as the default page on the portal subdomain
    ssh -i "$SSH_KEY" -o StrictHostKeyChecking=no "$VPS_USER@$VPS_HOST" \
        "mkdir -p '$PORTAL_DIR' && \
         cp '$REMOTE_DIR/index.html' '$PORTAL_DIR/index.html' && \
         cp -r '$REMOTE_DIR/Build' '$PORTAL_DIR/Build' 2>/dev/null; \
         cp -r '$REMOTE_DIR/TemplateData' '$PORTAL_DIR/TemplateData' 2>/dev/null; \
         sudo chown -R www-data:www-data '$PORTAL_DIR' 2>/dev/null || true"

    echo "   ✅ Portal updated."
fi

echo ""
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║  ✅ Deployment complete!                                    ║"
echo "║                                                              ║"
echo "║  Game:   $GAME_NAME"
echo "║  VPS:    $VPS_HOST:$REMOTE_DIR"
if [[ "$UPDATE_PORTAL" == true ]]; then
    echo "║  Portal:  Updated"
fi
echo "╚══════════════════════════════════════════════════════════════╝"