using System;
using System.IO;
using System.Text.Json;

namespace Redbright.App;

public sealed class AppSettings
{
	public bool StartMinimizedToTray { get; set; } = false;
	public double BrightnessPercent { get; set; } = 100.0;
	public bool RedOnlyActive { get; set; } = false;
	public bool PauseBrightness { get; set; } = false;
	public double SavedBrightnessBeforePause { get; set; } = 100.0;
	public bool AutoStart { get; set; } = false;
	public int HotkeyModifiers { get; set; } = 0; // MOD_* flags
	public int HotkeyVirtualKey { get; set; } = 0; // VK_*
	public int HotkeyBrightnessModifiers { get; set; } = 0;
	public int HotkeyBrightnessVirtualKey { get; set; } = 0;
	public int HotkeyColorModifiers { get; set; } = 0;
	public int HotkeyColorVirtualKey { get; set; } = 0;
}

public static class SettingsStorage
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true
	};

	private static string GetSettingsPath()
	{
		var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var dir = Path.Combine(root, "Redbright");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, "settings.json");
	}

	public static AppSettings Load()
	{
		try
		{
			var path = GetSettingsPath();
			if (File.Exists(path))
			{
				var json = File.ReadAllText(path);
				var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
				if (loaded != null)
				{
					loaded.BrightnessPercent = Math.Clamp(loaded.BrightnessPercent, 0.0, 100.0);
					return loaded;
				}
			}
		}
		catch
		{
			// swallow and fall back to defaults
		}
		return new AppSettings();
	}

	public static void Save(AppSettings settings)
	{
		try
		{
			var path = GetSettingsPath();
			settings.BrightnessPercent = Math.Clamp(settings.BrightnessPercent, 0.0, 100.0);
			var json = JsonSerializer.Serialize(settings, JsonOptions);
			File.WriteAllText(path, json);
		}
		catch
		{
			// ignore persistence errors
		}
	}
}


