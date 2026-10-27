# Catalogue des mods (`catalog.json`)

Le launcher télécharge [`catalog.json`](../catalog.json) depuis la branche `main` de ce dépôt à chaque démarrage. Une copie est aussi intégrée à l'exe, au cas où il n'y aurait pas de réseau. Le launcher utilise la version la plus récente des deux, d'après le champ `updated`.

**Pour ajouter ou corriger un mod**, il suffit de modifier ce fichier sur `main`, sans recompiler. Pense à mettre la date du jour dans `updated`.

## Structure

```json
{
  "schema": 1,
  "updated": "2026-10-27",
  "mods": [ { … }, { … } ]
}
```

## Champs d'un mod

| Champ | Rôle |
|---|---|
| `id` | Identifiant unique, en minuscules avec des tirets (ex. `unlock-the-fps`). |
| `name`, `author` | Nom affiché et auteur. |
| `category` | Au choix : `multiplayer`, `qol`, `graphics`, `overhaul` ou `other`. |
| `description` | Texte simple, ou `{ "fr": …, "en": …, "es": … }`. |
| `version` | Version affichée (facultatif). Par défaut, c'est le tag de la release GitHub. |
| `nexus` | Numéro du mod sur Nexus. Il sert au lien vers la page et à reconnaître un `.zip` téléchargé depuis Nexus. |
| `source` | D'où vient le mod (voir ci-dessous). |
| `games` | **Versions de `eldenring.exe` sur lesquelles le mod a été testé**, ex. `["2.7.2"]` ou `["2.7.*"]`. Une liste vide `[]` veut dire « non vérifié ». |
| `natives` | DLL à charger, avec des chemins relatifs à la racine de l'archive (ex. `SeamlessCoop/ersc.dll`). |
| `packages` | Dossiers qui remplacent des fichiers du jeu (`regulation.bin`, `chr/`, `parts/`…). `""` désigne toute l'archive. |
| `deploy` | Fichiers à **copier dans le dossier du jeu**, pour les mods qui lisent leur config depuis `Game\…` : `[{ "from": "mods/X", "to": "mods/X" }]`. Le launcher les retire quand le mod est désactivé. |
| `conflicts`, `requires` | Identifiants de mods incompatibles ou requis (pris en compte à partir de la phase 2). |
| `gameplay` | `true` si le mod change le gameplay : en coop, tous les joueurs doivent l'avoir. |
| `settings` | Réglages affichés dans le launcher (voir ci-dessous). |

### `source`

```json
{ "type": "github", "repo": "auteur/depot", "tag": "v1.2", "asset": "^MonMod\\.zip$", "sha256": "…" }
```

- `tag` est facultatif : sans lui, le launcher prend la dernière release, et le mod est **mis à jour automatiquement**.
- `asset` est une expression régulière qui désigne le fichier à télécharger dans la release.
- `sha256` est facultatif quand GitHub publie lui-même l'empreinte (c'est le cas des releases récentes). Il est **obligatoire en pratique** pour les vieux fichiers, pour lesquels GitHub n'en publie pas.
- Un mod hébergé uniquement sur Nexus utilise `{ "type": "nexus" }`. Le launcher ouvre alors la page Nexus du mod : l'utilisateur télécharge le `.zip`, puis « Importer un .zip » le reconnaît grâce à son numéro Nexus.

### `settings`

```json
{
  "file": "mods/MonMod/config.ini",
  "tabs": [
    {
      "title": { "fr": "GÉNÉRAL", "en": "GENERAL", "es": "GENERAL" },
      "reset": true,
      "items": [
        { "type": "note", "label": "Texte d'aide" },
        { "key": "enabled", "section": "general", "type": "toggle", "default": "1", "label": "Activé" },
        { "key": "limit", "section": "general", "type": "slider", "min": 30, "max": 360, "step": 5, "default": "300", "format": "{0} FPS", "label": "Limite" },
        { "key": "mode", "section": "general", "type": "choice", "default": "0", "choices": ["Lent", "Rapide"], "label": "Mode" },
        { "key": "name", "section": "general", "type": "text", "maxLength": 32, "label": "Nom" }
      ]
    }
  ]
}
```

- **`file`** est relatif au dossier du mod. Pour l'instant, seul le format INI est pris en charge.
- **Types de contrôles** :
  - `toggle` écrit 0/1 (ou true/false, si c'est ce que le fichier utilise déjà) ;
  - `slider` est un curseur entre `min` et `max` ;
  - `choice` enregistre la position du choix (0, 1, 2…) ;
  - `text` est un champ de saisie ;
  - `note` affiche seulement un texte d'aide.
- **Textes traduits** : tous les textes (`label`, `hint`, `placeholder`, `format`, `zero`, chaque `choice`) acceptent `{ "fr", "en", "es" }`.
- **Réglages conservés** : les valeurs de l'utilisateur survivent aux mises à jour et aux réinstallations (copie dans `%LOCALAPPDATA%\ERSeamlessLauncher\settings`).
- **Mod importé sans `settings`** : le launcher génère une page de réglages à partir de son `.ini` (une section = un onglet, 0/1 = interrupteur).

## Vérifier un mod pour une nouvelle version du jeu

1. Installe le mod et lance une partie.
2. S'il fonctionne, ajoute la version affichée dans le launcher (« Elden Ring v2.7.2 ») à `games`.
3. Mets à jour `updated`.

Le bouton « Testé OK » du launcher fait la même chose, mais seulement sur ton PC.
