# CIAEditor

Application to extract and rebuild CIAs for the Nintendo 3DS (CTR-Importable-Archive).

This application is powered by the following tools:
- [makerom](https://github.com/3DSGuy/Project_CTR/tree/master/makerom)
- [ninfs](https://github.com/ihaveamac/ninfs)
- [ctrtool](https://github.com/3DSGuy/Project_CTR/tree/master/ctrtool)
- [3dstool](https://github.com/dnasdw/3dstool)

How to use:

1. Run `CIAEditor extract [PATH_TO_CIA] [EXTRACTED_DIR]` (with EXTRACTED_DIR being the name of the directory that the CIA should be extracted into) which will try to extract the NCCH contents of the provided CIA.
If the provided CIA is encrypted, it will automatically try to decrypt it using ninfs, which requires you to have these files in `AppData\Roaming\3ds` on windows and `~/3ds/` on linux:
- The ARM9 BootROM from a 3DS (boot9.bin)
- A seed database files for CIAs that need it (seeddb.bin)

If any of these files are not found, the CIA will not be decrypted and the program will exit.

2. Run `CIAEditor rebuild [PATH_TO_EXTRACTED_DIR]`, which will try to rebuild the unpacked CIA, resulting in the edited CIA being named as `[ORIGINAL_NAME]_Edited.cia`.
