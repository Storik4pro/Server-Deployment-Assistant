using Instances;
using ServerDeploymentAssistant.src.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerDeploymentAssistant.src.Helpers
{
    public class StateHelper
    {
        private static StateHelper _instance;
        private static readonly object _lock = new object();
        public static StateHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new StateHelper();
                    return _instance;
                }
            }
        }

        public EventHandler OnNotifyOpenedTabs;

        public void NotifyOpenedTabs()
        {
            OnNotifyOpenedTabs?.Invoke(null, EventArgs.Empty);
        }

        public VideoStreamServer streamServer { get; set; }
        public AudioStreamServer audioServer { get; set; }

        public bool isLoadingNow { get; set; } = false;

        public bool enablePressButtonRequest { get; set; } = true;

    }
}
