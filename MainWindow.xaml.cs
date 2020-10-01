using System;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.Win32;
using Hardcodet.Wpf.TaskbarNotification;

namespace WBAssistant
{

    public partial class MainWindow : Window
    {
        //        static readonly string banner = @"
        // _   _       _       __ _    ___ ______ 
        //| \ | |     | |     /_ | |  / _ \____  |
        //|  \| |_   _| |_ _ __| | |_| | | |  / / 
        //| . ` | | | | __| '__| | __| | | | / /  
        //| |\  | |_| | |_| |  | | |_| |_| |/ /   
        //|_| \_|\__,_|\__|_|  |_|\__|\___//_/    

        //";
        static readonly string banner = @"
 __          ______                  _     _              _   
 \ \        / /  _ \   /\           (_)   | |            | |  
  \ \  /\  / /| |_) | /  \   ___ ___ _ ___| |_ __ _ _ __ | |_ 
   \ \/  \/ / |  _ < / /\ \ / __/ __| / __| __/ _` | '_ \| __|
    \  /\  /  | |_) / ____ \\__ \__ \ \__ \ || (_| | | | | |_ 
     \/  \/   |____/_/    \_\___/___/_|___/\__\__,_|_| |_|\__|
                                                              
";

        const string appName = "WBAssistant";
        const string version = "0.5.0";
        readonly Logger logger;
        bool exitFlag = false;

        public MainWindow()
        {
            InitializeComponent();

            logger = new Logger(this);
            log_TextBox.AppendText(banner + "\r\n", "Blue");
            logger.LogC($"Version {version}, created by Nutr1t07 (Nelson Xiao)");


            Task.Factory.StartNew(() => new Copier(logger).StartCopierListen());

            if (!Directory.Exists("WBAData"))
            {
                Directory.CreateDirectory("WBAData");
                Directory.CreateDirectory("WBAData\\log");
            }

            // initial "Run on startup" checkbox
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (registryKey.GetValue(appName) != null)
                runOnStartup_CheckBox.IsChecked = true;
            else
                runOnStartup_CheckBox.IsChecked = false;

            // initial "Switch wallpaper" checkbox
            if (File.Exists("WBAData\\switchWall"))
            {
                swtichWallpaper_CheckBox.IsChecked = true;
                new Wallpaper(logger).randomPickWall();
            }
            else
                swtichWallpaper_CheckBox.IsChecked = false;

        }

        private void runOnStartup_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (runOnStartup_CheckBox.IsChecked ?? false)
            {
                registryKey.SetValue(appName, System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            }
            else
            {
                registryKey.DeleteValue(appName);
            }
        }

        private void notifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (Visibility == Visibility.Hidden)
                Visibility = Visibility.Visible;
            else
                Visibility = Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!exitFlag)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            exitFlag = true;
            Close();
        }

        private void swtichWallpaper_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (swtichWallpaper_CheckBox.IsChecked ?? false)
                File.WriteAllText("WBAData\\switchWall", "");
            else if (File.Exists("WBAData\\switchWall"))
                File.Delete("WBAData\\switchWall");
        }
    }

    public class Logger
    {
        readonly MainWindow context;

        public Logger(MainWindow mw)
        {
            context = mw;
        }


        public void LogI(string str)
        {
            context.Dispatcher.Invoke(() =>
            {
                context.log_TextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [Info] ", "Green");
                context.log_TextBox.AppendText(str + "\r\n", "Black");
            });
            File.AppendAllText("WBAData\\log\\info.log", "[" + DateTime.Now.ToString("") + "] [Info] " + str + "\r\n");
        }

        public void LogC(string str)
        {
            context.Dispatcher.Invoke(() =>
            {
                context.log_TextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [Core] ", "DimGray");
                context.log_TextBox.AppendText(str + "\r\n", "Black");
            });
        }

        public void LogW(string str)
        {
            context.Dispatcher.Invoke(() =>
            {
                context.log_TextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [Warn] ", "Gold");
                context.log_TextBox.AppendText(str + "\r\n", "Black");
            });
        }
        public void LogE(string str)
        {
            context.Dispatcher.Invoke(() =>
            {
                context.log_TextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [Error] ", "Red");
                context.log_TextBox.AppendText(str + "\r\n", "Black");
            });
        }

        public void WriteE(string str)
        {
            File.AppendAllText("WBAData\\log\\error.log", "[" + DateTime.Now.Date.ToString() + "]" + str + "\r\n");
        }

        public void LogTrigger(string str)
        {
            context.Dispatcher.Invoke(() =>
            {
                context.log_TextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] [Trigger] ", "DeepSkyBlue");
                context.log_TextBox.AppendText(str + "\r\n", "Black");
            });
            File.AppendAllText("WBAData\\log\\info.log", "[" + DateTime.Now.ToString("") + "] [Trigger] " + str + "\r\n");
        }
    }
    public static class Extensions
    {
        public static void AppendText(this RichTextBox box, string text, string color)
        {
            BrushConverter bc = new BrushConverter();
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd)
            {
                Text = text
            };
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFromString(color));
            }
            catch (FormatException) { }
        }
    }
}
