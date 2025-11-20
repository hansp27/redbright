using System;
using System.Runtime.InteropServices;

namespace Redbright.Core;

public sealed class GammaRampService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Ramp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll", EntryPoint = "SetDeviceGammaRamp")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ref Ramp ramp);

    [DllImport("gdi32.dll", EntryPoint = "GetDeviceGammaRamp")]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, ref Ramp ramp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private readonly object _sync = new();
    private IntPtr _desktopHdc = IntPtr.Zero;
    private Ramp? _originalRamp;
    private bool _isRedOnlyActive;
    private double _currentBrightnessPercent = 100.0;

    public bool IsRedOnlyActive
    {
        get
        {
            lock (_sync) { return _isRedOnlyActive; }
        }
    }

    public double CurrentBrightnessPercent
    {
        get
        {
            lock (_sync) { return _currentBrightnessPercent; }
        }
    }

    public GammaRampService()
    {
        Initialize();
    }

    private static Ramp CreateEmptyRamp()
    {
        return new Ramp
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256]
        };
    }

    private void Initialize()
    {
        lock (_sync)
        {
            if (_desktopHdc == IntPtr.Zero)
            {
                _desktopHdc = GetDC(IntPtr.Zero);
            }

            if (_originalRamp is null)
            {
                var temp = CreateEmptyRamp();
                if (GetDeviceGammaRamp(_desktopHdc, ref temp))
                {
                    _originalRamp = temp;
                }
                else
                {
                    // Fallback: construct a linear identity ramp as "original"
                    _originalRamp = BuildLinearIdentityRamp();
                }
            }
        }
    }

    public void ApplyRedOnlyBrightness(double brightnessPercent)
    {
        lock (_sync)
        {
            Initialize();

            if (brightnessPercent is double.NaN) brightnessPercent = 100.0;
            brightnessPercent = Math.Clamp(brightnessPercent, 0.0, 100.0);
            _currentBrightnessPercent = brightnessPercent;

            var ramp = CreateEmptyRamp();
            double scale = brightnessPercent / 100.0;

            for (int i = 0; i < 256; i++)
            {
                // Base linear ramp value 0..65535
                uint baseValue = (uint)((i * 65535) / 255);
                uint redValue = (uint)Math.Round(baseValue * scale);
                if (redValue > 65535) redValue = 65535;

                ramp.Red[i] = (ushort)redValue;
                ramp.Green[i] = 0;
                ramp.Blue[i] = 0;
            }

            SetDeviceGammaRamp(_desktopHdc, ref ramp);
            _isRedOnlyActive = true;
        }
    }

    public void ApplyBrightnessOnly(double brightnessPercent)
    {
        lock (_sync)
        {
            Initialize();
            if (brightnessPercent is double.NaN) brightnessPercent = 100.0;
            brightnessPercent = Math.Clamp(brightnessPercent, 0.0, 100.0);
            _currentBrightnessPercent = brightnessPercent;

            var ramp = CreateEmptyRamp();
            double scale = brightnessPercent / 100.0;

            for (int i = 0; i < 256; i++)
            {
                uint baseValue = (uint)((i * 65535) / 255);
                uint value = (uint)Math.Round(baseValue * scale);
                if (value > 65535) value = 65535;

                ushort v = (ushort)value;
                ramp.Red[i] = v;
                ramp.Green[i] = v;
                ramp.Blue[i] = v;
            }

            SetDeviceGammaRamp(_desktopHdc, ref ramp);
            _isRedOnlyActive = false;
        }
    }

    public void ApplyColorOnlyRed()
    {
        lock (_sync)
        {
            Initialize();
            var ramp = CreateEmptyRamp();
            for (int i = 0; i < 256; i++)
            {
                ushort value = (ushort)((i * 65535) / 255);
                ramp.Red[i] = value;
                ramp.Green[i] = 0;
                ramp.Blue[i] = 0;
            }
            SetDeviceGammaRamp(_desktopHdc, ref ramp);
            _isRedOnlyActive = true;
        }
    }

    public void RestoreOriginal()
    {
        lock (_sync)
        {
            if (_desktopHdc == IntPtr.Zero || _originalRamp is null) return;

            var original = _originalRamp.Value;
            SetDeviceGammaRamp(_desktopHdc, ref original);
            _isRedOnlyActive = false;
        }
    }

    /// <summary>
    /// Attempts to read the current device gamma ramp.
    /// </summary>
    public bool TryGetCurrentRamp(out Ramp current)
    {
        lock (_sync)
        {
            Initialize();
            current = CreateEmptyRamp();
            if (_desktopHdc == IntPtr.Zero) return false;
            return GetDeviceGammaRamp(_desktopHdc, ref current);
        }
    }

    private Ramp BuildExpectedRampForCurrent()
    {
        var ramp = CreateEmptyRamp();
        double scale = Math.Clamp(_currentBrightnessPercent, 0.0, 100.0) / 100.0;
        if (_isRedOnlyActive)
        {
            for (int i = 0; i < 256; i++)
            {
                uint baseValue = (uint)((i * 65535) / 255);
                uint redValue = (uint)Math.Round(baseValue * scale);
                if (redValue > 65535) redValue = 65535;
                ramp.Red[i] = (ushort)redValue;
                ramp.Green[i] = 0;
                ramp.Blue[i] = 0;
            }
        }
        else
        {
            for (int i = 0; i < 256; i++)
            {
                uint baseValue = (uint)((i * 65535) / 255);
                uint value = (uint)Math.Round(baseValue * scale);
                if (value > 65535) value = 65535;
                ushort v = (ushort)value;
                ramp.Red[i] = v;
                ramp.Green[i] = v;
                ramp.Blue[i] = v;
            }
        }
        return ramp;
    }

    /// <summary>
    /// Verifies whether the current device gamma ramp approximately matches the expected ramp
    /// for the current mode and brightness. Returns a tuple indicating match state, number of
    /// differing entries, and a brief hint for logging.
    /// </summary>
    public (bool isMatch, int diffCount, string hint) VerifyAppliedRamp()
    {
        lock (_sync)
        {
            Initialize();
            if (_desktopHdc == IntPtr.Zero)
            {
                return (false, 0, "no_hdc");
            }
            var expected = BuildExpectedRampForCurrent();
            var current = CreateEmptyRamp();
            if (!GetDeviceGammaRamp(_desktopHdc, ref current))
            {
                return (false, 0, "get_gamma_failed");
            }

            int diffs = 0;
            const int tolerance = 1; // allow +/-1 for rounding
            for (int i = 0; i < 256; i++)
            {
                if (Math.Abs(current.Red[i] - expected.Red[i]) > tolerance) diffs++;
                if (Math.Abs(current.Green[i] - expected.Green[i]) > tolerance) diffs++;
                if (Math.Abs(current.Blue[i] - expected.Blue[i]) > tolerance) diffs++;
            }

            bool isIdentityLike = IsIdentityLike(current);
            string hint = isIdentityLike ? "identity_like" : (diffs == 0 ? "exact" : "mismatch");
            return (diffs == 0, diffs, hint);
        }
    }

    private static bool IsIdentityLike(Ramp ramp)
    {
        // Quick heuristic: channels equal and approximately linear at a few sample points
        int[] sampleIdx = new[] { 0, 1, 64, 128, 192, 254, 255 };
        foreach (var i in sampleIdx)
        {
            if (ramp.Red[i] != ramp.Green[i] || ramp.Red[i] != ramp.Blue[i]) return false;
            ushort expected = (ushort)((i * 65535) / 255);
            if (Math.Abs(ramp.Red[i] - expected) > 2) return false;
        }
        return true;
    }

    private static Ramp BuildLinearIdentityRamp()
    {
        var ramp = CreateEmptyRamp();
        for (int i = 0; i < 256; i++)
        {
            ushort value = (ushort)((i * 65535) / 255);
            ramp.Red[i] = value;
            ramp.Green[i] = value;
            ramp.Blue[i] = value;
        }
        return ramp;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            try
            {
                if (_isRedOnlyActive)
                {
                    RestoreOriginal();
                }
            }
            finally
            {
                if (_desktopHdc != IntPtr.Zero)
                {
                    _ = ReleaseDC(IntPtr.Zero, _desktopHdc);
                    _desktopHdc = IntPtr.Zero;
                }
            }
        }
        GC.SuppressFinalize(this);
    }
}


