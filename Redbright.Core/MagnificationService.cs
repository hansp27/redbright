using System;
using System.Runtime.InteropServices;

namespace Redbright.Core;

public sealed class MagnificationService : IDisposable
{
	private bool _initialized;
	private bool _active;

	[StructLayout(LayoutKind.Sequential)]
	private struct MagColorEffect
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
		public float[] Transform;
	}

	[DllImport("Magnification.dll", ExactSpelling = true)]
	private static extern bool MagInitialize();

	[DllImport("Magnification.dll", ExactSpelling = true)]
	private static extern bool MagUninitialize();

	[DllImport("Magnification.dll", ExactSpelling = true)]
	private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect pEffect);

	private static MagColorEffect BuildIdentity()
	{
		return new MagColorEffect
		{
			Transform = new float[]
			{
				1, 0, 0, 0, 0,
				0, 1, 0, 0, 0,
				0, 0, 1, 0, 0,
				0, 0, 0, 1, 0,
				0, 0, 0, 0, 1
			}
		};
	}

	private static MagColorEffect BuildGrayscaleMatrix(float rWeight, float gWeight, float bWeight)
	{
		// R' = G' = B' = rWeight*R + gWeight*G + bWeight*B
		// A' = A
		return new MagColorEffect
		{
			Transform = new float[]
			{
				rWeight, gWeight, bWeight, 0, 0, // Red output
				rWeight, gWeight, bWeight, 0, 0, // Green output
				rWeight, gWeight, bWeight, 0, 0, // Blue output
				0,       0,       0,       1, 0, // Alpha preserved
				0,       0,       0,       0, 1  // Identity for translation
			}
		};
	}

	private static MagColorEffect BuildGrayscaleMatrix(float rWeight, float gWeight, float bWeight, float gain)
	{
		var r = rWeight * gain;
		var g = gWeight * gain;
		var b = bWeight * gain;
		return new MagColorEffect
		{
			Transform = new float[]
			{
				r, g, b, 0, 0,
				r, g, b, 0, 0,
				r, g, b, 0, 0,
				0, 0, 0, 1, 0,
				0, 0, 0, 0, 1
			}
		};
	}

	private void EnsureInitialized()
	{
		if (_initialized) return;
		_initialized = MagInitialize();
	}

	private static MagColorEffect BuildRedMixMatrix(float gWeight, float bWeight)
	{
		// R' = 1*R + gWeight*G + bWeight*B (system clamps to [0,1])
		// G' = 0, B' = 0, A' = A
		return new MagColorEffect
		{
			Transform = new float[]
			{
				1f,     gWeight, bWeight, 0, 0,
				0,      0,       0,       0, 0,
				0,      0,       0,       0, 0,
				0,      0,       0,       1, 0,
				0,      0,       0,       0, 1
			}
		};
	}

	public bool EnableGrayscale()
	{
		EnsureInitialized();
		if (!_initialized) return false;
		// Use Rec.709 luminance weights to preserve perceived brightness
		var effect = BuildGrayscaleMatrix(0.2126f, 0.7152f, 0.0722f);
		var ok = MagSetFullscreenColorEffect(ref effect);
		_active = ok && _initialized;
		return _active;
	}

	public bool EnableGrayscale(float gain)
	{
		EnsureInitialized();
		if (!_initialized) return false;
		if (gain <= 0) gain = 1.0f;
		// Only reset to identity when an effect is already active; avoid extra state changes on first apply
		if (_active)
		{
			var identity = BuildIdentity();
			_ = MagSetFullscreenColorEffect(ref identity);
		}
		var effect = BuildGrayscaleMatrix(0.2126f, 0.7152f, 0.0722f, gain);
		var ok = MagSetFullscreenColorEffect(ref effect);
		_active = ok && _initialized;
		return _active;
	}

	private static MagColorEffect BuildRedLuminanceMatrix(float rWeight, float gWeight, float bWeight)
	{
		// R' = rWeight*R + gWeight*G + bWeight*B
		// G' = 0, B' = 0, A' = A
		return new MagColorEffect
		{
			Transform = new float[]
			{
				rWeight, gWeight, bWeight, 0, 0, // Red output
				0,       0,       0,       0, 0, // Green output
				0,       0,       0,       0, 0, // Blue output
				0,       0,       0,       1, 0, // Alpha preserved
				0,       0,       0,       0, 1  // Identity for translation
			}
		};
	}

	public bool EnableRedLuminance()
	{
		EnsureInitialized();
		if (!_initialized) return false;
		var effect = BuildRedLuminanceMatrix(0.2126f, 0.7152f, 0.0722f);
		var ok = MagSetFullscreenColorEffect(ref effect);
		_active = ok && _initialized;
		return _active;
	}

	public bool EnableRedMix()
	{
		EnsureInitialized();
		if (!_initialized) return false;
		// Use Rec.709 weights for G/B contribution to red; keep red at full strength
		var effect = BuildRedMixMatrix(0.7152f, 0.0722f);
		var ok = MagSetFullscreenColorEffect(ref effect);
		_active = ok && _initialized;
		return _active;
	}

	private static MagColorEffect BuildRedMixRowMatrix(int rowIndex, int[] rgbColOrder, float gWeight, float bWeight)
	{
		// Build matrix with weights in selected output row (0=R,1=G,2=B), others zero; alpha preserved
		var m = new float[25];
		// Alpha row
		m[15] = 0; m[16] = 0; m[17] = 0; m[18] = 1; m[19] = 0;
		// Last row
		m[20] = 0; m[21] = 0; m[22] = 0; m[23] = 0; m[24] = 1;

		// Target row offset
		int off = Math.Clamp(rowIndex, 0, 2) * 5;
		int rCol = rgbColOrder.Length >= 3 ? rgbColOrder[0] : 0;
		int gCol = rgbColOrder.Length >= 3 ? rgbColOrder[1] : 1;
		int bCol = rgbColOrder.Length >= 3 ? rgbColOrder[2] : 2;
		m[off + rCol] = 1f;       // keep original red
		m[off + gCol] = gWeight;  // add green
		m[off + bCol] = bWeight;  // add blue
		// other color rows remain zeros

		return new MagColorEffect { Transform = m };
	}

	public bool EnableRedMixRow(int rowIndex, int[] rgbColOrder)
	{
		EnsureInitialized();
		if (!_initialized) return false;
		var effect = BuildRedMixRowMatrix(rowIndex, rgbColOrder, 0.7152f, 0.0722f);
		var ok = MagSetFullscreenColorEffect(ref effect);
		_active = ok && _initialized;
		return _active;
	}

	public void Disable()
	{
		if (!_initialized) return;
		var identity = BuildIdentity();
		_ = MagSetFullscreenColorEffect(ref identity);
		_active = false;
	}

	public bool IsActive => _active;

	public void Dispose()
	{
		try
		{
			if (_active)
			{
				Disable();
			}
		}
		finally
		{
			if (_initialized)
			{
				_initialized = false;
				try { _ = MagUninitialize(); } catch { /* ignore */ }
			}
		}
		GC.SuppressFinalize(this);
	}
}


