"""
Vérifie les mods DLL du catalogue sur une version d'eldenring.exe, sans lancer le jeu.

Pour chaque mod, les signatures (AOB) qu'il cherche en mémoire sont tirées de son code source. Le script contrôle :
  1. que chaque signature existe dans l'exe, donc que le mod trouvera où appliquer son correctif ;
  2. que les octets attendus à l'endroit du correctif sont bien là (le mod n'écrit rien sinon) ;
  3. qu'aucun octet n'est modifié par deux mods différents : ce serait une vraie incompatibilité.

Usage : python tools/verify-patches.py ["…\\ELDEN RING\\Game\\eldenring.exe"]

Sources :
  techiew/EldenRingMods, commit ebdd808 (release « Binaries »)
  SchuhBaum/FreeLockOnCamera v0.2.4, SourceCode/DllMain.cpp et Include/CameraFix.cpp
"""
import re
import sys

EXE = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\eldenring.exe"

# (id du mod, signature, décalage des octets écrits, nombre d'octets écrits, octets attendus à cet endroit ou None)
PATCHES = [
    ("unlock-the-fps", "c7 ? ? 89 88 88 3c eb ? 89 ? 18 eb ? 89 ? 18 c7", 3, 4, "89 88 88 3c"),
    ("unlock-the-fps", "c7 ? ef 3c 00 00 00 c7 ? f3 01 00 00 00", 0, 7, None),
    ("adjust-the-fov", "8d ? ? ? ? 0f 28 ? e8 ? ? ? ? 80 ? ? ? ? ? ? ? 0f 28 ? f3 ? 0f 10 ? ? ? ? ? ? 0f 57 ? f3 ? 0f 59", -1, 14, None),
    ("remove-chromatic-aberration", "0f 11 ? 60 ? 8d ? 80 00 00 00 0f 10 ? a0 00 00 00 0f 11 ? f0 ? 8d ? b0 00 00 00 0f 10 ? 0f 11 ? 0f 10 ? 10", 0x2f, 4, "0f 11 ? ?"),
    ("remove-vignette", "f3 0f 10 ? 50 f3 0f 59 ? ? ? ? ? e8 ? ? ? ? f3 ? 0f 5c ? f3 ? 0f 59 ? ? 8d ? ? a0 00 00 00", 0x17, 5, "f3 ? 0f 59 ?"),
    ("increase-animation-distance", "c7 ? ? ? 01 00 00 00 f3 ? 0f 10 ? ? ? f3 ? 0f 10 ? ? ? f3 0f 59 ? ? ? ? ? ? 0f 28 ? f3 ? 0f 5c ? ? 58", 0x48, 7, "f3 ? 0f 5e ? ? ?"),
    ("remove-black-bars", "48 c7 45 b8 fe ff ff ff 48 89 58 10 48 89 70 18 48 89 78 20 0f 29 70 c8 48 8b", 0x94, 1, "74"),
    ("disable-rune-loss", "b0 01 ? 8b ? e8 ? ? ? ? ? 8b ? ? ? 32 c0 ? 83 ? 28 c3", 5, 5, "e8"),
    ("fix-the-camera", "0f 29 ? ? ? ? ? ? 0f 28 ? ? 8b ? e8 ? ? ? ? ? 0f b6 ? ? ? ? 0f 28 ? ? 8b ? e8 ? ? ? ? ? 8b ? ? 0f 28 ? ? 8b ? e8 ? ? ? ? ? 8d ? ? ? 8b ? ? 8d", 0, 7, None),
    ("fix-the-camera", "80 ? ? ? ? ? 00 74 ? ? 8b ? e8 ? ? ? ? eb ? 0f 28 ? ? ? ? ? ? 8d", 7, 1, "74"),
    ("free-lock-on-camera", "0f 28 ? 30 0f 28 ? 40 0f 29 ? d0", 3, 1, "30"),
    ("free-lock-on-camera", "0f 28 f2 f3 0f 59 75 d0 0f 28 ca 0f c6 ca 55 f3 0f 59 4d d4 f3 0f 58 f1 0f c6 d2 aa f3 0f 59 55 d8 f3 0f 58 f2", 0, 37, None),
    ("free-lock-on-camera", "48 8b 01 48 85 c0 74 06 f3 0f 10 40 0c", 0, 13, None),
    ("free-lock-on-camera", "c6 81 10 03 00 00 01", 0, 7, None),
    ("free-lock-on-camera", "38 93 10 03 00 00 74 26", 0, 8, None),
    ("free-lock-on-camera", "f3 0f 10 43 64 f3 0f 10 4b 60", 0, 10, None),
    ("free-lock-on-camera", "48 c7 83 4c 29 00 00 00 00 f0 41", 0, 11, None),
    ("free-lock-on-camera", "75 18 49 8b 5e 08", 0, 6, None),
    ("free-lock-on-camera", "f3 0f 10 44 24 34 f3 0f 58 86 78 29 00 00", 0, 14, None),
    ("free-lock-on-camera", "c7 83 30 29 00 00 00 00 70 41", 0, 10, None),
    ("free-lock-on-camera", "88 86 31 28 00 00 8b 0d", 0, 8, None),
    ("free-lock-on-camera", "80 b9 30 28 00 00 00 0f 94 c0", 0, 10, None),
    ("free-lock-on-camera", "48 8b cf e8 ? ? ? ? 84 c0 74 41", 8, 4, "84 c0 74 41"),
    ("free-lock-on-camera", "48 89 5c 24 20 55 56 41 57 48 8d 6c 24 90", 0, 14, None),
    ("free-lock-on-camera", "a8 20 74 10 80 be", 0, 6, None),
    ("free-lock-on-camera", "80 ? ? ? ? ? 00 74 ? ? 8b ? e8 ? ? ? ? eb ? 0f 28 ? ? ? ? ? ? 8d", 7, 1, "74"),
]


def pattern(aob):
    return re.compile(b"".join(b"." if t == "?" else re.escape(bytes([int(t, 16)])) for t in aob.split()), re.S)


def main():
    exe = open(EXE, "rb").read()
    ranges, failed = [], set()
    for mod, aob, offset, length, expected in PATCHES:
        hit = pattern(aob).search(exe)  # comme les mods : première occurrence
        if not hit:
            failed.add(mod)
            print(f"  ABSENT    {mod:30} {aob[:50]}")
            continue
        at = hit.start() + offset
        if expected and not pattern(expected).match(exe, at):
            failed.add(mod)
            print(f"  MODIFIÉ   {mod:30} octets inattendus à {at:#x}")
        ranges.append((mod, at, at + length))

    for mod in dict.fromkeys(p[0] for p in PATCHES):
        print(("  OK        " if mod not in failed else "  ÉCHEC     ") + mod)

    overlaps = sorted({(a[0], b[0]) for i, a in enumerate(ranges) for b in ranges[i + 1:]
                       if a[0] != b[0] and a[1] < b[2] and b[1] < a[2]})
    for a, b in overlaps:
        print(f"  CONFLIT   {a} <-> {b} : mêmes octets du jeu modifiés")
    if not overlaps:
        print("  aucun octet modifié par deux mods")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
