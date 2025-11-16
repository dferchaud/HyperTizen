# HyperTizen - Tizen 8.0+ Fork

### Color up your Tizen TV with HyperTizen!
HyperTizen is a Hyperion / HyperHDR capturer for Tizen TVs.

---

## ⚠️ Honest Disclaimer

This project **doesn't actually work yet** (or may only partially work). It started with [someone else's excellent work](https://github.com/reisxd/HyperTizen), and most of the "development" was done by burning through way too many AI credits. There's a good chance that half the code is complete gibberish that just *looks* technical. Use at your own risk, and lower your expectations accordingly.

If you somehow find this useful, or just want to support questionable AI-driven development practices:

**[☕ Buy me a coffee](https://ko-fi.com/H2H719VB0U)**

---

<p align="center">
    <a href="https://discord.gg/m2P7v8Y2qR">
       <picture>
           <source height="24px" media="(prefers-color-scheme: dark)" srcset="https://user-images.githubusercontent.com/13122796/178032563-d4e084b7-244e-4358-af50-26bde6dd4996.png" />
           <img height="24px" src="https://user-images.githubusercontent.com/13122796/178032563-d4e084b7-244e-4358-af50-26bde6dd4996.png" />
       </picture>
       </a>
       <a href="https://www.youtube.com/@tizenbrew">
      <picture>
         <source height="24px" media="(prefers-color-scheme: dark)" srcset="https://user-images.githubusercontent.com/13122796/178032714-c51c7492-0666-44ac-99c2-f003a695ab50.png" />
         <img height="24px" src="https://user-images.githubusercontent.com/13122796/178032714-c51c7492-0666-44ac-99c2-f003a695ab50.png" />
     </picture>
     </a>
</p>

---

## About This Fork

This is an **experimental fork** of [HyperTizen](https://github.com/reisxd/HyperTizen) focused on exploring screen capture functionality for **Tizen 8.0+ TVs**. The original HyperTizen uses capture APIs that may have different availability on newer TV models. This fork provides a scaffolding structure for researching and implementing potential capture methods for Tizen 8.0+ compatibility.

### Status: Research & Development

This fork is primarily focused on research and exploration of potential Tizen 8+ capture methods. The current implementation provides **scaffolding and structure** for capture methods, but actual capture functionality is **NOT YET IMPLEMENTED**. This is a starter project to lay the groundwork for future capture method implementations.

**Capture Architecture:** HyperTizen includes a systematic `ICaptureMethod` interface structure designed to support multiple capture approaches. The `CaptureMethodSelector` is configured to test available methods on startup (T8SDK → T7SDK → PixelSampling), but the underlying capture functionality is not operational.

---

## How to Use the WebSocket Log Viewer

This fork includes a **real-time browser-based log viewer** that's essential for debugging on your TV.

### Accessing Logs

1. **Start HyperTizen** on your TV
2. **Open your browser** on any device on the same network
3. **Navigate to:** `http://<YOUR_TV_IP>:45678`
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

The log viewer (`logs.html`) is served automatically when HyperTizen is running. This is particularly useful for debugging capture issues, monitoring performance, and understanding what's happening on the TV without needing SSH or console access.

---

## Browser-Based Control Panel

In addition to logs, HyperTizen provides a **full control panel** accessible from any browser on your network.

### Accessing the Control Panel

1. **Start HyperTizen** on your TV
2. **Open your browser** on any device on the same network
3. **Open the control panel:**
   - **Option 1**: Navigate to `http://<YOUR_TV_IP>:45678/controls.html`
   - **Option 2**: Download `controls.html` from this repository and open it locally, then enter your TV's IP

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

```
http://192.168.1.100:45678/controls.html
```

Or open `controls.html` locally and enter:
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
- **Capture Method Selector**: Tests and selects best available capture method (when implemented)
- **Log Level Filtering**: Client-side filtering in browser (Debug/Info/Warning/Error/Performance)

### Not Yet Implemented

- **Capture Methods**: All capture method implementations (T8SDK, T7SDK, PixelSampling, etc.) are scaffolding only
  - No actual frame or pixel capture is currently functional
  - These are placeholders for future implementation

### Research Notes on Tizen 8.0+ Capture

- **Standard APIs**: May have different availability on Tizen 8.0+ compared to earlier versions
- **Alternative Methods**: Various approaches exist but require implementation and testing
- **Framework Differences**: Tizen 8.0+ has architectural changes that affect capture capabilities

---

## Installation

**Note:** This is a starter project with capture method scaffolding only. Actual capture functionality needs to be implemented before the app can capture video frames.

To install HyperTizen on your Samsung TV running Tizen, you'll need Tizen Studio. You can download it from the [official website](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html).

### Installation Steps

1. Download the latest release from the [releases page](https://github.com/reisxd/HyperTizen/releases/latest) (or build from this fork).

2. Change the Host PC IP address to your PC's IP address by following [this guide](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html#Connecting-the-TV-and-SDK)

3. Install the package:
```bash
tizen install -n path/to/io.gh.reisxd.HyperTizen.tpk
```

Note that `tizen` is in `C:\tizen-studio\tools\ide\bin` on Windows and in `~/tizen-studio/tools/ide/bin` on Linux.

If you get `install failed[118, -12], reason: Check certificate error` error, you'll have to resign the package (see below).

4. Install TizenBrew to your TV. Follow [this guide](https://github.com/reisxd/TizenBrew/blob/main/docs/README.md).

5. **Install the HyperTizen UI** via TizenBrew's GitHub module manager:

   **Using the Module Manager:**
   - Press the **[GREEN]** button on your remote to open TizenBrew module manager
   - Navigate to "Add GitHub Module"
   - Enter the module path:

   **Install from this fork** (Tizen 8+ with pixel sampling):
   ```
   iceteaSA/HyperTizen/HyperTizenUI
   ```

   **Install from original repo** (Tizen 7 only):
   ```
   reisxd/HyperTizen/HyperTizenUI
   ```

   **Format:**
   ```
   <username>/<repository>/<folder-path>
   ```
   - Installs from the default branch (usually `main`)
   - `username/repository` - GitHub repository owner and name
   - `folder-path` - Path to the app folder within the repository

   > **Note:** To test development branches, you'll need to manually update files on your TV or wait for the branch to be merged to main.

### Resigning the Package

1. Change the Host PC IP address to your PC's IP address by following [this guide](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html#Connecting-the-TV-and-SDK)

2. After following the guide for the Tizen Studio installation, you have to create a certificate profile. You can follow [this guide](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html).

3. Sign the package:
```bash
tizen package -t tpk -s YourProfileName -o path/to/output/dir -- path/to/io.gh.reisxd.HyperTizen.tpk

# Example:
# tizen package -t tpk -s HyperTizen -o release -- io.gh.reisxd.HyperTizen.tpk
```

4. You should now be able to install the package.

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