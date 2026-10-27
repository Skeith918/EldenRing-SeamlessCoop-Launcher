using System;
using System.Collections.Generic;
using System.Globalization;

namespace ERSeamless
{
    /// <summary>Textes du launcher. Chaque entrée : { français, anglais, espagnol }. Les textes des mods viennent du catalogue.</summary>
    public static class L
    {
        public static readonly string[] Codes = { "fr", "en", "es" };
        public static readonly string[] Names = { "Français", "English", "Español" };

        static int index = 1;

        public static string Code { get { return Codes[index]; } }

        public static CultureInfo Culture { get { return CultureInfo.GetCultureInfo(Code); } }

        public static IEnumerable<string> Keys { get { return Table.Keys; } }

        /// <summary>Langue de Windows si elle est disponible, sinon anglais.</summary>
        public static string Detect()
        {
            var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Array.IndexOf(Codes, two) >= 0 ? two : "en";
        }

        public static void Set(string code)
        {
            int i = Array.IndexOf(Codes, code ?? "");
            index = i >= 0 ? i : Array.IndexOf(Codes, Detect());
        }

        public static string T(string key)
        {
            string[] t;
            return Table.TryGetValue(key, out t) ? t[index] : key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(Culture, T(key), args);
        }

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ── écran principal ──
            { "tagline", new[] { "Explorez l'Entre-terre à plusieurs, sans interruption.", "Explore the Lands Between together, seamlessly.", "Explora las Tierras Intermedias en compañía, sin interrupciones." } },
            { "lbl_game", new[] { "JEU", "GAME", "JUEGO" } },
            { "lbl_mods", new[] { "MODS", "MODS", "MODS" } },
            { "lbl_engine", new[] { "MOTEUR", "ENGINE", "MOTOR" } },
            { "lang_tip", new[] { "Langue du launcher", "Launcher language", "Idioma del launcher" } },
            { "nav_play", new[] { "JOUER", "PLAY", "JUGAR" } },
            { "nav_mods", new[] { "MODS", "MODS", "MODS" } },
            { "manage", new[] { "Gérer", "Manage", "Gestionar" } },
            { "not_found", new[] { "Introuvable", "Not found", "No encontrado" } },
            { "game_tip_missing", new[] { "Elden Ring n'a pas été trouvé dans tes bibliothèques Steam : indique où se trouve eldenring.exe.", "Elden Ring wasn't found in your Steam libraries: point to eldenring.exe.", "No se encontró Elden Ring en tus bibliotecas de Steam: indica dónde está eldenring.exe." } },
            { "browse_change", new[] { "Changer", "Change", "Cambiar" } },
            { "browse_locate", new[] { "Localiser", "Locate", "Buscar" } },
            { "browse_title", new[] { "Sélectionne eldenring.exe (dossier ELDEN RING\\Game)", "Select eldenring.exe (ELDEN RING\\Game folder)", "Selecciona eldenring.exe (carpeta ELDEN RING\\Game)" } },
            { "mods_none", new[] { "Aucun mod installé", "No mods installed", "Ningún mod instalado" } },
            { "solo_mode", new[] { "Seamless désactivé (solo)", "Seamless disabled (solo)", "Seamless desactivado (solo)" } },
            { "plus_mods", new[] { " + {0} mod(s)", " + {0} mod(s)", " + {0} mod(s)" } },
            { "n_updates", new[] { " · {0} mise(s) à jour", " · {0} update(s)", " · {0} actualización(es)" } },
            { "unverified_tip", new[] { "Non vérifiés pour la version {0} : {1}", "Not verified for version {0}: {1}", "No verificados para la versión {0}: {1}" } },
            { "engine_me3_status", new[] { "me3 {0} · EAC et online officiel coupés", "me3 {0} · EAC and official online off", "me3 {0} · EAC y online oficial desactivados" } },
            { "engine_me3_update", new[] { "  →  {0} disponible", "  →  {0} available", "  →  {0} disponible" } },
            { "engine_missing", new[] { "me3 sera installé", "me3 will be installed", "me3 se instalará" } },
            { "engine_classic_status", new[] { "ersc_launcher · Seamless seul", "ersc_launcher · Seamless only", "ersc_launcher · solo Seamless" } },
            { "engine_tip", new[] {
                "me3 (ModEngine 3) lance Elden Ring avec tes mods, sans Easy Anti-Cheat et sans connexion aux serveurs officiels de FromSoftware : aucun risque de ban. Les mods restent hors du dossier du jeu, et le bouton Jouer de Steam lance toujours la version vanilla avec EAC.",
                "me3 (ModEngine 3) launches Elden Ring with your mods, without Easy Anti-Cheat and without connecting to FromSoftware's official servers: no ban risk. Mods stay outside the game folder, and Steam's Play button still launches the vanilla game with EAC.",
                "me3 (ModEngine 3) inicia Elden Ring con tus mods, sin Easy Anti-Cheat y sin conectarse a los servidores oficiales de FromSoftware: sin riesgo de baneo. Los mods se quedan fuera de la carpeta del juego, y el botón Jugar de Steam sigue iniciando el juego vanilla con EAC." } },
            { "steam_ok", new[] { "Connecté", "Connected", "Conectado" } },
            { "steam_waiting", new[] { "En attente de connexion", "Waiting for login", "Esperando inicio de sesión" } },
            { "steam_autostart", new[] { "Sera démarré automatiquement", "Will be started automatically", "Se iniciará automáticamente" } },
            { "btn_locate_game", new[] { "LOCALISER LE JEU", "LOCATE THE GAME", "BUSCAR EL JUEGO" } },
            { "btn_install", new[] { "INSTALLER", "INSTALL", "INSTALAR" } },
            { "btn_play", new[] { "JOUER", "PLAY", "JUGAR" } },
            { "btn_update_n", new[] { "METTRE À JOUR ({0})", "UPDATE ({0})", "ACTUALIZAR ({0})" } },

            // ── réglages (page Jouer) ──
            { "reset_defaults", new[] { "Valeurs par défaut", "Default values", "Valores por defecto" } },
            { "section_launcher", new[] { "LAUNCHER", "LAUNCHER", "LAUNCHER" } },
            { "engine", new[] { "Moteur de lancement", "Launch engine", "Motor de arranque" } },
            { "engine_me3", new[] { "me3 (multi-mods)", "me3 (multi-mod)", "me3 (multi-mod)" } },
            { "engine_classic", new[] { "Seamless seul", "Seamless only", "Solo Seamless" } },
            { "auto_update", new[] { "Mises à jour automatiques", "Automatic updates", "Actualizaciones automáticas" } },
            { "auto_update_hint", new[] { "Mods et me3 mis à jour au démarrage", "Mods and me3 updated at startup", "Mods y me3 actualizados al iniciar" } },
            { "close_on_launch", new[] { "Fermer le launcher au lancement du jeu", "Close the launcher when the game starts", "Cerrar el launcher al iniciar el juego" } },
            { "backup", new[] { "Backup auto des personnages", "Auto-backup characters", "Copia automática de personajes" } },
            { "backup_hint", new[] { "Copie avant chaque partie (10 dernières gardées)", "Copy before each session (last 10 kept)", "Copia antes de cada partida (se guardan las 10 últimas)" } },
            { "btn_import", new[] { "Importer ma progression vanilla (.sl2 → Seamless)", "Import my vanilla progress (.sl2 → Seamless)", "Importar mi progreso vanilla (.sl2 → Seamless)" } },
            { "btn_game_folder", new[] { "Dossier du jeu", "Game folder", "Carpeta del juego" } },
            { "btn_saves_folder", new[] { "Dossier des saves", "Saves folder", "Carpeta de partidas" } },
            { "settings_of", new[] { "Réglages · {0}", "Settings · {0}", "Ajustes · {0}" } },
            { "close", new[] { "Fermer", "Close", "Cerrar" } },

            // ── page Mods ──
            { "sub_catalog", new[] { "CATALOGUE", "CATALOG", "CATÁLOGO" } },
            { "sub_installed", new[] { "INSTALLÉS ({0})", "INSTALLED ({0})", "INSTALADOS ({0})" } },
            { "sub_discover", new[] { "DÉCOUVRIR", "DISCOVER", "DESCUBRIR" } },
            { "import_zip", new[] { "Importer un .zip", "Import a .zip", "Importar un .zip" } },
            { "import_dir", new[] { "Importer un dossier", "Import a folder", "Importar una carpeta" } },
            { "mods_info", new[] { "Elden Ring v{0}", "Elden Ring v{0}", "Elden Ring v{0}" } },
            { "cat_compatible", new[] { "Compatibles avec ta version ({0})", "Compatible with your version ({0})", "Compatibles con tu versión ({0})" } },
            { "cat_empty", new[] { "Aucun mod du catalogue n'est encore vérifié pour cette version.", "No catalog mod is verified for this version yet.", "Ningún mod del catálogo está verificado aún para esta versión." } },
            { "cat_unverified", new[] { "Non vérifiés pour {0}", "Not verified for {0}", "No verificados para {0}" } },
            { "cat_unverified_hint", new[] {
                "Ces mods n'ont pas été testés sur ta version du jeu. Ils peuvent fonctionner : essaie-les, puis marque-les « Testé OK » dans Installés.",
                "These mods haven't been tested on your game version. They may work: try them, then mark them « Tested OK » in Installed.",
                "Estos mods no se han probado en tu versión del juego. Puede que funcionen: pruébalos y márcalos « Probado OK » en Instalados." } },
            { "installed_empty", new[] { "Aucun mod installé. Installe-en depuis le catalogue ou importe un .zip.", "No mods installed. Install some from the catalog or import a .zip.", "Ningún mod instalado. Instala alguno desde el catálogo o importa un .zip." } },
            { "load_order_hint", new[] {
                "Ordre de chargement : du haut vers le bas. L'interrupteur désactive un mod sans le désinstaller. Deux mods incompatibles ne peuvent pas être activés ensemble.",
                "Load order: top to bottom. The switch disables a mod without uninstalling it. Two incompatible mods can't be enabled together.",
                "Orden de carga: de arriba abajo. El interruptor desactiva un mod sin desinstalarlo. Dos mods incompatibles no se pueden activar a la vez." } },
            { "badge_incompatible", new[] { "Incompatible avec {0}", "Incompatible with {0}", "Incompatible con {0}" } },
            { "badge_blocked_by", new[] { "✕ Bloqué par {0}", "✕ Blocked by {0}", "✕ Bloqueado por {0}" } },
            { "blocked_tip", new[] { "Impossible à activer tant que {0} est activé.", "Can't be enabled while {0} is enabled.", "No se puede activar mientras {0} esté activado." } },
            { "catalog_will_be_disabled", new[] {
                "Incompatible avec {0}, qui est activé : il s'installera désactivé.",
                "Incompatible with {0}, which is enabled: it will be installed disabled.",
                "Incompatible con {0}, que está activado: se instalará desactivado." } },
            { "conflict_title", new[] { "Mods incompatibles", "Incompatible mods", "Mods incompatibles" } },
            { "conflict_line", new[] { "{0} : {1}", "{0}: {1}", "{0}: {1}" } },
            { "conflict_declared", new[] { "Incompatibilité vérifiée, déclarée dans le catalogue.", "Verified incompatibility, declared in the catalog.", "Incompatibilidad verificada, declarada en el catálogo." } },
            { "conflict_files", new[] {
                "Les deux remplacent les mêmes fichiers du jeu ({0}) : un seul des deux serait chargé.",
                "Both replace the same game files ({0}): only one of them would be loaded.",
                "Ambos reemplazan los mismos archivos del juego ({0}): solo se cargaría uno de los dos." } },
            { "conflict_deploy", new[] { "Les deux copient le même fichier dans le dossier du jeu ({0}).", "Both copy the same file into the game folder ({0}).", "Ambos copian el mismo archivo en la carpeta del juego ({0})." } },
            { "enable_blocked_text", new[] {
                "{0} ne peut pas être activé en même temps que {1} :\n\n{2}\n\nDésactive d'abord {1}, puis active {0}.",
                "{0} can't be enabled together with {1}:\n\n{2}\n\nDisable {1} first, then enable {0}.",
                "{0} no se puede activar a la vez que {1}:\n\n{2}\n\nDesactiva primero {1} y luego activa {0}." } },
            { "install_blocked_text", new[] {
                "{0} est installé mais reste désactivé, car il est incompatible avec {1} (activé) :\n\n{2}\n\nPour l'utiliser, désactive {1} puis active {0} dans Mods → Installés.",
                "{0} is installed but stays disabled because it's incompatible with {1} (enabled):\n\n{2}\n\nTo use it, disable {1}, then enable {0} in Mods → Installed.",
                "{0} está instalado pero sigue desactivado porque es incompatible con {1} (activado):\n\n{2}\n\nPara usarlo, desactiva {1} y luego activa {0} en Mods → Instalados." } },
            { "resolved_text", new[] {
                "Des mods incompatibles étaient activés en même temps. Pour éviter les plantages, ceux chargés en dernier ont été désactivés :\n\n{0}\n\nTu peux choisir lequel garder dans Mods → Installés.",
                "Incompatible mods were enabled at the same time. To avoid crashes, the ones loaded last were disabled:\n\n{0}\n\nYou can choose which one to keep in Mods → Installed.",
                "Había mods incompatibles activados a la vez. Para evitar cierres, se han desactivado los que se cargan al final:\n\n{0}\n\nPuedes elegir cuál conservar en Mods → Instalados." } },
            { "resolved_line", new[] { "{0}, incompatible avec {1} : {2}", "{0}, incompatible with {1}: {2}", "{0}, incompatible con {1}: {2}" } },
            { "category_multiplayer", new[] { "Multijoueur", "Multiplayer", "Multijugador" } },
            { "category_qol", new[] { "Confort", "Quality of life", "Calidad de vida" } },
            { "category_graphics", new[] { "Graphismes", "Graphics", "Gráficos" } },
            { "category_overhaul", new[] { "Refonte", "Overhaul", "Revisión total" } },
            { "category_other", new[] { "Divers", "Other", "Otros" } },
            { "badge_compatible", new[] { "✓ {0}", "✓ {0}", "✓ {0}" } },
            { "badge_tested", new[] { "✓ Testé", "✓ Tested", "✓ Probado" } },
            { "badge_unverified", new[] { "⚠ Non vérifié", "⚠ Not verified", "⚠ No verificado" } },
            { "badge_installed", new[] { "Installé", "Installed", "Instalado" } },
            { "badge_disabled", new[] { "Désactivé", "Disabled", "Desactivado" } },
            { "badge_gameplay", new[] { "Gameplay", "Gameplay", "Jugabilidad" } },
            { "gameplay_tip", new[] { "Modifie le gameplay : en coop, tous les joueurs doivent avoir exactement le même.", "Changes gameplay: in co-op, every player must have exactly the same.", "Cambia la jugabilidad: en cooperativo, todos los jugadores deben tener exactamente el mismo." } },
            { "badge_in_catalog", new[] { "Dans le catalogue", "In the catalog", "En el catálogo" } },
            { "by_author", new[] { "par {0}", "by {0}", "por {0}" } },
            { "source_github", new[] { "GitHub", "GitHub", "GitHub" } },
            { "source_nexus", new[] { "Nexus", "Nexus", "Nexus" } },
            { "source_manual", new[] { "Manuel", "Manual", "Manual" } },
            { "btn_install_mod", new[] { "Installer", "Install", "Instalar" } },
            { "btn_update_mod", new[] { "Mettre à jour", "Update", "Actualizar" } },
            { "btn_nexus_page", new[] { "Page Nexus", "Nexus page", "Página de Nexus" } },
            { "btn_mod_page", new[] { "Page du mod", "Mod page", "Página del mod" } },
            { "nexus_manual_tip", new[] { "Télécharge le .zip sur Nexus, puis clique sur « Importer un .zip ».", "Download the .zip from Nexus, then click « Import a .zip ».", "Descarga el .zip en Nexus y pulsa « Importar un .zip »." } },
            { "move_up", new[] { "Charger plus tôt", "Load earlier", "Cargar antes" } },
            { "move_down", new[] { "Charger plus tard", "Load later", "Cargar después" } },
            { "n_dlls", new[] { "{0} DLL", "{0} DLL(s)", "{0} DLL" } },
            { "n_packages", new[] { "{0} dossier(s) de fichiers du jeu", "{0} game file folder(s)", "{0} carpeta(s) de archivos del juego" } },
            { "has_deploy", new[] { "fichiers copiés dans le jeu", "files copied into the game folder", "archivos copiados en la carpeta del juego" } },
            { "btn_settings", new[] { "Réglages", "Settings", "Ajustes" } },
            { "btn_mark_tested", new[] { "Testé OK", "Tested OK", "Probado OK" } },
            { "btn_uninstall_mod", new[] { "Désinstaller", "Uninstall", "Desinstalar" } },
            { "enable_unverified_title", new[] { "Mod non vérifié", "Unverified mod", "Mod no verificado" } },
            { "enable_unverified_text", new[] {
                "{0} n'est pas vérifié pour la version {1} du jeu : il peut provoquer des plantages. L'activer quand même ?",
                "{0} isn't verified for game version {1}: it may cause crashes. Enable it anyway?",
                "{0} no está verificado para la versión {1} del juego: puede provocar cierres. ¿Activarlo de todos modos?" } },
            { "enable_anyway", new[] { "Activer", "Enable", "Activar" } },
            { "mark_tested_done", new[] { "{0} marqué comme testé sur la version {1}.", "{0} marked as tested on version {1}.", "{0} marcado como probado en la versión {1}." } },
            { "installing_mod", new[] { "Installation de {0}…", "Installing {0}…", "Instalando {0}…" } },
            { "installed_mod", new[] { "{0} installé.", "{0} installed.", "{0} instalado." } },
            { "installed_unverified", new[] {
                "{0} n'est pas encore vérifié pour la version {1} du jeu. Il est actif : lance une partie pour l'essayer, puis marque-le « Testé OK » (Mods → Installés) s'il fonctionne, ou désactive-le.",
                "{0} isn't verified for game version {1} yet. It's enabled: start a game to try it, then mark it « Tested OK » (Mods → Installed) if it works, or disable it.",
                "{0} aún no está verificado para la versión {1} del juego. Está activado: inicia una partida para probarlo y márcalo « Probado OK » (Mods → Instalados) si funciona, o desactívalo." } },
            { "uninstall_mod_title", new[] { "Désinstaller {0} ?", "Uninstall {0}?", "¿Desinstalar {0}?" } },
            { "uninstall_mod_text", new[] {
                "Le mod et les fichiers qu'il avait copiés dans le jeu seront supprimés. Ses réglages sont gardés pour une future réinstallation.",
                "The mod and the files it copied into the game will be removed. Its settings are kept for a future reinstall.",
                "Se eliminarán el mod y los archivos que copió en el juego. Sus ajustes se conservan para una futura reinstalación." } },
            { "import_zip_title", new[] { "Choisis l'archive .zip du mod", "Choose the mod's .zip archive", "Elige el archivo .zip del mod" } },
            { "filter_zip", new[] { "Archives .zip", ".zip archives", "Archivos .zip" } },
            { "import_dir_title", new[] { "Choisis le dossier du mod (déjà extrait)", "Choose the mod's folder (already extracted)", "Elige la carpeta del mod (ya extraída)" } },
            { "importing", new[] { "Import du mod…", "Importing the mod…", "Importando el mod…" } },
            { "import_mod_failed", new[] { "Import impossible", "Import failed", "No se pudo importar" } },
            { "import_mod_done_title", new[] { "Mod importé", "Mod imported", "Mod importado" } },
            { "import_mod_done", new[] { "{0} est installé : {1}. Règle son ordre ou désactive-le dans l'onglet Installés.", "{0} is installed: {1}. Set its order or disable it in the Installed tab.", "{0} está instalado: {1}. Ajusta su orden o desactívalo en la pestaña Instalados." } },
            { "import_catalog_note", new[] { "Reconnu comme {0} du catalogue : sa configuration vérifiée est utilisée.", "Recognized as {0} from the catalog: its verified setup is used.", "Reconocido como {0} del catálogo: se usa su configuración verificada." } },
            { "import_unverified_note", new[] { "Ce mod ne vient pas du catalogue : il est marqué « non vérifié » jusqu'à ce que tu le testes.", "This mod isn't from the catalog: it's marked « not verified » until you test it.", "Este mod no viene del catálogo: está marcado « no verificado » hasta que lo pruebes." } },
            { "import_loader_ignored", new[] { "Le chargeur dinput8.dll fourni avec le mod est ignoré : me3 le remplace.", "The dinput8.dll loader shipped with the mod is ignored: me3 replaces it.", "Se ignora el cargador dinput8.dll incluido con el mod: me3 lo sustituye." } },
            { "search_btn", new[] { "RECHERCHER", "SEARCH", "BUSCAR" } },
            { "search_placeholder", new[] { "Rechercher un mod sur Nexus…", "Search a mod on Nexus…", "Buscar un mod en Nexus…" } },
            { "discover_hint", new[] {
                "Mods de Nexus non vérifiés (contenu adulte exclu). Sans compte Nexus : ouvre la page, télécharge le .zip, puis « Importer un .zip ».",
                "Unverified Nexus mods (adult content excluded). Without a Nexus account: open the page, download the .zip, then « Import a .zip ».",
                "Mods de Nexus no verificados (contenido adulto excluido). Sin cuenta de Nexus: abre la página, descarga el .zip y luego « Importar un .zip »." } },
            { "searching", new[] { "Recherche…", "Searching…", "Buscando…" } },
            { "search_failed", new[] { "Recherche impossible : {0}", "Search failed: {0}", "No se pudo buscar: {0}" } },
            { "search_none", new[] { "Aucun résultat.", "No results.", "Sin resultados." } },
            { "updated_on", new[] { "mis à jour le {0}", "updated {0}", "actualizado el {0}" } },

            // ── installation, mises à jour ──
            { "ok", new[] { "OK", "OK", "Aceptar" } },
            { "cancel", new[] { "Annuler", "Cancel", "Cancelar" } },
            { "game_running_title", new[] { "Elden Ring est ouvert", "Elden Ring is running", "Elden Ring está abierto" } },
            { "game_running_install", new[] { "Ferme le jeu avant d'installer, mettre à jour ou retirer un mod.", "Close the game before installing, updating or removing a mod.", "Cierra el juego antes de instalar, actualizar o quitar un mod." } },
            { "access_title", new[] { "Autorisation nécessaire", "Permission needed", "Se necesita permiso" } },
            { "access_text", new[] {
                "Certains mods doivent copier des fichiers dans le dossier du jeu, qui est protégé en écriture. Le launcher va demander une seule fois les droits administrateur pour t'en donner l'accès.",
                "Some mods need to copy files into the game folder, which is write-protected. The launcher will ask for administrator rights once to give you access.",
                "Algunos mods necesitan copiar archivos en la carpeta del juego, que está protegida contra escritura. El launcher pedirá permisos de administrador una sola vez para darte acceso." } },
            { "access_ok", new[] { "Autoriser", "Allow", "Permitir" } },
            { "requesting_admin", new[] { "Demande des droits administrateur…", "Requesting administrator rights…", "Solicitando permisos de administrador…" } },
            { "access_failed", new[] {
                "Impossible d'écrire dans le dossier du jeu. Lance le launcher en administrateur (clic droit → Exécuter en tant qu'administrateur).",
                "Can't write to the game folder. Run the launcher as administrator (right-click → Run as administrator).",
                "No se puede escribir en la carpeta del juego. Ejecuta el launcher como administrador (clic derecho → Ejecutar como administrador)." } },
            { "downloading", new[] { "Téléchargement de {0}…", "Downloading {0}…", "Descargando {0}…" } },
            { "base_installed", new[] { "Seamless Co-op et me3 sont installés.", "Seamless Co-op and me3 are installed.", "Seamless Co-op y me3 están instalados." } },
            { "install_failed_title", new[] { "Installation impossible", "Installation failed", "No se pudo instalar" } },
            { "install_done_title", new[] { "Installation terminée", "Installation complete", "Instalación completada" } },
            { "need_password", new[] { "Dernière étape : choisis un mot de passe de session (le même pour tous tes amis), puis clique sur JOUER.", "Last step: pick a session password (the same for all your friends), then click PLAY.", "Último paso: elige una contraseña de sesión (la misma para todos tus amigos) y pulsa JUGAR." } },
            { "import_auto_done", new[] { "Ta progression vanilla a été copiée vers Seamless. Le fichier .sl2 d'origine n'a pas été touché.", "Your vanilla progress was copied to Seamless. The original .sl2 file was left untouched.", "Tu progreso vanilla se ha copiado a Seamless. El archivo .sl2 original no se ha tocado." } },
            { "import_auto_failed", new[] { "La copie de ta sauvegarde vanilla a échoué ({0}). Tu peux réessayer depuis l'onglet Avancé.", "Copying your vanilla save failed ({0}). You can retry from the Advanced tab.", "No se pudo copiar tu partida vanilla ({0}). Puedes reintentarlo desde la pestaña Avanzado." } },
            { "update_failed_title", new[] { "Mise à jour impossible", "Update failed", "No se pudo actualizar" } },
            { "updating", new[] { "Mise à jour de {0}…", "Updating {0}…", "Actualizando {0}…" } },
            { "updates_done", new[] { "Tout est à jour.", "Everything is up to date.", "Todo está actualizado." } },
            { "patch_title", new[] { "Elden Ring a été mis à jour (v{0})", "Elden Ring was updated (v{0})", "Elden Ring se ha actualizado (v{0})" } },
            { "patch_text", new[] {
                "Le jeu est passé de la version {0} à {1}. Ces mods ne sont pas vérifiés pour la nouvelle version et ont été désactivés pour éviter un plantage : {2}.\n\nTu peux les réactiver dans Mods → Installés, ou attendre leur mise à jour.",
                "The game went from version {0} to {1}. These mods aren't verified for the new version and were disabled to avoid a crash: {2}.\n\nYou can re-enable them in Mods → Installed, or wait for their update.",
                "El juego ha pasado de la versión {0} a la {1}. Estos mods no están verificados para la nueva versión y se han desactivado para evitar un cierre: {2}.\n\nPuedes reactivarlos en Mods → Instalados o esperar a su actualización." } },
            { "legacy_title", new[] { "Nettoyer l'ancienne installation ?", "Clean up the old installation?", "¿Limpiar la instalación anterior?" } },
            { "legacy_text", new[] {
                "L'ancienne version du launcher avait installé Seamless directement dans le dossier du jeu (ersc_launcher.exe et SeamlessCoop). Seamless est maintenant chargé par me3 depuis le dossier du launcher et tes réglages ont été repris : ces fichiers ne servent plus. Les supprimer ?",
                "The previous launcher version installed Seamless directly into the game folder (ersc_launcher.exe and SeamlessCoop). Seamless is now loaded by me3 from the launcher's folder and your settings were carried over: those files are no longer used. Remove them?",
                "La versión anterior del launcher instaló Seamless directamente en la carpeta del juego (ersc_launcher.exe y SeamlessCoop). Ahora me3 carga Seamless desde la carpeta del launcher y tus ajustes se han conservado: esos archivos ya no se usan. ¿Eliminarlos?" } },
            { "legacy_ok", new[] { "Nettoyer", "Clean up", "Limpiar" } },
            { "legacy_keep", new[] { "Garder", "Keep", "Conservar" } },
            { "legacy_done", new[] { "Ancienne installation supprimée : le dossier du jeu est redevenu vanilla.", "Old installation removed: the game folder is vanilla again.", "Instalación anterior eliminada: la carpeta del juego vuelve a ser vanilla." } },

            // ── lancement ──
            { "already_running_title", new[] { "Elden Ring est déjà lancé", "Elden Ring is already running", "Elden Ring ya está en marcha" } },
            { "already_running_text", new[] { "Ferme d'abord le jeu en cours (vanilla ou modé), puis relance.", "Close the running game first (vanilla or modded), then try again.", "Cierra primero el juego en curso (vanilla o con mods) y vuelve a intentarlo." } },
            { "classic_title", new[] { "Mode « Seamless seul »", "« Seamless only » mode", "Modo « Solo Seamless »" } },
            { "classic_needs_seamless", new[] { "Ce mode lance le jeu avec ersc_launcher.exe : il a besoin de Seamless Co-op installé et activé. Repasse sur me3 dans Avancé pour jouer sans Seamless.", "This mode launches the game with ersc_launcher.exe: it needs Seamless Co-op installed and enabled. Switch back to me3 in Advanced to play without Seamless.", "Este modo inicia el juego con ersc_launcher.exe: necesita Seamless Co-op instalado y activado. Vuelve a me3 en Avanzado para jugar sin Seamless." } },
            { "classic_ignores_text", new[] { "En mode « Seamless seul », les autres mods actifs sont ignorés. Lancer quand même ?", "In « Seamless only » mode, the other enabled mods are ignored. Launch anyway?", "En modo « Solo Seamless », los demás mods activos se ignoran. ¿Iniciar de todos modos?" } },
            { "no_password_title", new[] { "Aucun mot de passe de session", "No session password", "Sin contraseña de sesión" } },
            { "no_password_text", new[] { "Sans mot de passe, tu ne pourras ni rejoindre ni accueillir d'amis. Lancer quand même ?", "Without a password you can't join or host friends. Launch anyway?", "Sin contraseña no podrás unirte a tus amigos ni recibirlos. ¿Iniciar de todos modos?" } },
            { "launch_anyway", new[] { "Lancer quand même", "Launch anyway", "Iniciar igualmente" } },
            { "choose_password", new[] { "Choisir un mot de passe", "Set a password", "Elegir contraseña" } },
            { "steam_missing", new[] { "Steam est introuvable. Elden Ring a besoin de Steam pour fonctionner.", "Steam was not found. Elden Ring needs Steam to work.", "No se encontró Steam. Elden Ring necesita Steam para funcionar." } },
            { "starting_steam", new[] { "Démarrage de Steam…", "Starting Steam…", "Iniciando Steam…" } },
            { "steam_timeout", new[] { "Steam ne s'est pas connecté. Connecte-toi à Steam puis réessaie.", "Steam didn't log in. Sign in to Steam and try again.", "Steam no ha iniciado sesión. Inicia sesión en Steam y vuelve a intentarlo." } },
            { "backing_up", new[] { "Copie de sécurité des personnages…", "Backing up characters…", "Haciendo copia de los personajes…" } },
            { "launching", new[] { "Lancement d'Elden Ring…", "Launching Elden Ring…", "Iniciando Elden Ring…" } },
            { "launch_not_detected", new[] {
                "Le jeu ne semble pas avoir démarré. Vérifie que ton antivirus n'a pas bloqué ersc_launcher.exe et que Steam est bien connecté.",
                "The game doesn't seem to have started. Check that your antivirus didn't block ersc_launcher.exe and that Steam is logged in.",
                "Parece que el juego no se ha iniciado. Comprueba que tu antivirus no haya bloqueado ersc_launcher.exe y que Steam tenga la sesión iniciada." } },
            { "launch_not_detected_me3", new[] {
                "Le jeu ne semble pas avoir démarré. Vérifie que ton antivirus n'a pas bloqué me3 (dossier %LOCALAPPDATA%\\ERSeamlessLauncher\\me3) et que Steam est bien connecté. En secours, le mode « Seamless seul » est dans Avancé.",
                "The game doesn't seem to have started. Check that your antivirus didn't block me3 (folder %LOCALAPPDATA%\\ERSeamlessLauncher\\me3) and that Steam is logged in. As a fallback, « Seamless only » mode is in Advanced.",
                "Parece que el juego no se ha iniciado. Comprueba que tu antivirus no haya bloqueado me3 (carpeta %LOCALAPPDATA%\\ERSeamlessLauncher\\me3) y que Steam tenga la sesión iniciada. Como alternativa, el modo « Solo Seamless » está en Avanzado." } },
            { "deploy_warn", new[] { "Jeu lancé · {0} fichier(s) de mod non copié(s) car déjà présent(s) dans le jeu (voir le journal).", "Game launched · {0} mod file(s) not copied because already in the game folder (see the log).", "Juego iniciado · {0} archivo(s) de mod no copiado(s) porque ya existen en el juego (ver el registro)." } },
            { "launched", new[] { "Bon voyage, Sans-éclat.", "Safe travels, Tarnished.", "Buen viaje, Sinluz." } },
            { "launch_failed", new[] { "Lancement impossible", "Launch failed", "No se pudo iniciar" } },

            // ── sauvegardes ──
            { "no_vanilla_title", new[] { "Aucune sauvegarde vanilla", "No vanilla save", "Sin partida vanilla" } },
            { "no_vanilla_text", new[] { "Aucun fichier ER0000.sl2 trouvé dans {0}.", "No ER0000.sl2 file found in {0}.", "No se encontró ningún archivo ER0000.sl2 en {0}." } },
            { "game_running_import", new[] { "Ferme le jeu avant d'importer une sauvegarde.", "Close the game before importing a save.", "Cierra el juego antes de importar una partida." } },
            { "import_replace", new[] {
                "Ta sauvegarde Seamless actuelle (ER0000.{0}) va être remplacée par ta progression vanilla. Une copie de sécurité est faite juste avant, dans le dossier {1}.",
                "Your current Seamless save (ER0000.{0}) will be replaced by your vanilla progress. A backup is made right before, in the {1} folder.",
                "Tu partida Seamless actual (ER0000.{0}) se sustituirá por tu progreso vanilla. Justo antes se hace una copia de seguridad en la carpeta {1}." } },
            { "import_copy", new[] {
                "Ta progression vanilla (ER0000.sl2) va être copiée vers Seamless (ER0000.{0}). Le fichier vanilla n'est pas modifié.",
                "Your vanilla progress (ER0000.sl2) will be copied to Seamless (ER0000.{0}). The vanilla file is not modified.",
                "Tu progreso vanilla (ER0000.sl2) se copiará a Seamless (ER0000.{0}). El archivo vanilla no se modifica." } },
            { "import_title", new[] { "Importer ta progression vanilla ?", "Import your vanilla progress?", "¿Importar tu progreso vanilla?" } },
            { "import_ok", new[] { "Importer", "Import", "Importar" } },
            { "import_done", new[] { "Progression vanilla importée.", "Vanilla progress imported.", "Progreso vanilla importado." } },
            { "import_failed_title", new[] { "Import impossible", "Import failed", "No se pudo importar" } },

            // ── erreurs ──
            { "err_ratelimit", new[] { "GitHub limite temporairement les requêtes. Réessaie dans quelques minutes.", "GitHub is temporarily rate-limiting requests. Try again in a few minutes.", "GitHub está limitando las solicitudes temporalmente. Vuelve a intentarlo en unos minutos." } },
            { "err_network", new[] { "Impossible de joindre le serveur. Vérifie ta connexion internet puis réessaie.", "Can't reach the server. Check your internet connection and try again.", "No se puede conectar con el servidor. Comprueba tu conexión a internet y vuelve a intentarlo." } },
            { "err_access", new[] { "Accès refusé à un fichier. Ferme le jeu et réessaie, ou lance le launcher en administrateur.", "Access denied to a file. Close the game and try again, or run the launcher as administrator.", "Acceso denegado a un archivo. Cierra el juego y vuelve a intentarlo, o ejecuta el launcher como administrador." } },
            { "err_locked", new[] { "Un fichier est verrouillé ou inaccessible. Ferme Elden Ring et réessaie.", "A file is locked or unavailable. Close Elden Ring and try again.", "Un archivo está bloqueado o no es accesible. Cierra Elden Ring y vuelve a intentarlo." } },
            { "err_no_zip", new[] { "Cette version GitHub ne contient pas de fichier téléchargeable.", "This GitHub release has no downloadable file.", "Esta versión de GitHub no contiene ningún archivo descargable." } },
            { "err_no_asset", new[] { "Aucun fichier attendu dans la dernière version de {0}.", "No expected file in the latest release of {0}.", "Ningún archivo esperado en la última versión de {0}." } },
            { "err_not_downloadable", new[] { "{0} n'est pas téléchargeable automatiquement : récupère-le sur Nexus puis importe le .zip.", "{0} can't be downloaded automatically: get it from Nexus, then import the .zip.", "{0} no se puede descargar automáticamente: consíguelo en Nexus e importa el .zip." } },
            { "err_bad_host", new[] { "Adresse de téléchargement inattendue ({0}), installation annulée.", "Unexpected download address ({0}), installation cancelled.", "Dirección de descarga inesperada ({0}), instalación cancelada." } },
            { "err_sha", new[] {
                "Le fichier téléchargé ne correspond pas à l'empreinte SHA-256 attendue : installation annulée par sécurité. Réessaie plus tard.",
                "The downloaded file doesn't match the expected SHA-256 fingerprint: installation cancelled for safety. Try again later.",
                "El archivo descargado no coincide con la huella SHA-256 esperada: instalación cancelada por seguridad. Vuelve a intentarlo más tarde." } },
            { "err_archive", new[] { "Archive inattendue : des fichiers décrits par le catalogue sont absents. Le format du mod a peut-être changé.", "Unexpected archive: files described by the catalog are missing. The mod's format may have changed.", "Archivo inesperado: faltan archivos descritos en el catálogo. Puede que el formato del mod haya cambiado." } },
            { "err_archive_format", new[] { "Format non pris en charge : importe un .zip, ou extrais l'archive (.7z, .rar) et importe le dossier.", "Unsupported format: import a .zip, or extract the archive (.7z, .rar) and import the folder.", "Formato no compatible: importa un .zip, o extrae el archivo (.7z, .rar) e importa la carpeta." } },
            { "err_import_mismatch", new[] { "Cette archive ne correspond pas au mod {0} du catalogue.", "This archive doesn't match the catalog's {0} mod.", "Este archivo no corresponde al mod {0} del catálogo." } },
            { "err_import_empty", new[] { "Aucun fichier de mod reconnu (ni DLL, ni fichiers du jeu).", "No recognizable mod files (no DLL, no game files).", "No se reconoce ningún archivo de mod (ni DLL, ni archivos del juego)." } },
            { "err_antivirus", new[] {
                "Des fichiers du mod ont disparu juste après l'installation : ton antivirus les a sans doute mis en quarantaine (faux positif fréquent pour les mods qui s'injectent dans le jeu). Ajoute une exclusion pour %LOCALAPPDATA%\\ERSeamlessLauncher puis réinstalle.",
                "Some mod files vanished right after installation: your antivirus most likely quarantined them (a common false positive for mods that inject into the game). Add an exclusion for %LOCALAPPDATA%\\ERSeamlessLauncher, then reinstall.",
                "Algunos archivos del mod desaparecieron justo después de la instalación: probablemente tu antivirus los puso en cuarentena (falso positivo habitual en mods que se inyectan en el juego). Añade una exclusión para %LOCALAPPDATA%\\ERSeamlessLauncher y reinstala." } },
            { "err_me3_av", new[] {
                "Des fichiers de me3 ont disparu juste après l'installation : ton antivirus les a sans doute mis en quarantaine (faux positif connu). Ajoute une exclusion pour %LOCALAPPDATA%\\ERSeamlessLauncher puis réessaie.",
                "Some me3 files vanished right after installation: your antivirus most likely quarantined them (a known false positive). Add an exclusion for %LOCALAPPDATA%\\ERSeamlessLauncher, then try again.",
                "Algunos archivos de me3 desaparecieron justo después de la instalación: probablemente tu antivirus los puso en cuarentena (falso positivo conocido). Añade una exclusión para %LOCALAPPDATA%\\ERSeamlessLauncher y vuelve a intentarlo." } },
            { "warn_deploy_exists", new[] { "{0} existe déjà dans le dossier du jeu, non remplacé ({1})", "{0} already exists in the game folder, not replaced ({1})", "{0} ya existe en la carpeta del juego, no se reemplaza ({1})" } },
        };
    }
}
