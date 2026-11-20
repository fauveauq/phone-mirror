# Phone Mirror

Recopie l'écran d'un téléphone Android sur un PC Windows, avec une latence visée sous les 100 ms en USB.

## Fonctionnement

1. **Téléphone** : le serveur open source de scrcpy (licence Apache 2.0) est copié et lancé via adb, sans rien installer. Il capture l'écran et l'encode en H.264 avec l'encodeur matériel du téléphone.
2. **Transport** : un tunnel `adb forward` en USB transporte le flux brut, sans mise en mémoire tampon.
3. **PC** : FFmpeg décode chaque image dès son arrivée (mode faible latence), puis la fenêtre WPF affiche toujours la plus récente à chaque rafraîchissement de l'écran. Si l'affichage prend du retard, les images en trop sont sautées au lieu de s'accumuler.
4. **Contrôle** : une seconde connexion transmet la souris, le clavier et le presse-papiers au téléphone.

| Fichier | Rôle |
|---|---|
| `Core/AdbRunner.cs` | Exécution des commandes adb |
| `Core/AdbDevices.cs` | Liste des appareils branchés |
| `Core/DeviceWatcher.cs` | Attente du téléphone pour la reconnexion automatique |
| `Core/ScrcpyServer.cs` | Déploiement et lancement du serveur sur le téléphone |
| `Core/ScrcpyConnector.cs` | Ouverture des connexions vidéo et contrôle |
| `Core/ScrcpyVideoStream.cs` | Lecture du protocole vidéo de scrcpy |
| `Core/ScrcpyControlChannel.cs` | Messages de contrôle (toucher, touches, texte, presse-papiers) |
| `Core/H264Decoder.cs` | Décodage FFmpeg et conversion en BGRA |
| `Core/FrameExchange.cs` | Triple tampon entre décodage et affichage |
| `Core/MirrorSession.cs` | Orchestration d'une session |
| `Core/UserSettings.cs` | Réglages enregistrés |
| `Core/AppLog.cs` | Journal de diagnostic |
| `Input/*.cs` | Souris, clavier et raccourcis transmis au téléphone |
| `MainWindow.xaml(.cs)` | Fenêtre principale |
| `SettingsWindow.xaml(.cs)` | Fenêtre de réglages |

## Installation

Il faut le **SDK .NET 10** (ou Visual Studio 2026), puis trois dépendances à placer dans `third_party` :

```
third_party/
├── adb/            adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll
├── ffmpeg/         avcodec-*.dll, avutil-*.dll, swscale-*.dll, etc.
└── scrcpy-server   (fichier sans extension)
```

- **adb** : contenu de « SDK Platform-Tools for Windows » (site développeurs Android). Si adb est déjà dans le PATH, ce dossier est facultatif.
- **scrcpy-server** : sur la page des releases GitHub de scrcpy, télécharger `scrcpy-server-v5.0.1` et le renommer `scrcpy-server`. La version doit correspondre exactement à `ServerVersion` dans `Core/MirrorOptions.cs`.
- **FFmpeg** : une build « shared » 64 bits de FFmpeg **9.0.1** (par exemple `ffmpeg-9.0.1-full_build-shared` de gyan.dev), en copiant le contenu de son dossier `bin`. La version majeure des DLL doit correspondre à celle des paquets `FFmpeg.AutoGen` du `.csproj`.

Puis :

```
dotnet run -c Release
```

Sur le téléphone : activer les options pour les développeurs et le débogage USB, brancher le câble et accepter l'autorisation qui s'affiche.

## Utilisation

- **F2** : afficher ou masquer les statistiques (images décodées et affichées par seconde, erreurs).
- **F3** : réglages (téléphone, résolution, débit, images par seconde), enregistrés dans `%LOCALAPPDATA%\PhoneMirror\settings.json`. Utile notamment pour choisir le téléphone quand plusieurs sont branchés.
- Au débranchement, la recopie reprend toute seule quand le téléphone revient. Le bouton « Réessayer » reste disponible.
- Journal de diagnostic : `%LOCALAPPDATA%\PhoneMirror\journal.log`, recréé à chaque lancement.

### Souris et clavier

| Action sur le PC | Effet sur le téléphone |
|---|---|
| Clic gauche, glisser | Toucher, faire défiler, balayer |
| Molette (Maj + molette) | Défilement vertical (horizontal) |
| Clic droit | Retour (allume l'écran s'il est éteint) |
| Clic du milieu | Accueil |
| Boutons latéraux de la souris | Applications récentes / notifications |
| Frappe au clavier | Texte (accents compris), Entrée, Retour arrière, flèches… |
| Ctrl+V | Colle le presse-papiers du PC sur le téléphone |
| Ctrl+C / Ctrl+X | Copie / coupe la sélection du téléphone vers le PC |
| Alt+H / Alt+B / Alt+S | Accueil / Retour / Applications récentes |
| Alt+↑ / Alt+↓ | Volume + / − |
| Alt+P | Bouton marche/arrêt |
| Alt+N / Alt+Maj+N | Ouvrir / fermer les notifications |
| Alt+R | Faire pivoter l'écran du téléphone |
| Alt+O / Alt+Maj+O | Éteindre / rallumer l'écran du téléphone (la recopie continue) |

Le texte copié sur le téléphone arrive aussi automatiquement dans le presse-papiers du PC.

## Licences

Le serveur scrcpy est sous Apache 2.0. Les builds FFmpeg « full » sont sous GPL : à prendre en compte si l'application est un jour distribuée (une build LGPL suffit pour le décodage H.264).
