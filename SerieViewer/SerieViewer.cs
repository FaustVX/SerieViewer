using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;

namespace SerieViewer
{
	[ComVisible(true)]
	[COMServerAssociation(AssociationType.Directory)]
	public class SerieViewer : SharpContextMenu
	{
		[DebuggerDisplay("{Name}, {Saisons.Count} saisons")]
		public class Serie
		{
			public Serie(string name, IEnumerable<Saison> saisons)
			{
				Name = name;
				Saisons = new List<Saison>();
				foreach (var saison in saisons)
					Saisons.Add(new Saison(saison.Numero, saison.Episodes, this));
			}

			public string Name { get; }
			public IList<Saison> Saisons { get; }
			public Saison this[int saison] => Saisons.First(s => s.Numero == saison);
		}

		[DebuggerDisplay("{Serie.Name} S{Numero}, {Episodes.Count} episodes")]
		public class Saison
		{
			public Saison(int numero, IEnumerable<Episode> episodes, Serie serie = default(Serie))
			{
				Numero = numero;
				Serie = serie;
				Episodes = new List<Episode>();
				foreach (var episode in episodes)
					Episodes.Add(new Episode(episode.Numero, episode.File, this));
			}

			public int Numero { get; }
			public Serie Serie { get; }
			public IList<Episode> Episodes { get; }
			public Episode this[int episode] => Episodes.First(e => e.Numero == episode);

			public static bool operator ==(Saison s1, Saison s2)
				=> s1?.Serie.Name == s2?.Serie.Name && s1?.Numero == s2?.Numero;

			public static bool operator !=(Saison s1, Saison s2)
				=> !(s1 == s2);

			public bool Equals(Saison other)
				=> Numero == other.Numero && Serie.Equals(other.Serie) && Equals(Episodes, other.Episodes);

			public override bool Equals(object obj)
				=> !ReferenceEquals(null, obj) && (obj is Saison && Equals((Saison)obj));

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = Numero;
					hashCode = (hashCode * 397) ^ Serie.GetHashCode();
					hashCode = (hashCode * 397) ^ (Episodes?.GetHashCode() ?? 0);
					return hashCode;
				}
			}
		}

		[DebuggerDisplay("{Saison.Serie.Name} S{Saison.Numero}E{Numero}")]
		public class Episode
		{
			public Episode(int numero, FileInfo file, Saison saison = default(Saison))
			{
				Numero = numero;
				File = file;
				Saison = saison;
				Serie = Saison?.Serie;
			}

			public int Numero { get; }
			public FileInfo File { get; }
			public Saison Saison { get; }
			public Serie Serie { get; }

			public void Launch()
			{
				Process.Start(File.FullName);
			}

			public Episode NextEpisode()
			{
				if (Saison.Episodes.Last() == this)
					return Serie.Saisons.Last() == Saison ? Serie[1][1] : Serie.Saisons.SkipWhile(s => s != Saison).Take(2).Last()[1];
				return Saison.Episodes.SkipWhile(ep => ep != this).Take(2).Last();
			}

			public static bool operator ==(Episode ep1, Episode ep2)
				=> ep1?.Saison == ep2?.Saison &&
				   ep1?.Numero == ep2?.Numero;

			public static bool operator !=(Episode ep1, Episode ep2)
				=> !(ep1 == ep2);

			public bool Equals(Episode other)
				=> Numero == other.Numero && Equals(File, other.File) && Saison.Equals(other.Saison) && Serie.Equals(other.Serie);

			public override bool Equals(object obj)
				=> !ReferenceEquals(null, obj) && (obj is Episode && Equals((Episode)obj));

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = Numero;
					hashCode = (hashCode * 397) ^ (File?.GetHashCode() ?? 0);
					hashCode = (hashCode * 397) ^ Saison.GetHashCode();
					hashCode = (hashCode * 397) ^ Serie.GetHashCode();
					return hashCode;
				}
			}
		}

		private static readonly string File = "Saison:1" + Environment.NewLine + "Episode:1";
		private FileInfo InfoFile => new FileInfo(Path.Combine(AppData.FullName, SelectedDirectory.Name + ".sv"));

		private static readonly Regex Regex = new Regex(@"^(.+)[\\\/]\1\s*S(\d+)[\\\/]\1\s*S\2E(\d+)(.*)\.(mkv|avi|mp4)$");
		private DirectoryInfo SelectedDirectory => new DirectoryInfo(SelectedItemPaths.First());
		private static DirectoryInfo AppData { get; } = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FaustVX", "SerieViewer"));

		private Serie CurrentSerie => new Serie(SelectedDirectory.Name,
				from saisonDir in SelectedDirectory.EnumerateDirectories()
				where saisonDir.Name.ToLower().StartsWith($"{SelectedDirectory.Name.ToLower()} s")
				let episodes =
					from episodeFile in saisonDir.EnumerateFiles()
					where Regex.IsMatch(GetRelativePath(episodeFile, SelectedDirectory))
					let index = episodeFile.Name.LastIndexOf('E')
					let stop = episodeFile.Name.IndexOf('.', index)
					let num = int.Parse(episodeFile.Name.Substring(index + 1, stop - index - 1))
					orderby num
					select new Episode(num, episodeFile)
				let index = saisonDir.Name.LastIndexOf('S')
				let num = int.Parse(saisonDir.Name.Substring(index + 1))
				orderby num
				select new Saison(num, episodes));

		private Episode CurrentEpisode
		{
			get
			{
				if (!InfoFile.Exists)
					using (var writer = InfoFile.CreateText())
						writer.Write(File);
				string[] fileInfo;
				using (var text = InfoFile.OpenText())
					fileInfo = text.ReadToEnd().Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);

				var saison = int.Parse(fileInfo.FirstOrDefault(line => line.ToLower().StartsWith("saison"))?.Split(':')[1] ?? "1");
				var episode = int.Parse(fileInfo.FirstOrDefault(line => line.ToLower().StartsWith("episode"))?.Split(':')[1] ?? "1");

				return CurrentSerie[saison][episode];
			}
		}

		private Episode NextEpisode => CurrentEpisode.NextEpisode();

		public SerieViewer()
		{
			if (!AppData.Exists)
				AppData.Create();
		}

		protected override bool CanShowMenu()
			=> SelectedItemPaths.Take(2).Count() == 1 &&
			   Directory.Exists(SelectedItemPaths.First()) &&
			   SelectedDirectory.GetFiles("*", SearchOption.AllDirectories)
				   .Select(file => GetRelativePath(file, SelectedDirectory))
				   .Any(relativePath => Regex.IsMatch(relativePath));

		private void LaunchEpisode(Episode episode)
		{
			episode.Launch();
			System.IO.File.WriteAllLines(InfoFile.FullName, new[] { $"Saison:{episode.Saison.Numero}", $"Episode:{episode.Numero}"});
		}

		protected override ContextMenuStrip CreateMenu()
		{
			var currentEpisodeMenu = new ToolStripMenuItem("Current Episode");
			currentEpisodeMenu.Click += (s, e) => LaunchEpisode(CurrentEpisode);

			var nextEpisodeMenu = new ToolStripMenuItem("Next Episode");
			nextEpisodeMenu.Click += (s, e) => LaunchEpisode(NextEpisode);

			var menu = new ContextMenuStrip()
			{
				Items =
				{
					new ToolStripMenuItem("Watch", null, currentEpisodeMenu, nextEpisodeMenu)
				}
			};

			menu.Enter += (s, e) =>
			{
				menu.Items[0].Text = $"Watch \"{SelectedDirectory.Name}\"";
				var item = menu.Items[0] as ToolStripMenuItem;
				item.DropDownItems[0].Text = $"Current Episode (S{CurrentEpisode.Saison.Numero:00}E{CurrentEpisode.Numero:00})";
				item.DropDownItems[1].Text = $"Next Episode (S{NextEpisode.Saison.Numero:00}E{NextEpisode.Numero:00})";
			};

			return menu;
		}

		private static string GetRelativePath(FileSystemInfo nested, DirectoryInfo parentDirectory)
			=> Uri.UnescapeDataString(new Uri(parentDirectory.FullName).MakeRelativeUri(new Uri(nested.FullName)).ToString());

		private static T Do<T>(T obj, Action<T> action)
		{
			action(obj);
			return obj;
		}
	}
}
