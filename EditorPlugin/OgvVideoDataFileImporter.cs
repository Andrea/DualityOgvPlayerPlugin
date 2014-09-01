using System;
using System.IO;
using Duality;
using Duality.Editor;

namespace OgvPlayer.Editor
{
	public class OgvVideoDataFileImporter : IFileImporter
	{
		public bool CanImportFile(string srcFile)
		{
			string ext = Path.GetExtension(srcFile).ToLower();
			return  ext.EndsWith(".ogv", StringComparison.CurrentCultureIgnoreCase);
		}
		public void ImportFile(string srcFile, string targetName, string targetDir)
		{
			string[] output = this.GetOutputFiles(srcFile, targetName, targetDir);
			OgvVideo res = new OgvVideo(srcFile);
			res.Save(output[0]);
		}
		public string[] GetOutputFiles(string srcFile, string targetName, string targetDir)
		{
			string targetResPath = PathHelper.GetFreePath(Path.Combine(targetDir, targetName), OgvVideo.FileExt);
			return new string[] { targetResPath };
		}


		public bool IsUsingSrcFile(ContentRef<Resource> r, string srcFile)
		{
			ContentRef<OgvVideo> a = r.As<OgvVideo>();
			return a != null && a.Res.SourcePath == srcFile;
		}
		public void ReimportFile(ContentRef<Resource> r, string srcFile)
		{
			var video = r.Res as OgvVideo;
			if (video != null) 
				video.LoadOgvVorbisData(srcFile);
		}
	}
}