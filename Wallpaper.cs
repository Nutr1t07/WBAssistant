using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace WBAssistant
{
    class Wallpaper
    {
        readonly Logger logger;
        public Wallpaper(Logger lgr)
        {
            logger = lgr;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(UInt32 uiAction, UInt32 uiParam, String pvParam, UInt32 fWinIni);
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        public  void randomPickWall()
        {
            if (!Directory.Exists("walls"))
                return;
            if (!File.Exists("WBAData\\usedWalls.txt"))
                File.WriteAllText("WBAData\\usedWalls.txt", "");

            var paddingWalls = getAllFiles("walls");
            if (paddingWalls.Count == 0)
                return;

            var usedWalls = File.ReadAllText("WBAData\\usedWalls.txt").Split('|');
            logger.LogI($"{usedWalls.Length} of {paddingWalls.Count} wallpapers used");

            Random random = new Random();
            int pickedIndex = random.Next(0, paddingWalls.Count - 1);
            string wallPath;
            for (int i = 0; i < 10; ++i)
            {
                if (Array.IndexOf(usedWalls, paddingWalls[pickedIndex]) < 0)
                    break;
                if (i == 9)
                {
                    File.WriteAllText("usedWalls.txt", "");
                    logger.LogW($"cannot find unused wallpaper, erasing usedWalls.txt");
                    // clear all used wallpapers
                    break;
                }
                pickedIndex = random.Next(0, paddingWalls.Count - 1);
            }
            wallPath = paddingWalls[pickedIndex];

            logger.LogI($"wallpaper {wallPath} selected");
            SetImage(wallPath);
            File.AppendAllText("WBAData\\usedWalls.txt", wallPath + "|");
        }

        private static List<string> getAllFiles(string path)
        {
            var files = new List<string>(Directory.GetFiles(path));
            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                files.AddRange(getAllFiles(dir));
            }
            return files;
        }

        private static void SetImage(string filename)
        {
            Bitmap bm = new Bitmap(filename);
            string bmpAbsPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\WBAData\\wall.bmp";
            bm.Save(bmpAbsPath);
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, bmpAbsPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
    }
}
