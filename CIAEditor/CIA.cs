using System;
using System.IO;
using System.Linq;

namespace CIAEditor
{
    public static class CIA
    {
        public static void ExtractCIA(string sourceCiaPath, string destinationFolder)
        {
            string CIAName = new FileInfo(sourceCiaPath).Name;
            string ExtractedFolder = Directory.CreateDirectory($"{destinationFolder}/extracted").FullName;

            if (!File.Exists(sourceCiaPath))
            {
                throw new FileNotFoundException($"Specified CIA File {sourceCiaPath} was not found.");
            }
            else if (Tools.toolPaths.Any(file => !File.Exists(file)))
            {
                throw new FileNotFoundException($"One or more tools was/were not found. Please make sure that these tools exist in the tools folder:\n- 3dstool\nmakerom\nctrtool");
            }

            File.Copy(sourceCiaPath, $"{destinationFolder}/{CIAName}");

            Tools.RunOneLineCommand(Tools.toolPaths[0], destinationFolder, $"--content=DecryptedApp \"{CIAName}\"");

            string[] NCCHImagePaths = Directory.GetFiles(destinationFolder, "DecryptedApp*");

            if (Tools.ReadHexUTF8(NCCHImagePaths[0], 0x18F, 0x190, false) != "04")
            {
                bool isDLC = Tools.IsDLC(Tools.ReadHexUTF8(NCCHImagePaths[0], 0x150, 0x160, true));
                Directory.Delete(destinationFolder, true);

                ExtractCIA(Tools.DecryptCIA(sourceCiaPath, isDLC), destinationFolder);
                Environment.Exit(0);
            }

            foreach (string NCCHImage in NCCHImagePaths)
            {
                string contentIndex = $"{new FileInfo(NCCHImage).Name.Replace("DecryptedApp.", "")}";

                File.Move(NCCHImage, $"{ExtractedFolder}\\{contentIndex}.ncch");

                DirectoryInfo contentDir = Directory.CreateDirectory($"{ExtractedFolder}/{contentIndex}");

                Tools.RunOneLineCommand(Tools.toolPaths[1], contentDir.FullName, $"-xtf {Tools.GetNCCHType($"{ExtractedFolder}\\{contentIndex}.ncch")} {ExtractedFolder}\\{contentIndex}.ncch --header NCCHHeader.bin --exh ExHeader.bin --exefs ExeFS.bin --romfs RomFS.bin --logo LogoLZ.bin --plain PlainRGN.bin");
                Tools.RunOneLineCommand(Tools.toolPaths[1], contentDir.FullName, "-xutf exefs ExeFS.bin --exefs-dir ExeFS --header ExeFSHeader.bin");
                Tools.RunOneLineCommand(Tools.toolPaths[1], contentDir.FullName, "-xtf romfs RomFS.bin --romfs-dir RomFS");

                if (File.Exists($"{ExtractedFolder}\\{contentIndex}\\ExeFS\\banner.bnr"))
                {
                    Tools.RunOneLineCommand(Tools.toolPaths[1], $"{ExtractedFolder}\\{contentIndex}\\ExeFS", "-x -t banner -f banner.bnr --banner-dir ExtractedBanner");
                }
            }
        }

        public static void RebuildCIA(string extractedFolder, string exportedCiaName)
        {
            DirectoryInfo extractedDir = new DirectoryInfo($"{extractedFolder}/extracted");
            bool IsDlc = Tools.IsDLC(extractedDir.GetFiles("*.ncch")[0].FullName);

            foreach (FileInfo ncchFile in extractedDir.GetFiles("*.ncch"))
            {
                ncchFile.Delete();
            }

            foreach (DirectoryInfo content in extractedDir.GetDirectories())
            {
                DirectoryInfo[] contentFolders = content.GetDirectories();

                if (contentFolders.Any(c => c.Name == "ExeFS"))
                {
                    File.Delete($"{content.FullName}/ExeFS.bin");

                    if (File.Exists($"{content.FullName}/ExeFS/banner.bnr"))
                    {
                        File.Delete($"{content.FullName}/ExeFS/ExtractedBanner/banner.bnr");
                        Tools.RunOneLineCommand(Tools.toolPaths[1], $"{content.FullName}/ExeFS", "-c -t banner -f banner.bnr --banner-dir ExtractedBanner");

                        Directory.Delete($"{content.FullName}/ExeFS/ExtractedBanner", true);
                    }

                    Tools.RunOneLineCommand(Tools.toolPaths[1], content.FullName, "-ctf exefs EditedExeFS.bin --exefs-dir ExeFS --header ExeFSHeader.bin");

                    Directory.Delete($"{content.FullName}/ExeFS", true);
                }

                File.Delete($"{content.FullName}/RomFS.bin");

                Tools.RunOneLineCommand(Tools.toolPaths[1], content.FullName, "-ctf romfs EditedRomFS.bin --romfs-dir RomFS");

                Directory.Delete($"{content.FullName}/RomFS", true);

                string contentType = Tools.GetNCCHType($"{content.FullName}/NCCHHeader.bin");

                if (contentType == "cxi")
                {
                    Tools.RunOneLineCommand(Tools.toolPaths[1], content.FullName, $"-ctf cxi {content.Name}.cxi --header NCCHHeader.bin --exh ExHeader.bin --exefs EditedExeFS.bin --romfs EditedRomFS.bin --logo LogoLZ.bin --plain PlainRGN.bin --not-encrypt");
                    File.Move($"{content.FullName}/{content.Name}.cxi", $"{extractedDir.FullName}/{content.Name}.cxi");
                }
                else
                {
                    Tools.RunOneLineCommand(Tools.toolPaths[1], content.FullName, $"-ctf cfa {content.Name}.cfa --header NCCHHeader.bin --romfs EditedRomFS.bin --exefs EditedExeFS.bin --not-encrypt");
                    File.Move($"{content.FullName}/{content.Name}.cfa", $"{extractedDir.FullName}/{content.Name}.cfa");
                }

                content.Delete(true);
            }

            Tools.RunMakerom(extractedDir, IsDlc);
            File.Move($"{extractedDir.FullName}/output.cia", $"{extractedDir.Parent}/{exportedCiaName}_Edited.cia");
        }
    }
}