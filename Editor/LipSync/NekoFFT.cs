using System;

namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// Minimal in-place iterative radix-2 FFT. Allocation-free once constructed, so it can be
    /// reused across thousands of analysis frames without hammering the GC.
    /// </summary>
    internal sealed class NekoFFT
    {
        readonly int _size;
        readonly int _bits;
        readonly int[] _reverse;
        readonly float[] _cos;
        readonly float[] _sin;
        readonly float[] _window;

        public int Size { get { return _size; } }
        /// <summary>Number of usable magnitude bins (0 .. Nyquist).</summary>
        public int Bins { get { return _size / 2 + 1; } }

        public NekoFFT(int size)
        {
            if (size < 8 || (size & (size - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two and at least 8.", "size");

            _size = size;
            _bits = 0;
            while ((1 << _bits) < size) _bits++;

            _reverse = new int[size];
            for (int i = 0; i < size; i++)
            {
                int r = 0;
                for (int b = 0; b < _bits; b++)
                    if ((i & (1 << b)) != 0) r |= 1 << (_bits - 1 - b);
                _reverse[i] = r;
            }

            _cos = new float[size / 2];
            _sin = new float[size / 2];
            for (int i = 0; i < size / 2; i++)
            {
                double a = -2.0 * Math.PI * i / size;
                _cos[i] = (float)Math.Cos(a);
                _sin[i] = (float)Math.Sin(a);
            }

            // Periodic Hann window.
            _window = new float[size];
            for (int i = 0; i < size; i++)
                _window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));
        }

        /// <summary>
        /// Windows <paramref name="input"/> (length == Size), runs the transform and writes
        /// linear magnitudes into <paramref name="magnitudes"/> (length &gt;= Bins).
        /// </summary>
        public void MagnitudeSpectrum(float[] input, float[] re, float[] im, float[] magnitudes)
        {
            for (int i = 0; i < _size; i++)
            {
                int r = _reverse[i];
                re[r] = input[i] * _window[i];
                im[r] = 0f;
            }

            for (int len = 2; len <= _size; len <<= 1)
            {
                int half = len >> 1;
                int step = _size / len;
                for (int i = 0; i < _size; i += len)
                {
                    int k = 0;
                    for (int j = i; j < i + half; j++)
                    {
                        float wr = _cos[k];
                        float wi = _sin[k];
                        int j2 = j + half;
                        float tr = re[j2] * wr - im[j2] * wi;
                        float ti = re[j2] * wi + im[j2] * wr;
                        re[j2] = re[j] - tr;
                        im[j2] = im[j] - ti;
                        re[j] += tr;
                        im[j] += ti;
                        k += step;
                    }
                }
            }

            int bins = Bins;
            float norm = 2f / _size;
            for (int i = 0; i < bins; i++)
                magnitudes[i] = (float)Math.Sqrt(re[i] * re[i] + im[i] * im[i]) * norm;
        }
    }
}
