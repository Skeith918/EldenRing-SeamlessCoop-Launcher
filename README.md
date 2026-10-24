# Elden Ring – Seamless Co-op Launcher

Un seul fichier : `EldenRing-SeamlessCoop-Launcher.exe` (~700 Ko, aucune installation, .NET Framework 4.8 déjà présent dans Windows 10/11).

![Aperçu du launcher](docs/screenshot.png)

## Télécharger

1. Télécharge `EldenRing-SeamlessCoop-Launcher.exe` dans la [dernière release](../../releases/latest).
2. Lance-le.

L'exe n'est pas signé, donc Windows SmartScreen peut afficher « Windows a protégé votre ordinateur ». Clique alors sur « Informations complémentaires », puis « Exécuter quand même ».

## Ce qu'il fait

- **Trouve le jeu tout seul** : il cherche dans toutes les bibliothèques Steam, et sinon tu cliques sur « Localiser » pour lui indiquer `eldenring.exe`.
- **Installe et met à jour Seamless Co-op** depuis le GitHub officiel du mod (`yuiamoroll/EldenRingSeamlessCoopRelease`). Il vérifie l'empreinte SHA-256 publiée sur GitHub et conserve tes réglages à chaque mise à jour. Au démarrage, il regarde s'il existe une nouvelle version et affiche alors un bouton « Mettre à jour ».
- **Configure le mod** depuis l'interface (`SeamlessCoop\ersc_settings.ini`) :
  - mot de passe ;
  - envahisseurs, malus à la mort, invocations, mode spectateur ;
  - infos affichées au-dessus des joueurs, logos d'intro, volume ;
  - les 6 réglages de difficulté par joueur ;
  - extension des sauvegardes, affichage du Steam ID.
- **Laisse EAC et le online officiel de côté** : le jeu est lancé par `ersc_launcher.exe`, donc sans Easy Anti-Cheat et sans connexion aux serveurs de FromSoftware. Aucun fichier du jeu n'est modifié. Le bouton « Jouer » de Steam lance toujours la version vanilla avec EAC.
- **Sauvegardes** :
  - à la 1re installation, ta progression vanilla (`ER0000.sl2`) est copiée vers `ER0000.co2`, sauf si une sauvegarde Seamless existe déjà ;
  - un bouton permet de refaire cet import ;
  - avant chaque partie, une copie de sécurité est faite dans `%APPDATA%\EldenRing\<SteamID>\ERSC_Backups` (les 10 dernières sont gardées) ;
  - le `.sl2` vanilla n'est jamais modifié.
- **Lance la partie** : il démarre Steam si besoin, attend que tu sois connecté, lance le jeu avec le mod, puis se ferme pour ne consommer aucune ressource pendant la partie (option).
- **Trois langues** : français, anglais et espagnol. Au premier lancement, il prend la langue de Windows (anglais si ce n'est aucune des trois). Le bouton 🌐 en haut à droite change la langue sans redémarrer, et le choix est mémorisé.

## Bon à savoir

- **Antivirus** : Seamless Co-op injecte une DLL dans le jeu, donc Windows Defender le signale parfois (faux positif). Si le launcher te dit que des fichiers ont disparu après l'installation, ajoute une exclusion pour le dossier `ELDEN RING\Game`, puis clique sur « Réinstaller le mod ».
- **Tous les joueurs** doivent avoir la même version du mod et le même mot de passe.
- **Mod en français / espagnol** : depuis la v1.9, Seamless Co-op ne fournit plus que l'anglais. Avec un jeu en français ou en espagnol, il affichait donc « Translation file for locale "french" not found ». Le launcher installe lui-même `french.json`, `spanish.json` et `latam.json` dans `SeamlessCoop\locale` :
  - ces fichiers reprennent les traductions de la communauté (v1.8.9), complétées et corrigées (`assets/locale`) ;
  - un texte ajouté ou modifié dans une future version du mod reste en anglais, au lieu d'afficher une traduction fausse ;
  - si le mod fournit de nouveau ses propres traductions, le launcher ne les remplace pas.
- **Changer de fond** : pose une image `launcher-background.jpg` (ou `.png`) à côté de l'exe.
- **Fichiers du launcher** : préférences et journal dans `%LOCALAPPDATA%\ERSeamlessLauncher`.
- **Ajouter une langue** : tous les textes sont dans `src/Lang.cs`, chaque ligne contenant le français, l'anglais et l'espagnol. Pour une nouvelle langue, ajoute son code, son nom et une traduction de plus sur chaque ligne, puis recompile.

## Recompiler

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Le build utilise `csc.exe` fourni avec Windows. Les sources sont dans `src/`, les ressources dans `assets/` et le banc d'essai dans `tests/`.

## Crédits et licence

Projet de fan non officiel, sans lien avec Bandai Namco, FromSoftware ou l'auteur de Seamless Co-op.

- Seamless Co-op © Yui (LukeYui). Le mod est téléchargé depuis son dépôt officiel, il n'est pas inclus ici.
- Traductions du mod (`assets/locale`) : communauté Seamless Co-op, les crédits sont dans chaque fichier. Elles ont été complétées pour la v2.0.1.
- Image de fond : capture officielle d'Elden Ring (Steam), © Bandai Namco / FromSoftware.
- Police Cinzel sous licence SIL OFL (`assets/Cinzel-OFL.txt`).
- Le code du launcher est sous licence MIT (voir `LICENSE`). L'image, la police et les traductions gardent leurs propres droits.
