# Phone Mirror — contexte du projet

Application Windows qui affiche en temps réel l'écran d'un téléphone Android branché en USB.

## Objectifs

- **Latence** : exigence stricte de moins de 0,5 s entre le téléphone et le PC ; viser moins de 100 ms.
- **Qualité** : application robuste, rapide, soignée, facile à maintenir et pratique à utiliser.
- Textes de l'interface, messages d'erreur et commentaires en **français**.

## Environnement

- PC sous Windows, téléphone Android relié en USB (débogage USB activé).
- .NET 10, WPF, C#. Exécution : `dotnet run -c Release`. Tests : `dotnet test PhoneMirror.slnx` (xUnit, `tests/PhoneMirror.Tests`).
- Dépôt GitHub **public** : <https://github.com/fauveauq/phone-mirror> (branche `main`). Rien de secret ni d'interne dans les fichiers ; commits signés avec l'adresse noreply GitHub (configurée dans ce dépôt), jamais l'e-mail professionnel.
- Source des mises à jour : `UpdateSource` dans `settings.json` (null = releases GitHub ; sinon autre dépôt, adresse web ou dossier réseau). Publication : `build-installer.ps1 -Publish` (GitHub) ou `-SharePath \\serveur\partage` (réseau). Augmenter `<Version>` avant chaque publication.
- Installeur : `installer/build-installer.ps1` (Velopack, outil local `vpk` dans `dotnet-tools.json`) → `installer/out/PhoneMirror-win-Setup.exe`, installation par utilisateur dans `%LOCALAPPDATA%\PhoneMirror`. Mises à jour automatiques depuis les releases GitHub (`Core/AppUpdater.cs`, `-Publish`). Point d'entrée `Program.cs` (Velopack avant WPF).
- Données utilisateur dans `%APPDATA%\PhoneMirror` (réglages, journal), jamais dans `%LOCALAPPDATA%\PhoneMirror` (dossier d'installation effacé à la désinstallation).
- Pas d'iPhone à prendre en charge.

## Architecture

- **Téléphone** : on réutilise le serveur de scrcpy **v5.0.1** (Apache 2.0), copié et lancé via `adb shell app_process`, sans installer d'application. Il encode l'écran en H.264 avec l'encodeur matériel.
- **Transport** : tunnel `adb forward tcp:27183 localabstract:scrcpy`, options `tunnel_forward=true audio=false control=true`. Ordre imposé par le serveur (`DesktopConnection.java`) : connexion vidéo (octet factice), puis connexion de contrôle, et seulement ensuite nom de l'appareil et codec sur la connexion vidéo.
- **Son** : `audio=true audio_codec=opus`, connexion ouverte entre vidéo et contrôle. En-tête u32 : `opus`, 0 = désactivé par le téléphone (Android < 11), 1 = erreur. Paquets au format vidéo sans session ; le paquet de config (OpusHead) est ignoré (48 kHz stéréo par défaut). Décodage FFmpeg (`OpusDecoder`), lecture NAudio `WasapiPlayer` partagé 40 ms avec suivi de la sortie par défaut (incompatible avec IAudioClient3 : ne pas combiner). Tampon vidé au-delà de 150 ms de retard. Une panne audio ne doit jamais couper la vidéo.
- **Contrôle** : format binaire de `ControlMessageReader.java` (v5.0.1). Positions exprimées dans la taille courante de la vidéo, sinon ignorées par le serveur. Pointeur « souris » (-1) avec bouton principal = injecté comme un doigt. Le serveur renvoie le presse-papiers du téléphone (`DeviceMessageWriter.java`).
- **Protocole** (scrcpy 4.x/5.x, voir `doc/develop.md` du dépôt scrcpy au tag v5.0.1) : octet factice, nom de l'appareil (64 octets), identifiant du codec (u32), puis des en-têtes de 12 octets. Bit 63 = paquet de session (largeur, hauteur), bit 62 = configuration (SPS/PPS, à fusionner avec l'image suivante), bit 61 = image clé.
- **PC** : décodage FFmpeg (FFmpeg.AutoGen 9.0.1.1, API `FFmpeg.AutoGen.Abstractions` + `Bindings.DynamicallyLoaded`) en mode faible latence (`AV_CODEC_FLAG_LOW_DELAY`, `thread_count = 1`), conversion en BGRA, triple tampon (`FrameExchange`), affichage WPF via `WriteableBitmap` sur `CompositionTarget.Rendering`. L'affichage prend toujours l'image la plus récente pour que la latence ne s'accumule jamais.

| Fichier | Rôle |
|---|---|
| `Core/AdbRunner.cs` | Exécution des commandes adb |
| `Core/ScrcpyServer.cs` | Déploiement et lancement du serveur |
| `Core/ScrcpyConnector.cs` | Ouverture des connexions vidéo et contrôle, dans l'ordre attendu |
| `Core/ScrcpyVideoStream.cs` | Protocole vidéo |
| `Core/ScrcpyControlChannel.cs` | Messages de contrôle et presse-papiers du téléphone |
| `Core/AdbDevices.cs` | Liste des appareils (`adb devices -l`) |
| `Core/UserSettings.cs` | Réglages utilisateur (`%APPDATA%\PhoneMirror\settings.json`), y compris écran éteint, premier plan, position de la fenêtre |
| `Core/FileTransfer.cs` | Envoi de fichiers (`adb push` vers Download) et installation d'APK |
| `Core/AppUpdater.cs` | Mises à jour Velopack depuis les releases GitHub |
| `UI/FileDropHandler.cs` | Glisser-déposer de fichiers sur la fenêtre |
| `UI/FullScreenToggle.cs` | Plein écran (F11) |
| `UI/ClickRippleLayer.cs` | Ondes visuelles : vers l'extérieur au clic gauche, vers l'intérieur au clic droit (calque `ClickLayer`) ; options `ShowClicks`, `ClickStyle`, `ClickColor`, `ClickSize` (énumérations écrites en toutes lettres dans settings.json) |
| `Input/MouseForwarder.cs` | Souris → toucher, défilement (×3), boutons |
| `Input/KeyboardForwarder.cs` | Clavier → texte, touches Android, presse-papiers |
| `Input/PhoneShortcuts.cs` | Raccourcis Alt+touche |
| `Input/AndroidKeys.cs` | Codes de touches Android |
| `SettingsWindow.xaml(.cs)` | Réglages (F3) |
| `Core/MediaPacketReader.cs` | En-têtes de 12 octets, commun vidéo et audio |
| `Core/ScrcpyAudioStream.cs` | Flux audio (en-tête, paquets) |
| `Core/AudioPipeline.cs` | Thread audio : lecture, décodage, lecture sur le PC |
| `Core/OpusDecoder.cs` | Décodage Opus FFmpeg → float entrelacé |
| `Core/AudioPlayer.cs` | Sortie WASAPI (NAudio), retard plafonné |
| `Assets/make-icon.ps1` | Génère `Assets/PhoneMirror.ico` (exe, fenêtres, installeur) |
| `Core/H264Decoder.cs` | Décodage FFmpeg et conversion |
| `Core/FrameExchange.cs` | Triple tampon décodage → affichage |
| `Core/MirrorSession.cs` | Orchestration d'une session |
| `Core/DeviceWatcher.cs` | Attente du téléphone (`adb get-state` chaque seconde) pour la reconnexion automatique |
| `Core/AppLog.cs` | Journal dans `%APPDATA%\PhoneMirror\journal.log` (statistiques toutes les 5 s) |
| `MainWindow.xaml(.cs)` | Interface (F2 = statistiques) |

## Dépendances externes (dans `third_party`, non versionnées)

- `third_party/adb/` : adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll (Android SDK Platform-Tools).
- `third_party/scrcpy-server` : fichier `scrcpy-server-v5.0.1` des releases GitHub de scrcpy, renommé, sans extension. Sa version doit égaler `ServerVersion` dans `Core/MirrorOptions.cs`.
- `third_party/ffmpeg/` : build **LGPL** « shared » 64 bits de FFmpeg 9.0 (BtbN/FFmpeg-Builds), limitée à avcodec-63, avutil-61, swscale-10, swresample-7 + `LICENSE-FFmpeg.txt`. Ne pas utiliser de build GPL (« full ») : l'application est distribuée. La version majeure doit correspondre aux paquets FFmpeg.AutoGen.

## État actuel

- Compile sans erreur ni avertissement ; testé sur un Galaxy S20 (Android 13) : affichage jusqu'à environ 55 i/s, latence jugée très bonne à l'œil.
- Contrôle souris, clavier et presse-papiers opérationnel (validé par l'utilisateur).
- Réglages par défaut : débit 16 Mbit/s, `MaxSize` 1920, 60 i/s, mise à l'échelle WPF `HighQuality`. La fenêtre prend les proportions de la vidéo à chaque rotation.
- SDK .NET installé dans `%LOCALAPPDATA%\Microsoft\dotnet` (hors PATH) : définir `DOTNET_ROOT` sur ce dossier pour lancer l'exécutable.
- Ne jamais tuer un processus adb avec son arbre (`entireProcessTree`) : il peut être le parent du démon adb qu'il vient de démarrer.
- Signalé une fois, non reproduit : le clavier du PC qui ne répondait plus dans les autres applications pendant que le téléphone était branché. Test réalisé : la reconnexion ne vole pas le focus.

## Feuille de route

1. ~~Faire compiler et fonctionner l'affichage, mesurer la latence réelle.~~ Fait.
2. ~~Contrôle souris et clavier (socket de contrôle de scrcpy, `control=true`).~~ Fait.
3. ~~Reconnexion automatique au rebranchement du câble.~~ Fait (abandon après 3 coupures en moins de 10 s).
4. Décodage matériel (D3D11VA) et rendu Direct3D pour réduire encore la latence. En attente : la latence actuelle est jugée très bonne.
5. ~~Réglages accessibles depuis l'interface (résolution, débit, appareil).~~ Fait (F3).
6. ~~Confort : plein écran, glisser-déposer, écran éteint, premier plan, position mémorisée ; tests ; installeur.~~ Fait.
7. ~~Son du téléphone sur le PC (scrcpy `audio=true`, Opus, Android 11+).~~ Fait (v1.1.0), validé par l'utilisateur : synchro et qualité bonnes.

## Règles de travail

- Demander avant de modifier l'architecture ou d'ajouter une dépendance.
- Garder des classes courtes à responsabilité unique, et traiter proprement les erreurs (messages clairs pour l'utilisateur).
- Toute modification touchant le chemin vidéo doit préserver la faible latence (pas de file d'attente, pas de tampon supplémentaire).
