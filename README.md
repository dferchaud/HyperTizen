# HyperTizen - Fork Multi-Version Tizen

### Color up your Tizen TV with HyperTizen!
HyperTizen is a Hyperion / HyperHDR capturer for Tizen TVs (Tizen 6, 7, 8, and 9+).

---

## ⚠️ Honest Disclaimer

This project **doesn't actually work yet** (or may only partially work). It started with [someone else's excellent work](https://github.com/reisxd/HyperTizen), and most of the "development" was done by burning through way too many AI credits. There's a good chance that half the code is complete gibberish that just *looks* technical. Use at your own risk, and lower your expectations accordingly.

If you somehow find this useful, or just want to support questionable AI-driven development practices:

**[☕ Buy me a coffee/AI credits](https://ko-fi.com/H2H719VB0U)**

---

## About This Fork

This is a fork of [HyperTizen](https://github.com/reisxd/HyperTizen) with extended support for **Tizen 6, 7, 8, and 9+ TVs**. The original HyperTizen primarily supported Tizen 7. This fork implements multiple capture methods with automatic detection to support a wider range of Samsung TV models.

### Status: Functional

This fork supports screen capture on **Tizen 6, 7, 8, and 9+ TVs** with automatic method selection.

**✅ Pixel Sampling Capture Method**: Now **IMPLEMENTED** using `libvideoenhance.so`
- Samples 16 pixels from screen edges for ambient lighting
- Converts 10-bit RGB to NV12 format for FlatBuffers transmission
- Supports both Tizen 6 and Tizen 7+ API variants
- Requires hardware testing to verify color accuracy and coordinate mapping
- Pretty bad performance, but it works! Sorta. Basically takes the dominant color on the screen. And flickering.

**⚠️ Other Capture Methods**: T8SDK and T7SDK remain as scaffolding (not yet implemented)

**Capture Architecture:** HyperTizen uses a systematic `ICaptureMethod` interface with automatic fallback. The `CaptureMethodSelector` tests available methods on startup (T8SDK → T7SDK → PixelSampling) and selects the first working method.

---

## How to Use the WebSocket Log Viewer

This fork includes a **real-time browser-based log viewer** that's essential for debugging on your TV.

### Accessing Logs

1. **Start HyperTizen** on your TV
2. **Open your browser** on any device on the same network
3. **Enter your IP:** `<YOUR_TV_IP>`, `45678`
4. The log viewer will automatically connect and display real-time logs

### Log Viewer Features

- **Real-time streaming**: See logs as they happen
- **Auto-reconnect**: Automatically reconnects if connection is lost
- **Exponential backoff**: Smart retry logic prevents connection spam
- **Color-coded output**: Easy to read and filter
- **Persistent across sessions**: Reconnects when TV restarts HyperTizen

### Finding Your TV's IP Address

You can find your TV's IP address in:
- **Settings** → **General** → **Network** → **Network Status** → **IP Settings**

Or use your router's admin panel to find connected devices.

### Example

```
http://192.168.1.100:45678
```

The log viewer (`logs.html`). This is particularly useful for debugging capture issues, monitoring performance, and understanding what's happening on the TV.

---

## Browser-Based Control Panel

In addition to logs, HyperTizen provides a **full control panel** accessible from any browser on your network.

### Accessing the Control Panel

1. **Start HyperTizen** on your TV
2. **Open on your browser** on any device on the same network
3. **Open the control panel:**
   - Download `controls.html` from this repository and open it locally, then enter your TV's IP

### Control Panel Features

The control panel (`controls.html`) provides the same functionality as the HyperTizenUI but through a standard browser:

**Service Control:**
- ▶️ Start/Stop capture
- ⏸️ Pause/Resume capture
- 🔄 Restart HyperTizen service
- 🌈 Rainbow border indicator when capturing

**SSDP Device Management:**
- 🔍 Scan for Hyperion/HyperHDR devices on your network
- ✓ Select and apply devices
- View device details (name, URL)

**Live Monitoring:**
- 📊 Real-time service status (state, FPS, frames captured, errors)
- 📋 Live log streaming (same as logs.html)
- ⏱️ Uptime and connection status
- 🔌 Dual WebSocket status indicators (control + logs)

**WebSocket Connections:**
- Port **45677**: Control WebSocket (send commands)
- Port **45678**: Logs WebSocket (receive logs)
- Auto-reconnect with exponential backoff
- Persistent settings (saves TV IP in browser)

### Example


Open `controls.html` locally and enter:
```
TV IP: 192.168.1.100
Control Port: 45677
Logs Port: 45678
```

The control panel is perfect for:
- Managing HyperTizen from your phone/tablet/computer
- Testing capture without accessing the TV UI
- Monitoring service status during troubleshooting
- Selecting Hyperion/HyperHDR servers without using the TV remote

---

## What Works (and What Doesn't)

### Implemented & Functional

- **WebSocket Log Streaming**: Real-time debugging via browser (port 45678)
- **Browser-Based Control Panel**: Full service control and monitoring (control port 45677, logs port 45678)
- **Architecture Framework**: Structured `ICaptureMethod` interface with automatic fallback selection
- **System Info Detection**: Detects Tizen version and TV capabilities
- **Capture Method Selector**: Tests and selects best available capture method automatically
- **Log Level Filtering**: Client-side filtering in browser (Debug/Info/Warning/Error/Performance)
- **✅ Pixel Sampling Capture**: Full implementation using `libvideoenhance.so`
  - 16-point edge sampling for ambient lighting
  - 10-bit to 8-bit RGB conversion
  - RGB to NV12 color space conversion
  - FlatBuffers integration for HyperHDR/Hyperion
  - **Status**: Code complete, terrible

### Partially Implemented

- **T8SDK Capture Method**: Scaffolding exists, core implementation not yet added
- **T7SDK Capture Method**: Scaffolding exists, core implementation not yet added

### Known Issues & Testing Needed

**Pixel Sampling Method:**
- ⚠️ **Color accuracy**: Basically takes the dominant color on the screen
- **Flickering**: Random white flicker now and then

### Testing the Pixel Sampling Implementation

To test the pixel sampling capture method on your Tizen 8.0+ TV:

1. **Build and install** the updated HyperTizen package on your TV
2. **Start the service** and monitor via WebSocket logs
3. **Watch for log messages** showing:
   - `PixelSampling: Library found, available`
   - Color values being sampled (10-bit RGB)
4. **Connect to HyperHDR/Hyperion** and verify ambient lighting displays correctly
5. **Test color accuracy**: Display pure colors (red, green, blue) and verify they appear correctly
6. **Test edge mapping**: Move content along edges and verify LEDs respond in correct direction

### Research Notes on Tizen 8.0+ Capture

- **Standard APIs**: May have different availability on Tizen 8.0+ compared to earlier versions
- **VideoEnhance Library**: `libvideoenhance.so` provides pixel sampling API that works on Tizen 6, 7, and 8+
- **Alternative Methods**: VTable-based frame capture (T8SDK) and legacy APIs (T7SDK) require further research
- **Framework Differences**: Tizen 8.0+ has architectural changes that affect some capture capabilities

---

## Compatibilité TV Samsung

### TVs Testées et Confirmées

Ce projet est compatible avec les TVs Samsung suivantes :

#### Tizen 6.0 (2021)
- **Samsung Q80A Series** (confirmé)
  - QE55Q80A ✅
  - Autres modèles Q80A (devrait fonctionner)

#### Tizen 7, 8, 9+
- Compatibilité via détection automatique de la méthode de capture
- Le système teste plusieurs méthodes et sélectionne automatiquement celle qui fonctionne

### Comment vérifier la compatibilité de votre TV

1. **Vérifier votre version Tizen :**
   - Allez dans **Paramètres** → **Support** → **À propos de ce téléviseur**
   - Notez la version du logiciel (contient la version Tizen)

2. **Versions Tizen par année de modèle :**
   - 2021 : Tizen 6.0 (ex: Q80A, Q90A, etc.)
   - 2022 : Tizen 6.5/7.0
   - 2023 : Tizen 7.0/8.0
   - 2024+ : Tizen 8.0/9.0+

### Méthodes de capture supportées

Le projet détecte automatiquement et utilise la meilleure méthode disponible :

| Méthode | Tizen Version | Performance | Description |
|---------|---------------|-------------|-------------|
| **T9 Video Capture** | Tizen 9+ | ⚡⚡⚡ Excellente | Capture vidéo complète (priorité haute) |
| **T9 Display Capture** | Tizen 9+ | ⚡⚡ Bonne | Capture d'affichage alternative |
| **T8 SDK Capture** | Tizen 8 | ⚡ Moyenne | Méthode SDK Tizen 8 |
| **T7 SDK Capture** | Tizen 7 | ⚡ Moyenne | Méthode SDK Tizen 7 |
| **Pixel Sampling** | Tizen 6+ | ⚠️ Basique | Échantillonnage de 16 pixels (fallback universel) |

**Note :** La méthode "Pixel Sampling" fonctionne sur toutes les versions de Tizen mais offre des performances limitées. C'est la méthode qui sera utilisée sur le **QE55Q80A (Tizen 6.0)**.

---

## Installation sur votre TV Samsung

### Prérequis

Avant de commencer, vous aurez besoin de :

1. **Un ordinateur** (Windows, Mac ou Linux)
2. **Tizen Studio** - Téléchargeable sur le [site officiel Samsung](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html)
3. **Votre TV Samsung** connectée au même réseau que votre ordinateur
4. **TizenBrew** installé sur votre TV (voir ci-dessous)

### Étape 1 : Installer TizenBrew sur votre TV

TizenBrew est nécessaire pour exécuter HyperTizen. Suivez le guide complet d'installation :

👉 [Guide d'installation TizenBrew](https://github.com/reisxd/TizenBrew/blob/main/docs/README.md)

### Étape 2 : Préparer Tizen Studio

1. **Télécharger et installer Tizen Studio** depuis le [site Samsung](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html)

2. **Configurer la connexion avec votre TV :**
   - Suivez [ce guide](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html#Connecting-the-TV-and-SDK)
   - Notez l'adresse IP de votre TV (trouvable dans **Paramètres** → **Général** → **Réseau** → **État du réseau**)

3. **Créer un profil de certificat :**
   - Suivez [ce guide](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html)
   - Notez le nom de votre profil (ex: "HyperTizen")

### Étape 3 : Télécharger ou Compiler HyperTizen

**Option A : Télécharger la version précompilée (recommandé)**

Téléchargez le fichier `.tpk` depuis la [page des releases](https://github.com/reisxd/HyperTizen/releases/latest)

**Option B : Compiler depuis les sources**

Voir la section [Building from Source](#building-from-source) ci-dessous.

### Étape 4 : Signer le package (obligatoire)

Le package doit être signé avec vos propres certificats :

```bash
tizen package -t tpk -s VotreNomDeProfil -o dossier/de/sortie -- chemin/vers/io.gh.reisxd.HyperTizen.tpk
```

**Exemple concret :**
```bash
# Windows (depuis PowerShell ou CMD)
cd C:\tizen-studio\tools\ide\bin
.\tizen package -t tpk -s HyperTizen -o C:\HyperTizen\signed -- C:\HyperTizen\io.gh.reisxd.HyperTizen.tpk

# Linux/Mac
cd ~/tizen-studio/tools/ide/bin
./tizen package -t tpk -s HyperTizen -o ~/HyperTizen/signed -- ~/HyperTizen/io.gh.reisxd.HyperTizen.tpk
```

Remplacez :
- `HyperTizen` par le nom de votre profil de certificat
- Les chemins par vos propres chemins

### Étape 5 : Installer sur la TV

```bash
tizen install -n chemin/vers/le/fichier/signé.tpk
```

**Exemple concret :**
```bash
# Windows
cd C:\tizen-studio\tools\ide\bin
.\tizen install -n C:\HyperTizen\signed\io.gh.reisxd.HyperTizen.tpk

# Linux/Mac
cd ~/tizen-studio/tools/ide/bin
./tizen install -n ~/HyperTizen/signed/io.gh.reisxd.HyperTizen.tpk
```

**Note :** La commande `tizen` se trouve dans :
- Windows : `C:\tizen-studio\tools\ide\bin\`
- Linux/Mac : `~/tizen-studio/tools/ide/bin/`

### Étape 6 : Installer l'interface HyperTizen UI

Utilisez le gestionnaire de modules de TizenBrew :

1. **Appuyez sur le bouton [VERT]** de votre télécommande pour ouvrir TizenBrew
2. **Naviguez vers "Add GitHub Module"**
3. **Entrez le chemin du module :**

   ```
   iceteaSA/HyperTizen/HyperTizenUI
   ```

   (Pour ce fork avec support Tizen 6/7/8/9)

4. **Validez** et attendez l'installation

### Étape 7 : Lancer HyperTizen

1. Ouvrez TizenBrew (bouton [VERT])
2. Trouvez et lancez **HyperTizen UI**
3. Configurez votre serveur Hyperion/HyperHDR
4. Démarrez la capture !

### Vérification de l'installation

Pour vérifier que HyperTizen fonctionne correctement sur votre **QE55Q80A** :

1. **Ouvrez les logs dans votre navigateur :**
   ```
   http://IP_DE_VOTRE_TV:45678
   ```

2. **Vous devriez voir :**
   ```
   Current Tizen version: 6.0
   Testing methods in priority order...
   PixelSampling: Library found, available
   PixelSampling Test: SUCCESS
   ✓ SELECTED: Pixel Sampling
   ```

3. **Ouvrez le panneau de contrôle :**
   - Téléchargez `controls.html` de ce dépôt
   - Ouvrez-le dans votre navigateur
   - Entrez l'IP de votre TV
   - Contrôlez HyperTizen depuis votre ordinateur/téléphone

### Résolution de problèmes

**Erreur : `install failed[118, -12], reason: Check certificate error`**
- Vous devez signer le package avec vos certificats (voir Étape 4)

**Erreur : `device not found`**
- Vérifiez que votre TV est bien connectée au réseau
- Vérifiez que vous avez configuré la connexion dans Tizen Studio

**HyperTizen ne démarre pas**
- Vérifiez que TizenBrew est bien installé
- Consultez les logs via le navigateur (port 45678)

**Les captures sont lentes ou saccadées**
- Normal sur Tizen 6 avec la méthode Pixel Sampling
- La qualité dépend des performances de votre TV
- Considérez réduire le nombre de LEDs dans Hyperion/HyperHDR

---

## Building from Source

See the original [HyperTizen documentation](./docs/README.md) for general build instructions.

For this fork, additional development tools may be required for testing and debugging the Tizen 8+ capture methods.

---

## Credits

### Original HyperTizen Project

This fork is based on [HyperTizen by reisxd](https://github.com/reisxd/HyperTizen).

Original HyperTizen provides Hyperion/HyperHDR capture support for Tizen TVs running Tizen 7.0 and earlier firmware versions.

### This Fork

Tizen 8.0+ capture research and implementation by the community. Special thanks to:
- Original HyperTizen contributors for the foundational codebase
- TizenBrew project for enabling homebrew development on Samsung TVs
- Everyone testing and contributing to Tizen 8+ capture research

### Related Projects

- **[HyperTizen](https://github.com/reisxd/HyperTizen)** - Original project (Tizen 7 support)
- **[TizenBrew](https://github.com/reisxd/TizenBrew)** - Homebrew for Samsung Tizen TVs
- **[Hyperion](https://hyperion-project.org/)** - Ambient lighting software
- **[HyperHDR](https://github.com/awawa-dev/HyperHDR)** - HDR-capable fork of Hyperion

---

## Contributing

Contributions are welcome! If you have ideas for implementing capture methods or improving the architecture, please:

1. Review the existing capture method scaffolding in `HyperTizen/Capture/`
2. Test your changes on actual Tizen hardware
3. Submit pull requests with detailed explanations
4. Use the WebSocket log viewer to document behavior and test results

---

## License

Same as original HyperTizen project.

---

## Disclaimer

This is experimental software for research and educational purposes. Use at your own risk. This fork is not affiliated with Samsung or the official Tizen project.

This fork provides scaffolding and structure for exploring capture methods on Tizen 8.0+ TVs. Capture functionality is not yet implemented. Compatibility with specific TV models and firmware versions depends on future implementation and testing.