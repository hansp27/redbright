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
	public bool LoggingEnabled { get; set; } = false;
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
						// If logging is enabled in saved settings, enable and log snapshot
						if (loaded.LoggingEnabled)
						{
							AppLogger.SetEnabled(true);
							AppLogger.LogResult("settings.load", true, $"path={path}");
							AppLogger.LogConfigSnapshot("Saved configuration (on disk)", loaded);
						}
					return loaded;
				}
			}
				// If file missing and logger already enabled, note retrieval failure
				if (AppLogger.IsEnabled)
				{
					AppLogger.LogResult("settings.load", false, "settings file not found");
				}
		}
			catch (Exception ex)
		{
				// swallow and fall back to defaults
				if (AppLogger.IsEnabled)
				{
					AppLogger.LogResult("settings.load", false, ex.Message);
				}
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
				if (settings.LoggingEnabled)
				{
					AppLogger.SetEnabled(true);
					AppLogger.LogResult("settings.save", true, $"path={path}");
					AppLogger.LogConfigSnapshot("Saved configuration (on disk) after save", settings);
				}
		}
			catch (Exception ex)
		{
				// ignore persistence errors
				if (AppLogger.IsEnabled)
				{
					AppLogger.LogResult("settings.save", false, ex.Message);
				}
		}
	}
}


