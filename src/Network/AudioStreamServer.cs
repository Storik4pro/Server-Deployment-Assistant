using CefSharp.Structs;
using CefSharp;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using WebSocketSharp;
using ServerDeploymentAssistant.src.Managers;
using ServerDeploymentAssistant.src.Helpers;

namespace ServerDeploymentAssistant.src.Network
{
    public class AudioStreamServer : IDisposable
    {
        private static AudioStreamServer _instance;
        private static readonly object _lock = new object();
        public static AudioStreamServer Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new AudioStreamServer();
                    return _instance;
                }
            }
        }
        private readonly TcpListener listener;
        private NetworkStream netStream;
        private int channelCount = 2;
        private long _sequence = 0;
        private long _nextPtsUs = 0;

        public AudioStreamServer(int port = 0000)
        {
            StateHelper.Instance.audioServer = this;
        }

        public class AudioStreamingBehavior : WebSocketBehavior
        {
            protected override void OnOpen()
            {
                Logger.CreateLog("[AUDIO CONNECTION] Audio WebSocket connection opened.");
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Logger.CreateLog("[AUDIO CONNECTION] Audio WebSocket connection closed.");
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                Logger.CreateLog("[AUDIO CONNECTION] Received message: " + e.Data);
            }
        }

        private static WebSocketServer audioWebSocketServer;

        public void StartAudioStreamingServer(int port)
        {
            try
            {
                audioWebSocketServer = new WebSocketServer($"ws://0.0.0.0:{port}");

                audioWebSocketServer.AddWebSocketService<AudioStreamingBehavior>("/");
                audioWebSocketServer.Log.Output = (logData, s) =>
                {
                    // if (logData.Level == LogLevel.Fatal || logData.Level == LogLevel.Error) return;
                };

                audioWebSocketServer.Start();
                Logger.CreateLog($"[AUDIO CONNECTION] Audio WebSocket server started ...");
            }
            catch (Exception ex)
            {
                Logger.CreateError($"[AUDIO CONNECTION] Failed to start audio WebSocket server: {ex.Message}");
                return;
            }
        }
        public static void StreamAudio(byte[] audioData)
        {
            if (audioWebSocketServer != null)
            {
                foreach (var path in audioWebSocketServer.WebSocketServices.Paths)
                {
                    var sessions = audioWebSocketServer.WebSocketServices[path].Sessions;
                    sessions.Broadcast(audioData);
                }
            }
        }


        public void OnAudioStreamStarted(object sender, Tuple<IWebBrowser, IBrowser, AudioParameters, int> tuple)
        {

        }

        public void OnAudioStreamPacket(object sender, Tuple<IWebBrowser, IBrowser, IntPtr, int, long> tuple)
        {
            IntPtr data = tuple.Item3;
            int frames = tuple.Item4;
            const int sampleRate = 41000;
            int outSampleRate = SettingsManager.Instance.GetValue<int>("AudioStreamSettings", "AudioStreamResamplingRate");
            bool useResampling = SettingsManager.Instance.GetValue<bool>("AudioStreamSettings", "UseAudioResampling");
            bool useGzipCompress = SettingsManager.Instance.GetValue<bool>("AudioStreamSettings", "UseAudioGzipCompress");

            float[][] src = new float[channelCount][];
            for (int c = 0; c < channelCount; c++)
                src[c] = new float[frames];

            unsafe
            {
                var srcPtr = (float**)data.ToPointer();
                for (int c = 0; c < channelCount; c++)
                    for (int f = 0; f < frames; f++)
                        src[c][f] = srcPtr[c][f];
            }

            float[] interleaved = new float[frames * channelCount];
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < channelCount; c++)
                    interleaved[f * channelCount + c] = src[c][f];

            var sourceProvider = new BufferedWaveProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount))
            {
                BufferLength = interleaved.Length * sizeof(float)
            };
            byte[] buffer = new byte[interleaved.Length * sizeof(float)];
            Buffer.BlockCopy(interleaved, 0, buffer, 0, buffer.Length);
            sourceProvider.AddSamples(buffer, 0, buffer.Length);

            var sampleProvider = sourceProvider.ToSampleProvider();

            ISampleProvider provider = sampleProvider;
            int processingRate = sampleRate;
            if (useResampling && outSampleRate > 0 && outSampleRate != sampleRate)
            {
                provider = new WdlResamplingSampleProvider(sampleProvider, outSampleRate);
                processingRate = outSampleRate;
            }

            int expectedFrames = useResampling && processingRate != sampleRate
                ? (int)((long)frames * processingRate / sampleRate)
                : frames;
            float[] processed = new float[expectedFrames * channelCount];
            int read = provider.Read(processed, 0, processed.Length);

            int totalSamples = read;
            int byteCount = totalSamples * sizeof(float);

            double durationUs = totalSamples / (double)processingRate * 1_000_000;
            long ptsUs = _nextPtsUs;
            _nextPtsUs = (long)Math.Round(_nextPtsUs + durationUs);

            byte[] header = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes((int)_sequence++), 0, header, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ptsUs), 0, header, 4, 8);

            byte[] payloadBytes = new byte[byteCount];
            Buffer.BlockCopy(processed, 0, payloadBytes, 0, byteCount);
            byte[] compressedPayload;

            if (useGzipCompress)
            {
                using (var msPayload = new MemoryStream())
                {
                    using (var gzip = new GZipStream(msPayload, CompressionMode.Compress, leaveOpen: true))
                    {
                        gzip.Write(payloadBytes, 0, payloadBytes.Length);
                    }
                    compressedPayload = msPayload.ToArray();
                }
            }
            else
            {
                compressedPayload = payloadBytes;
            }

            using (var msFinal = new MemoryStream())
            {
                msFinal.Write(header, 0, header.Length);
                msFinal.Write(compressedPayload, 0, compressedPayload.Length);
                StreamAudio(msFinal.ToArray());
            }
        }

        public void Dispose()
        {
            netStream?.Dispose();
            listener.Stop();
        }
    }
}
