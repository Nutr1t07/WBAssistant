using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;

namespace WBAssistant
{
    class Copier
    {
        readonly Logger logger;
        public Copier(Logger lgr)
        {
            logger = lgr;
        }

        static string GetValidName(string strIn)
        {
            try
            {
                return Regex.Replace(strIn, @"[^\w\-. ]", "",
                                RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            catch (RegexMatchTimeoutException)
            {
                return String.Empty;
            }
        }

        public void StartCopierListen()
        {

            logger.LogC("USB monitoring started");
            ManagementEventWatcher watcher = new ManagementEventWatcher();
            //WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            WqlEventQuery query = new WqlEventQuery(
                "SELECT * FROM __InstanceOperationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive' AND TargetInstance.InterfaceType = 'USB'");
            watcher.EventArrived += new EventArrivedEventHandler(Watcher_EventArrived);
            watcher.Query = query;
            watcher.Start();
            while (true)
                watcher.WaitForNextEvent();
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject mbo = e.NewEvent.Properties["TargetInstance"].Value as ManagementBaseObject;
            string pnpDeviceId = mbo.Properties["PNPDeviceID"].Value.ToString();
            string deviceId = mbo.Properties["DeviceID"].Value.ToString();
            string driveLetter = null;

            try
            {
                foreach (var partition in new ManagementObjectSearcher(
        "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + deviceId
        + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition").Get())
                {
                    foreach (var disk in new ManagementObjectSearcher(
                                "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='"
                                    + partition["DeviceID"]
                                    + "'} WHERE AssocClass = Win32_LogicalDiskToPartition").Get())
                    {
                        driveLetter = disk["Name"].ToString();
                        break;
                    }
                    break;
                }
            }
            catch (Exception) { return; }

            if (driveLetter == null)
                return;

            DriveInfo drive = new DriveInfo(driveLetter);
            if (drive.IsReady)
            {
                logger.LogTrigger("driver " + drive.VolumeLabel + " detected");
                StartExplorer(driveLetter);

                string[] exclusion;
                try
                {
                    exclusion = File.ReadAllText("WBAData\\exclusion.txt").Split("\r\n");
                }
                catch (Exception exp)
                {
                    exclusion = new string[] { };
                    if (!File.Exists("WBAData\\exclusion.txt"))
                    {
                        logger.LogW("exclusion.txt not found, creating empty exclusion list");
                        File.WriteAllText("WBAData\\exclusion.txt", "");
                    }
                    else
                        logger.LogE($"{exp.GetType().Name} occured while opening exclusion.txt");
                }
                if (Array.IndexOf(exclusion, drive.VolumeLabel) >= 0)
                {
                    logger.LogI("The USB device is in exclusion, skipped");
                    return;
                }

                string savePath = "";
                string[] specified_exts;
                try
                {
                    savePath = File.ReadAllText("WBAData\\savePath.txt");
                }
                catch (Exception) { File.WriteAllText("WBAData\\savePath.txt", Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\"); }

                try
                {
                    specified_exts = File.ReadAllText("WBAData\\extension.txt").Split('|');
                }
                catch (Exception)
                {
                    //extRegex = @"^.*\.(doc|docx|xlsx|ppt|pptx|pdf|xls|txt)$";
                    specified_exts = new string[] { "doc", "docx", "xls", "xlsx", "ppt", "pptx", "pdf", "txt" };
                    string extStr = String.Join('|', specified_exts);
                    logger.LogW("extension.txt not found. fallback to default matching pattern: " + extStr);
                    File.WriteAllText("WBAData\\extension.txt", extStr);

                }

                string dirName = GetDirName(driveLetter, pnpDeviceId, savePath + GetValidName(drive.VolumeLabel));
                if (!Directory.Exists(dirName))
                {
                    logger.LogI("creating new directory " + dirName);
                    Directory.CreateDirectory(dirName);

                    logger.LogI("writing device info");
                    File.WriteAllText(dirName + "DeviceInfo.txt", pnpDeviceId);
                }

                logger.LogI("analyzing file tree");
                string fileTree = string.Join("\r\n", GetFileTree(driveLetter));
                File.WriteAllText(dirName + "FileTree.txt", fileTree);

                var all_files = GetSpecfiedFiles(driveLetter, specified_exts);
                if (all_files == null)
                    return;

                var files = all_files.Item1;
                var files_skiped = all_files.Item2;

                int errorCnt = 0;
                int skipCnt = 0;
                int alreadyExist = 0;
                int copyCnt = 0;

                List<string> skipExt = new List<string>();
                foreach (var fs in files_skiped)
                {
                    skipCnt += fs.Value;
                    skipExt.Add($"{fs.Value} *.{fs.Key}");
                }
                logger.LogI("" + (files.Count + skipCnt) + " files found");
                if (skipExt.Count > 0)
                    logger.LogI($"{skipCnt} files not match: ({String.Join('|', skipExt.ToArray())})");
                foreach (string file in files)
                {
                    try
                    {
                        string dest = dirName + file.Substring(3);
                        string destDir = Path.GetDirectoryName(dest);
                        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                        FileInfo fi = new FileInfo(dest);
                        if (!fi.Exists || fi.LastWriteTime < new FileInfo(file).LastWriteTime)
                        {
                            File.Copy(file, dest, true);
                            ++copyCnt;
                        }
                        else
                            ++alreadyExist;
                    }
                    catch (DirectoryNotFoundException) { ++errorCnt; }
                    catch (Exception err)
                    {
                        logger.WriteE(err.GetType().Name + " occured while copying file " + file);
                        ++errorCnt;
                    }
                }
                logger.LogI(copyCnt.ToString() + " files copied, " + alreadyExist + " files already exists, " + errorCnt + " errors occured");
            }
        }

        private Tuple<List<string>, Dictionary<string, int>> GetSpecfiedFiles(string path, string[] exts)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(path);
                string[] files = Directory.GetFiles(path);

                var ret = new List<string>();
                var skiped = new Dictionary<string, int>();
                foreach (string dir in dirs)
                {
                    var childs = GetSpecfiedFiles(dir, exts);
                    if (childs == null)
                        continue;
                    var childs_ret = childs.Item1;
                    var childs_skiped = childs.Item2;
                    foreach (var retChild in childs_ret)
                        ret.Add(retChild);
                    foreach (var skipChild in childs_skiped)
                        if (skiped.ContainsKey(skipChild.Key))
                            ++skiped[skipChild.Key];
                        else
                            skiped[skipChild.Key] = skipChild.Value;
                }
                foreach (string file in files)
                    if (Array.IndexOf(exts, file.Split('.')[^1].ToLower()) >= 0)
                        ret.Add(file);
                    else
                    {
                        string ext = file.Split('.')[^1].ToLower();
                        if (skiped.ContainsKey(ext))
                            ++skiped[ext];
                        else
                            skiped[ext] = 1;
                    }

                return new Tuple<List<string>, Dictionary<string, int>>(ret, skiped);
            }
            catch (UnauthorizedAccessException) { return null; }
            catch (Exception) { logger.LogE("error occured while analyzing directory " + path); return null; }
        }

        private List<string> GetFileTree(string path)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(path);
                string[] files = Directory.GetFiles(path);

                List<string> ret = new List<string>();
                foreach (string dir in dirs)
                {
                    List<string> childs = GetFileTree(dir);

                    ret.Add("───" + dir.Split('\\')[^1]);
                    if (childs != null)
                        for (int i = 0; i < childs.Count; ++i)
                            if (childs[i][0] == '─')
                                ret.Add("    ├" + childs[i]);
                            else
                                ret.Add("    │" + childs[i]);
                }
                foreach (string file in files)
                    ret.Add("   " + file.Split('\\')[^1]);
                return ret;
            }
            catch (UnauthorizedAccessException) { return null; }
            catch (Exception) { logger.LogE("getting file infos under " + path); return null; }
        }

        static bool CheckIfNeedNewDirName(string driverName, string pnpDeviceId, string dirName)
        {
            if (!Directory.Exists(dirName))
                return false;
            try
            {
                string pnpId = File.ReadAllText(dirName + "\\DeviceInfo.txt");
                if (pnpDeviceId == pnpId)
                    return false;
            }
            //try
            //{
            //    string[] dirs = Directory.GetDirectories(dirName);
            //    string[] files = Directory.GetFiles(dirName);
            //    for (int i = 0; i < dirs.Length; ++i)
            //        if (Directory.Exists(driverName + "\\" + dirs[i].Split('\\')[^1]))
            //            return false;
            //    for (int i = 0; i < files.Length; ++i)
            //        if (File.Exists(driverName + "\\" + files[i].Split('\\')[^1]))
            //            return false;
            //}
            catch { return true; }
            return true;
        }

        static string GetDirName(string driverName, string pnpId, string originDriverLetter)
        {
            if (CheckIfNeedNewDirName(driverName, pnpId, originDriverLetter))
            {
                int i = 1;
                while (CheckIfNeedNewDirName(driverName, pnpId, originDriverLetter + " (" + i + ")"))
                    ++i;
                return originDriverLetter + " (" + i + ")\\";
            }
            return originDriverLetter + "\\";
        }

        private static void StartExplorer(string driveName)
        {
            var p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/c explorer.exe " + driveName;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
        }
    }
}
