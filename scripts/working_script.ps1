Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class Gamma {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct RAMP {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Blue;
    }

    [DllImport("gdi32.dll", EntryPoint="SetDeviceGammaRamp")]
    public static extern bool SetDeviceGammaRamp(IntPtr hdc, ref RAMP ramp);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}
"@

$hdc = [Gamma]::GetDC([IntPtr]::Zero)
$ramp = New-Object Gamma+RAMP
$ramp.Red = 0..255 | ForEach-Object { [uint16](($_ * 65535) / 255) }
$ramp.Green = 0..255 | ForEach-Object { [uint16]0 }
$ramp.Blue = 0..255 | ForEach-Object { [uint16]0 }

[Gamma]::SetDeviceGammaRamp($hdc, [ref]$ramp) | Out-Null
[Gamma]::ReleaseDC([IntPtr]::Zero, $hdc) | Out-Null