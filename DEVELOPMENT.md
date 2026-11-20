# Phone Mirror — développement

Le guide d'installation et d'utilisation se trouve dans le [README](README.md). Ce document s'adresse aux personnes qui compilent ou modifient l'application.

## Fonctionnement

1. **Téléphone** : le serveur open source de scrcpy (licence Apache 2.0) est copié et lancé via adb, sans rien installer. Il capture l'écran et l'encode en H.264 avec l'encodeur matériel du téléphone.
2. **Transport** : un tunnel `adb forward` en USB transporte le flux brut, sans mise en mémoire tampon.
3. **PC** : FFmpeg décode chaque image dès son arrivée, en mode faible latence. La fenêtre WPF affiche toujours la plus récente à chaque rafraîchissement de l'écran. Si l'affichage prend du retard, les images en trop sont sautées au lieu de s'accumuler.
4. **Son** (Android 11 ou plus récent) : le téléphone envoie le son en Opus. FFmpeg le décode et WASAPI (NAudio) le joue, avec un retard plafonné à 150 ms pour rester synchronisé avec l'image.
5. **Contrôle** : une connexion supplémentaire transmet la souris, le clavier et le presse-papiers au téléphone.

| Fichier | Rôle |
|---|---|
| `Core/AdbRunner.cs` | Exécution des commandes adb |
| `Core/AdbDevices.cs` | Liste des appareils branchés |
| `Core/DeviceWatcher.cs` | Attente du téléphone pour la reconnexion automatique |
| `Core/ScrcpyServer.cs` | Déploiement et lancement du serveur sur le téléphone |
| `Core/ScrcpyConnector.cs` | Ouverture des connexions vidéo, son et contrôle |
| `Core/ScrcpyVideoStream.cs` | Lecture du protocole vidéo de scrcpy |
| `Core/ScrcpyControlChannel.cs` | Messages de contrôle (toucher, touches, texte, presse-papiers) |
| `Core/MediaPacketReader.cs` | Lecture des paquets vidéo et audio |
| `Core/ScrcpyAudioStream.cs`, `Core/AudioPipeline.cs` | Flux audio et sa boucle de lecture |
| `Core/OpusDecoder.cs`, `Core/AudioPlayer.cs` | Décodage Opus (FFmpeg) et lecture WASAPI (NAudio) |
| `Core/H264Decoder.cs` | Décodage FFmpeg et conversion en BGRA |
| `Core/FrameExchange.cs` | Triple tampon entre décodage et affichage |
| `Core/MirrorSession.cs` | Orchestration d'une session |
| `Core/UserSettings.cs` | Réglages enregistrés |
| `Core/AppUpdater.cs` | Mises à jour (Velopack) |
| `Core/AppLog.cs` | Journal de diagnostic |
| `Input/*.cs` | Souris, clavier et raccourcis transmis au téléphone |
| `UI/*.cs` | Plein écran, glisser-déposer, ondes de clic |
| `MainWindow.xaml(.cs)` | Fenêtre principale |
| `SettingsWindow.xaml(.cs)` | Fenêtre de réglages |

## Compiler

Il faut le **SDK .NET 10** (ou Visual Studio 2026), puis placer trois dépendances dans `third_party` :

```
third_party/
├── adb/            adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll
├── ffmpeg/         avcodec-*.dll, avutil-*.dll, swscale-*.dll, swresample-*.dll
└── scrcpy-server   (fichier sans extension)
```

- **adb** : le contenu de « SDK Platform-Tools for Windows », sur le site développeurs Android. Ce dossier est facultatif si adb est déjà dans le PATH.
- **scrcpy-server** : télécharger `scrcpy-server-v5.0.1` depuis la page des releases GitHub de scrcpy, puis le renommer `scrcpy-server`. Sa version doit correspondre exactement à `ServerVersion` dans `Core/MirrorOptions.cs`.
- **FFmpeg** : la build **LGPL** « shared » 64 bits de FFmpeg 9.0 publiée par BtbN (`ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip`, dans les releases GitHub de BtbN/FFmpeg-Builds).
  - Seuls les fichiers `avcodec-63.dll`, `avutil-61.dll`, `swscale-10.dll` et `swresample-7.dll` de son dossier `bin` sont nécessaires.
  - Ajouter aussi `LICENSE.txt`, renommé en `LICENSE-FFmpeg.txt`.
  - La version majeure des DLL doit correspondre à celle des paquets `FFmpeg.AutoGen` déclarés dans le `.csproj`.

Ensuite :

```
dotnet run -c Release          # lancer l'application
dotnet test PhoneMirror.slnx   # tests unitaires (protocole, réglages, adb)
```

## Construire et publier l'installeur

```
.\installer\build-installer.ps1            # installer\out\PhoneMirror-win-Setup.exe
.\installer\build-installer.ps1 -Publish   # crée aussi une release GitHub (mises à jour automatiques)
.\installer\build-installer.ps1 -SharePath \\serveur\partage\PhoneMirror   # ou publie sur un dossier réseau
```

Le script enchaîne trois étapes :

1. il lance les tests ;
2. il publie une version autonome, qui embarque le runtime .NET ;
3. il l'empaquette avec [Velopack](https://velopack.io).

Avant chaque publication, augmenter `<Version>` dans `PhoneMirror.csproj`.

Les applications installées vérifient les mises à jour au démarrage. Elles téléchargent la nouvelle version en arrière-plan et l'installent à la fermeture.

La source des mises à jour est par défaut la page des releases GitHub de ce dépôt. Pour en changer, renseigner `UpdateSource` dans `%APPDATA%\PhoneMirror\settings.json` :

```json
"UpdateSource": "\\\\serveur\\partage\\PhoneMirror"
```

Valeurs possibles :

- un dépôt GitHub (`https://github.com/…`) ;
- une adresse web contenant les fichiers produits par Velopack ;
- un dossier local ou réseau.

## Licences

- **Serveur scrcpy** : Apache 2.0.
- **FFmpeg** : build LGPL, livrée en DLL séparées et non modifiées. Sa licence est fournie dans `third_party/ffmpeg`. Ne pas revenir à une build « full » (GPL) pour l'installeur.
- **Velopack** et **NAudio** : MIT.
- **Icône** : dessinée par le script `Assets/make-icon.ps1`, à relancer après chaque modification.
- **adb** (Android SDK Platform-Tools) : soumis aux conditions de licence du SDK Android. L'installeur l'embarque, comme le fait scrcpy. À valider avant toute diffusion hors de l'entreprise.
