using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;

namespace SerieViewer
{
	[ComVisible(true)]
	[COMServerAssociation(AssociationType.Directory)]
	public class SerieViewer : SharpContextMenu
	{
		private static readonly string File = "Saison:1" + Environment.NewLine + "Episode:1";
		private DirectoryInfo SelectedDirectory => new DirectoryInfo(SelectedItemPaths.First());

		private Serie CurrentSerie => Serie.FromDirectoryInfo(SelectedDirectory);

		private Episode CurrentEpisode
		{
			get
			{
				if (!CurrentSerie.File.Exists)
					using (var writer = CurrentSerie.File.CreateText())
						writer.Write(File);
				string[] fileInfo;
				using (var text = CurrentSerie.File.OpenText())
					fileInfo = text.ReadToEnd().Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);

				var saison = int.Parse(fileInfo.FirstOrDefault(line => line.ToLower().StartsWith("saison"))?.Split(':')[1] ?? "1");
				var episode = int.Parse(fileInfo.FirstOrDefault(line => line.ToLower().StartsWith("episode"))?.Split(':')[1] ?? "1");

				return CurrentSerie[saison][episode];
			}
		}

		private Episode NextEpisode => CurrentEpisode.NextEpisode();

		protected override bool CanShowMenu()
			=> SelectedItemPaths.Take(2).Count() == 1 &&
			   Directory.Exists(SelectedItemPaths.First()) &&
			   SelectedDirectory.GetFiles("*", SearchOption.AllDirectories)
				   .Select(file => Helper.GetRelativePath(file, SelectedDirectory))
				   .Any(relativePath => Helper.Regex.IsMatch(relativePath));

		protected override ContextMenuStrip CreateMenu()
		{
			var currentEpisodeMenu = new ToolStripMenuItem("Current Episode");
			currentEpisodeMenu.Click += (s, e) => CurrentEpisode.Launch();

			var nextEpisodeMenu = new ToolStripMenuItem("Next Episode");
			nextEpisodeMenu.Click += (s, e) => (CurrentSerie.File.Exists ? NextEpisode : CurrentEpisode).Launch();
			var menu = new ToolStripMenuItem("Watch", null, currentEpisodeMenu, nextEpisodeMenu);

			var context = new ContextMenuStrip()
			{
				Items = {menu}
			};

			return context;
		}
	}

	[ComVisible(true)]
	[COMServerAssociation(AssociationType.AllFiles)]
	public class SetCurrentEpisode : SharpContextMenu
	{
		private FileInfo FileInfo => new FileInfo(SelectedItemPaths.First());

		protected override bool CanShowMenu()
			=> Helper.Regex.IsMatch(Helper.GetRelativePath(FileInfo, FileInfo.Directory?.Parent));

		protected override ContextMenuStrip CreateMenu()
		{
			var setCurrentMenu = new ToolStripMenuItem("Set Current Episode");
			setCurrentMenu.Click += (s, e) =>
			{
				var serie = Serie.FromDirectoryInfo(FileInfo.Directory?.Parent);
				var episode = serie?.Saisons.SelectMany(saison => saison.Episodes)
					.FirstOrDefault(ep => ep.File.FullName == FileInfo.FullName);
				
				episode?.SetCurrentEpisode();
			};

			return new ContextMenuStrip()
			{
				Items =
				{
					new ToolStripMenuItem("Watch", null, setCurrentMenu)
				}
			};
		}
	}
}
