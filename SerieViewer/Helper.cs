using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SerieViewer
{
	internal static class Helper
	{
		public static DirectoryInfo AppData { get; } = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FaustVX", "SerieViewer"));
		public static FileInfo Params { get; } = new FileInfo(Path.Combine(AppData.FullName, "Params.sv"));
		public static Regex Regex { get; }

		public static string GetRelativePath(FileSystemInfo nested, DirectoryInfo parentDirectory)
			=> Uri.UnescapeDataString(new Uri(parentDirectory.FullName).MakeRelativeUri(new Uri(nested.FullName)).ToString());

		private static T Do<T>(T obj, Action<T> action)
		{
			action(obj);
			return obj;
		}

		private static string[] AllowedExtensions()
			=> (File.ReadAllLines(Params.FullName).FirstOrDefault(line => line.ToLower().StartsWith("extensions:")) ?? "Extensions:mkv|avi|mp4|wmv|mp3")
				.Split(':')[1].Split('|');

		static Helper()
		{
			if (!AppData.Exists)
				AppData.Create();
			if (!Params.Exists)
				File.WriteAllLines(Params.FullName, new[] { "Extensions:mkv|avi|mp4|wmv|mp3" });

			Regex = new Regex(@"^(.+)[\\\/]\1\s*S(\d+)(?:[\\\/]\1\s*S\2E(\d+)(.*)\.(" + string.Join("|", AllowedExtensions()) + "))?$");
		}
	}
}