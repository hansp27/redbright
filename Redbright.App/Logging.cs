using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Redbright.App;

public static class AppLogger
{
	private static readonly object _sync = new();
	private static volatile bool _enabled = false;
	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		WriteIndented = true
	};

	public static void SetEnabled(bool enabled)
	{
		_enabled = enabled;
		if (_enabled)
		{
			SafeAppend($"[info] Logging enabled");
		}
		else
		{
			SafeAppend($"[info] Logging disabled");
		}
	}

	public static bool IsEnabled => _enabled;

	public static string GetLogsDirectory()
	{
		return AppPaths.GetLogsDirectory();
	}

	public static string GetCurrentAppLogPath()
	{
		var file = $"app-{DateTime.UtcNow:yyyyMMdd}.txt";
		return Path.Combine(GetLogsDirectory(), file);
	}

	public static void EnsureLogFile()
	{
		try
		{
			var path = GetCurrentAppLogPath();
			if (!File.Exists(path))
			{
				SafeAppend("[info] Created new log file");
			}
		}
		catch { /* ignore */ }
	}

	public static void Log(string message)
	{
		if (!_enabled) return;
		SafeAppend(message);
	}

	public static void LogResult(string action, bool success, string? details = null)
	{
		if (!_enabled) return;
		var status = success ? "success" : "failure";
		if (!string.IsNullOrWhiteSpace(details))
		{
			SafeAppend($"[result] {action}: {status} - {details}");
		}
		else
		{
			SafeAppend($"[result] {action}: {status}");
		}
	}

	public static void LogChange(string name, object? oldValue, object? newValue)
	{
		if (!_enabled) return;
		SafeAppend($"[change] {name}: '{oldValue}' -> '{newValue}'");
	}

	public static void LogConfigSnapshot(string heading, AppSettings settings)
	{
		if (!_enabled) return;
		try
		{
			var json = JsonSerializer.Serialize(settings, _jsonOptions);
			SafeAppend($"[snapshot] {heading}\n{json}");
		}
		catch (Exception ex)
		{
			SafeAppend($"[warn] Failed to serialize settings snapshot: {ex.Message}");
		}
	}

	public static void LogSavedAndWorking(AppSettings saved, AppSettings working)
	{
		if (!_enabled) return;
		LogConfigSnapshot("Saved configuration (on disk)", saved);
		LogConfigSnapshot("Working configuration (in-memory)", working);
	}

	private static void SafeAppend(string message)
	{
		try
		{
			var timestamp = DateTime.UtcNow.ToString("o");
			var pid = Environment.ProcessId;
			var line = $"{timestamp} [pid:{pid}] {message}";
			var path = GetCurrentAppLogPath();
			lock (_sync)
			{
				File.AppendAllText(path, line + Environment.NewLine);
			}
		}
		catch
		{
			// swallow logging failures
		}
	}
}


