# HyperTizen (Tizen 6.0 fork)

Ambient-lighting capturer for **HyperHDR** on Samsung Tizen TVs. It captures the TV picture on the TV itself and streams it to HyperHDR over the network.

This fork starts from [lowryn/HyperTizen](https://github.com/lowryn/HyperTizen) (itself based on [reisxd/HyperTizen](https://github.com/reisxd/HyperTizen) and [SryEyes](https://github.com/SryEyes/HyperTizen)) and adapts it to a **Samsung QE55Q80A (Tizen 6.0, 2021)**.

## Status

Verified on a QE55Q80AATXXC (Tizen 6.0), with HyperHDR running on a PC:

- Full-frame capture works: `secvideo_api_capture_screen` from `libsec-video-capture.so.0` returns NV12 480x270 frames.
- The frames reach HyperHDR over FlatBuffers/TCP (port 19400) and the video feed shows up in HyperHDR.
- The native service is started from the TV by the TizenBrew module, with no PC involved, and the TV UI connects to it.

Not verified or not working:

- **The service does not start by itself after the TV reboots.** Open TizenBrew and launch the HyperTizen module each time (see [Daily use](#daily-use)).
- Behaviour on standby/wake, colour accuracy and latency in real use.
- Protected (DRM) content cannot be captured, by design.
- Hyperion was not tested, only HyperHDR.

## How it works

| Part | What it does |
|---|---|
| `HyperTizen/` | C# native service (`io.gh.reisxd.HyperTizen`). Captures frames, sends them to HyperHDR, and serves a small HTTP/WebSocket control interface on port 8086. |
| `HyperTizenUI/` | TizenBrew module. `index.html` is the on-TV screen. `js/service.js` runs inside TizenBrew and launches the native service. |

The module script launches the service the same way `tizen run` does: it connects to the TV's own `sdbd` on `127.0.0.1:26101` and sends `shell:0 debug io.gh.reisxd.HyperTizen` (the protocol used by the `adbhost` library that TizenBrew itself relies on). `tizen.application.launch` and `launchAppControl` are kept as fallbacks but fail with `Unknown error` on this TV. `service.js` also serves a status page on port 8087.

## Daily use

1. Turn the TV on. Make sure HyperHDR is running on your PC.
2. Open **TizenBrew** (green button on the remote) and launch the **HyperTizen** module. Its script starts the native service. Wait a few seconds.
3. On the module screen (labels are in French), check that **Service** shows *Connecte*.
4. Enter the HyperHDR address (`PC-IP:19400`, never `localhost`) and press **Enregistrer** (first time, or when the PC IP changes).
5. Press **Activer la capture**. The **HyperHDR** line should show *Connecte*.

The enabled state and the HyperHDR address are stored by the service, so after the first setup only steps 2 and 3 are needed. Uninstalling the app resets them.

## First-time setup

### Requirements

- .NET SDK, Tizen Studio with the TV tools, and a Samsung certificate profile that includes your **TV's DUID**.
- TizenBrew installed on the TV.
- HyperHDR with the **FlatBuffers server enabled** (port 19400), and an inbound firewall rule for that port on the PC.

### Build and sign

```powershell
cd HyperTizen
dotnet build -c Release --no-incremental
cd C:\tizen-studio\tools\ide\bin
.\tizen package -t tpk -s YourProfile -- C:\path\to\HyperTizen\bin\Release\tizen90\io.gh.reisxd.HyperTizen-1.0.0.tpk
```

The build signs with a default certificate; the `package` step re-signs it with yours. `install failed[118, ...]` means the certificate does not match the TV.

### Install the service

The TV's Developer Mode must list **your PC's IP** as the host, otherwise `sdb` refuses the connection.

```powershell
.\sdb connect TV_IP:26101
.\tizen uninstall -p io.gh.reisxd.HyperTizen -s TV_IP:26101
.\tizen install -n C:\path\to\io.gh.reisxd.HyperTizen-1.0.0.tpk -s TV_IP:26101
```

### Set up TizenBrew

TizenBrew needs Developer Mode's host IP to be `127.0.0.1`, which is incompatible with `sdb` from the PC. Change it and restart the TV (the installed app stays). Switch back to your PC's IP when you need to reinstall.

In TizenBrew, add the GitHub module:

```
dferchaud/HyperTizen/HyperTizenUI
```

jsDelivr caches module files for hours. To be sure of getting a given version, pin a commit: `dferchaud/HyperTizen@<commit>/HyperTizenUI`. Then tick **Settings > Autolaunch service** for the module.

## Control interface

Port 8086 (native service). There is **no authentication**: keep it on your local network.

| Request | Purpose |
|---|---|
| `GET /logs` | Service log, including the previous run (useful after a crash) |
| `GET /set?key=K&value=V` | Set `enabled`, `fbsServer`, `rpcServer` or `t7_probe` |
| `GET /frame.bmp` | One fresh capture, viewable in a browser |
| WebSocket | Used by the TV screen (`SetConfig`, `ReadConfig`; live keys `connected`, `capturing`, `logs`) |

Port 8087 (module script, only while TizenBrew is running): `GET /status` shows whether the launch worked, and `GET /launch` asks for one.

```powershell
curl.exe "http://TV_IP:8086/set?key=enabled&value=true"
curl.exe "http://TV_IP:8086/set?key=fbsServer&value=PC_IP:19400"
curl.exe -i http://TV_IP:8086/logs
curl.exe http://TV_IP:8087/status
```

## Troubleshooting

| Symptom | Check |
|---|---|
| **Service** line shows *Injoignable* | The service is not running. Open `http://TV_IP:8087/status` and read the `sdb:` lines of the log. |
| `status` page unreachable | TizenBrew is not running or the module script did not start; relaunch the module. You may still have an old cached version: re-add it with a pinned commit. |
| **HyperHDR** line shows *Non connecte*, `Connection refused` in `/logs` | FlatBuffers server disabled in HyperHDR, wrong IP (use the PC's LAN IP), or the firewall blocks port 19400. |
| `sdb: authentification demandee` in `/status` | The TV asked for authentication; please report it with the log. |
| Service stops after enabling capture | Native crash. The next start skips the capture probe (`t7_probe` = `crashed`). Retry with `/set?key=t7_probe&value=retry`. |
| `sdb connect` or `tizen install` fails | Developer Mode host IP is not your PC's (for example it is `127.0.0.1` for TizenBrew). |

## Findings about this firmware

- `libvideoenhance.so` (19 KB) has no `*rgb_measure*` functions, so the original pixel-sampling method is impossible on this TV.
- `secvideo_api_capture_screen`'s info struct is larger than the 36-byte `Info_t`. Passing 36 bytes kills the process with no exception, so the code passes a zeroed 256-byte native buffer (Y size at offset 0, UV size at 4, Y pointer at 16, UV pointer at 20).
- `Newtonsoft.Json` fails to load here (`manifest definition does not match`); it was replaced by a small parser in `MiniJson.cs`.
- `dlog` returns nothing, so the service keeps its own log in memory and in a file, served at `/logs`.
- `on-boot` in the manifest is ignored for this sideloaded app.

## Credits

Original project by [reisxd](https://github.com/reisxd/HyperTizen). Performance work by [lowryn](https://github.com/lowryn/HyperTizen). NV12 capture research by [SryEyes](https://github.com/SryEyes/HyperTizen).
