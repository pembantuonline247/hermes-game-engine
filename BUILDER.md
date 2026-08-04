# 🏗️ Hermes Game Engine — Builder Guide

> CI/CD, deployment, and build pipeline documentation for the Hermes Game
> Engine. This guide covers everything you need to go from a Unity project
> to a live, playable game on the Hermes portal.

---

## Table of Contents

- [Pipeline Overview](#pipeline-overview)
- [GitHub Actions — Automated Builds](#github-actions--automated-builds)
  - [Manual Trigger (workflow_dispatch)](#manual-trigger)
  - [Secrets Needed](#secrets-needed)
- [Local Deployment — deploy.sh](#local-deployment--deploysh)
- [Quickstart: Unity Project → Build → Deploy](#quickstart)
- [Troubleshooting](#troubleshooting)

---

## Pipeline Overview

```
┌──────────────┐     ┌───────────────┐     ┌──────────────────┐
│  Unity Editor │     │  GitHub        │     │   Production VPS  │
│  (Local dev)  │     │  Actions       │     │   103.40.207.193  │
└──────┬───────┘     └───────┬───────┘     └────────┬─────────┘
       │                     │                       │
       │  Push to main       │                       │
       │────────────────────►│                       │
       │                     │                       │
       │         ┌───────────┴───────────┐           │
       │         │  Matrix Build         │           │
       │         │  ┌─────────────────┐  │           │
       │         │  │ WebGL           │  │           │
       │         │  │ StandaloneWin64 │  │           │
       │         │  │ Android         │  │           │
       │         │  └─────────────────┘  │           │
       │         │                       │           │
       │         │  Artifacts uploaded   │           │
       │         └───────────────────────┘           │
       │                     │                       │
       │  ./deploy.sh        │                       │
       │  (manual) ──────────┼──────────────────────►│
       │                     │                       │
       │                     │  (future: auto-CD)    │
       │                     │──────────────────────►│
       │                     │                       │
       │                     │              ┌────────┴────────┐
       │                     │              │ /opt/games/     │
       │                     │              │ builds/{game}/  │
       │                     │              │ portal/         │
       │                     │              └─────────────────┘
```

### Flow Description

1. **Develop** locally in the Unity Editor.
2. **Push** to `main` or `master` on GitHub — or trigger a **manual build** via
   `workflow_dispatch`.
3. **GitHub Actions** runs a matrix build (WebGL / Windows / Android), caches
   the `Library/` folder for speed, activates a Unity license, executes the
   build, and uploads artifacts.
4. **Deploy** the WebGL build to the production VPS using `./deploy.sh`
   (currently manual; can be automated in a future CD step).
5. **Play** — the game is live at the Hermes portal URL.

---

## GitHub Actions — Automated Builds

The workflow file is located at:
```
.github/workflows/unity-build.yml
```

### Triggers

| Trigger | Description |
|---------|-------------|
| `push` to `main` / `master` | Automatic build on every commit |
| `workflow_dispatch` (manual) | Trigger from the GitHub UI with optional overrides |

### Manual Trigger

1. Go to your repository on GitHub.
2. Click the **Actions** tab.
3. Select **"Unity Build Pipeline"** from the left sidebar.
4. Click the **"Run workflow"** dropdown button.
5. (Optional) Override the target platform or add extra build options.
6. Click **"Run workflow"**.

The workflow will:
- Build **WebGL**, **StandaloneWindows64**, and **Android** in parallel (or
  just the platform you selected manually).
- Upload each build as an artifact.
- For **WebGL**, also upload a `.zip` file named `hermes-webgl-downloadable`
  for easy local testing.

### Secrets Needed

These must be configured in your GitHub repository under:
**Settings → Secrets and variables → Actions → Repository secrets**.

| Secret | Description |
|--------|-------------|
| `UNITY_LICENSE` | Your Unity Personal/Plus/Pro license as a base64-encoded `.ulf` file. Get it via `unity-linux` CLI: `unity-editor -batchmode -createManualActivationFile` |
| `UNITY_USERNAME` | Unity account email address |
| `UNITY_PASSWORD` | Unity account password (use a machine-readable account if possible; enable "save password" on the account) |
| `CLOUDFLARE_TOKEN` | *(Reserved for future)* API token for Cloudflare deployment/DDNS updates |
| `VPS_SSH_KEY` | Private SSH key (the content of `~/.ssh/dealpulse_key`) to connect to the production VPS |
| `VPS_HOST` | VPS IP or hostname (`103.40.207.193`) |
| `VPS_USER` | SSH user (`rocky`) |

> ⚠️ **Unity License Note:** The `game-ci/unity-builder` action handles
> activation/deactivation automatically for most cases. However, if you
> encounter license activation limits, set up a separate Unity ID with a
> "machine account" approach (no 2FA) for CI.

### Artifacts Produced

After a successful run, download from the workflow summary page:

| Artifact | Platform | Contents |
|----------|----------|----------|
| `hermes-webgl` | WebGL | Full WebGL build folder |
| `hermes-webgl-downloadable` | WebGL | `.zip` of the WebGL build (one-click download) |
| `hermes-windows64` | StandaloneWindows64 | Windows executable + data |
| `hermes-android` | Android | `.apk` or `.aab` (depending on project settings) |

---

## Local Deployment — deploy.sh

The `deploy.sh` script uploads a local WebGL build to the production VPS and
handles permissions. It is intended to be run **from your local machine** or
from a CI runner.

### Prerequisites

- SSH key at `~/.ssh/dealpulse_key` (or set `SSH_KEY` env var)
- `scp` and `ssh` installed (present on macOS, Linux, and Git Bash on Windows)
- Network access to `103.40.207.193`

### Usage

```bash
# Deploy a game build
./deploy.sh --game my-game --build ./Builds/WebGL

# Deploy and also update the portal landing page
./deploy.sh --game my-game --build ./Builds/WebGL --update-portal

# Show help
./deploy.sh --help
```

### Options

| Flag | Required | Description |
|------|----------|-------------|
| `--game NAME` | ✅ | Game name — used as the subdirectory on the VPS (`/opt/games/builds/<NAME>/`) |
| `--build PATH` | ✅ | Path to the local WebGL build output directory |
| `--update-portal` | ❌ | Also copy `index.html` + assets to the portal directory (`/opt/games/portal/`) so the game becomes the default landing page |
| `--help` | ❌ | Show help text and exit |

### What It Does

1. **Creates** the remote directory `/opt/games/builds/<game-name>/` on the VPS.
2. **SCP** all files from the local build directory to the remote directory.
3. **Fixes permissions** — sets ownership to `www-data` and ensures files are
   readable by nginx.
4. **Optionally updates the portal** — copies `index.html`, `Build/`, and
   `TemplateData/` to `/opt/games/portal/`.

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `VPS_HOST` | `103.40.207.193` | VPS IP/hostname |
| `VPS_USER` | `rocky` | SSH user |
| `SSH_KEY` | `~/.ssh/dealpulse_key` | Path to SSH private key |

---

## Quickstart

> 🎯 **From Unity project → Build → Live on the portal in 5 steps.**

### Step 1: Set Up Secrets

Go to your GitHub repository → **Settings → Secrets and variables → Actions**
and add the secrets listed [above](#secrets-needed).

### Step 2: Push to Main

```bash
git add .
git commit -m "feat: my awesome game"
git push origin main
```

GitHub Actions will automatically trigger a build for all three platforms.

### Step 3: Download the WebGL Artifact

1. Go to the **Actions** tab on GitHub.
2. Click the latest successful workflow run.
3. Download the `hermes-webgl-downloadable` artifact (a `.zip` file).

### Step 4: Deploy to VPS

```bash
# Extract if needed, then run
./deploy.sh --game my-game --build ./Builds/WebGL --update-portal
```

### Step 5: Verify

Visit the Hermes portal URL in your browser. Your game should be live!

> 💡 **Local testing:** Before deploying, run the WebGL build locally by
> serving it with a simple HTTP server:
> ```bash
> cd Builds/WebGL
> python3 -m http.server 8000
> # Open http://localhost:8000 in your browser
> ```

---

## Troubleshooting

### Unity License Activation Fails

- Ensure `UNITY_LICENSE` is a **valid, base64-encoded** `.ulf` file.
- Unity has a **concurrent activation limit** (2 for Personal). Deactivate old
  licenses manually or use a dedicated CI account.
- If using 2FA on your Unity account, create a machine account without 2FA for
  CI.

### "Permission denied" When Running deploy.sh

- Ensure `~/.ssh/dealpulse_key` exists and has permissions `600`:
  ```bash
  chmod 600 ~/.ssh/dealpulse_key
  ```
- The first time you connect, you may need to accept the host key:
  ```bash
  ssh -i ~/.ssh/dealpulse_key -o StrictHostKeyChecking=no rocky@103.40.207.193
  ```

### Build Artifact is Empty

- Check that the Unity build method name in the workflow matches the method in
  your C# editor script exactly.
- Verify `UNITY_VERSION` in the workflow matches your project's Unity version.

### WebGL Build Doesn't Load in Browser

- Open the browser **Developer Console** (F12) and check for errors.
- Common issues: missing `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy`
  headers for threaded WebGL builds.
- If using threading, configure your web server to send:
  ```
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
  ```

---

## Future Automation

The following are planned but not yet implemented:

- **Auto-deploy** on successful WebGL build (CD step in GitHub Actions)
- **Rollback** support (`deploy.sh --rollback <game> <version>`)
- **Cloudflare** cache purging post-deploy
- **Slack/Discord** notifications on build success/failure

---

*Hermes Game Engine — Builder Guide v1.0*