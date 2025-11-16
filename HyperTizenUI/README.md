# HyperTizen Control Center UI

**Version:** 1.2.0

A modern, TV-optimized control interface for HyperTizen screen capture on Samsung Tizen TVs. Features real-time monitoring, service control, SSDP device discovery, and live log streaming - all controllable via TV remote.

---

## Features

### 🎮 Remote Control Navigation
- Fully navigable with TV remote (arrow keys, Enter, Back)
- Visual focus indicators with smooth animations
- Optimized for 10-foot TV viewing experience
- Large, readable fonts and high-contrast colors

### 🎨 Visual Feedback
- **Animated rainbow gradient ring** around the screen when capturing (5% width)
- Real-time status indicators
- Color-coded log levels (Debug, Info, Warning, Error, Performance)
- Smooth transitions and animations

### 📊 Service Control
- Start/Stop/Pause/Resume capture
- Restart HyperTizen service
- Real-time service state monitoring
- Frame rate and performance metrics
- Connection status tracking

### 🔍 SSDP Device Discovery
- Automatic discovery of Hyperion/HyperHDR servers on network
- Easy device selection via remote
- Apply selected server with one button press
- Live device count display

### 📜 Live Log Streaming
- Real-time log viewing from HyperTizen service
- Auto-scroll support (toggleable)
- Color-coded log levels for easy reading
- Clear logs button
- Persistent log history

### 📈 Statistics Display
- Service state (Idle, Starting, Capturing, Paused, Stopping, Error)
- Frames captured counter
- Average FPS calculation
- Uptime tracking
- Error count monitoring

---

## Installation

### Via TizenBrew Module Manager (Recommended)

1. **Install TizenBrew** on your Samsung TV following [this guide](https://github.com/reisxd/TizenBrew/blob/main/docs/README.md)

2. **Open Module Manager:**
   - Press **[GREEN]** button on your remote
   - Navigate to "Add GitHub Module"

3. **Enter module path:**
   ```
   iceteaSA/HyperTizen/HyperTizenUI
   ```

4. **Launch the app** from TizenBrew menu

### Manual Installation

If you need to install manually or test development versions:

1. Copy all files from `HyperTizenUI` folder to your TV
2. Install via Tizen Studio or TizenBrew developer mode
3. Launch the app

---

## Usage

### First Launch

1. The UI will automatically attempt to connect to the HyperTizen service
2. If WebSocket connection fails, check that the HyperTizen service is running
3. Use SSDP scan to find your Hyperion/HyperHDR server
4. Select your server and click "Apply Selection"

### Navigation

- **↑/↓ Arrow Keys:** Navigate between buttons and controls
- **Enter:** Activate selected button/control
- **Back:** Exit app (returns to TizenBrew)
- **Menu:** Open TV settings (system function)

### Service Control

- **Start Capture:** Begin screen capture and streaming to Hyperion/HyperHDR
- **Stop Capture:** Stop capturing
- **Pause:** Temporarily pause capture (service stays running)
- **Resume:** Resume from paused state
- **Restart Service:** Full service restart (useful if errors occur)

### SSDP Device Discovery

1. Click **Rescan** button to search for Hyperion/HyperHDR servers
2. Devices will appear in the list below
3. Click a device to select it (highlighted in blue)
4. Click **Apply Selection** to save and use the selected server

### Rainbow Border Indicator

When capture is active, an animated rainbow gradient ring (5% width) appears around the screen edges:
- **Hidden:** Service is not capturing
- **Visible & Animating:** Capture is active
- Cycles through rainbow colors smoothly (3-second rotation)

---

## WebSocket Communication

The UI communicates with the HyperTizen service via WebSocket on two ports:

- **Port 8086:** Control messages (config, SSDP, service control)
- **Port 45678:** Log streaming (real-time logs)

### Supported Control Messages

| Event | ID | Description |
|-------|----|----|
| SetConfig | 0 | Set configuration value |
| ReadConfig | 1 | Read configuration value |
| ReadConfigResult | 2 | Config read response |
| ScanSSDP | 3 | Trigger SSDP scan |
| SSDPScanResult | 4 | SSDP scan results |

---

## Configuration

### Auto-Start Service

The HyperTizen service will automatically start when enabled. You can control this via the UI or by editing preferences on the TV.

### WebSocket Ports

If you need to change the WebSocket ports, edit:
- `js/wsClient.js` - Control WebSocket port (default: 8086)
- Logs connection is hardcoded to port 45678

---

## Troubleshooting

### WebSocket Connection Failed

**Symptoms:** Status shows "WebSocket: Disconnected"

**Solutions:**
1. Ensure HyperTizen service is running on TV
2. Check TV IP address is correct (shown in logs)
3. Restart both service and UI
4. Check TV firewall settings (if applicable)

### No SSDP Devices Found

**Symptoms:** "0 device(s) found" after scanning

**Solutions:**
1. Ensure Hyperion/HyperHDR is running on network
2. Check both devices are on same network/subnet
3. Try manual server configuration if SSDP discovery fails
4. Wait a few seconds and click Rescan again

### Service Won't Start

**Symptoms:** "Start Capture" doesn't begin capture

**Solutions:**
1. Check logs for error messages (look for red entries)
2. Verify Hyperion/HyperHDR server address is correct
3. Restart the HyperTizen service
4. Check that Tizen 8+ pixel sampling initialized (see logs)

### Rainbow Border Not Showing

**Symptoms:** No visual indicator when capturing

**Solutions:**
1. Ensure service is actually capturing (check logs)
2. Hard refresh browser if viewing in TV browser (Ctrl+Shift+R)
3. Check `main.css` loaded correctly (no 404 errors)
4. Try restarting the UI app

---

## Version History

### v1.2.0 (Current)
- Added animated rainbow gradient ring indicator (5% width)
- Improved remote control navigation
- Enhanced focus indicators
- Updated to support pixel sampling fallback
- Better error handling and status reporting

### v1.1.0
- Added service control buttons (Start/Stop/Pause/Resume/Restart)
- Implemented SSDP device discovery
- Added live log streaming
- Real-time statistics display

### v1.0.0
- Initial release
- Basic UI framework
- WebSocket communication

---

## Technical Details

### Technology Stack
- **Frontend:** HTML5, CSS3, JavaScript (ES6+)
- **Styling:** Custom CSS with TV-optimized layouts
- **Communication:** WebSocket API
- **Platform:** Tizen WebAPI (Samsung Smart TV)

### Browser Compatibility
- Samsung Tizen 8.0+ browser
- Modern WebKit-based browsers
- WebSocket support required

### File Structure
```
HyperTizenUI/
├── index.html          # Main UI
├── main.css            # Styling & animations
├── package.json        # TizenBrew module metadata
├── config.xml          # Tizen app configuration
├── icon.png           # App icon
└── js/
    ├── wsClient.js    # WebSocket client logic
    └── service.js     # Service launcher
```

---

## Contributing

This UI is part of the [HyperTizen project](https://github.com/iceteaSA/HyperTizen). To contribute:

1. Fork the repository
2. Make your changes to `HyperTizenUI/` files
3. Test on actual Tizen 8+ TV hardware
4. Submit pull request with clear description

---

## License

Same as HyperTizen project - see main repository.

---

## Support

For issues, questions, or feature requests:
- **GitHub Issues:** https://github.com/iceteaSA/HyperTizen/issues
- **Discord:** Join the Hyperion/TizenBrew community
- **Documentation:** See main HyperTizen README.md

---

**Made for Samsung Tizen 8+ TVs** | Part of the HyperTizen project
