using CefSharp.OffScreen;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ServerDeploymentAssistant.src.Helpers
{
    public class CertificateHelper
    {

        public class ConnectionInfo
        {
            public bool IsTls { get; set; }
            public bool HasCertError { get; set; }
            public CefSharp.CertStatus CertProblems { get; set; }
            public string TlsVersion { get; set; }
            public string Url { get; set; }
            public X509Certificate2 Certificate { get; set; }
        }
        public static Task<ConnectionInfo> GetCurrentConnectionInfoAsync(ChromiumWebBrowser browser)
        {
            var tcs = new TaskCompletionSource<ConnectionInfo>();

            Cef.UIThreadTaskFactory.StartNew(() =>
            {
                try
                {
                    var result = new ConnectionInfo();

                    var ibrowser = browser.GetBrowser();
                    if (ibrowser == null)
                    {
                        tcs.SetResult(result);
                        return;
                    }

                    var host = ibrowser.GetHost();
                    if (host == null)
                    {
                        tcs.SetResult(result);
                        return;
                    }

                    var navEntry = host.GetVisibleNavigationEntry();
                    if (navEntry == null)
                    {
                        tcs.SetResult(result);
                        return;
                    }

                    result.Url = navEntry.DisplayUrl;

                    var sslStatus = navEntry.SslStatus;
                    if (sslStatus == null)
                    {
                        tcs.SetResult(result);
                        return;
                    }

                    result.IsTls = sslStatus.IsSecureConnection;

                    result.CertProblems = sslStatus.CertStatus;
                    result.HasCertError = sslStatus.CertStatus != CefSharp.CertStatus.None;

                    result.TlsVersion = sslStatus.SslVersion.ToString();

                    result.Certificate = sslStatus.X509Certificate;

                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public async static void GetAndSendCertInformation(ChromiumWebBrowser chromiumWebBrowser)
        {
            if (chromiumWebBrowser == null)
                return;
            ConnectionInfo info = await GetCurrentConnectionInfoAsync(chromiumWebBrowser);

            if (info.Certificate == null)
            {
                return;
            }

            bool isTls = info.IsTls;
            bool hasCertError = info.HasCertError;
            string certErrorName = info.CertProblems.ToString();
            string tlsVersion = info.TlsVersion;
            string url = info.Url;

            X509Certificate2 cert = info.Certificate;
            string subject = cert.Subject;
            string issuer = cert.Issuer;
            DateTime validFrom = cert.NotBefore;
            DateTime validUntil = cert.NotAfter;
            string thumbprint = cert.Thumbprint;
            string serialNumber = cert.SerialNumber;
            string publicKey = cert.GetPublicKeyString();

            ConnectionSecurePacket connectionSecurePacket = new ConnectionSecurePacket
            {
                IsSecureConnection = isTls,
                CertificateError = hasCertError,
                CertificateErrorName = certErrorName,
                TlsVersion = tlsVersion.ToString(),
                Url = url,
                Subject = subject,
                Issuer = issuer,
                ValidFromTime = validFrom,
                ValidToTime = validUntil,
                Thumbprint = thumbprint,
                SerialNumber = serialNumber,
                PublicKey = publicKey,
            };
            StateHelper.Instance.streamServer.SendPacket(JsonConvert.SerializeObject(connectionSecurePacket));

        }
    }
}
