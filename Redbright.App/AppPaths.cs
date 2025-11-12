using System;
using System.IO;

namespace Redbright.App;

public static class AppPaths
{
	public static string GetProductFolderName()
	{
#if DEBUG
		return "Redbright-Dev";
#else
		return "Redbright";
#endif
	}

	public static string GetProductRootDirectory()
	{
		var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		var dir = Path.Combine(root, GetProductFolderName());
		Directory.CreateDirectory(dir);
		return dir;
	}

	public static string GetLogsDirectory()
	{
		var dir = Path.Combine(GetProductRootDirectory(), "logs");
		Directory.CreateDirectory(dir);
		return dir;
	}

	public static string GetSettingsPath()
	{
		return Path.Combine(GetProductRootDirectory(), "settings.json");
	}
}


