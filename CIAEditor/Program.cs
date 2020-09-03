using System;
using System.IO;

namespace CIAEditor
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Invalid Parameters. Usage:\n\nCIAEditor extract [PATH_TO_CIA]\nor:\n\nPCMode64 rebuild [PATH_TO_EXTRACTED_DIR]");
            }
            else if (args[0] == "extract" && File.Exists(args[1]))
            {
                CIA.ExtractCIA(args[1], "extractedCIA");
            }
            else if (args[0] == "rebuild" && Directory.Exists(args[1]))
            {
                CIA.RebuildCIA(args[1], Path.GetFileNameWithoutExtension(Directory.GetFiles(args[1], "*.cia")[0]));
            }
            else
            {
                Console.WriteLine("Invalid Parameters.\nUsage:\nCIAEditor extract [PATH_TO_CIA]\nor:\n\nPCMode64 rebuild [PATH_TO_EXTRACTED_DIR]");
            }
        }
    }
}