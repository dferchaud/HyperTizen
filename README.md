# HyperTizen (base lowryn, adaptée Tizen 6.0)

Capture d'ambiance pour HyperHDR / Hyperion sur TV Samsung Tizen.

Cette branche repart du fork [lowryn/HyperTizen](https://github.com/lowryn/HyperTizen) (lui-même basé sur [reisxd/HyperTizen](https://github.com/reisxd/HyperTizen) et les recherches de [SryEyes](https://github.com/SryEyes/HyperTizen)), avec des correctifs pour Tizen 6.0 (ex. Samsung QE55Q80A, 2021).

## État réel

- **Non testé sur matériel** : ni le code de cette branche ni le fork lowryn n'ont été testés sur un QE55Q80A. Le fork lowryn a été testé sur un QE55QN90C (Tizen 9).
- Sur Tizen 6, seule la capture par **pixel sampling** (`libvideoenhance.so`, entrées `cs_ve_*`) est utilisée : 8 zones autour de l'écran, environ 9 FPS annoncés par lowryn sur Tizen 9. La capture plein cadre NV12 n'est tentée que sur Tizen 8 ou plus.
- Le contrôle passe par un WebSocket sur le port **8086** de la TV (utilisé par `HyperTizenUI`). Il n'y a **pas** de serveur de logs sur le port 45678 : les logs se lisent avec `sdb dlog`.

## Différences avec le fork lowryn

- `SystemInfo` ne plante plus si la version de Tizen est illisible (elle est alors traitée comme un firmware ancien).
- Le démarrage journalise le modèle et la version de Tizen.
- Une erreur au démarrage du serveur WebSocket de contrôle est journalisée au lieu d'être perdue.
- Le sondage des API `libvideoenhance` (`ppi_ve_*`, `ve_*`, `cs_ve_*`) journalise la raison de chaque échec.

## Compilation

Prérequis : .NET SDK, Tizen Studio (avec les outils TV) et un profil de certificat Samsung qui contient le DUID de votre TV.

```powershell
cd HyperTizen
dotnet build -c Release
```

Le TPK est produit dans `HyperTizen/bin/Release/tizen90/io.gh.reisxd.HyperTizen-1.0.0.tpk`. La compilation le signe avec un certificat par défaut ; il faut le re-signer avec votre profil :

```powershell
cd C:\tizen-studio\tools\ide\bin
.\tizen package -t tpk -s VotreProfil -- C:\chemin\vers\HyperTizen\bin\Release\tizen90\io.gh.reisxd.HyperTizen-1.0.0.tpk
```

## Installation sur la TV

1. Activer le **mode développeur** sur la TV et y autoriser l'IP de votre PC.
2. Connecter la TV :
   ```powershell
   .\sdb connect IP_TV:26101
   ```
3. Installer :
   ```powershell
   .\tizen install -n C:\chemin\vers\io.gh.reisxd.HyperTizen-1.0.0.tpk -s IP_TV:26101
   ```
   `install failed[118, -4]` ou `-12` : le certificat ne correspond pas à la TV (DUID absent) ou le paquet n'est pas signé avec votre profil.
4. Lancer le service :
   ```powershell
   .\tizen run -p io.gh.reisxd.HyperTizen -s IP_TV:26101
   ```
5. Installer l'interface via TizenBrew (module GitHub) : `dferchaud/HyperTizen/HyperTizenUI`. TizenBrew installe depuis la branche par défaut du dépôt : tant que cette branche n'y est pas fusionnée, le module installé peut être l'ancienne interface (ports 45677/45678), incompatible avec ce service (port 8086). Le service fonctionne sans l'interface ; l'interface ne fait que le piloter.

## Diagnostic

Les logs du service s'affichent avec :

```powershell
.\sdb dlog HyperTizen
```

Messages utiles au démarrage :

- `Service starting: model=... tizen=...` : le service démarre et voit la bonne version.
- `cap_mode: libve (Tizen < 8)` : le chemin pixel sampling est choisi.
- `API probe: cs_ve_* found` : `libvideoenhance` répond avec l'API de Tizen 6.
- `API probe: ... unavailable: ...` : raison de l'échec de chaque variante.
- `Control WebSocket server failed: ...` : le port 8086 n'a pas pu être ouvert.

Erreur « Control WebSocket error » dans l'interface : le service ne tourne pas ou le port 8086 n'est pas joignable. Vérifiez d'abord que le service est lancé (`tizen run`) et que `sdb dlog HyperTizen` affiche `Service starting`.

## Configuration de HyperHDR

- Sans réglage, le service cherche HyperHDR par SSDP et utilise `ws://IP:19400/`. Si l'envoi d'images échoue, définissez l'adresse manuellement depuis l'interface (`rpcServer`).
- Le port 19400 est le port FlatBuffers par défaut de HyperHDR ; à vérifier si HyperHDR ne reçoit rien.

## Crédits

Projet d'origine par [reisxd](https://github.com/reisxd/HyperTizen). Optimisations de performance par [lowryn](https://github.com/lowryn/HyperTizen). Recherche sur la capture NV12 par [SryEyes](https://github.com/SryEyes/HyperTizen).
