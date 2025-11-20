# PowerShell script to restore the default gamma ramp
# This creates and applies a linear identity gamma ramp (normal colors)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class GammaRestore {
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

Write-Host "Restoring default gamma ramp..."

# Get device context for the desktop
$hdc = [GammaRestore]::GetDC([IntPtr]::Zero)

if ($hdc -eq [IntPtr]::Zero) {
    Write-Error "Failed to get device context"
    exit 1
}

try {
    # Create a new RAMP structure
    $ramp = New-Object GammaRestore+RAMP
    
    # Build a linear identity ramp (default/normal gamma)
    # Each entry i has value (i * 65535) / 255
    # All three channels (R, G, B) are set to the same values
    $ramp.Red = 0..255 | ForEach-Object { [uint16](($_ * 65535) / 255) }
    $ramp.Green = 0..255 | ForEach-Object { [uint16](($_ * 65535) / 255) }
    $ramp.Blue = 0..255 | ForEach-Object { [uint16](($_ * 65535) / 255) }
    
    # Apply the gamma ramp
    $result = [GammaRestore]::SetDeviceGammaRamp($hdc, [ref]$ramp)
    
    if ($result) {
        Write-Host "Successfully restored default gamma ramp!" -ForegroundColor Green
    } else {
        Write-Error "Failed to set device gamma ramp"
        exit 1
    }
} finally {
    # Always release the device context
    [GammaRestore]::ReleaseDC([IntPtr]::Zero, $hdc) | Out-Null
}

