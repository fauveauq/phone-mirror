# Phone Mirror

Recopie l'écran d'un téléphone Android sur un PC Windows, avec une latence visée sous les 100 ms en USB.

## Fonctionnement

1. **Téléphone** : le serveur open source de scrcpy (licence Apache 2.0) est copié et lancé via adb, sans rien installer. Il capture l'écran et l'encode en H.264 avec l'encodeur matériel du téléphone.
2. **Transport** : un tunnel `adb forward` en USB transporte le flux brut, sans mise en mémoire tampon.
3. **PC** : FFmpeg décode chaque image dès son arrivée (mode faible latence), puis la fenêtre WPF affiche toujours la plus récente à chaque rafraîchissement de l'écran. Si l'affichage prend du retard, les images en trop sont sautées au lieu de s'accumuler.
4. **Son** (Android 11 ou plus récent) : le téléphone envoie le son en Opus, décodé par FFmpeg et joué par WASAPI (NAudio) avec un retard plafonné à 150 ms pour rester synchronisé avec l'image.
5. **Contrôle** : une connexion de plus transmet la souris, le clavier et le presse-papiers au téléphone.

| Fichier | Rôle |
|---|---|
| `Core/AdbRunner.cs` | Exécution des commandes adb |
| `Core/AdbDevices.cs` | Liste des appareils branchés |
| `Core/DeviceWatcher.cs` | Attente du téléphone pour la reconnexion automatique |
| `Core/ScrcpyServer.cs` | Déploiement et lancement du serveur sur le téléphone |
| `Core/ScrcpyConnector.cs` | Ouverture des connexions vidéo et contrôle |
| `Core/ScrcpyVideoStream.cs` | Lecture du protocole vidéo de scrcpy |
| `Core/ScrcpyControlChannel.cs` | Messages de contrôle (toucher, touches, texte, presse-papiers) |
| `Core/MediaPacketReader.cs` | Lecture des paquets vidéo et audio |
| `Core/ScrcpyAudioStream.cs`, `Core/AudioPipeline.cs` | Flux audio et sa boucle de lecture |
| `Core/OpusDecoder.cs`, `Core/AudioPlayer.cs` | Décodage Opus (FFmpeg) et lecture WASAPI (NAudio) |
| `Core/H264Decoder.cs` | Décodage FFmpeg et conversion en BGRA |
| `Core/FrameExchange.cs` | Triple tampon entre décodage et affichage |
| `Core/MirrorSession.cs` | Orchestration d'une session |
| `Core/UserSettings.cs` | Réglages enregistrés |
| `Core/AppLog.cs` | Journal de diagnostic |
| `Input/*.cs` | Souris, clavier et raccourcis transmis au téléphone |
| `MainWindow.xaml(.cs)` | Fenêtre principale |
| `SettingsWindow.xaml(.cs)` | Fenêtre de réglages |

## Installation pour les utilisateurs

1. Lancer `PhoneMirror-win-Setup.exe`. L'installation se fait pour l'utilisateur courant, sans droits administrateur et sans rien d'autre à installer. Elle crée un raccourci « Phone Mirror » dans le menu Démarrer.
2. Sur le téléphone : activer les options pour les développeurs et le **débogage USB**, brancher le câble et accepter l'autorisation qui s'affiche.

Windows SmartScreen peut avertir que l'éditeur est inconnu, car l'installeur n'est pas signé : « Informations complémentaires » puis « Exécuter quand même ».

Désinstallation : Paramètres Windows > Applications. Les réglages restent dans `%APPDATA%\PhoneMirror`.

## Développement

Il faut le **SDK .NET 10** (ou Visual Studio 2026), puis trois dépendances à placer dans `third_party` :

```
third_party/
├── adb/            adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll
├── ffmpeg/         avcodec-*.dll, avutil-*.dll, swscale-*.dll, etc.
└── scrcpy-server   (fichier sans extension)
```

- **adb** : contenu de « SDK Platform-Tools for Windows » (site développeurs Android). Si adb est déjà dans le PATH, ce dossier est facultatif.
- **scrcpy-server** : sur la page des releases GitHub de scrcpy, télécharger `scrcpy-server-v5.0.1` et le renommer `scrcpy-server`. La version doit correspondre exactement à `ServerVersion` dans `Core/MirrorOptions.cs`.
- **FFmpeg** : la build **LGPL** « shared » 64 bits de FFmpeg 9.0 de BtbN (`ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip`, releases GitHub de BtbN/FFmpeg-Builds). Seuls `avcodec-63.dll`, `avutil-61.dll`, `swscale-10.dll` et `swresample-7.dll` de son dossier `bin` sont nécessaires, plus `LICENSE.txt` renommé `LICENSE-FFmpeg.txt`. La version majeure des DLL doit correspondre à celle des paquets `FFmpeg.AutoGen` du `.csproj`.

Puis :

```
dotnet run -c Release          # lancer l'application
dotnet test PhoneMirror.slnx   # tests unitaires (protocole, réglages, adb)
```

### Construire l'installeur

```
.\installer\build-installer.ps1            # installer\out\PhoneMirror-win-Setup.exe
.\installer\build-installer.ps1 -Publish   # crée aussi une release GitHub (mises à jour automatiques)
.\installer\build-installer.ps1 -SharePath \\serveur\partage\PhoneMirror   # ou publie sur un dossier réseau
```

Le script lance les tests, publie une version autonome (avec le runtime .NET), puis l'empaquette avec [Velopack](https://velopack.io). Avant chaque publication, augmenter `<Version>` dans `PhoneMirror.csproj`.

Les applications installées vérifient les mises à jour au démarrage, téléchargent la nouvelle version en arrière-plan et l'installent à la fermeture. La source est par défaut les releases GitHub de ce dépôt. Pour en changer, renseigner `UpdateSource` dans `%APPDATA%\PhoneMirror\settings.json` :

```json
"UpdateSource": "\\\\serveur\\partage\\PhoneMirror"
```

Valeurs possibles : un dépôt GitHub (`https://github.com/…`), une adresse web contenant les fichiers produits par Velopack, ou un dossier local ou réseau.

## Utilisation

- **F2** : afficher ou masquer les statistiques (images décodées et affichées par seconde, erreurs).
- **F3** : réglages, enregistrés dans `%APPDATA%\PhoneMirror\settings.json` :
  - téléphone (utile quand plusieurs sont branchés), résolution, débit, images par seconde ;
  - son du téléphone sur le PC (activé par défaut ; pendant la recopie, le son ne sort plus du téléphone) ;
  - éteindre l'écran du téléphone pendant la recopie ;
  - fenêtre toujours au premier plan ;
  - montrer les clics par une onde (activé par défaut), pratique en partage d'écran.
- **F11** : plein écran.
- **Glisser-déposer** un fichier sur la fenêtre : un APK est installé, tout autre fichier est copié dans le dossier Téléchargements du téléphone.
- La position et la taille de la fenêtre sont mémorisées.
- Au débranchement, la recopie reprend toute seule quand le téléphone revient. Le bouton « Réessayer » reste disponible.
- Journal de diagnostic : `%APPDATA%\PhoneMirror\journal.log`, recréé à chaque lancement.

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

## Limites

- **Appels téléphoniques** : le son d'un appel ne passe pas par la sortie audio capturée (Android le traite à part), il reste donc sur le téléphone, et le micro du PC ne peut pas être envoyé dans l'appel. Pour téléphoner depuis le PC, utiliser « Lien avec Windows » (Phone Link) en Bluetooth, qui fonctionne en parallèle de Phone Mirror.
- **Son** : Android 11 minimum ; certaines applications interdisent la capture de leur son.

## Licences

- Serveur scrcpy : Apache 2.0.
- FFmpeg : build LGPL, en DLL séparées non modifiées (licence fournie dans `third_party/ffmpeg`). Ne pas revenir à une build « full » (GPL) pour l'installeur.
- Velopack : MIT. NAudio : MIT.
- Icône : dessinée par `Assets/make-icon.ps1` (à relancer après modification).
- adb (Android SDK Platform-Tools) : soumis aux conditions de licence du SDK Android. L'installeur l'embarque, comme le fait scrcpy ; à valider avant une diffusion hors de l'entreprise.
