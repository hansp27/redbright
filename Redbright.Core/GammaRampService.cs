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


