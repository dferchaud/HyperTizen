# HyperTizen (base lowryn, adaptée Tizen 6.0)

Capture d'ambiance pour HyperHDR / Hyperion sur TV Samsung Tizen.

Cette branche repart du fork [lowryn/HyperTizen](https://github.com/lowryn/HyperTizen) (lui-même basé sur [reisxd/HyperTizen](https://github.com/reisxd/HyperTizen) et les recherches de [SryEyes](https://github.com/SryEyes/HyperTizen)) et l'adapte à un **Samsung QE55Q80A (Tizen 6.0, 2021)**.

## État réel (QE55Q80A, Tizen 6.0)

Vérifié sur la TV :
- Le service démarre, écoute sur le port 8086 et répond à `/logs`, `/set` et `/frame.bmp`.
- Le service est lancé depuis la TV par le module TizenBrew (lancement par sdb), sans PC, et l'interface s'y connecte.
- La capture plein cadre **fonctionne** : `secvideo_api_capture_screen` (`/usr/lib/libsec-video-capture.so.0`) renvoie 0, et `/frame.bmp` affiche bien l'image de la TV (NV12, 480x270).

Non vérifié :
- L'envoi des images à HyperHDR / Hyperion (aucun serveur n'était disponible lors des tests).
- Le démarrage automatique après un redémarrage de la TV (`on-boot` du manifest n'est pas respecté ; il faut que TizenBrew soit lancé et que « Autolaunch service » soit coché).
- Le comportement avec du contenu protégé (DRM), qui ne peut pas être capturé.
- Le rendu des couleurs et la latence en conditions réelles.

## Ce qu'on a découvert sur ce firmware

- `libvideoenhance.so` (19 Ko) ne contient **aucune** fonction `*rgb_measure*` : la méthode par échantillonnage de pixels est impossible sur ce modèle.
- `libsec-video-capture.so.0` exporte `secvideo_api_capture_screen`. La structure attendue par cette fonction est plus grande que les 36 octets de `Info_t` : avec une structure de 36 octets, le processus est tué par un débordement mémoire (sans exception). Le code utilise donc un tampon natif de 256 octets, avec les champs aux offsets 0 (taille Y), 4 (taille UV), 16 (pointeur Y) et 20 (pointeur UV).
- `Newtonsoft.Json` ne se charge pas sur ce firmware (`manifest definition does not match`). Il a été remplacé par un petit lecteur JSON (`MiniJson.cs`).
- `dlog` ne renvoie rien sur ce modèle : le service garde ses messages en mémoire et dans un fichier.

## Interface HTTP du service (port 8086)

| Adresse | Rôle |
|---|---|
| `GET /logs` | Logs du service, avec ceux de l'exécution précédente (utile après un plantage) |
| `GET /set?key=K&value=V` | Applique une configuration (`enabled`, `rpcServer`, `fbsServer`, `t7_probe`) |
| `GET /frame.bmp` | Une capture de l'écran, affichable dans un navigateur |
| WebSocket | Protocole de `HyperTizenUI` (`SetConfig`, `ReadConfig`, `ScanSSDP`) |

Exemples :
```powershell
curl.exe "http://IP_TV:8086/set?key=enabled&value=true"
curl.exe "http://IP_TV:8086/set?key=fbsServer&value=IP_HYPERHDR:19400"
curl.exe -i http://IP_TV:8086/logs
```

Ces points d'accès n'ont aucune authentification : ne les exposez pas hors de votre réseau local.

## Compilation

Prérequis : .NET SDK, Tizen Studio (avec les outils TV) et un profil de certificat Samsung qui contient le DUID de votre TV.

```powershell
cd HyperTizen
dotnet build -c Release --no-incremental
```

Le TPK est produit dans `HyperTizen/bin/Release/tizen90/io.gh.reisxd.HyperTizen-1.0.0.tpk`. Il faut le re-signer avec votre profil :

```powershell
cd C:\tizen-studio\tools\ide\bin
.\tizen package -t tpk -s VotreProfil -- C:\chemin\vers\HyperTizen\bin\Release\tizen90\io.gh.reisxd.HyperTizen-1.0.0.tpk
```

## Installation sur la TV

1. Activer le **mode développeur** sur la TV et y mettre l'IP de votre PC comme hôte, puis redémarrer la TV.
2. Connecter la TV :
   ```powershell
   .\sdb connect IP_TV:26101
   ```
3. Installer et lancer :
   ```powershell
   .\tizen uninstall -p io.gh.reisxd.HyperTizen -s IP_TV:26101
   .\tizen install -n C:\chemin\vers\io.gh.reisxd.HyperTizen-1.0.0.tpk -s IP_TV:26101
   .\tizen run -p io.gh.reisxd.HyperTizen -s IP_TV:26101
   ```
   Désinstaller d'abord remet les préférences à zéro.
   `install failed[118, ...]` : le certificat ne correspond pas à la TV (DUID absent).
4. Activer la capture :
   ```powershell
   curl.exe "http://IP_TV:8086/set?key=enabled&value=true"
   ```

Si l'IP du PC hôte du mode développeur est remplacée par autre chose (par exemple `127.0.0.1` pour TizenBrew), `sdb connect` et `tizen install` ne fonctionnent plus depuis le PC.

## Diagnostic

Ouvrez `http://IP_TV:8086/logs`. Lignes utiles :

- `Service starting: model=... tizen=...` : le service démarre.
- `Control server listening on http://*:8086/` : le port est ouvert.
- `T7 probe: ...` puis `T7 capture probe: secvideo_api_capture_screen returned 0` : la bibliothèque de capture répond.
- `cap_mode: secvideo-t7` : le mode plein cadre est retenu.
- `SSDP: no HyperHDR found` : aucun serveur trouvé. Renseignez-le avec `fbsServer`.

Si la sonde native tue le service, le démarrage suivant la saute (`t7_probe` passe à `crashed`). Pour la réessayer : `curl.exe "http://IP_TV:8086/set?key=t7_probe&value=retry"`.

## Interface TizenBrew (HyperTizenUI)

Le dossier `HyperTizenUI` est une appli web affichée sur la TV (module TizenBrew). Elle se connecte au service par WebSocket (port 8086) et affiche :
- l'état du service, de la capture, le mode de capture et la connexion à HyperHDR ;
- les derniers messages du journal du service.

Commandes à la télécommande : flèches haut/bas pour naviguer, Entrée pour valider.
- **Démarrer le service** : demande à la TV de lancer `io.gh.reisxd.HyperTizen` (`tizen.application.launch`). Si le service est injoignable, l'interface essaie aussi de le lancer une fois d'elle-même.
- **Activer / Désactiver la capture**.
- **Adresse de HyperHDR** (`IP:port`, port 19400 par défaut) : à renseigner avec l'adresse réseau de la machine qui fait tourner HyperHDR (pas `localhost`).

Ce qui n'est pas vérifié : la logique de l'interface a été testée avec un faux navigateur et un faux service, pas sur une vraie TV. On ne sait pas si le lancement du service par cette page fonctionne sur votre firmware, ni si TizenBrew expose `tizen.application` aux modules. Le service fonctionne sans l'interface.

### Lancement du service par TizenBrew

TizenBrew exécute le script `js/service.js` du module dans un bac à sable Node.js (v12.4.0 sur le QE55Q80A) et le lui charge depuis le CDN jsDelivr, avec mise en cache. Ce script lance le service natif, dans cet ordre :

1. **Par sdb**, comme `tizen run` : connexion à `127.0.0.1:26101` (le `sdbd` de la TV, accessible parce que le mode développeur est réglé sur `127.0.0.1` pour TizenBrew) et commande `shell:0 debug io.gh.reisxd.HyperTizen`. C'est le mécanisme que TizenBrew utilise pour ses propres applis ; le protocole est celui de la bibliothèque `adbhost`.
2. En repli, `tizen.application.launchAppControl` (opération `service`), puis `tizen.application.launch`. Sur le QE55Q80A, ces deux appels échouent avec `Unknown error`.

Il vérifie que le service répond avant chaque essai (jusqu'à 2 minutes) et ouvre un petit serveur d'état sur le port **8087** :
- `GET /status` : état, API Tizen disponible ou non, journal des tentatives ;
- `GET /launch` : demande un lancement et renvoie l'état.

La page (servie par TizenBrew) s'en sert quand elle n'a pas d'API `tizen`. Pour comprendre un échec depuis le PC :
```powershell
curl.exe http://IP_TV:8087/status
```
Ce serveur n'a pas d'authentification et écoute sur tout le réseau local ; il ne peut que relancer HyperTizen.

Dans TizenBrew, **Settings > Autolaunch service** permet de démarrer ce script dès que TizenBrew démarre.

Vérifié sur le QE55Q80A : le lancement par sdb fonctionne. Non vérifié : que TizenBrew soit lui-même lancé au démarrage de la TV, et que jsDelivr serve la dernière version du script (le cache peut durer plusieurs heures ; on peut épingler un commit : `dferchaud/HyperTizen@<commit>/HyperTizenUI`).

### Installer l'interface comme appli TV (sans TizenBrew)

TizenBrew exige une IP de mode développeur (`127.0.0.1`) incompatible avec `tizen run` depuis le PC, et la page n'y a pas accès à `tizen.application`. Installée comme appli TV, la page a ses propres droits et peut lancer le service. Non vérifié sur la TV.

```powershell
cd C:\tizen-studio\tools\ide\bin
.\tizen package -t wgt -s VotreProfil -o C:\chemin\build -- C:\chemin\HyperTizen\HyperTizenUI
.\tizen install -n C:\chemin\build\HyperTizen.wgt -s IP_TV:26101
.\tizen run -p 6jwjAZfoVq.HyperTizenUI -s IP_TV:26101
```

Le nom exact du fichier `.wgt` est affiché par la commande `package`. L'IP du PC doit être dans le mode développeur.

TizenBrew installe un module depuis la branche par défaut de son dépôt : tant que cette branche n'y est pas fusionnée, le module installé peut être une autre version.

## Crédits

Projet d'origine par [reisxd](https://github.com/reisxd/HyperTizen). Optimisations de performance par [lowryn](https://github.com/lowryn/HyperTizen). Recherche sur la capture NV12 par [SryEyes](https://github.com/SryEyes/HyperTizen).
