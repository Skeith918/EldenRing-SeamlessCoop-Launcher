using System;
using System.Collections.Generic;
using System.Globalization;

namespace ERSeamless
{
    /// <summary>Textes du launcher. Chaque entrée : { français, anglais, espagnol }.</summary>
    public static class L
    {
        public static readonly string[] Codes = { "fr", "en", "es" };
        public static readonly string[] Names = { "Français", "English", "Español" };

        static int index = 1;

        public static string Code { get { return Codes[index]; } }

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
            return string.Format(T(key), args);
        }

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ── écran principal ──
            { "tagline", new[] { "Explorez l'Entre-terre à plusieurs, sans interruption.", "Explore the Lands Between together, seamlessly.", "Explora las Tierras Intermedias en compañía, sin interrupciones." } },
            { "lbl_game", new[] { "JEU", "GAME", "JUEGO" } },
            { "lang_tip", new[] { "Langue du launcher", "Launcher language", "Idioma del launcher" } },
            { "tab_session", new[] { "SESSION", "SESSION", "SESIÓN" } },
            { "tab_diff", new[] { "DIFFICULTÉ", "DIFFICULTY", "DIFICULTAD" } },
            { "tab_adv", new[] { "AVANCÉ", "ADVANCED", "AVANZADO" } },
            { "eac_tip", new[] {
                "Seamless Co-op lance eldenring.exe sans Easy Anti-Cheat et sans connexion aux serveurs officiels de FromSoftware : aucun risque de ban. Les fichiers du jeu ne sont pas modifiés, le bouton Jouer de Steam lance toujours la version vanilla avec EAC.",
                "Seamless Co-op starts eldenring.exe without Easy Anti-Cheat and without connecting to FromSoftware's official servers: no ban risk. Game files are not modified; Steam's Play button still launches the vanilla game with EAC.",
                "Seamless Co-op inicia eldenring.exe sin Easy Anti-Cheat y sin conectarse a los servidores oficiales de FromSoftware: sin riesgo de baneo. Los archivos del juego no se modifican; el botón Jugar de Steam sigue iniciando el juego vanilla con EAC." } },

            // ── onglet Session ──
            { "pwd_label", new[] { "Mot de passe de session", "Session password", "Contraseña de sesión" } },
            { "pwd_hint", new[] { "Le même pour tous les joueurs du groupe", "Must be the same for every player in the group", "La misma para todos los jugadores del grupo" } },
            { "pwd_placeholder", new[] { "Choisis un mot de passe…", "Pick a password…", "Elige una contraseña…" } },
            { "invaders", new[] { "Envahisseurs", "Invaders", "Invasores" } },
            { "invaders_hint", new[] { "D'autres joueurs peuvent envahir votre monde", "Other players can invade your world", "Otros jugadores pueden invadir tu mundo" } },
            { "debuffs", new[] { "Malus à la mort", "Death debuffs", "Penalización al morir" } },
            { "debuffs_hint", new[] { "Essence de putréfaction, soignée au site de grâce", "Rot Essence, cured at a Site of Grace", "Esencia de podredumbre, se cura en un lugar de gracia" } },
            { "summons", new[] { "Invocations d'esprits", "Spirit summons", "Invocación de espíritus" } },
            { "summons_hint", new[] { "Cendres d'esprit utilisables en coop", "Spirit Ashes usable in co-op", "Cenizas espirituales disponibles en cooperativo" } },
            { "spectate", new[] { "Toujours observer à la mort", "Always spectate on death", "Observar siempre al morir" } },
            { "spectate_hint", new[] { "Spectateur jusqu'au wipe ou au repos", "Spectate until party wipe or a rest", "Espectador hasta la derrota del grupo o un descanso" } },
            { "overhead", new[] { "Infos des joueurs", "Player info", "Info de jugadores" } },
            { "overhead_items", new[] { "Normal|Rien|Ping|Niveau|Morts|Niveau + ping", "Normal|None|Ping|Level|Deaths|Level + ping", "Normal|Nada|Ping|Nivel|Muertes|Nivel + ping" } },
            { "splash", new[] { "Passer les logos d'intro", "Skip intro logos", "Saltar logos de inicio" } },
            { "volume", new[] { "Volume au démarrage", "Startup volume", "Volumen al iniciar" } },
            { "volume_mute", new[] { "muet", "muted", "silencio" } },

            // ── onglet Difficulté ──
            { "diff_hint", new[] { "Bonus ajouté par joueur supplémentaire dans la session.", "Bonus added for each extra player in the session.", "Bonificación por cada jugador adicional en la sesión." } },
            { "enemy_hp", new[] { "PV des ennemis", "Enemy health", "Vida de enemigos" } },
            { "enemy_dmg", new[] { "Dégâts des ennemis", "Enemy damage", "Daño de enemigos" } },
            { "enemy_posture", new[] { "Posture des ennemis", "Enemy posture", "Postura de enemigos" } },
            { "boss_hp", new[] { "PV des boss", "Boss health", "Vida de jefes" } },
            { "boss_dmg", new[] { "Dégâts des boss", "Boss damage", "Daño de jefes" } },
            { "boss_posture", new[] { "Posture des boss", "Boss posture", "Postura de jefes" } },
            { "pct", new[] { "+{0} %", "+{0}%", "+{0} %" } },
            { "diff_reset", new[] { "Valeurs par défaut du mod", "Mod default values", "Valores por defecto del mod" } },

            // ── onglet Avancé ──
            { "ext_label", new[] { "Extension des sauvegardes Seamless", "Seamless save file extension", "Extensión de las partidas Seamless" } },
            { "ext_hint", new[] { "Tes sauvegardes vanilla (.sl2) ne sont jamais modifiées", "Your vanilla saves (.sl2) are never modified", "Tus partidas vanilla (.sl2) nunca se modifican" } },
            { "steamid", new[] { "Afficher le Steam ID des joueurs", "Show players' Steam ID", "Mostrar el Steam ID de los jugadores" } },
            { "steamid_hint", new[] { "Utile pour repérer et bloquer un tricheur", "Handy to spot and block a cheater", "Útil para detectar y bloquear a un tramposo" } },
            { "close_on_launch", new[] { "Fermer le launcher au lancement du jeu", "Close the launcher when the game starts", "Cerrar el launcher al iniciar el juego" } },
            { "backup", new[] { "Backup auto des personnages", "Auto-backup characters", "Copia automática de personajes" } },
            { "backup_hint", new[] { "Copie avant chaque partie (10 dernières gardées)", "Copy before each session (last 10 kept)", "Copia antes de cada partida (se guardan las 10 últimas)" } },
            { "btn_import", new[] { "Importer ma progression vanilla (.sl2 → Seamless)", "Import my vanilla progress (.sl2 → Seamless)", "Importar mi progreso vanilla (.sl2 → Seamless)" } },
            { "btn_game_folder", new[] { "Dossier du jeu", "Game folder", "Carpeta del juego" } },
            { "btn_saves_folder", new[] { "Dossier des saves", "Saves folder", "Carpeta de partidas" } },
            { "btn_reinstall", new[] { "Réinstaller le mod", "Reinstall mod", "Reinstalar el mod" } },
            { "btn_uninstall", new[] { "Désinstaller le mod", "Uninstall mod", "Desinstalar el mod" } },

            // ── état ──
            { "not_found", new[] { "Introuvable", "Not found", "No encontrado" } },
            { "game_tip_missing", new[] { "Elden Ring n'a pas été trouvé dans tes bibliothèques Steam : indique où se trouve eldenring.exe.", "Elden Ring wasn't found in your Steam libraries: point to eldenring.exe.", "No se encontró Elden Ring en tus bibliotecas de Steam: indica dónde está eldenring.exe." } },
            { "browse_change", new[] { "Changer", "Change", "Cambiar" } },
            { "browse_locate", new[] { "Localiser", "Locate", "Buscar" } },
            { "mod_missing", new[] { "Non installé", "Not installed", "No instalado" } },
            { "mod_available", new[] { " · {0} disponible", " · {0} available", " · {0} disponible" } },
            { "version_unknown", new[] { "Version inconnue", "Unknown version", "Versión desconocida" } },
            { "mod_update", new[] { "{0}  →  {1} disponible", "{0}  →  {1} available", "{0}  →  {1} disponible" } },
            { "mod_installed", new[] { "Installé", "Installed", "Instalado" } },
            { "mod_uptodate", new[] { " · à jour", " · up to date", " · actualizado" } },
            { "mod_unchecked", new[] { " · MAJ non vérifiée", " · update not checked", " · actualización no comprobada" } },
            { "latest_tip", new[] { "Dernière version : {0}", "Latest version: {0}", "Última versión: {0}" } },
            { "eac_off", new[] { "Désactivé en Seamless · vanilla intact", "Disabled in Seamless · vanilla untouched", "Desactivado en Seamless · vanilla intacto" } },
            { "eac_on", new[] { "Actif (jeu vanilla uniquement)", "Active (vanilla game only)", "Activo (solo juego vanilla)" } },
            { "steam_ok", new[] { "Connecté", "Connected", "Conectado" } },
            { "steam_waiting", new[] { "En attente de connexion", "Waiting for login", "Esperando inicio de sesión" } },
            { "steam_autostart", new[] { "Sera démarré automatiquement", "Will be started automatically", "Se iniciará automáticamente" } },
            { "btn_locate_game", new[] { "LOCALISER LE JEU", "LOCATE THE GAME", "BUSCAR EL JUEGO" } },
            { "btn_install", new[] { "INSTALLER", "INSTALL", "INSTALAR" } },
            { "btn_play", new[] { "JOUER", "PLAY", "JUGAR" } },
            { "btn_update", new[] { "METTRE À JOUR  ·  {0}", "UPDATE  ·  {0}", "ACTUALIZAR  ·  {0}" } },
            { "browse_title", new[] { "Sélectionne eldenring.exe (dossier ELDEN RING\\Game)", "Select eldenring.exe (ELDEN RING\\Game folder)", "Selecciona eldenring.exe (carpeta ELDEN RING\\Game)" } },

            // ── dialogues communs ──
            { "ok", new[] { "OK", "OK", "Aceptar" } },
            { "cancel", new[] { "Annuler", "Cancel", "Cancelar" } },
            { "game_running_title", new[] { "Elden Ring est ouvert", "Elden Ring is running", "Elden Ring está abierto" } },

            // ── installation ──
            { "game_running_install", new[] { "Ferme le jeu avant d'installer ou de mettre à jour le mod.", "Close the game before installing or updating the mod.", "Cierra el juego antes de instalar o actualizar el mod." } },
            { "access_title", new[] { "Autorisation nécessaire", "Permission needed", "Se necesita permiso" } },
            { "access_text", new[] {
                "Le dossier du jeu est protégé en écriture. Le launcher va demander une seule fois les droits administrateur pour t'en donner l'accès.",
                "The game folder is write-protected. The launcher will ask for administrator rights once to give you access.",
                "La carpeta del juego está protegida contra escritura. El launcher pedirá permisos de administrador una sola vez para darte acceso." } },
            { "access_ok", new[] { "Autoriser", "Allow", "Permitir" } },
            { "install_cancelled", new[] { "Installation annulée.", "Installation cancelled.", "Instalación cancelada." } },
            { "requesting_admin", new[] { "Demande des droits administrateur…", "Requesting administrator rights…", "Solicitando permisos de administrador…" } },
            { "access_failed", new[] {
                "Impossible d'écrire dans le dossier du jeu. Lance le launcher en administrateur (clic droit → Exécuter en tant qu'administrateur).",
                "Can't write to the game folder. Run the launcher as administrator (right-click → Run as administrator).",
                "No se puede escribir en la carpeta del juego. Ejecuta el launcher como administrador (clic derecho → Ejecutar como administrador)." } },
            { "checking_latest", new[] { "Recherche de la dernière version…", "Looking for the latest version…", "Buscando la última versión…" } },
            { "downloading", new[] { "Téléchargement de Seamless Co-op {0}…", "Downloading Seamless Co-op {0}…", "Descargando Seamless Co-op {0}…" } },
            { "installing", new[] { "Vérification et installation…", "Verifying and installing…", "Verificando e instalando…" } },
            { "installed_msg", new[] { "Seamless Co-op {0} est installé dans le dossier du jeu.", "Seamless Co-op {0} is installed in the game folder.", "Seamless Co-op {0} está instalado en la carpeta del juego." } },
            { "import_auto_done", new[] {
                "Ta progression vanilla a été copiée vers Seamless. Le fichier .sl2 d'origine n'a pas été touché.",
                "Your vanilla progress was copied to Seamless. The original .sl2 file was left untouched.",
                "Tu progreso vanilla se ha copiado a Seamless. El archivo .sl2 original no se ha tocado." } },
            { "import_auto_failed", new[] {
                "La copie de ta sauvegarde vanilla a échoué ({0}). Tu peux réessayer depuis l'onglet Avancé.",
                "Copying your vanilla save failed ({0}). You can retry from the Advanced tab.",
                "No se pudo copiar tu partida vanilla ({0}). Puedes reintentarlo desde la pestaña Avanzado." } },
            { "installed_status", new[] { "Seamless Co-op {0} installé.", "Seamless Co-op {0} installed.", "Seamless Co-op {0} instalado." } },
            { "install_failed_status", new[] { "Échec de l'installation.", "Installation failed.", "La instalación ha fallado." } },
            { "install_failed_title", new[] { "Installation impossible", "Installation failed", "No se pudo instalar" } },
            { "need_password", new[] {
                "Dernière étape : choisis un mot de passe de session (le même pour tous tes amis), puis clique sur JOUER.",
                "Last step: pick a session password (the same for all your friends), then click PLAY.",
                "Último paso: elige una contraseña de sesión (la misma para todos tus amigos) y pulsa JUGAR." } },
            { "install_done_title", new[] { "Installation terminée", "Installation complete", "Instalación completada" } },

            // ── lancement ──
            { "already_running_title", new[] { "Elden Ring est déjà lancé", "Elden Ring is already running", "Elden Ring ya está en marcha" } },
            { "already_running_text", new[] { "Ferme d'abord le jeu en cours (vanilla ou Seamless), puis relance.", "Close the running game first (vanilla or Seamless), then try again.", "Cierra primero el juego en curso (vanilla o Seamless) y vuelve a intentarlo." } },
            { "no_password_title", new[] { "Aucun mot de passe de session", "No session password", "Sin contraseña de sesión" } },
            { "no_password_text", new[] {
                "Sans mot de passe, tu ne pourras ni rejoindre ni accueillir d'amis. Lancer quand même ?",
                "Without a password you can't join or host friends. Launch anyway?",
                "Sin contraseña no podrás unirte a tus amigos ni recibirlos. ¿Iniciar de todos modos?" } },
            { "launch_anyway", new[] { "Lancer quand même", "Launch anyway", "Iniciar igualmente" } },
            { "choose_password", new[] { "Choisir un mot de passe", "Set a password", "Elegir contraseña" } },
            { "steam_missing", new[] { "Steam est introuvable. Seamless Co-op a besoin de Steam pour fonctionner.", "Steam was not found. Seamless Co-op needs Steam to work.", "No se encontró Steam. Seamless Co-op necesita Steam para funcionar." } },
            { "starting_steam", new[] { "Démarrage de Steam…", "Starting Steam…", "Iniciando Steam…" } },
            { "steam_timeout", new[] { "Steam ne s'est pas connecté. Connecte-toi à Steam puis réessaie.", "Steam didn't log in. Sign in to Steam and try again.", "Steam no ha iniciado sesión. Inicia sesión en Steam y vuelve a intentarlo." } },
            { "backing_up", new[] { "Copie de sécurité des personnages…", "Backing up characters…", "Haciendo copia de los personajes…" } },
            { "launching", new[] { "Lancement d'Elden Ring avec Seamless Co-op…", "Launching Elden Ring with Seamless Co-op…", "Iniciando Elden Ring con Seamless Co-op…" } },
            { "launch_not_detected", new[] {
                "Le jeu ne semble pas avoir démarré. Vérifie que ton antivirus n'a pas bloqué ersc_launcher.exe et que Steam est bien connecté.",
                "The game doesn't seem to have started. Check that your antivirus didn't block ersc_launcher.exe and that Steam is logged in.",
                "Parece que el juego no se ha iniciado. Comprueba que tu antivirus no haya bloqueado ersc_launcher.exe y que Steam tenga la sesión iniciada." } },
            { "launched", new[] { "Bon voyage, Sans-éclat.", "Safe travels, Tarnished.", "Buen viaje, Sinluz." } },
            { "launch_failed", new[] { "Lancement impossible", "Launch failed", "No se pudo iniciar" } },

            // ── import / désinstallation ──
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
            { "nothing_title", new[] { "Rien à désinstaller", "Nothing to uninstall", "Nada que desinstalar" } },
            { "nothing_text", new[] { "Seamless Co-op n'est pas installé dans ce dossier de jeu.", "Seamless Co-op isn't installed in this game folder.", "Seamless Co-op no está instalado en esta carpeta del juego." } },
            { "game_running_uninstall", new[] { "Ferme le jeu avant de désinstaller le mod.", "Close the game before uninstalling the mod.", "Cierra el juego antes de desinstalar el mod." } },
            { "uninstall_title", new[] { "Désinstaller Seamless Co-op ?", "Uninstall Seamless Co-op?", "¿Desinstalar Seamless Co-op?" } },
            { "uninstall_text", new[] {
                "ersc_launcher.exe et le dossier SeamlessCoop vont être supprimés du dossier du jeu. Tes sauvegardes Seamless et tes réglages sont conservés.",
                "ersc_launcher.exe and the SeamlessCoop folder will be removed from the game folder. Your Seamless saves and settings are kept.",
                "Se eliminarán ersc_launcher.exe y la carpeta SeamlessCoop de la carpeta del juego. Tus partidas Seamless y tus ajustes se conservan." } },
            { "uninstall_ok", new[] { "Désinstaller", "Uninstall", "Desinstalar" } },
            { "uninstalled", new[] { "Seamless Co-op désinstallé : le jeu est revenu à l'état vanilla.", "Seamless Co-op uninstalled: the game is back to vanilla.", "Seamless Co-op desinstalado: el juego ha vuelto a su estado vanilla." } },
            { "uninstall_failed_title", new[] { "Désinstallation incomplète", "Uninstall incomplete", "Desinstalación incompleta" } },

            // ── erreurs ──
            { "err_ratelimit", new[] { "GitHub limite temporairement les requêtes. Réessaie dans quelques minutes.", "GitHub is temporarily rate-limiting requests. Try again in a few minutes.", "GitHub está limitando las solicitudes temporalmente. Vuelve a intentarlo en unos minutos." } },
            { "err_network", new[] { "Impossible de joindre GitHub. Vérifie ta connexion internet puis réessaie.", "Can't reach GitHub. Check your internet connection and try again.", "No se puede conectar con GitHub. Comprueba tu conexión a internet y vuelve a intentarlo." } },
            { "err_access", new[] { "Accès refusé à un fichier du jeu. Ferme le jeu et réessaie, ou lance le launcher en administrateur.", "Access denied to a game file. Close the game and try again, or run the launcher as administrator.", "Acceso denegado a un archivo del juego. Cierra el juego y vuelve a intentarlo, o ejecuta el launcher como administrador." } },
            { "err_locked", new[] { "Un fichier est verrouillé ou inaccessible. Ferme Elden Ring et réessaie.", "A file is locked or unavailable. Close Elden Ring and try again.", "Un archivo está bloqueado o no es accesible. Cierra Elden Ring y vuelve a intentarlo." } },
            { "err_no_zip", new[] { "La dernière release GitHub ne contient pas d'archive du mod.", "The latest GitHub release has no mod archive.", "La última versión de GitHub no contiene el archivo del mod." } },
            { "err_bad_host", new[] { "Adresse de téléchargement inattendue ({0}), installation annulée.", "Unexpected download address ({0}), installation cancelled.", "Dirección de descarga inesperada ({0}), instalación cancelada." } },
            { "err_sha", new[] {
                "Le fichier téléchargé est corrompu (empreinte SHA-256 différente de celle publiée sur GitHub). Réessaie.",
                "The downloaded file is corrupted (its SHA-256 hash differs from the one published on GitHub). Try again.",
                "El archivo descargado está dañado (la huella SHA-256 no coincide con la publicada en GitHub). Vuelve a intentarlo." } },
            { "err_archive", new[] {
                "Archive inattendue : ersc_launcher.exe est absent. Le format du mod a peut-être changé.",
                "Unexpected archive: ersc_launcher.exe is missing. The mod's format may have changed.",
                "Archivo inesperado: falta ersc_launcher.exe. Puede que el formato del mod haya cambiado." } },
            { "err_antivirus", new[] {
                "Des fichiers du mod ont disparu juste après l'installation : ton antivirus les a sans doute mis en quarantaine (faux positif fréquent, le mod injecte une DLL dans le jeu). Ajoute une exclusion pour le dossier du jeu puis réinstalle.",
                "Some mod files vanished right after installation: your antivirus most likely quarantined them (a common false positive, since the mod injects a DLL into the game). Add an exclusion for the game folder, then reinstall.",
                "Algunos archivos del mod desaparecieron justo después de la instalación: probablemente tu antivirus los puso en cuarentena (falso positivo habitual, el mod inyecta una DLL en el juego). Añade una exclusión para la carpeta del juego y reinstala." } },
        };
    }
}
