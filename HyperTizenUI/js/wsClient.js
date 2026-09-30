let client;
let deviceIP;
const ssdpDevices = [];
let canEnable = false;

function open() {
    client = new WebSocket(`ws://${deviceIP}:8086`);
    client.onopen = onOpen;
    client.onmessage = onMessage;
    client.onerror = () => {
        location.reload();
    }
}

const events = {
    SetConfig: 0,
    ReadConfig: 1,
    ReadConfigResult: 2,
    ScanSSDP: 3,
    SSDPScanResult: 4
}

function send(json) {
    client.send(JSON.stringify(json));
}

function onOpen() {
    document.getElementById('status').innerHTML = 'Connected';
    document.getElementById('enabled').onchange = (e) => {
        if (!canEnable) {
            alert('Please select a device first');
            return e.target.checked = false;
        }
        send({ event: events.SetConfig, key: 'enabled', value: e.target.checked.toString() });
    }
    send({ event: events.ReadConfig, key: 'rpcServer' });
    send({ event: events.ReadConfig, key: 'enabled' });
    send({ event: events.ScanSSDP });
    setInterval(() => {
        send({ event: events.ScanSSDP });
    }, 10000);
}

function onMessage(data) {
    const msg = JSON.parse(data.data);
    switch(msg.Event) {
        case events.ReadConfigResult:
            if(msg.key === 'rpcServer' && !msg.error) {
                canEnable = true;
                document.getElementById('ssdpDeviceTitle').innerText = `SSDP Devices (Currently Connected to ${msg.value})`;
            } else if(msg.key === 'enabled' && !msg.error) {
                document.getElementById('enabled').checked = msg.value === 'true';
            }
            break;
        case events.SSDPScanResult: {
            for (const device of msg.devices) {
                const url = device.UrlBase.indexOf('https') === 0 ? device.UrlBase.replace('https', 'wss') : device.UrlBase.replace('http', 'ws');

                if (ssdpDevices.some(d => d.url === url)) {
                    continue;
                }
                
                const friendlyName = device.FriendlyName;
                document.getElementById('ssdpItems').innerHTML += `
                <div class="ssdpItem" data-uri="${url}" data-friendlyName="${friendlyName}" tabindex="0" onclick="setRPC('${url}')">
                    <a>${friendlyName}</a>
                </div>
                `;
                ssdpDevices.push({ url, friendlyName });
            }
        }
    }
}

window.setRPC = (url) =>  {
    canEnable = true;
    send({ event: events.SetConfig, key: 'rpcServer', value: url });
}