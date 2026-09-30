# HyperTizen

HyperTizen is a Hyperion / HyperHDR capturer for Tizen TVs.

# Installation

To install HyperTizen, you need to have a Samsung TV (Tizen) that has at least Tizen 6.5 (2022+).

You'll need Tizen Studio to install the app on your TV. You can download it from the [official website](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html).

1. Download the latest release from the [releases page](https://github.com/reisxd/HyperTizen/releases/latest).

2. Change the Host PC IP address to your PC's IP address by following [this](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html#Connecting-the-TV-and-SDK)

3. Install the package:
```bash
tizen install -n path/to/io.gh.reisxd.HyperTizen.tpk
```

Note that `tizen` is in `C:\tizen-studio\tools\ide\bin` on Windows and in `~/tizen-studio/tools/ide/bin` on Linux.

If you get `install failed[118, -12], reason: Check certificate error` error, you'll have to resign the package.

4. Install TizenBrew to your TV. Follow [this](https://github.com/reisxd/TizenBrew/blob/main/docs/README.md) guide.

5. Add `reisxd/HyperTizen/HyperTizenUI` as a GitHub module to the module manager. You can access the module manager by pressing the [GREEN] button on the remote.

## Resigning the package

1. Change the Host PC IP address to your PC's IP address by following [this](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html#Connecting-the-TV-and-SDK)

2. After following the guide for the Tizen Studio installation, you have to create a certificate profile. You can follow [this guide](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html).

3. Sign the package:
```bash
tizen package -t tpk -s YourProfileName -o path/to/output/dir -- path/to/io.gh.reisxd.HyperTizen.tpk

# Example:
# tizen package -t tpk -s HyperTizen -o release -- io.gh.reisxd.HyperTizen.tpk
```

4. You should now be able to install the package.