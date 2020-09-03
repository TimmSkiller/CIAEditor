using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CIAEditor
{
    public class Tools
    {
        public static string[] toolPaths = {
                $"{Environment.CurrentDirectory}/tools/ctrtool.exe",
                $"{Environment.CurrentDirectory}/tools/3dstool.exe",
                $"{Environment.CurrentDirectory}/tools/makerom.exe",
                $"{Environment.CurrentDirectory}/tools/ninfs.exe",
            };

        public static string[] requiredNinfsFiles = {
            $"{Environment.GetEnvironmentVariable("userprofile")}\\AppData\\Roaming\\3ds\\boot9.bin",
            $"{Environment.GetEnvironmentVariable("userprofile")}\\AppData\\Roaming\\3ds\\seeddb.bin"
        };

        public static void RunOneLineCommand(string pathToExecutable, string workingDir, string arguments)
        {
            Process p = new Process();

            p.StartInfo.FileName = pathToExecutable;
            p.StartInfo.WorkingDirectory = workingDir;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit();
            p.Close();
        }

        public static string ReadHexUTF8(string path, int startOffset, int endOffset, bool decode)
        {
            if (!File.Exists(path)) { throw new FileNotFoundException($"Could not find {path}"); }

            string result = "";

            List<byte> bytes = new List<byte>();
            BinaryReader reader = new BinaryReader(File.OpenRead(path));
            reader.BaseStream.Position = startOffset;

            for (int i = startOffset; i < endOffset; i++)
            {
                bytes.Add(reader.ReadByte());
            }

            if (decode)
            {
                result = Encoding.UTF8.GetString(bytes.ToArray());
            }
            else
            {
                foreach (byte b in bytes)
                {
                    result += b.ToString("X2");
                }
            }
            reader.Close();
            return result;
        }

        public static string DecryptCIA(string path, bool isDLC)
        {
            FileInfo inputCiaFileInfo = new FileInfo(path);
            string decryptedName = $"{inputCiaFileInfo.Name.Remove(inputCiaFileInfo.Name.LastIndexOf(".cia"))}_decrypted.cia";

            if (ReadHexUTF8(path, 0x0, 0x2, false).Trim() != "2020")
            {
                throw new ArgumentException($"The current CIA File in path {path} does not contain a valid CIA header.");
            }
            else if (requiredNinfsFiles.Any(c => !File.Exists(c)))
            {
                throw new FileNotFoundException("One or more files that are required by ninfs could not be found.");
            }

            string mountPoint = $"{Environment.CurrentDirectory}\\ninfs_mount";
            //starting ninfs, mounting CIA and reading data
            Process ninfs = new Process();

            ninfs.StartInfo.FileName = toolPaths[3];
            ninfs.StartInfo.Arguments = $"cia \"{path}\" \"{mountPoint}\"";
            ninfs.StartInfo.UseShellExecute = false;
            ninfs.StartInfo.CreateNoWindow = true;

            ninfs.Start();

            //wating for ninfs to mount CIA
            while (true)
            {
                if (File.Exists($"{mountPoint}\\tmd.bin"))
                {
                    break;
                }
            }

            string[] contentDirs = Directory.GetDirectories(mountPoint);

            Process makerom = new Process();

            makerom.StartInfo.FileName = toolPaths[2];
            makerom.StartInfo.Arguments += $"-f cia -o \"{decryptedName}\" ";

            foreach (string directory in contentDirs)
            {
                makerom.StartInfo.Arguments += $"-i {directory}/{(File.Exists($"{directory}/decrypted.cxi") ? "decrypted.cxi" : "decrypted.cfa")}:{GetContentIndex(Path.GetFileName(directory))} ";
            }

            if (isDLC)
            {
                makerom.StartInfo.Arguments += "-dlc ";
            }

            makerom.StartInfo.Arguments += "-ignoresign";

            Console.WriteLine($"\nDecrypting CIA \"{inputCiaFileInfo.Name}\"...\n");

            makerom.Start();
            makerom.WaitForExit();

            if (File.Exists($"{Environment.CurrentDirectory}/{decryptedName}"))
            {
                Console.WriteLine($"CIA was successfully decrypted. Output file: {Environment.CurrentDirectory}\\{decryptedName}");
                KillNinfs();
            }
            else
            {
                Console.WriteLine($"CIA could not be decrypted.");
                Environment.Exit(1);
            }

            return $"{decryptedName}";
        }

        public static string GetContentIndex(string contentName)
        {
            string output = "";
            string indexOne = contentName.Split('.')[0];
            string indexTwo = contentName.Split('.')[1];

            if (contentName == "0000.00000000")
            {
                return "0:0";
            }

            for (int i = 0; i < indexOne.Length; i++)
            {
                if (indexOne == "0000")
                {
                    output += "0x0";
                    break;
                }

                if (indexOne[i] != '0')
                {
                    string currentIndex = indexOne.Substring(i);

                    output += $"0x{currentIndex}";
                    break;
                }
            }

            output += ":";

            for (int i = 0; i < indexTwo.Length; i++)
            {
                if (indexTwo == "00000000")
                {
                    output += "0x0";
                    break;
                }

                if (indexTwo[i] != '0')
                {
                    string currentIndex = indexTwo.Substring(i);

                    output += $"0x{currentIndex}";
                    break;
                }
            }
            return output;
        }

        public static void KillNinfs()
        {
            foreach (Process p in Process.GetProcessesByName("ninfs"))
            {
                p.Kill();
            }
        }

        public static string GetNCCHType(string pathToNcchHeader)
        {
            string contentType = "";
            using (FileStream fs = new FileStream(pathToNcchHeader, FileMode.Open))
            {
                fs.Position = 0x18D;

                contentType = (fs.ReadByte() & 0x2) == 2 ? "cxi" : "cfa";

                fs.Close();
            }

            return contentType;
        }

        public static bool IsDLC(string NCCHPath)
        {
            using FileStream fs = new FileStream(NCCHPath, FileMode.Open);
            List<byte> productCodeHex = new List<byte>();

            fs.Position = 0x150;
            for (int i = (int)fs.Position; i < 0x160; i++)
            {
                productCodeHex.Add((byte)fs.ReadByte());
            }

            fs.Close();

            string productCodeString = Encoding.UTF8.GetString(productCodeHex.ToArray());

            return productCodeString.Split('-')[1] == "M";
        }

        public static void RunMakerom(DirectoryInfo contentsDir, bool isDlc)
        {
            if (contentsDir.GetFiles().Length == 0)
            {
                throw new ArgumentException($"No contents found in specified directories.");
            }

            Process makerom = new Process();

            makerom.StartInfo.FileName = toolPaths[2];
            makerom.StartInfo.WorkingDirectory = contentsDir.FullName;

            makerom.StartInfo.Arguments += "-f cia -o output.cia ";

            foreach (FileInfo ncch in contentsDir.GetFiles())
            {
                makerom.StartInfo.Arguments += $"-i {ncch.Name}:{GetContentIndex(ncch.Name).Replace(".ncch", "")} ";
            }

            if (isDlc)
            {
                makerom.StartInfo.Arguments += "-dlc ";
            }
            makerom.StartInfo.Arguments += "-ignoresign";

            makerom.Start();
            makerom.WaitForExit();
        }
    }
}