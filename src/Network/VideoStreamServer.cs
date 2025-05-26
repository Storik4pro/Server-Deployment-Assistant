using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using WebSocketSharp;
using CefSharp.Structs;
using CefSharp;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ServerDeploymentAssistant.src.Managers;
using ServerDeploymentAssistant.src.Helpers;

namespace ServerDeploymentAssistant.src.Network
{
    public class VideoStreamServer
    {
        public VideoStreamServer(int port)
        {
            StateHelper.Instance.streamServer = this;
            StartStreamingServer(port);
        }

        public class StreamingBehavior : WebSocketBehavior
        {
            public static int ScalingFactor = SettingsManager.Instance.GetValue<int>("VideoStreamSettings", "ScalingFactor");
            private string UpdateMode = "Dynamic";

            protected override void OnOpen()
            {
                Logger.CreateLog("[CONNECTION] WebSocket connection opened.");
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Logger.CreateLog("[CONNECTION] WebSocket connection closed.");
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                CommPacket packet = new CommPacket();
                try
                {
                    packet = JsonConvert.DeserializeObject<CommPacket>(e.Data);
                }
                catch (System.IO.IOException ioEx)
                {
                    Logger.CreateWarning($"WebSocket connection closed by remote host: {ioEx.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.CreateWarning($"Unable to process WebSocket message: {ex.Message}");
                    return;
                }
                switch (packet.PType)
                {
                    case PacketType.TextInputSend:
                        Logger.CreateLog($"Client send {packet.JSONData} text to active document element");
                        var textscript = @"(function (){document.activeElement.value='" + packet.JSONData + "'})();";

                        var textres = TabsManager.Instance.activeTab.Browser.EvaluateScriptAsync(textscript).ContinueWith(t => {

                            TabsManager.Instance.activeTab.Browser.GetBrowserHost().SendKeyEvent(new KeyEvent
                            {
                                WindowsKeyCode = 0x0D,
                                FocusOnEditableField = true,
                                IsSystemKey = false,
                                Type = KeyEventType.RawKeyDown
                            });

                        });
                        break;

                    case PacketType.RequestFullPageScreenshot:
                        Logger.CreateLog("Client requested a full-page screenshot. Getting ready ...");
                        BrowserHelper.SendFullPageScreenshot();
                        break;

                    case PacketType.ModeChange:
                        UpdateMode = packet.JSONData;
                        if (UpdateMode == "Static")
                        {
                            BrowserHelper.SendFullPageScreenshot();
                        }
                        Logger.CreateLog($"Client changed mode to: {packet.JSONData}");
                        break;

                    case PacketType.ACK:
                        Logger.CreateLog("ACK packet send. No action preferred for that.");
                        break;


                    case PacketType.SendKey:
                        Logger.CreateLog($"Client send KeyCode \"{packet.JSONData}\" to active document element.");

                        TabsManager.Instance.activeTab.Browser.GetBrowserHost().SendKeyEvent(new KeyEvent
                        {
                            WindowsKeyCode = int.Parse(packet.JSONData),
                            FocusOnEditableField = false,
                            IsSystemKey = false,
                            Type = KeyEventType.Char
                        });
                        break;
                    case PacketType.SendChar:
                        var charPacket = JObject.Parse(packet.JSONData);
                        string keyData = charPacket["JSONData"]?.ToString();
                        bool isShift = charPacket["Shift"]?.ToObject<bool>() ?? false;
                        bool isAltGr = charPacket["AltGr"]?.ToObject<bool>() ?? false;
                        bool isCtrl = charPacket["Ctrl"]?.ToObject<bool>() ?? false;
                        string layout = charPacket["Layout"]?.ToString();
                        Logger.CreateLog("Client send char packet. ");
                        Logger.CreateLog(
                            $"[CHAR PACKET] " +
                            $"<isShiftPressed>:{isShift}, " +
                            $"<isAltGrPressed>:{isAltGr}, " +
                            $"<isCtrlPressed>:{isCtrl}, " +
                            $"<layout>:{layout}, " +
                            $"<CHAR> is \"{keyData[0]}\"",
                            ConsoleColor.Cyan
                            );

                        if (string.IsNullOrEmpty(keyData))
                            break;

                        char ch = keyData[0];
                        KeyMapping km = KeyHelper.GetVirtualKey(ch, layout);

                        bool needShift = (km.ShiftState & 1) != 0 || isShift;
                        bool needCtrl = (km.ShiftState & 2) != 0 || isCtrl;
                        bool needAlt = (km.ShiftState & 4) != 0 || isAltGr;

                        var host = TabsManager.Instance.activeTab.Browser.GetBrowserHost();

                        if (needShift) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x10, Type = KeyEventType.KeyDown });
                        if (needCtrl) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x11, Type = KeyEventType.KeyDown });
                        if (needAlt) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x12, Type = KeyEventType.KeyDown });

                        host.SendKeyEvent(new KeyEvent
                        {
                            WindowsKeyCode = km.VkCode,
                            NativeKeyCode = (int)km.ScanCode,
                            Type = KeyEventType.RawKeyDown,
                        });

                        host.SendKeyEvent(new KeyEvent
                        {
                            WindowsKeyCode = ch,
                            NativeKeyCode = (int)km.ScanCode,
                            Type = KeyEventType.Char,
                        });

                        host.SendKeyEvent(new KeyEvent
                        {
                            WindowsKeyCode = km.VkCode,
                            NativeKeyCode = (int)km.ScanCode,
                            Type = KeyEventType.KeyUp,
                        });

                        if (needAlt) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x12, Type = KeyEventType.KeyUp });
                        if (needCtrl) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x11, Type = KeyEventType.KeyUp });
                        if (needShift) host.SendKeyEvent(new KeyEvent { WindowsKeyCode = 0x10, Type = KeyEventType.KeyUp });

                        break;

                    case PacketType.SendKeyCommand:
                        string commandKeyData = packet.JSONData;
                        int vKey = 0;
                        KeyEventType type = KeyEventType.RawKeyDown;

                        Logger.CreateLog($"Client send key command packet. ");
                        Logger.CreateLog($"[KEY COMMAND] {commandKeyData}");

                        switch (commandKeyData)
                        {
                            case "Enter": vKey = 0x0D; break;
                            case "Backspace": vKey = 0x08; break;

                        }
                        TabsManager.Instance.activeTab.Browser.GetBrowserHost().SendKeyEvent(new KeyEvent
                        {
                            WindowsKeyCode = vKey,
                            FocusOnEditableField = false,
                            IsSystemKey = false,
                            Type = type
                        });
                        break;

                    case PacketType.SetActivePage:
                        long Id = 000000;
                        Int64.TryParse(packet.JSONData, out Id);

                        var tabToActivate = TabsManager.Instance.tabs.FirstOrDefault(tab => tab.GlobalId == Id);
                        if (tabToActivate != null)
                        {
                            TabsManager.Instance.activeTab = tabToActivate;
                            TabsManager.Instance.CurrentBrowser = TabsManager.Instance.activeTab.Browser;
                            BrowserHelper.RemoveAudioHandlers();
                            BrowserHelper.SetAudioHandlersToBrowser(TabsManager.Instance.activeTab.Browser);
                            Logger.CreateLog($"Switched to existing tab. Unique id is: {tabToActivate.GlobalId}");
                        }
                        BrowserHelper.SendPageScreenshot();
                        break;

                    case PacketType.Navigation:
                        Logger.CreateLog(
                            $"Client send URL for navigation. URL is {packet.JSONData}, <isURL>:{Utils.IsUrl(packet.JSONData)}",
                            ConsoleColor.Cyan
                            );

                        if (Utils.IsUrl(packet.JSONData))
                        {
                            TabsManager.Instance.activeTab.Browser.LoadUrl(packet.JSONData.Replace("skipchk:", ""));
                        }
                        else
                        {
                            TabsManager.Instance.activeTab.Browser.LoadUrl(
                                SettingsManager.Instance.GetValue<string>("BrowserSettings", "SearchSystem") + packet.JSONData
                                );
                        }
                        break;

                    case PacketType.NavigateBack:
                        if (TabsManager.Instance.activeTab.Browser.CanGoBack) TabsManager.Instance.activeTab.Browser.Back();
                        break;

                    case PacketType.NavigateForward:
                        if (TabsManager.Instance.activeTab.Browser.CanGoForward) TabsManager.Instance.activeTab.Browser.Forward();
                        break;

                    case PacketType.SizeChange:
                        var jsonObject = JObject.Parse(packet.JSONData);
                        var w = jsonObject.Value<int>("Width");
                        var h = jsonObject.Value<int>("Height");

                        if (w == 0 || h == 0)
                        {
                            Logger.CreateWarning($"Window is trying to resize, but width or height is zero. Request canceled");
                            break;
                        }

                        ChangeActiveBrowserSize(w * ScalingFactor, h * ScalingFactor);

                        Logger.CreateLog($"Window is resized. New width is {w}. New height is {h}");


                        break;

                    case PacketType.TouchDown:
                        var t_down = JsonConvert.DeserializeObject<PointerPacket>(packet.JSONData);
                        var press = new TouchEvent()
                        {
                            Id = (int)0,
                            X = (float)t_down.px * TabsManager.Instance.activeTab.Browser.Size.Width,
                            Y = (float)t_down.py * TabsManager.Instance.activeTab.Browser.Size.Height,
                            PointerType = CefSharp.Enums.PointerType.Touch,
                            Pressure = 0,
                            Type = CefSharp.Enums.TouchEventType.Pressed,
                        };
                        TabsManager.Instance.activeTab.Browser.GetBrowser().GetHost().SendTouchEvent(press);
                        break;

                    case PacketType.TouchUp:
                        var t_up = JsonConvert.DeserializeObject<PointerPacket>(packet.JSONData);
                        var up = new TouchEvent()
                        {
                            Id = (int)0,
                            X = (float)t_up.px * TabsManager.Instance.activeTab.Browser.Size.Width,
                            Y = (float)t_up.py * TabsManager.Instance.activeTab.Browser.Size.Height,
                            PointerType = CefSharp.Enums.PointerType.Touch,
                            Pressure = 0,
                            Type = CefSharp.Enums.TouchEventType.Released,
                        };
                        TabsManager.Instance.activeTab.Browser.GetBrowser().GetHost().SendTouchEvent(up);
                        break;

                    case PacketType.TouchMoved:
                        var t_move = JsonConvert.DeserializeObject<PointerPacket>(packet.JSONData);
                        var move = new TouchEvent()
                        {
                            Id = (int)0,
                            X = (float)t_move.px * TabsManager.Instance.activeTab.Browser.Size.Width,
                            Y = (float)t_move.py * TabsManager.Instance.activeTab.Browser.Size.Height,
                            PointerType = CefSharp.Enums.PointerType.Touch,
                            Pressure = 0,
                            Type = CefSharp.Enums.TouchEventType.Moved,
                        };
                        TabsManager.Instance.activeTab.Browser.GetBrowser().GetHost().SendTouchEvent(move);
                        break;

                    case PacketType.GetTabsOpen:
                        NotifyOpenTabs();
                        break;

                    case PacketType.RequestTabScreenshot:
                        long _tabId = long.Parse(packet.JSONData);
                        Logger.CreateLog($"Client requested tab screenshot for {_tabId}");
                        var _tab = TabsManager.Instance.tabs.FirstOrDefault(t => t.GlobalId == _tabId);
                        if (_tab != null)
                        {
                            BrowserHelper.SendTabScreenshot(_tab);
                        }
                        break;

                    case PacketType.CloseTab:
                        Int64.TryParse(packet.JSONData, out long tabId);

                        string homeUrl = SettingsManager.Instance.GetValue<string>("BrowserSettings", "FirstRunUrl");

                        Logger.CreateLog($"Client closed the tab. Unique id is: {tabId}");

                        for (int i = 0; i < TabsManager.Instance.tabs.Count; i++)
                        {
                            if (TabsManager.Instance.tabs[i].GlobalId == tabId)
                            {

                                if (TabsManager.Instance.tabs[i].GlobalId == TabsManager.Instance.activeTab.GlobalId)
                                {
                                    if (TabsManager.Instance.tabs.Count - 1 != 0)
                                    {
                                        TabsManager.Instance.activeTab = TabsManager.Instance.tabs[0];
                                    }
                                    else
                                    {
                                        TabsManager.Instance.activeTab = new Tab
                                        {
                                            GlobalId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                            Title = "Page is loading...",
                                            Url = homeUrl,
                                            Browser = BrowserHelper.CreateChromiumWebBrowser(homeUrl)
                                        };
                                        TabsManager.Instance.tabs.Add(TabsManager.Instance.activeTab);
                                        TabsManager.Instance.CurrentBrowser = TabsManager.Instance.activeTab.Browser;
                                    }
                                }
                                TabsManager.Instance.tabs[i].Browser.Dispose();
                                TabsManager.Instance.tabs.Remove(TabsManager.Instance.tabs[i]);
                                NotifyOpenTabs();
                                break;
                            }
                        }
                        break;
                    case PacketType.OpenUrlInNewTab:
                        string url = packet.JSONData;

                        TabsManager.Instance.activeTab = new Tab
                        {
                            GlobalId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Title = "Page is loading...",
                            Url = url,
                            Browser = BrowserHelper.CreateChromiumWebBrowser(url)
                        };
                        TabsManager.Instance.tabs.Add(TabsManager.Instance.activeTab);
                        TabsManager.Instance.CurrentBrowser = TabsManager.Instance.activeTab.Browser;
                        NotifyOpenTabs();
                        Logger.CreateLog($"Client opened new tab. Unique id is: {TabsManager.Instance.activeTab.GlobalId}, URL is {url}");
                        break;

                    case PacketType.NewScreenShotRequest:
                        string size = packet.JSONData;
                        Int32.TryParse(size.Split('x')[1], out int height);
                        Int32.TryParse(size.Split('x')[0], out int width);
                        Logger.CreateLog(
                            $"Client requested new screenshot for current tab {TabsManager.Instance.activeTab.GlobalId} " +
                            $"with height {height}, width {width}"
                            );
                        
                        BrowserHelper.SendPageScreenshot();

                        var cp = new TextPacket
                        {
                            PType = TextPacketType.NavigatedUrl,
                            text = TabsManager.Instance.activeTab.Url
                        };
                        StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(cp));

                        break;

                    default:
                        break;
                }
            }
        }


        private static WebSocketServer webSocketServer;

        public static void StartStreamingServer(int port)
        {
            try
            {
                webSocketServer = new WebSocketServer($"ws://0.0.0.0:{port}");

                webSocketServer.AddWebSocketService<StreamingBehavior>("/");
                webSocketServer.Log.Output = (logData, s) =>
                {
                    // if (logData.Level == LogLevel.Fatal || logData.Level == LogLevel.Error) return;
                };

                webSocketServer.Start();
                Logger.CreateLog($"[CONNECTION] WebSocket server started ...");
            }
            catch (Exception ex)
            {
                Logger.CreateError($"Error starting WebSocket server: {ex.Message}");
                Logger.CreateLog("Continuation is impossible");
                Logger.RequestAnyButton();
                Environment.Exit(-1);
            }
        }

        public void NotifyOpenTabsRequest(object e, EventArgs args)
        {
            NotifyOpenTabs();
        }

        private static void NotifyOpenTabs()
        {
            string stringOpenTabs = "";
            stringOpenTabs = string.Join(";", TabsManager.Instance.tabs.Select(tab => $"{tab.Title}|{tab.GlobalId}"));

            var cp = new TextPacket
            {
                PType = TextPacketType.OpenPages,
                text = stringOpenTabs
            };
            webSocketServer.WebSocketServices.Broadcast(JsonConvert.SerializeObject(cp));
        }

        private static void SendNewScreenshot()
        {

        }

        private static void ChangeActiveBrowserSize(int width, int height)
        {
            TabsManager.Instance.activeTab.Browser.Size = new System.Drawing.Size(width, height);
        }

        public void SendPacket(byte[] data)
        {
            try
            {
                webSocketServer.WebSocketServices.Broadcast(data);
            }
            catch (Exception ex)
            {
                Logger.CreateWarning($"Error sending packet to client: {ex.Message}");
            }
        }
        public void SendPacket(string data)
        {
            try
            {
                webSocketServer.WebSocketServices.Broadcast(data);
            }
            catch (Exception ex)
            {
                Logger.CreateWarning($"Error sending packet to client: {ex.Message}");
            }
        }

    }
}
