using CefSharp;
using CefSharp.Enums;
using CefSharp.OffScreen;
using CefSharp.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CSCore.Codecs.WAV;
using System.Net.Sockets;
using System.Net;

namespace ServerDeploymentAssistant
{
    public class AudioHelper : IAudioHandler
    {
        public EventHandler<Tuple<IWebBrowser, IBrowser, IntPtr, int, long>> onAudioStreamPacket;
        public EventHandler<Tuple<IWebBrowser, IBrowser, AudioParameters, int>> onAudioStreamStarted;

        public AudioHelper() : base()
        {

        }

        public void Dispose()
        {
            
        }

        public bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters)
        {
            return true;
        }

        public void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage)
        {
            Console.WriteLine("Audio stream error: " + errorMessage);
        }

        public void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
            onAudioStreamPacket?.Invoke(this, Tuple.Create(chromiumWebBrowser, browser, data, noOfFrames, pts));
        }

        public void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
        {
            onAudioStreamStarted?.Invoke(this, Tuple.Create(chromiumWebBrowser, browser, parameters, channels));
        }

        public void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            
        }
    }
}
