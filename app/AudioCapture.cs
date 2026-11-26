using System;
using System.Threading;
using NAudio.Wave;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Bat am thanh he thong bang WASAPI loopback (NAudio) -> FFT -> 23 dai
    // pho (thang log 30Hz..16kHz) + phat hien nhip (beat o dai bass).
    // Bands01: 23 gia tri 0..1 (da auto-gain) cho visualizer / stream.
    // Levels100: 23 gia tri 0..100 cho khoi 0x78 (scale=100 cua protocol.py).
    // Neu khong bat duoc (khong co thiet bi / khong phai Windows), Available
    // = false va UI tu chuyen sang pho mo phong nhu ban Preview.
    // =====================================================================
    public sealed class AudioCapture : IDisposable
    {
        public bool Available { get; private set; }
        public string Error { get; private set; } = "";
        public float[] Bands01 { get; } = new float[L99Protocol.AudioBandCount];
        public int[] Levels100 { get; } = new int[L99Protocol.AudioBandCount];
        public bool Beat { get; private set; }

        private WasapiLoopbackCapture _capture;
        private readonly float[] _ring = new float[8192];
        private int _ringPos;
        private readonly object _gate = new object();
        private Thread _worker;
        private volatile bool _running;
        private float _gain = 4f;
        private float _bassAvg = 0.2f;
        private long _lastBeatTick;

        public void Start()
        {
            if (_running) return;
            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnData;
                _capture.StartRecording();
                Available = true;
                _running = true;
                _worker = new Thread(AnalyzeLoop) { IsBackground = true, Name = "AudioFFT" };
                _worker.Start();
            }
            catch (Exception ex)
            {
                Available = false;
                Error = ex.Message;
                try { _capture?.Dispose(); } catch { }
                _capture = null;
            }
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            var fmt = _capture.WaveFormat;
            int channels = Math.Max(1, fmt.Channels);
            int bytesPerSample = fmt.BitsPerSample / 8;
            if (bytesPerSample == 0) return;
            lock (_gate)
            {
                int frames = e.BytesRecorded / (bytesPerSample * channels);
                for (int i = 0; i < frames; i++)
                {
                    float sum = 0;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int off = (i * channels + ch) * bytesPerSample;
                        if (off + 4 > e.BytesRecorded) break;
                        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
                            sum += BitConverter.ToSingle(e.Buffer, off);
                        else if (bytesPerSample == 2)
                            sum += BitConverter.ToInt16(e.Buffer, off) / 32768f;
                    }
                    _ring[_ringPos] = sum / channels;
                    _ringPos = (_ringPos + 1) % _ring.Length;
                }
            }
        }

        private void AnalyzeLoop()
        {
            const int N = 2048;
            var re = new double[N];
            var im = new double[N];
            var window = new double[N];
            for (int i = 0; i < N; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
            // 23 dai log tu 30Hz -> 16kHz (gia dinh 48kHz; ti le chi lech nhe o tan so mau khac)
            double fMin = 30, fMax = 16000;
            var edges = new double[L99Protocol.AudioBandCount + 1];
            for (int b = 0; b <= L99Protocol.AudioBandCount; b++)
                edges[b] = fMin * Math.Pow(fMax / fMin, (double)b / L99Protocol.AudioBandCount);
            double sampleRate = 48000;
            try { if (_capture != null) sampleRate = _capture.WaveFormat.SampleRate; } catch { }

            while (_running)
            {
                Thread.Sleep(33); // ~30 lan/giay
                lock (_gate)
                {
                    for (int i = 0; i < N; i++)
                    {
                        int idx = (_ringPos - N + i) % _ring.Length;
                        if (idx < 0) idx += _ring.Length;
                        re[i] = _ring[idx] * window[i];
                        im[i] = 0;
                    }
                }
                Fft(re, im);
                var mags = new double[N / 2];
                for (int i = 0; i < N / 2; i++) mags[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]) / N;
                float peak = 0.0001f;
                var raw = new float[L99Protocol.AudioBandCount];
                for (int b = 0; b < L99Protocol.AudioBandCount; b++)
                {
                    int i0 = Math.Max(1, (int)(edges[b] / sampleRate * N));
                    int i1 = Math.Min(N / 2 - 1, Math.Max(i0 + 1, (int)(edges[b + 1] / sampleRate * N)));
                    double acc = 0;
                    for (int i = i0; i < i1; i++) acc += mags[i];
                    raw[b] = (float)(acc / (i1 - i0));
                    if (raw[b] > peak) peak = raw[b];
                }
                // Auto-gain: muc tieu chuan hoa theo dinh gan nhat, decay cham.
                float target = Math.Min(24f, Math.Max(1.2f, 0.9f / peak));
                _gain += (target - _gain) * 0.05f;
                float bass = 0;
                for (int b = 0; b < L99Protocol.AudioBandCount; b++)
                {
                    float v = Math.Min(1f, raw[b] * _gain);
                    Bands01[b] += (v - Bands01[b]) * 0.55f; // lam muot
                    Levels100[b] = (int)Math.Round(Math.Min(1f, Bands01[b]) * 100);
                    if (b < 3) bass += Bands01[b];
                }
                bass /= 3f;
                _bassAvg += (bass - _bassAvg) * 0.04f;
                long now = Environment.TickCount64;
                Beat = bass > Math.Max(0.25f, _bassAvg * 1.45f) && now - _lastBeatTick > 180;
                if (Beat) _lastBeatTick = now;
            }
        }

        private static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cwr = 1, cwi = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int u = i + k, v = i + k + len / 2;
                        double vr = re[v] * cwr - im[v] * cwi;
                        double vi = re[v] * cwi + im[v] * cwr;
                        re[v] = re[u] - vr; im[v] = im[u] - vi;
                        re[u] += vr; im[u] += vi;
                        double nwr = cwr * wr - cwi * wi;
                        cwi = cwr * wi + cwi * wr;
                        cwr = nwr;
                    }
                }
            }
        }

        public void Stop()
        {
            _running = false;
            try { _capture?.StopRecording(); } catch { }
            try { _capture?.Dispose(); } catch { }
            _capture = null;
            Available = false;
        }

        public void Dispose() => Stop();
    }
}
