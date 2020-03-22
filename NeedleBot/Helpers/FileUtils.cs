using System;
using System.IO;

namespace NeedleBot.Helpers
{
    public static class FileUtils
    {
        public static void DeleteFileQuietly(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                File.Delete(path);
            }
            catch
            {
                Logger.WriteMain($"Error deleting {path}", ConsoleColor.Red);
            }
        }
    }
}
