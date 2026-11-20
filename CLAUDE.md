# Phone Mirror — contexte du projet

Application Windows qui affiche en temps réel l'écran d'un téléphone Android branché en USB.

## Objectifs

- **Latence** : exigence stricte de moins de 0,5 s entre le téléphone et le PC ; viser moins de 100 ms.
- **Qualité** : application robuste, rapide, soignée, facile à maintenir et pratique à utiliser.
- Textes de l'interface, messages d'erreur et commentaires en **français**.

## Environnement

- PC sous Windows, téléphone Android relié en USB (débogage USB activé).
- .NET 10, WPF, C#. Exécution : `dotnet run -c Release`.
- Pas d'iPhone à prendre en charge.

## Architecture

- **Téléphone** : on réutilise le serveur de scrcpy **v5.0.1** (Apache 2.0), copié et lancé via `adb shell app_process`, sans installer d'application. Il encode l'écran en H.264 avec l'encodeur matériel.
- **Transport** : tunnel `adb forward tcp:27183 localabstract:scrcpy`, options `tunnel_forward=true audio=false control=true`. Ordre imposé par le serveur (`DesktopConnection.java`) : connexion vidéo (octet factice), puis connexion de contrôle, et seulement ensuite nom de l'appareil et codec sur la connexion vidéo.
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
| `Core/UserSettings.cs` | Réglages utilisateur (`%LOCALAPPDATA%\PhoneMirror\settings.json`) |
| `Input/MouseForwarder.cs` | Souris → toucher, défilement (×3), boutons |
| `Input/KeyboardForwarder.cs` | Clavier → texte, touches Android, presse-papiers |
| `Input/PhoneShortcuts.cs` | Raccourcis Alt+touche |
| `Input/AndroidKeys.cs` | Codes de touches Android |
| `SettingsWindow.xaml(.cs)` | Réglages (F3) |
| `Core/H264Decoder.cs` | Décodage FFmpeg et conversion |
| `Core/FrameExchange.cs` | Triple tampon décodage → affichage |
| `Core/MirrorSession.cs` | Orchestration d'une session |
| `Core/DeviceWatcher.cs` | Attente du téléphone (`adb get-state` chaque seconde) pour la reconnexion automatique |
| `Core/AppLog.cs` | Journal dans `%LOCALAPPDATA%\PhoneMirror\journal.log` (statistiques toutes les 5 s) |
| `MainWindow.xaml(.cs)` | Interface (F2 = statistiques) |

## Dépendances externes (dans `third_party`, non versionnées)

- `third_party/adb/` : adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll (Android SDK Platform-Tools).
- `third_party/scrcpy-server` : fichier `scrcpy-server-v5.0.1` des releases GitHub de scrcpy, renommé, sans extension. Sa version doit égaler `ServerVersion` dans `Core/MirrorOptions.cs`.
- `third_party/ffmpeg/` : DLL d'une build « shared » 64 bits de FFmpeg 9.0.1 (gyan.dev ou GyanD/codexffmpeg). La version majeure doit correspondre aux paquets FFmpeg.AutoGen.

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

## Règles de travail

- Demander avant de modifier l'architecture ou d'ajouter une dépendance.
- Garder des classes courtes à responsabilité unique, et traiter proprement les erreurs (messages clairs pour l'utilisateur).
- Toute modification touchant le chemin vidéo doit préserver la faible latence (pas de file d'attente, pas de tampon supplémentaire).
