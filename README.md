# Phone Mirror

**Affiche et pilote l'écran de votre téléphone Android sur votre PC Windows**, par câble USB, avec la souris et le clavier du PC.

- Image fluide (jusqu'à 60 i/s, ou plus) et quasi instantanée : moins d'un dixième de seconde de décalage.
- Le son du téléphone sort sur le PC (Android 11 ou plus récent).
- Souris, clavier et copier-coller entre le PC et le téléphone.
- Pratique en réunion ou en démonstration : chaque clic est signalé par une petite onde visible par les autres participants.
- **Mode démonstration** : n'afficher qu'une seule application (par exemple Salesforce Field Service), sans jamais montrer le reste du téléphone.
- Rien à installer sur le téléphone, aucun compte, aucune connexion Internet : tout passe par le câble.
- Mises à jour automatiques.

**[⬇ Télécharger l'installeur (Windows 10/11, 64 bits)](https://github.com/fauveauq/phone-mirror/releases/latest/download/PhoneMirror-win-Setup.exe)**. Les nouveautés de chaque version sont listées sur la [page des versions](https://github.com/fauveauq/phone-mirror/releases).

---

## Avant d'installer : compatibilité

| Il vous faut | Non pris en charge |
|---|---|
| Un téléphone **Android 5** ou plus récent (Android 11 pour le son) | **iPhone et iPad** : Phone Mirror fonctionne uniquement avec Android |
| Un PC **Windows 10 ou 11**, 64 bits | **Mac et Linux** |
| Un **câble USB de données** | **Connexion sans fil** : uniquement par câble |
| Le **débogage USB** autorisé sur le téléphone | **Téléphone géré par l'entreprise** (MDM, Intune…) : le débogage USB peut y être bloqué, vérifiez avant d'installer |

---

## Installation

### 1. Sur le PC

1. Téléchargez [PhoneMirror-win-Setup.exe](https://github.com/fauveauq/phone-mirror/releases/latest/download/PhoneMirror-win-Setup.exe) et lancez-le.
2. Si Windows affiche « Windows a protégé votre ordinateur », cliquez sur **Informations complémentaires**, puis **Exécuter quand même**. Ce message s'affiche parce que l'installeur n'est pas signé numériquement.
3. L'installation dure quelques secondes. Elle ne demande **pas de droits administrateur**, et Phone Mirror s'ouvre tout seul ensuite. Pour le relancer plus tard, cherchez **Phone Mirror** dans le menu Démarrer.

Aucun autre logiciel n'est nécessaire : tout ce qu'il faut est fourni avec l'installeur.

### 2. Sur le téléphone (une seule fois)

Le PC a besoin de l'autorisation du téléphone, via le **débogage USB** :

1. **Activez les options pour les développeurs.** Ouvrez **Paramètres › À propos du téléphone** (sur Samsung : **Paramètres › À propos du téléphone › Informations sur le logiciel**). Touchez **7 fois** la ligne **Numéro de build**, puis saisissez votre code si le téléphone le demande. Le message « Vous êtes maintenant développeur » s'affiche.
2. **Activez le débogage USB.** Ouvrez **Paramètres › Options pour les développeurs** (sur certains téléphones : **Paramètres › Système › Options pour les développeurs**) et activez **Débogage USB**.
   - Sur Xiaomi, Redmi ou POCO, activez aussi **Débogage USB (paramètres de sécurité)**, sinon la souris et le clavier ne fonctionnent pas.
3. **Branchez le téléphone** au PC avec un câble USB de données (certains câbles de charge ne transportent pas les données).
4. Sur le téléphone, la question **« Autoriser le débogage USB ? »** apparaît. Cochez **Toujours autoriser sur cet ordinateur**, puis touchez **Autoriser**.

L'écran du téléphone apparaît alors dans la fenêtre Phone Mirror. Les fois suivantes, il suffit de brancher le câble.

---

## Utilisation

Ouvrez Phone Mirror, puis branchez le téléphone, dans l'ordre que vous voulez. La recopie démarre toute seule. Si le câble se débranche, elle reprend d'elle-même dès que le téléphone revient.

### Souris

| Sur le PC | Sur le téléphone |
|---|---|
| Clic gauche | Toucher |
| Clic gauche maintenu et glisser | Faire défiler, balayer, déplacer |
| Molette | Défilement vertical |
| Maj + molette | Défilement horizontal |
| Clic droit | Retour (rallume aussi l'écran s'il est éteint) |
| Clic de la molette | Accueil |
| Boutons latéraux de la souris | Applications récentes / volet des notifications |

### Clavier

Tapez directement au clavier : le texte arrive sur le téléphone, accents compris, ainsi que les touches Entrée, Retour arrière et les flèches.

| Raccourci | Effet |
|---|---|
| **Ctrl+V** | Colle sur le téléphone le texte copié sur le PC |
| **Ctrl+C** / **Ctrl+X** | Copie / coupe la sélection du téléphone vers le PC |
| **Alt+H** | Accueil |
| **Alt+B** | Retour |
| **Alt+S** | Applications récentes |
| **Alt+↑** / **Alt+↓** | Volume + / − |
| **Alt+P** | Bouton marche/arrêt |
| **Alt+N** / **Alt+Maj+N** | Ouvrir / fermer les notifications |
| **Alt+R** | Faire pivoter l'écran du téléphone |
| **Alt+O** / **Alt+Maj+O** | Éteindre / rallumer l'écran du téléphone (la recopie continue) |
| **F2** | Afficher ou masquer les statistiques (images par seconde) |
| **F3** | Réglages |
| **F11** | Plein écran |

Un texte copié sur le téléphone arrive aussi automatiquement dans le presse-papiers du PC.

### Fichiers

Faites glisser un fichier sur la fenêtre :
- un fichier **.apk** est installé sur le téléphone ;
- tout autre fichier (photo, PDF…) est copié dans le dossier **Téléchargements** du téléphone.

### Réglages (F3)

| Réglage | Usage |
|---|---|
| Téléphone | Choisir le téléphone quand plusieurs sont branchés |
| Résolution, débit, images par seconde | Netteté et fluidité. Les valeurs par défaut conviennent à la plupart des cas. Baissez-les si le PC ou le téléphone peinent. |
| Son du téléphone sur le PC | Activé par défaut. Pendant la recopie, le son sort du PC et plus du téléphone. |
| Éteindre l'écran du téléphone | Économise la batterie et garde le contenu discret ; la recopie continue sur le PC |
| Toujours au premier plan | La fenêtre reste au-dessus des autres |
| Afficher | Tout l'écran du téléphone, ou **une seule application** (voir ci-dessous) |
| Clics | Montrer ou masquer les ondes de clic, et choisir leur style (onde et point, onde seule ou point seul), leur couleur et leur taille |

La position et la taille de la fenêtre sont mémorisées d'une fois sur l'autre.

---

### Mode démonstration : une seule application

Pour une démonstration, par exemple simuler le travail d'un technicien sur le terrain avec Salesforce Field Service, choisissez dans **F3 › Afficher** l'application à montrer.

- Phone Mirror crée un **écran virtuel** sur le téléphone et y lance l'application. Seul cet écran est envoyé au PC : la barre d'état, les notifications, l'écran d'accueil et les autres applications n'apparaissent jamais.
- Si l'application est quittée ou fermée, l'image est remplacée par l'écran **« Démonstration en pause »**, avec un bouton pour la relancer.
- L'écran du téléphone reste libre pendant ce temps : vous pouvez vous en servir sans que ce soit diffusé.
- **Connectez-vous à l'application en mode normal avant la démonstration.** Beaucoup d'écrans de connexion sont protégés contre la capture et s'afficheraient en noir.

Limites :
- Le son reste celui de tout le téléphone.
- Les demandes d'autorisation Android s'affichent, mais si l'application ouvre une autre application (appareil photo, navigation GPS…), l'écran passe en pause.
- Le clavier du téléphone peut s'ouvrir sur l'écran physique : utilisez plutôt le clavier du PC.

## Mises à jour

Elles sont automatiques. Au démarrage, Phone Mirror vérifie s'il existe une nouvelle version et la télécharge en arrière-plan. Un message signale qu'elle est prête, et elle s'installe à la **fermeture** de l'application. Elle est donc active au lancement suivant.

## Questions fréquentes

**La fenêtre reste sur « En attente du téléphone ».**
- Vérifiez que le débogage USB est activé (étape 2 de l'installation) et essayez un autre câble ou un autre port USB.
- Débranchez puis rebranchez le téléphone, et regardez s'il affiche la question « Autoriser le débogage USB ? ».
- Si vous avez refusé cette question par erreur, ouvrez **Options pour les développeurs › Révoquer les autorisations de débogage USB**, puis rebranchez.

**L'image s'affiche mais la souris ne fait rien (Xiaomi, Redmi, POCO).**
Activez **Débogage USB (paramètres de sécurité)** dans les options pour les développeurs.

**Il n'y a pas de son.**
Le son demande Android 11 ou plus récent. Certaines applications interdisent la capture de leur son, par exemple Netflix.

**Pendant un appel téléphonique, le son reste sur le téléphone.**
C'est une limite d'Android, qui isole le son des appels. Pour téléphoner depuis le PC, utilisez **Lien avec Windows** (Phone Link) en Bluetooth : il fonctionne en même temps que Phone Mirror.

**L'écran est noir alors que l'application est ouverte.**
L'écran affiché est protégé contre la capture, comme c'est souvent le cas pour un écran de connexion ou de paiement. Aucune application de recopie ne peut l'afficher. Connectez-vous d'abord sur le téléphone. Si tous les écrans restent noirs, c'est la politique de sécurité de l'application ou de l'organisation qui interdit la capture.

**L'image est moins nette en mode paysage.**
Dans F3, choisissez la résolution **Native** et un débit plus élevé.

**Où sont les réglages et le journal ?**
Dans `%APPDATA%\PhoneMirror` : `settings.json` pour les réglages, et `journal.log` pour le journal de diagnostic, recréé à chaque lancement. Joignez le journal si vous signalez un problème.

**Comment désinstaller ?**
Ouvrez **Paramètres Windows › Applications › Applications installées › Phone Mirror › Désinstaller**. Désactivez ensuite le débogage USB sur le téléphone si vous ne vous en servez plus.

## Confidentialité

Tout reste entre le téléphone et le PC, par le câble USB : aucune image, aucun son ni aucune frappe n'est envoyé sur Internet. La seule connexion Internet sert à vérifier les mises à jour sur cette page GitHub.

---

## Pour les développeurs

Le fonctionnement, la compilation et la construction de l'installeur sont décrits dans [DEVELOPMENT.md](DEVELOPMENT.md).

## Licences

Phone Mirror s'appuie sur :
- le serveur de [scrcpy](https://github.com/Genymobile/scrcpy) (Apache 2.0) ;
- [FFmpeg](https://ffmpeg.org) en build LGPL ;
- [NAudio](https://github.com/naudio/NAudio) et [Velopack](https://velopack.io) (MIT) ;
- adb (Android SDK Platform-Tools).

Le détail des licences se trouve dans [DEVELOPMENT.md](DEVELOPMENT.md#licences).
