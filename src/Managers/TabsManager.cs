using CefSharp;
using CefSharp.OffScreen;
using Instances;
using ServerDeploymentAssistant.src.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerDeploymentAssistant.src.Managers
{
    public class Tab
    {
        public long GlobalId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public ChromiumWebBrowser Browser { get; set; }
    }

    public class TabsManager
    {
        private static TabsManager _instance;
        private static readonly object _lock = new object();
        public static TabsManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new TabsManager();
                    return _instance;
                }
            }
        }
        
        public List<Tab> tabs = new List<Tab>();

        public Tab activeTab;

        public ChromiumWebBrowser CurrentBrowser { get; set; }
        public void SetActiveBrowser(ChromiumWebBrowser chromiumWebBrowser)
        {
            CurrentBrowser = chromiumWebBrowser;
        }

        public TabsManager() 
        { 
            
        }


        public void CreateNewTabWithUrl(string targetUrl)
        {
            var newTabBrowser = Helpers.BrowserHelper.CreateChromiumWebBrowser(targetUrl);
            var newTab = new Tab
            {
                GlobalId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Title = "Page is loading...",
                Url = targetUrl,
                Browser = newTabBrowser
            };
            AddTab(newTab);
        }

        public void CreateNewBrowser(string url)
        {
            var tab = new Tab
            {
                GlobalId = DateTime.Now.Ticks,
                Title = "Page is loading ...",
                Url = url,
                Browser = BrowserHelper.CreateChromiumWebBrowser(url)
            };
            AddTab(tab);
        }

        public void AddTab(Tab tab)
        {
            tabs.Add(tab);
            activeTab = tab;
        }

        public void ChangeActiveBrowser(Tab tab)
        {
            BrowserHelper.RemoveAudioHandlers();

            tabs.Add(tab);
            activeTab = tab;
            CurrentBrowser = tab.Browser;
            BrowserHelper.SetAudioHandlersToBrowser(CurrentBrowser);
        }

        public ChromiumWebBrowser GetActiveBrowser()
        {
            if (activeTab != null)
            {
                return activeTab.Browser;
            }
            return null;
        }

    }
}
