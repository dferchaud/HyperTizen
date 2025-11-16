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

### Status: Active Development

This fork is focused on implementing screen capture functionality for **Tizen 8.0+ TVs**.

**✅ Pixel Sampling Capture Method**: Now **IMPLEMENTED** using `libvideoenhance.so`
- Samples 16 pixels from screen edges for ambient lighting
- Converts 10-bit RGB to NV12 format for FlatBuffers transmission
- Supports both Tizen 6 and Tizen 7+ API variants
- Requires hardware testing to verify color accuracy and coordinate mapping

**⚠️ Other Capture Methods**: T8SDK and T7SDK remain as scaffolding (not yet implemented)

**Capture Architecture:** HyperTizen uses a systematic `ICaptureMethod` interface with automatic fallback. The `CaptureMethodSelector` tests available methods on startup (T8SDK → T7SDK → PixelSampling) and selects the first working method.

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
- **Capture Method Selector**: Tests and selects best available capture method automatically
- **Log Level Filtering**: Client-side filtering in browser (Debug/Info/Warning/Error/Performance)
- **✅ Pixel Sampling Capture**: Full implementation using `libvideoenhance.so`
  - 16-point edge sampling for ambient lighting
  - 10-bit to 8-bit RGB conversion
  - RGB to NV12 color space conversion
  - FlatBuffers integration for HyperHDR/Hyperion
  - **Status**: Code complete, awaiting hardware testing

### Partially Implemented

- **T8SDK Capture Method**: Scaffolding exists, core implementation not yet added
- **T7SDK Capture Method**: Scaffolding exists, core implementation not yet added

### Known Issues & Testing Needed

**Pixel Sampling Method:**
- ⚠️ **Color accuracy**: Original implementation had color detection issues (red→yellow) - needs verification on actual hardware
- ⚠️ **Coordinate mapping**: Original had inverted bottom/left zones - improved bounds checking added but needs testing
- ⚠️ **Performance**: Pixel sampling is slower than frame capture - actual FPS unknown until hardware testing
- ⚠️ **10-bit clamping**: Uses simple clamping (not scaling) from 10-bit to 8-bit - may need adjustment based on testing

### Testing the Pixel Sampling Implementation

To test the pixel sampling capture method on your Tizen 8.0+ TV:

1. **Build and install** the updated HyperTizen package on your TV
2. **Start the service** and monitor via WebSocket logs at `http://<TV_IP>:45678`
3. **Watch for log messages** showing:
   - `PixelSampling: Library found, available`
   - `PixelSampling Test: SUCCESS - Screen: WxH, Points: N`
   - Color values being sampled (10-bit RGB)
4. **Connect to HyperHDR/Hyperion** and verify ambient lighting displays correctly
5. **Test color accuracy**: Display pure colors (red, green, blue) and verify they appear correctly
6. **Test edge mapping**: Move content along edges and verify LEDs respond in correct direction

**What to report:**
- Screen resolution detected
- Number of capture points supported
- Sleep time required by API
- Color accuracy (any red→yellow issues?)
- Coordinate mapping (any inverted edges?)
- Performance (FPS achieved)

### Research Notes on Tizen 8.0+ Capture

- **Standard APIs**: May have different availability on Tizen 8.0+ compared to earlier versions
- **VideoEnhance Library**: `libvideoenhance.so` provides pixel sampling API that works on Tizen 6, 7, and 8+
- **Alternative Methods**: VTable-based frame capture (T8SDK) and legacy APIs (T7SDK) require further research
- **Framework Differences**: Tizen 8.0+ has architectural changes that affect some capture capabilities

---

## Installation

**Note:** The Pixel Sampling capture method is now implemented. Build and install to test on your Tizen 8.0+ TV.

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