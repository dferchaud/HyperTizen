// WebSocket client for HyperTizen Control Center
// Handles dual WebSocket connections: control (45677) and logs (45678)

let controlWS = null;
let logsWS = null;
let deviceIP = '';
let logCount = 0;
let ssdpDevices = [];
let selectedDevices = new Set();
let isCapturing = false;
let reconnectAttempts = 0;
const maxReconnectDelay = 30000;

// Event types (must match C# DataTypes.cs)
const Events = {
    SetConfig: 0,
    ReadConfig: 1,
    ReadConfigResult: 2,
    ScanSSDP: 3,
    SSDPScanResult: 4,
    GetLogs: 5,
    LogsResult: 6,
    StatusUpdate: 7,
    PauseCapture: 8,
    ResumeCapture: 9,
    GetStatus: 10,
    StatusResult: 11,
    RestartService: 12
};

// Initialize the application
window.initializeApp = function(ip) {
    deviceIP = ip;
    addLog('Info', `Device IP: ${ip}`);

    // Connect to control WebSocket
    connectControlWS();

    // Connect to logs WebSocket
    connectLogsWS();

    // Set up button handlers
    setupButtonHandlers();

    // Start periodic SSDP scanning
    startPeriodicSSDPScan();

    // Start periodic status updates
    startPeriodicStatusUpdate();
};

// ============================================================================
// WebSocket Connection Management
// ============================================================================

function connectControlWS() {
    const url = `ws://${deviceIP}:45677`;
    addLog('Info', `Connecting to control WebSocket at ${url}...`);
    updateStatus('connectionStatus', 'Connecting...');

    try {
        controlWS = new WebSocket(url);

        controlWS.onopen = function() {
            addLog('Info', 'Control WebSocket connected');
            updateStatus('connectionStatus', 'Connected');
            updateStatus('wsStatus', 'WebSocket: Connected');
            reconnectAttempts = 0;

            // Request initial configuration
            send({ Event: Events.ReadConfig, key: 'enabled' });
            send({ Event: Events.ReadConfig, key: 'rpcServer' });

            // Trigger initial SSDP scan
            send({ Event: Events.ScanSSDP });
        };

        controlWS.onmessage = function(event) {
            try {
                const data = JSON.parse(event.data);
                handleControlMessage(data);
            } catch (err) {
                console.error('Error parsing control message:', err);
            }
        };

        controlWS.onerror = function(error) {
            addLog('Error', 'Control WebSocket error');
            console.error('Control WebSocket error:', error);
        };

        controlWS.onclose = function() {
            addLog('Warning', 'Control WebSocket disconnected');
            updateStatus('connectionStatus', 'Disconnected');
            updateStatus('wsStatus', 'WebSocket: Disconnected');
            scheduleReconnect('control');
        };

    } catch (err) {
        addLog('Error', `Failed to connect to control WebSocket: ${err.message}`);
        scheduleReconnect('control');
    }
}

function connectLogsWS() {
    const url = `ws://${deviceIP}:45678`;
    addLog('Info', `Connecting to logs WebSocket at ${url}...`);

    try {
        logsWS = new WebSocket(url);

        logsWS.onopen = function() {
            addLog('Info', 'Logs WebSocket connected');
        };

        logsWS.onmessage = function(event) {
            const logMessage = event.data;
            parseAndAddLog(logMessage);
        };

        logsWS.onerror = function(error) {
            console.error('Logs WebSocket error:', error);
        };

        logsWS.onclose = function() {
            addLog('Warning', 'Logs WebSocket disconnected');
            scheduleReconnect('logs');
        };

    } catch (err) {
        addLog('Error', `Failed to connect to logs WebSocket: ${err.message}`);
        scheduleReconnect('logs');
    }
}

function scheduleReconnect(type) {
    reconnectAttempts++;
    const delay = Math.min(1000 * Math.pow(2, reconnectAttempts - 1), maxReconnectDelay);

    addLog('Info', `Reconnecting ${type} WebSocket in ${(delay / 1000).toFixed(1)}s... (attempt ${reconnectAttempts})`);

    setTimeout(() => {
        if (type === 'control') {
            connectControlWS();
        } else if (type === 'logs') {
            connectLogsWS();
        }
    }, delay);
}

// ============================================================================
// Message Handling
// ============================================================================

function send(message) {
    if (controlWS && controlWS.readyState === WebSocket.OPEN) {
        controlWS.send(JSON.stringify(message));
    } else {
        addLog('Warning', 'Control WebSocket not connected. Cannot send message.');
    }
}

function handleControlMessage(data) {
    switch (data.Event) {
        case Events.ReadConfigResult:
            handleConfigResult(data);
            break;

        case Events.SSDPScanResult:
            handleSSDPResult(data);
            break;

        case Events.StatusUpdate:
            handleStatusUpdate(data);
            break;

        case Events.LogsResult:
            handleLogsResult(data);
            break;

        case Events.StatusResult:
            handleStatusResult(data);
            break;

        default:
            console.log('Unknown event type:', data.Event, data);
    }
}

function handleConfigResult(data) {
    if (data.error) {
        addLog('Warning', `Config read error for '${data.key}': ${data.value}`);
        return;
    }

    if (data.key === 'enabled') {
        const isEnabled = data.value === 'true' || data.value === true;
        updateCaptureState(isEnabled);
    } else if (data.key === 'rpcServer') {
        addLog('Info', `Current RPC server: ${data.value}`);
        updateStatus('serviceStatus', 'Running');
    }
}

function handleSSDPResult(data) {
    if (!data.devices || !Array.isArray(data.devices)) {
        addLog('Warning', 'Invalid SSDP result format');
        return;
    }

    addLog('Info', `Found ${data.devices.length} SSDP device(s)`);

    // Clear existing devices
    ssdpDevices = [];
    const deviceList = document.getElementById('deviceList');
    deviceList.innerHTML = '';

    if (data.devices.length === 0) {
        deviceList.innerHTML = '<div class="no-devices">No devices found. Click Rescan to try again.</div>';
        document.getElementById('ssdpCount').textContent = '0';
        return;
    }

    // Add new devices
    data.devices.forEach((device, index) => {
        const url = device.UrlBase.indexOf('https') === 0
            ? device.UrlBase.replace('https', 'wss')
            : device.UrlBase.replace('http', 'ws');

        // Avoid duplicates
        if (ssdpDevices.some(d => d.url === url)) {
            return;
        }

        ssdpDevices.push({
            friendlyName: device.FriendlyName,
            url: url,
            urlBase: device.UrlBase
        });

        // Create device item
        const deviceItem = document.createElement('div');
        deviceItem.className = 'device-item';
        deviceItem.setAttribute('tabindex', `${10 + index}`);
        deviceItem.setAttribute('data-url', url);

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.id = `device-${index}`;
        checkbox.checked = selectedDevices.has(url);
        checkbox.onchange = function(e) {
            e.stopPropagation();
            toggleDeviceSelection(url, deviceItem);
        };

        const nameSpan = document.createElement('span');
        nameSpan.className = 'device-name';
        nameSpan.textContent = device.FriendlyName;

        const urlSpan = document.createElement('span');
        urlSpan.className = 'device-url';
        urlSpan.textContent = url;

        deviceItem.appendChild(checkbox);
        deviceItem.appendChild(nameSpan);
        deviceItem.appendChild(urlSpan);

        deviceItem.onclick = function() {
            checkbox.checked = !checkbox.checked;
            toggleDeviceSelection(url, deviceItem);
        };

        if (selectedDevices.has(url)) {
            deviceItem.classList.add('selected');
        }

        deviceList.appendChild(deviceItem);
    });

    document.getElementById('ssdpCount').textContent = ssdpDevices.length.toString();
}

function handleStatusUpdate(data) {
    addLog('Info', `Status: ${data.status} - ${data.message}`);

    // Update UI based on status
    if (data.status === 'capturing') {
        setRainbowBorder(true);
        updateCaptureState(true);
        updateStatus('serviceState', 'Capturing');
    } else if (data.status === 'stopped') {
        setRainbowBorder(false);
        updateCaptureState(false);
        updateStatus('serviceState', 'Stopped');
    } else if (data.status === 'paused') {
        setRainbowBorder(false);
        updateCaptureState(false);
        updateStatus('serviceState', 'Paused');
    }
}

function handleStatusResult(data) {
    addLog('Info', `Status received: ${data.state}, Frames: ${data.framesCaptured}, FPS: ${data.averageFPS}`);

    // Update all status fields
    updateStatus('serviceState', data.state);
    updateStatus('framesCaptured', data.framesCaptured.toLocaleString());
    updateStatus('averageFPS', data.averageFPS.toFixed(1));
    updateStatus('errorCount', data.errorCount.toString());
    updateStatus('uptime', data.uptime);
    updateStatus('connectionStatus', data.isConnected ? 'Connected' : 'Disconnected');

    // Update capture state based on service state
    if (data.state === 'Capturing') {
        setRainbowBorder(true);
        updateCaptureState(true);
    } else if (data.state === 'Paused') {
        setRainbowBorder(false);
        updateCaptureState(false);
    } else {
        setRainbowBorder(false);
        updateCaptureState(false);
    }

    // Update active server highlight if provided
    if (data.activeServerUrl) {
        updateActiveServerHighlight(data.activeServerUrl);
    }

    // Log last error if present
    if (data.lastError && data.lastError !== 'None') {
        addLog('Warning', `Last error: ${data.lastError}`);
    }
}

function handleLogsResult(data) {
    if (data.logs && Array.isArray(data.logs)) {
        data.logs.forEach(log => {
            parseAndAddLog(log);
        });
    }
}

// ============================================================================
// Device Selection
// ============================================================================

function toggleDeviceSelection(url, deviceItem) {
    if (selectedDevices.has(url)) {
        selectedDevices.delete(url);
        deviceItem.classList.remove('selected');
    } else {
        selectedDevices.add(url);
        deviceItem.classList.add('selected');
    }

    addLog('Info', `Device ${selectedDevices.has(url) ? 'selected' : 'deselected'}: ${url}`);
}

function applyDeviceSelection() {
    if (selectedDevices.size === 0) {
        addLog('Warning', 'No devices selected. Please select at least one device.');
        return;
    }

    // For now, use the first selected device
    // TODO: Support multiple devices in C# backend
    const firstDevice = Array.from(selectedDevices)[0];

    addLog('Info', `Applying device selection: ${firstDevice}`);
    send({ Event: Events.SetConfig, key: 'rpcServer', value: firstDevice });

    // If there are multiple devices selected, log a note
    if (selectedDevices.size > 1) {
        addLog('Warning', `Multiple devices selected (${selectedDevices.size}), but only the first one will be used. Multi-device support is coming soon!`);
    }
}

// ============================================================================
// Service Control
// ============================================================================

function startCapture() {
    if (selectedDevices.size === 0) {
        addLog('Error', 'Please select a device before starting capture');
        return;
    }

    addLog('Info', 'Starting capture...');
    send({ Event: Events.SetConfig, key: 'enabled', value: 'true' });
    updateCaptureState(true);
}

function stopCapture() {
    addLog('Info', 'Stopping capture...');
    send({ Event: Events.SetConfig, key: 'enabled', value: 'false' });
    updateCaptureState(false);
}

function restartService() {
    addLog('Info', 'Requesting service restart...');
    try {
        // Send restart command to backend service
        send({ Event: Events.RestartService });
        addLog('Info', 'Restart command sent. Service will restart momentarily...');
    } catch (err) {
        addLog('Error', `Failed to send restart command: ${err.message}`);
    }
}

function rescanDevices() {
    addLog('Info', 'Rescanning for SSDP devices...');
    send({ Event: Events.ScanSSDP });
}

function pauseCapture() {
    addLog('Info', 'Pausing capture...');
    send({ Event: Events.PauseCapture });
}

function resumeCapture() {
    addLog('Info', 'Resuming capture...');
    send({ Event: Events.ResumeCapture });
}

function getStatus() {
    send({ Event: Events.GetStatus });
}

// ============================================================================
// UI Updates
// ============================================================================

function updateStatus(elementId, text) {
    const element = document.getElementById(elementId);
    if (element) {
        element.textContent = text;
    }
}

function updateCaptureState(isEnabled) {
    isCapturing = isEnabled;

    const statusIndicator = document.getElementById('captureStatus');
    const statusText = document.getElementById('captureStatusText');
    const captureState = document.getElementById('captureState');

    if (isEnabled) {
        statusIndicator.className = 'status-indicator capturing';
        statusText.textContent = 'Capturing';
        captureState.textContent = 'Active';
        setRainbowBorder(true);
    } else {
        statusIndicator.className = 'status-indicator ready';
        statusText.textContent = 'Ready';
        captureState.textContent = 'Stopped';
        setRainbowBorder(false);
    }
}

function setRainbowBorder(active) {
    const rainbowBorder = document.getElementById('rainbowBorder');
    if (rainbowBorder) {
        if (active) {
            rainbowBorder.classList.add('active');
        } else {
            rainbowBorder.classList.remove('active');
        }
    }
}

function updateActiveServerHighlight(activeServerUrl) {
    // Remove "active" class from all device items
    const deviceItems = document.querySelectorAll('.device-item');
    deviceItems.forEach(item => {
        item.classList.remove('active');
        const badge = item.querySelector('.active-badge');
        if (badge) {
            badge.remove();
        }
    });

    // Add "active" class and badge to the matching device
    if (activeServerUrl) {
        deviceItems.forEach(item => {
            const itemUrl = item.getAttribute('data-url');
            if (itemUrl === activeServerUrl) {
                item.classList.add('active');

                // Add visual badge to indicate this is the active server
                const badge = document.createElement('span');
                badge.className = 'active-badge';
                badge.textContent = '● ACTIVE';
                badge.style.cssText = 'color: #4caf50; font-weight: bold; margin-left: 10px; font-size: 20px;';

                const nameElement = item.querySelector('.device-name');
                if (nameElement && !item.querySelector('.active-badge')) {
                    nameElement.appendChild(badge);
                }

                addLog('Debug', `Active server highlighted: ${activeServerUrl}`);
            }
        });
    }
}

// ============================================================================
// Logging
// ============================================================================

function parseAndAddLog(message) {
    // Parse format: [HH:mm:ss] [Type] Message
    const match = message.match(/\[(\d{2}:\d{2}:\d{2})\]\s*\[(\w+)\]\s*(.*)/);

    if (match) {
        const [, timestamp, type, text] = match;
        addLogEntry(timestamp, type, text);
    } else {
        // Fallback for unparsed messages
        const now = new Date();
        const timestamp = `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`;
        addLogEntry(timestamp, 'Info', message);
    }
}

function addLog(type, message) {
    const now = new Date();
    const timestamp = `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`;
    addLogEntry(timestamp, type, message);
}

function addLogEntry(timestamp, type, message) {
    const container = document.getElementById('logsContainer');
    const entry = document.createElement('div');
    entry.className = `log-entry ${type.toLowerCase()}`;

    const timestampSpan = document.createElement('span');
    timestampSpan.className = 'timestamp';
    timestampSpan.textContent = `[${timestamp}]`;

    const typeSpan = document.createElement('span');
    typeSpan.className = 'type';
    typeSpan.textContent = `[${type}]`;

    const messageSpan = document.createElement('span');
    messageSpan.className = 'message';
    messageSpan.textContent = message;

    entry.appendChild(timestampSpan);
    entry.appendChild(typeSpan);
    entry.appendChild(messageSpan);

    container.appendChild(entry);

    logCount++;
    document.getElementById('logCount').textContent = logCount;

    // Auto-scroll if enabled
    if (document.getElementById('autoScroll').checked) {
        container.scrollTop = container.scrollHeight;
    }

    // Keep only last 500 log entries to prevent memory issues
    const entries = container.getElementsByClassName('log-entry');
    while (entries.length > 500) {
        container.removeChild(entries[0]);
        logCount--;
    }
}

function clearLogs() {
    const container = document.getElementById('logsContainer');
    container.innerHTML = '';
    logCount = 0;
    document.getElementById('logCount').textContent = '0';
    addLog('Info', 'Logs cleared');
}

function pad(num) {
    return num.toString().padStart(2, '0');
}

// ============================================================================
// Button Handlers
// ============================================================================

function setupButtonHandlers() {
    document.getElementById('btnStart').onclick = startCapture;
    document.getElementById('btnStop').onclick = stopCapture;
    document.getElementById('btnPause').onclick = pauseCapture;
    document.getElementById('btnResume').onclick = resumeCapture;
    document.getElementById('btnRestart').onclick = restartService;
    document.getElementById('btnRescan').onclick = rescanDevices;
    document.getElementById('btnApplyDevices').onclick = applyDeviceSelection;
    document.getElementById('btnClearLogs').onclick = clearLogs;
}

// ============================================================================
// Periodic Tasks
// ============================================================================

function startPeriodicSSDPScan() {
    // Scan every 30 seconds
    setInterval(() => {
        if (controlWS && controlWS.readyState === WebSocket.OPEN) {
            send({ Event: Events.ScanSSDP });
        }
    }, 30000);
}

function startPeriodicStatusUpdate() {
    // Request detailed status every 5 seconds
    setInterval(() => {
        if (controlWS && controlWS.readyState === WebSocket.OPEN) {
            getStatus();
        }
    }, 5000);
}

// ============================================================================
// Cleanup
// ============================================================================

window.addEventListener('beforeunload', function() {
    if (controlWS) controlWS.close();
    if (logsWS) logsWS.close();
});
