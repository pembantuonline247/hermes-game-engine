# 🏪 Hermes Game Engine — Multi-Store Deployment Guide

This guide covers setting up automated publishing to **all target channels**.

---

## 📋 Prerequisites Checklist

| Channel | Account | API/Tool | Cost | Status |
|---------|---------|----------|------|--------|
| **Web Portal** | ✅ VPS | SSH/deploy.sh | Free | **LIVE** |
| **itch.io** | Need signup | butler CLI | Free | 🔶 Credentials |
| **Steam** | Need signup | steamcmd | ~$100/game | 🔶 Credentials |
| **Epic Games** | Need signup | BuildPatchTool | Free | 🔶 Credentials |
| **Google Play** | Need signup | Fastlane + service acct | $25 one-time | 🔶 Credentials |
| **Apple App Store** | Need signup | Fastlane + API key | $99/yr | 🔶 Credentials |

---

## 1️⃣ Web Portal (games.pembantu.online) ✅ LIVE

```bash
# Manual deploy from local Unity WebGL build
./deploy.sh --game "my-game" --build "./Builds/WebGL"
```

**Auto-deploy via GitHub Actions:** Already configured. On every push to `main`, WebGL builds auto-deploy to `/opt/games/builds/{commit-sha}/`.

---

## 2️⃣ itch.io

### Setup
1. Create account: https://itch.io/register
2. Go to https://itch.io/user/settings/api-keys → Generate API key
3. Add to GitHub Secrets: `BUTLER_API_KEY`

### Publish
The `Pipelines/butler.sh` script is ready:
```bash
# Upload game to itch.io
Pipelines/butler.sh --game-slug "my-game" --user "your-itch-user"
```

### Auto in CI
Add this job to `.github/workflows/unity-build.yml`:
```yaml
deploy-itchio:
  runs-on: ubuntu-latest
  needs: [build]
  if: needs.build.result == 'success'
  steps:
    - uses: actions/download-artifact@v4
      with: { name: hermes-webgl, path: build }
    - name: Upload to itch.io
      run: |
        curl -L -o butler.zip https://broth.itch.ovh/butler/linux-amd64/LATEST
        unzip butler.zip && chmod +x butler
        ./butler push build ${{ secrets.BUTLER_USER }}/${{ secrets.BUTLER_GAME }}:html5
```

---

## 3️⃣ Steam

### Setup
1. Create Steamworks account: https://partner.steamgames.com/
2. Create new app → Get App ID
3. Install Steam SDK → configure `steam_appid.txt`
4. Add to GitHub Secrets: `STEAM_USERNAME`, `STEAM_PASSWORD`, `STEAM_APP_ID`

### Build Script
The `Pipelines/steam.vdf` template is ready. Configure with your app ID:
```bash
steamcmd +login $STEAM_USERNAME $STEAM_PASSWORD +run_app_build Pipelines/steam.vdf +quit
```

---

## 4️⃣ Epic Games Store

### Setup
1. Create Epic Dev Portal account: https://dev.epicgames.com/
2. Create Organization → Get Org ID
3. Create Product → Get Product ID
4. Download BuildPatchTool from Epic Dev Portal
5. Add to GitHub Secrets: `EPIC_ORG_ID`, `EPIC_PRODUCT_ID`

### Publish
```bash
BuildPatchTool.exe -OrganizationId=$EPIC_ORG_ID \
  -ProductId=$EPIC_PRODUCT_ID \
  -BuildRoot=./Builds/Windows \
  -BuildVersion=$(git describe --tags) \
  -CloudDir=./EpicBuildOutput
```

---

## 5️⃣ Google Play Store

### Setup
1. Create Google Play Console account ($25): https://play.google.com/console/
2. Create service account → download JSON key
3. Upload first APK manually (initial setup)
4. Add to GitHub Secrets: `GOOGLE_SVC_ACCT` (full JSON content)

### Fastlane Setup
```bash
# Install fastlane
gem install fastlane

# Init in project
cd Pipelines/Fastlane
fastlane init
```

### Publish
```bash
cd Pipelines/Fastlane
fastlane android deploy
```

---

## 6️⃣ Apple App Store

### Setup
1. Join Apple Developer Program ($99/yr): https://developer.apple.com/programs/
2. Create App Store Connect API key
3. Generate iOS distribution certificate
4. Add to GitHub Secrets: `APPLE_API_KEY`, `APPLE_ISSUER_ID`

### Fastlane Setup
```bash
cd Pipelines/Fastlane
fastlane ios init
```

### Publish
```bash
cd Pipelines/Fastlane
fastlane ios deploy
```

---

## 🔐 Setting GitHub Secrets

Go to: **GitHub repo → Settings → Secrets and variables → Actions → New repository secret**

### Secrets already set ✅
| Name | Value |
|------|-------|
| `VPS_HOST` | `103.40.207.193` |
| `VPS_USER` | `rocky` |
| `VPS_SSH_KEY` | Private SSH key to deploy to web portal |
| `CLOUDFLARE_TOKEN` | Cloudflare API token for DNS |

### Secrets you need to add 🔶
| Name | Where to get it |
|------|-----------------|
| `UNITY_USERNAME` | Your Unity account email |
| `UNITY_PASSWORD` | Your Unity account password |
| `UNITY_LICENSE` | Unity license XML (see Unity → Manage License) |
| `BUTLER_API_KEY` | https://itch.io/user/settings/api-keys |
| `BUTLER_USER` | Your itch.io username |
| `BUTLER_GAME` | Your itch.io game slug |
| `STEAM_USERNAME` | Your Steamworks login |
| `STEAM_PASSWORD` | Your Steamworks password |
| `STEAM_APP_ID` | Your Steam app numeric ID |
| `EPIC_ORG_ID` | Epic Dev Portal → Organization |
| `EPIC_PRODUCT_ID` | Epic Dev Portal → Product |
| `GOOGLE_SVC_ACCT` | Google Play Console → Service Account JSON |
| `APPLE_API_KEY` | App Store Connect → API Key |
| `APPLE_ISSUER_ID` | App Store Connect → Issuer ID |

---

## 🚀 Quickstart: Full Pipeline Flow

```
Unity Editor (local) 
  → Push code to GitHub main
  → GitHub Actions builds WebGL + Windows + Android
  → WebGL deploys to games.pembantu.online/builds/{sha}/
  → itch.io/Steam/Epic/Google/Apple (when creds added)
  → Cloudflare cache purged
  → Status reported back to you
```