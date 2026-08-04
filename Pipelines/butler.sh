#!/usr/bin/env bash
# =============================================================================
# butler.sh — itch.io push script template for Hermes Game Engine
# =============================================================================
# Uses Butler (https://itch.io/docs/butler/) to push builds to itch.io.
# Usage:
#   chmod +x butler.sh
#   ./butler.sh <platform> [channel]
#
# Examples:
#   ./butler.sh windows
#   ./butler.sh android
#   ./butler.sh webgl
#   ./butler.sh windows beta
#
# Environment variables (set these in CI or locally):
#   BUTLER_API_KEY    — Your itch.io API key (required)
#   ITCH_USER         — Your itch.io username or organisation (required)
#   ITCH_GAME         — Your itch.io game slug (required)
# =============================================================================

set -euo pipefail

# -------------------------------------------------------------------------
# Configuration
# -------------------------------------------------------------------------

BUTLER_API_KEY="${BUTLER_API_KEY:-}"
ITCH_USER="${ITCH_USER:-}"
ITCH_GAME="${ITCH_GAME:-}"

# Default channel names (maps platform argument to itch.io channel)
declare -A CHANNELS
CHANNELS[windows]="windows"
CHANNELS[android]="android"
CHANNELS[webgl]="webgl"
CHANNELS[macos]="macos"
CHANNELS[linux]="linux"

# Build output paths (relative to project root, override via env)
BUILD_ROOT="${BUILD_ROOT:-$(cd "$(dirname "$0")/.." && pwd)/Builds}"

# -------------------------------------------------------------------------
# Pre-flight checks
# -------------------------------------------------------------------------

if [[ -z "$BUTLER_API_KEY" ]]; then
    echo "ERROR: BUTLER_API_KEY environment variable is not set."
    echo "       Get your API key from https://itch.io/user/settings/api-keys"
    exit 1
fi

if [[ -z "$ITCH_USER" ]]; then
    echo "ERROR: ITCH_USER environment variable is not set."
    exit 1
fi

if [[ -z "$ITCH_GAME" ]]; then
    echo "ERROR: ITCH_GAME environment variable is not set."
    exit 1
fi

PLATFORM="${1:-}"
if [[ -z "$PLATFORM" ]]; then
    echo "Usage: $0 <platform> [channel]"
    echo ""
    echo "Platforms: ${!CHANNELS[*]}"
    exit 1
fi

CHANNEL="${2:-${CHANNELS[$PLATFORM]:-}}"
if [[ -z "$CHANNEL" ]]; then
    echo "ERROR: Unknown platform '$PLATFORM'. Valid platforms: ${!CHANNELS[*]}"
    exit 1
fi

BUILD_DIR="${BUILD_ROOT}/${PLATFORM^}"
if [[ ! -d "$BUILD_DIR" ]]; then
    echo "ERROR: Build directory not found: $BUILD_DIR"
    echo "       Make sure you have built the game for $PLATFORM first."
    exit 1
fi

# -------------------------------------------------------------------------
# Push
# -------------------------------------------------------------------------

echo "=============================================="
echo "  itch.io Butler Push"
echo "  User:    $ITCH_USER"
echo "  Game:    $ITCH_GAME"
echo "  Channel: $CHANNEL"
echo "  Source:  $BUILD_DIR"
echo "=============================================="

# Export the API key so butler can pick it up
export BUTLER_API_KEY

butler push \
    --if-changed \
    --userversion-file "$(dirname "$0")/version.txt" \
    "$BUILD_DIR" \
    "$ITCH_USER/$ITCH_GAME:$CHANNEL"

echo ""
echo "✅ Push to itch.io complete!"
echo "   https://$ITCH_USER.itch.io/$ITCH_GAME"