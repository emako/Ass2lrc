using System;
using System.IO;

namespace Ass2lrc
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            for (int i = default; i < args.Length; i++)
            {
                FileInfo fileInfo = new(args[i]);

                if (fileInfo.Exists)
                {
                    string targetFilename = $"{fileInfo.DirectoryName}{Path.DirectorySeparatorChar}{fileInfo.Name.Substring(0, fileInfo.Name.Length - fileInfo.Extension.Length)}.lrc";

                    AssHelper.AssToLyric(args[i], targetFilename);
                }
            }
        }
    }
}
