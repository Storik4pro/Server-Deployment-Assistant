using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServerDeploymentAssistant.src
{
    public class Utils
    {
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            Logger.CreateError("No network adapters with an IPv4 address in the system. Continuation is impossible.");
            Logger.RequestAnyButton();
            Environment.Exit(-1);
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public static bool IsUrl(string urlString)
        {
            if (urlString.StartsWith("skipchk:"))
                return true;
            if (string.IsNullOrWhiteSpace(urlString))
                return false;

            const string pattern =
                @"^(?:(?:https?://)?)" +
                @"(?:www\.)?" +
                @"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?" +
                @"(?:\.[A-Za-z]{2,})+" +
                @"(?::\d{1,5})?" +
                @"(?:/[\w\-.~:/?#[\]@!$&'()*+,;=%]*)?$";

            return Regex.IsMatch(
                urlString,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            );
        }
    }
}
