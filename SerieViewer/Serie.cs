using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SerieViewer
{
	[DebuggerDisplay("{Name}, {Saisons.Count} saisons")]
	public class Serie
	{
		public Serie(string name, IEnumerable<Saison> saisons)
		{
			Name = name;
			Saisons = new List<Saison>();
			File = new FileInfo(Path.Combine(Helper.AppData.FullName, ("Serie - " + Name + ".sv")));
			foreach (var saison in saisons)
				Saisons.Add(new Saison(saison.Numero, saison.Episodes, this));
		}

		public string Name { get; }
		public IList<Saison> Saisons { get; }
		public Saison this[int saison] => Saisons.First(s => s.Numero == saison);
		public FileInfo File { get; }

		public static Serie FromDirectoryInfo(DirectoryInfo directory)
			=> new Serie(directory.Name,
				from saisonDir in directory.EnumerateDirectories()
				where saisonDir.Name.ToLower().StartsWith($"{directory.Name.ToLower()} s")
				let episodes =
					from episodeFile in saisonDir.EnumerateFiles()
					where Helper.Regex.IsMatch(Helper.GetRelativePath(episodeFile, directory))
					let regex = Helper.Regex.Match(Helper.GetRelativePath(episodeFile, directory))
					let num = int.Parse(regex.Groups[3].Value)
					orderby num
					select new Episode(num, episodeFile)
				where episodes.Any()
				let regex = Helper.Regex.Match(Helper.GetRelativePath(saisonDir, directory))
				let num = int.Parse(regex.Groups[2].Value)
				orderby num
				select new Saison(num, episodes));
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
			SetCurrentEpisode();
		}

		public Episode NextEpisode()
		{
			if (Saison.Episodes.Last() == this)
				return Serie.Saisons.Last() == Saison ? Serie[1][1] : Serie.Saisons.SkipWhile(s => s != Saison).Take(2).Last()[1];
			return Saison.Episodes.SkipWhile(ep => ep != this).Take(2).Last();
		}

		public void SetCurrentEpisode()
			=> System.IO.File.WriteAllLines(Serie.File.FullName, new[] { $"Saison:{Saison.Numero}", $"Episode:{Numero}" });

		public static bool operator ==(Episode ep1, Episode ep2)
			=> ep1?.Saison == ep2?.Saison &&
			   ep1?.Numero == ep2?.Numero;

		public static bool operator !=(Episode ep1, Episode ep2)
			=> !(ep1 == ep2);
	}
}