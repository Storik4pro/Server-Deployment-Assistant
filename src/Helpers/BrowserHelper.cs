using CefSharp;
using CefSharp.OffScreen;
using ServerDeploymentAssistant.src.Managers;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CefSharp.Enums;
using ServerDeploymentAssistant.src.Network;

namespace ServerDeploymentAssistant.src.Helpers
{
    public class BrowserHelper
    {
        class CustomLifeSpanHandler : ILifeSpanHandler
        {
            public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;

            public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

            public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

            public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
                string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
                IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                newBrowser = null;
                var existingTab = TabsManager.Instance.tabs.FirstOrDefault(tab => tab.Url == targetUrl);
                if (existingTab != null)
                {
                    TabsManager.Instance.activeTab = existingTab;
                    Logger.CreateLog($"Switched to existing tab. Unique id is: {targetUrl}");
                }
                else
                {
                    var currentBrowser = chromiumWebBrowser as ChromiumWebBrowser;

                    if (currentBrowser == null)
                    {
                        Logger.CreateWarning("Something went wrong. Cannot create new tab.");
                        return true;
                    }

                    var newTabBrowser = CreateChromiumWebBrowser(targetUrl);
                    var newTab = new Tab
                    {
                        GlobalId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Title = "Page is loading...",
                        Url = targetUrl,
                        Browser = newTabBrowser
                    };

                    TabsManager.Instance.AddTab(newTab);
                    StateHelper.Instance.NotifyOpenedTabs(); 
                }
                return true;
            }
        }

        public class TestRHI : DefaultRenderHandler
        {
            private ChromiumWebBrowser browser;

            public TestRHI(ChromiumWebBrowser browser) : base(browser)
            {
                this.browser = browser;
            }

            public override void OnVirtualKeyboardRequested(IBrowser browser, TextInputMode inputMode)
            {
                base.OnVirtualKeyboardRequested(browser, inputMode);


                Logger.CreateLog($"Virtual Keyboard Requested for {inputMode}.");
                if (inputMode == TextInputMode.None)
                {
                    StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(new TextPacket
                    {
                        PType = TextPacketType.TextInputCancel
                    }));
                }
                else
                {
                    browser.EvaluateScriptAsync(JavaScriptHelper.SetCursorInInputField);
                    var response = browser.EvaluateScriptAsync(JavaScriptHelper.GetActiveElementText).ContinueWith(t =>
                    {
                        StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(new TextPacket
                        {
                            PType = TextPacketType.TextInputContent,
                            text = (string)t.Result.Result
                        }));
                    });
                }
            }
        }

        private static void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            Tab activeTab = TabsManager.Instance.activeTab;

            if (activeTab == null || activeTab.Browser != sender)
            {
                return;
            }

            StateHelper.Instance.isLoadingNow = e.IsLoading;

            var cp = new TextPacket
            {
                PType = TextPacketType.LoadingStateChanged,
                text = e.IsLoading ? "LOADING" : "COMPLETE"
            };
            StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(cp));

            if (!StateHelper.Instance.isLoadingNow)
            {
                SendPageScreenshot();
            }

            SendNavigationInformation();
        }

        private static void Browser_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            Tab activeTab = TabsManager.Instance.activeTab;
            if (activeTab == null || activeTab.Browser != sender)
            {
                return;
            }

            activeTab.Url = e.Address;


            var cp = new TextPacket
            {
                PType = TextPacketType.NavigatedUrl,
                text = activeTab.Url
            };
            StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(cp));

            SendNavigationInformation();
        }

        private static void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            List<Tab> openTabs = TabsManager.Instance.tabs;
            foreach (var tab in openTabs)
            {
                if (tab.Browser.GetBrowser().Identifier == e.Browser.Identifier)
                {
                    tab.Title = e.Title;
                    string readyToSendText = $"{tab.Title}|{tab.GlobalId}";
                    // Console.WriteLine($"{readyToSendText}");
                    var cp = new TextPacket
                    {
                        PType = TextPacketType.EditOpenTabTitle,
                        text = readyToSendText
                    };
                    StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(cp));
                }

            }
        }

        public static ChromiumWebBrowser CreateChromiumWebBrowser(string url)
        {
            RemoveAudioHandlers();
            ChromiumWebBrowser _browser;
            _browser = new ChromiumWebBrowser(url);
            _browser.Size = new System.Drawing.Size(1440 / 2, 1248);
            _browser.LoadingStateChanged += Browser_LoadingStateChanged;
            _browser.AddressChanged += Browser_AddressChanged;
            _browser.RenderHandler = new TestRHI(_browser);
            _browser.Paint += CefPaint;
            _browser.LifeSpanHandler = new CustomLifeSpanHandler();
            _browser.TitleChanged += Browser_TitleChanged;

            AudioHelper audioHelper = new AudioHelper();
            audioHelper.onAudioStreamStarted += Network.AudioStreamServer.Instance.OnAudioStreamStarted;
            audioHelper.onAudioStreamPacket += Network.AudioStreamServer.Instance.OnAudioStreamPacket;
            _browser.AudioHandler = audioHelper;
            return _browser;
        }

        private static void CefPaint(object sender, OnPaintEventArgs e)
        {
            Tab activeTab = TabsManager.Instance.activeTab;

            if (activeTab == null || activeTab.Browser != sender)
            {
                if (activeTab != null)
                {
                    // pass
                }
                return;
            }
            if (StateHelper.Instance.isLoadingNow)
            {
                return;
            }
            if (e.BufferHandle == IntPtr.Zero)
            {
                Logger.CreateWarning("BufferHandle is empty. Cannot correctly handle browser Paint signal.");
                return;
            }

            int bufferSize = e.Width * e.Height * 4;
            if (bufferSize <= 0)
            {
                Logger.CreateWarning("Invalid buffer size. Cannot correctly handle browser Paint signal.");
                return;
            }

            bool useVideoGzipCompress = SettingsManager.Instance.GetValue<bool>("VideoStreamSettings", "UseVideoGzipCompress");

            var buffer = new byte[bufferSize];
            Marshal.Copy(e.BufferHandle, buffer, 0, buffer.Length);

            using (var bitmap = new Bitmap(e.Width, e.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, e.Width, e.Height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                Marshal.Copy(buffer, 0, bitmapData.Scan0, buffer.Length);
                bitmap.UnlockBits(bitmapData);

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    var pngData = ms.ToArray();

                    byte[] finalData;

                    if (useVideoGzipCompress)
                    {
                        using (var outMs = new MemoryStream())
                        {
                            using (var gzip = new GZipStream(outMs, CompressionMode.Compress, leaveOpen: true))
                            {
                                gzip.Write(pngData, 0, pngData.Length);
                            }
                            finalData = outMs.ToArray();
                        }
                    }
                    else
                    {
                        finalData = pngData;
                    }

                    StateHelper.Instance.streamServer.SendPacket(finalData);
                }
            }
        }
        public static async void SendTabScreenshot(Tab tab)
        {
            try
            {
                var screenshot = await tab.Browser.CaptureScreenshotAsync(
                    CefSharp.DevTools.Page.CaptureScreenshotFormat.Png,
                    quality: 100
                );

                var packet = new
                {
                    Type = "TabScreenshot",
                    TabId = tab.GlobalId,
                    Data = Convert.ToBase64String(screenshot)
                };

                var json = JsonConvert.SerializeObject(packet);
                StateHelper.Instance.streamServer.SendPacket(json);
                Logger.CreateLog($"Screenshot for tab with unique id {tab.GlobalId} sent to client.");
            }
            catch (Exception ex)
            {
                Logger.CreateWarning($"Error capturing screenshot for tab with unique id {tab.GlobalId}: {ex.Message}");
            }
        }

        public static async void SendFullPageScreenshot()
        {
            Tab activeTab = TabsManager.Instance.activeTab;
            ChromiumWebBrowser browser = activeTab.Browser;

            int quality = SettingsManager.Instance.GetValue<int>("VideoStreamSettings", "FullPageScreenshotQuality");
            try
            {
                var dimensions = await browser.EvaluateScriptAsync(JavaScriptHelper.SetFullPageSize);

                if (dimensions.Success && dimensions.Result is IDictionary<string, object> result)
                {
                    int fullWidth = Convert.ToInt32(result["width"]);
                    int fullHeight = Convert.ToInt32(result["height"]);

                    var originalSize = browser.Size;

                    browser.Size = new System.Drawing.Size(fullWidth, fullHeight);

                    byte[] screenshot = await GetScreenshotBytes(browser, quality);
                    
                    browser.Size = originalSize;

                    const int chunkSize = 1024 * 1024 * 4;
                    var totalChunks = (int)Math.Ceiling((double)screenshot.Length / chunkSize);

                    for (int i = 0; i < totalChunks; i++)
                    {
                        var chunk = screenshot.Skip(i * chunkSize).Take(chunkSize).ToArray();

                        var packet = new
                        {
                            Type = "FullPageScreenshot",
                            ChunkIndex = i,
                            TotalChunks = totalChunks,
                            Data = Convert.ToBase64String(chunk)
                        };

                        var json = JsonConvert.SerializeObject(packet);

                        StateHelper.Instance.streamServer.SendPacket(json);
                        Logger.CreateLog($"Sending chunk for FullPageScreenshot {i + 1}/{totalChunks}");
                    }
                    Logger.CreateLog("Full-page screenshot sent to WebSocket clients.");
                }
                else
                {
                    Logger.CreateWarning("Failed to retrieve page dimensions.");
                }
            }
            catch (Exception ex)
            {
                Logger.CreateWarning("Error capturing full-page screenshot: " + ex.Message);
            }
        }

        private static async Task<byte[]> GetScreenshotBytes(ChromiumWebBrowser browser, int quality = 50)
        {
            if (browser == null)
            {
                Logger.CreateWarning("Error capturing screenshot: Browser isn't initialized");
            }
            try
            {
                byte[] screenshot = await browser.CaptureScreenshotAsync(CefSharp.DevTools.Page.CaptureScreenshotFormat.Png, quality: quality);
                return screenshot;
            }
            catch (Exception ex)
            {
                Logger.CreateWarning("Error capturing screenshot: " + ex.Message);
                return null;
            }
        }

        public static async void SendPageScreenshot()
        {
            int quality = SettingsManager.Instance.GetValue<int>("VideoStreamSettings", "FullPageScreenshotQuality");

            ChromiumWebBrowser browser = TabsManager.Instance.GetActiveBrowser();

            byte[] screenshot = await GetScreenshotBytes(browser, quality);

            if (screenshot == null)
            {
                return;
            }

            var pngData = screenshot.ToArray();

            byte[] compressedData;
            using (var outMs = new MemoryStream())
            {
                using (var gzip = new GZipStream(outMs, CompressionMode.Compress, leaveOpen: true))
                {
                    gzip.Write(pngData, 0, pngData.Length);
                }
                compressedData = outMs.ToArray();
            }

            StateHelper.Instance.streamServer.SendPacket(compressedData);
        }

        public static void RemoveAudioHandlers()
        {
            List<Tab> openTabs = TabsManager.Instance.tabs;
            AudioStreamServer audioStreamingServer = StateHelper.Instance.audioServer;

            for (int i = 0; i < openTabs.Count; i++)
            {
                AudioHelper audioHelper = openTabs[i].Browser.AudioHandler as AudioHelper;

                if (audioHelper == null)
                    continue;

                audioHelper.onAudioStreamStarted -= audioStreamingServer.OnAudioStreamStarted;
                audioHelper.onAudioStreamPacket -= audioStreamingServer.OnAudioStreamPacket;
            }
        }

        public static void SetAudioHandlersToBrowser(ChromiumWebBrowser chromiumWebBrowser)
        {
            AudioHelper audioHelper = new AudioHelper();
            audioHelper.onAudioStreamStarted += StateHelper.Instance.audioServer.OnAudioStreamStarted;
            audioHelper.onAudioStreamPacket += StateHelper.Instance.audioServer.OnAudioStreamPacket;
            chromiumWebBrowser.AudioHandler = audioHelper;
        }

        private static void SendNavigationInformation()
        {
            Tab activeTab = TabsManager.Instance.activeTab;

            var canGoBackPacket = new TextPacket
            {
                PType = TextPacketType.IsClientCanSendGoBackRequest,
                text = activeTab.Browser.CanGoBack ? "true" : "false"
            };
            var canGoForwardPacket = new TextPacket
            {
                PType = TextPacketType.IsClientCanSendGoForwardRequest,
                text = activeTab.Browser.CanGoForward ? "true" : "false"
            };
            
            StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(canGoBackPacket));
            StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(canGoForwardPacket));
        }
    }
}
