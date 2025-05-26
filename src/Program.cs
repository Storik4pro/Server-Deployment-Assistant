using CefSharp;
using CefSharp.OffScreen;
using System;
using System.Drawing;
using System.IO;
using WebSocketSharp;
using WebSocketSharp.Server;
using CefSharp.Structs;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using NgrokApi;
using System.Threading;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CefSharp.Enums;
using System.Collections.Generic;
using CefSharp.DevTools.Browser;
using System.Threading.Tasks;
using System.Security.Policy;
using System.Diagnostics;
using System.Reflection.Emit;
using System.IO.Compression;
using ServerDeploymentAssistant.src.Network;
using ServerDeploymentAssistant.src.Helpers;
using ServerDeploymentAssistant.src.Managers;
using ServerDeploymentAssistant.src;
using System.Reflection;
using Instances;
using System.Deployment.Application;

namespace ServerDeploymentAssistant
{
    class Program
    {
        static ChromiumWebBrowser browser;

        static Tab activeTab = null;

        private static VideoStreamServer videoStreamServer;

        private static int port;
        private static int audioPort;
        private static string url;
        private static string cachePath;
        static void Main(string[] margs)
        {
            var version = Assembly.GetEntryAssembly().GetName().Version?.ToString();

            Console.Title = "Server Deployment Assistant";
            if (margs.Contains("--help"))
            {
                Console.WriteLine($"SERVER DEPLOYMENT ASSISTANT, version {version}. Help:");
                Console.WriteLine("  --help                          Show this help message.");
                Console.WriteLine("  --set-xml-settings-file <path>  Set custom XML settings file path.");
                Console.WriteLine("  --disable-press-button-request  Disable button press requests.");
                return;
            }

            Console.Clear();

            Logger.CreateLog($"SERVER DEPLOYMENT ASSISTANT, version {version}");
            Logger.CreateLog($"Run application with <\"--help\"> flag to view all available features", ConsoleColor.Cyan);
            Logger.CreateLog($"Getting ready for start ... ");

            int disablePressButtonRequestIndex = Array.IndexOf(margs, "--disable-press-button-request");
            if (disablePressButtonRequestIndex != -1)
            {
                StateHelper.Instance.enablePressButtonRequest = false;
                Logger.CreateLog($"Press button request is disabled.");
            }
            else
            {
                StateHelper.Instance.enablePressButtonRequest = true;
            }

            int setXmlIndex = Array.IndexOf(margs, "--set-xml-settings-file");
            if (setXmlIndex != -1)
            {
                if (setXmlIndex + 1 < margs.Length)
                {
                    string xmlPath = margs[setXmlIndex + 1];
                    SettingsManager.Instance.SetSettingsFile(xmlPath);
                }
                else
                {
                    Logger.CreateError("--set-xml-settings-file requires a file path argument.");
                    Logger.RequestAnyButton();
                    return;
                }
            }
            else
            {
                string _settingsFile;
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    var dataDir = ApplicationDeployment.CurrentDeployment.DataDirectory;
                    _settingsFile = Path.Combine(dataDir, "data", "settings", "settings.xml");
                    SettingsManager.Instance.SetSettingsFile(_settingsFile);
                }
                else
                {
                    var baseDir = AppContext.BaseDirectory;
                    _settingsFile = Path.Combine(baseDir, "data", "settings", "settings.xml");
                    SettingsManager.Instance.SetSettingsFile(_settingsFile);
                }
            }

            port = SettingsManager.Instance.GetValue<int>("VideoStreamSettings", "VideoStreamPort");
            audioPort = SettingsManager.Instance.GetValue<int>("AudioStreamSettings", "AudioStreamPort");
            url = SettingsManager.Instance.GetValue<string>("BrowserSettings", "FirstRunUrl");
            cachePath = SettingsManager.Instance.GetValue<string>("BrowserSettings", "CachePath");

            videoStreamServer = new VideoStreamServer(port);
            AudioStreamServer.Instance.StartAudioStreamingServer(audioPort);

            StateHelper.Instance.OnNotifyOpenedTabs += videoStreamServer.NotifyOpenTabsRequest;

            Logger.CreateLog($"URL used now <{url}>", ConsoleColor.Cyan);

            var settings = new CefSettings()
            {
                CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), cachePath),
                LogSeverity = LogSeverity.Disable,
                MultiThreadedMessageLoop = true,
            };
            settings.CefCommandLineArgs["user-agent"] = SettingsManager.Instance.GetValue<string>("BrowserSettings", "UserAgent");
            settings.CefCommandLineArgs["touch-events"] = "enabled";
            settings.EnableAudio();

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);

            TabsManager.Instance.CreateNewBrowser(url);

            Logger.CreateLog("");
            Logger.CreateLog("");

            Logger.CreateLog($"Port for streaming opened. Use <ws://{Utils.GetLocalIPAddress()}:{port}> to connect client.", ConsoleColor.Cyan);
            Logger.CreateLog($"Port for audio streaming opened. Use <ws://{Utils.GetLocalIPAddress()}:{audioPort}> to connect audio client.", ConsoleColor.Cyan);

            Logger.CreateLog($"Press <Control+C> to correctly close the ASSISTANT. ", ConsoleColor.Cyan);

            Logger.CreateLog("");
            Logger.CreateLog("");

            NetworkManager.StartUdpDiscoveryServer();

            while (true)
            {
                // pass
            }
            Cef.Shutdown();
        }

        
    }
}
