using KsWare.VsFileEditor.Dom;
using NuGet.Versioning;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KsWare.VsFileEditor;
using System.Reflection;
using System.Text;

namespace UpdatePackageReferences;

internal static class Program {

	private static readonly HashSet<string> s_validExtensions = [".csproj", ".vbproj", ".fsproj"];
	private static CmdLineArguments Args = new CmdLineArguments();

	private static void Main(string[] args) {
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Console.OutputEncoding = args.Contains("-unicode",StringComparer.OrdinalIgnoreCase) ? Encoding.Unicode : Encoding.GetEncoding(1252);
		WriteHeader();
		for (var i = 0; i < args.Length; i++) {
			var normArg = args[i].ToLower().Replace('/','-').Replace("--","-").Replace("-?","-help");
			switch (normArg) {
				// ReSharper disable StringLiteralTypo
				case "-help" : ShowHelpAndExit(); break;
				case "-referenceswitcher" : Args.ReferenceSwitcher = true; break;
				case "-major": Args.VersionFilter=VersionFilter.Major; break;
				case "-minor": Args.VersionFilter=VersionFilter.Minor; break;
				case "-patch": Args.VersionFilter=VersionFilter.Patch; break;
				case "-prerelease": Args.PreRelease=true; break;
				case "-unicode": break;
				case "-nochange": case "-readonly": case "-ro": Args.IsReadOnly = true; break;
				// ReSharper restore StringLiteralTypo
				default:
					if (File.Exists(args[i])) Args.InputPaths.Add(args[i]);
					else if (Directory.Exists(args[i])) Args.InputPaths.Add(args[i]);
					else Exit($"Unknown parameter at {i}: {args[i]}");
					break;
			}
		}

		Run(Args);
	}

	private static void WriteHeader() {
		var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		var copyright = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
		var hdr=$$"""
			KsWare UpdatePackageReferences v{{version}}
			{{copyright}}
			{{new string('-',70)}}
			""";
		Echo(hdr);
	}

	public static void Run(CmdLineArguments args) {
		Args = args;

		foreach (var path in Args.InputPaths) {
			if (File.Exists(path)) {
				var files = Path.GetExtension(path) == ".sln" ? SlnUtils.GetProjectFiles(path) : [path];
				foreach (var file in files) {
					UpdatePackageReferences(file);
				}
			}
			else if (Directory.Exists(path)) {
				//TODO 
			}
		}
	}

	private static void ShowHelpAndExit() {
		const string txt = """
			Usage: UpdatePackageReferences [<switches>] <file(s)>
			
			  -help -?                   Shows this help
			  -ReferenceSwitcher         special mode for ReferenceSwitcher
			  -Major                     update up to the highest major (default)
			  -Minor                     update up to the highest minor (major remains the same)
			  -Patch                     update up to the highest patch (major/minor remains the same)
			  -PreRelease                allow pre releases
			  -ro -readonly              does not make any changes
			  -unicode                   use Unicode ending
			  -color -noColor            turns colorization on/off (default: auto)
			  <file(s)>                  path(s) to sln/proj file(s)

			-ReferenceSwitcher:
			  If this is specified, only PackageReferences with matching ProjectReference will be updated.
			
			-color -noColor  
			  By default redirected console output (VS Console Window) does not use colors,
			  because ANSI sequences are not supported but System Console Windows does.
			  With -color -noColor you can force a mode. 
			""";
		Echo(txt);
		Environment.Exit(0);
	}

	private static void Exit(string msg) {
		Console.Error.WriteLine(msg);
		Environment.Exit(1);
	}
	
	private static void UpdatePackageReferences(string projPath) {
		Echo($"Processing '{Path.GetFileName(projPath)}'");
		var proj = ProjFile.Load(projPath);
		var packageReferences = proj.PackageReferences;

		var hasChanges = false;
		foreach (var packageReference in packageReferences) {
			var packageName = packageReference.Include; if (string.IsNullOrEmpty(packageName)) continue;
			var currentVersion = packageReference.Version; if (string.IsNullOrEmpty(currentVersion)) continue;
			var nv = $"{packageName} {currentVersion}";
			var limitedVersion = NuGetUtils.GetLatestPackageVersion(packageName, Limit(currentVersion, Args.VersionFilter), Args.PreRelease).ToSemVer();
			var latestVersion = NuGetUtils.GetLatestPackageVersion(packageName, null, Args.PreRelease).ToSemVer();
			var sLatest = formatLatest();
			if (Args.ReferenceSwitcher) {
				var hasProjectReference = proj.FindProjectReferenceForPackage(packageName)!=null;
				if (!hasProjectReference) {
					Echo($"  {Gray(nv,-50)} {Gray("skip")}{sLatest}");
					continue;
				}
			}
			if (limitedVersion == null) {
				Echo($"  {nv,-50} not found{sLatest}");
				continue;
			}
			//Echo($"Latest version of {packageName}: {latestVersion}");
			if (limitedVersion <= packageReference.Version.ToSemVer()!) {
				Echo($"  {nv,-50} {Green("latest")}");
				continue;
			}
			packageReference.Version = limitedVersion.ToString();
			Echo($"  {nv,-50} >> {Blue(limitedVersion.ToString())}{sLatest}");
			hasChanges = true;

			continue;
			string formatLatest() {
				if (latestVersion == null) return "";
				if (limitedVersion == null) return $" [{latestVersion}]";
				if (latestVersion == limitedVersion) return "";
				return $" [{latestVersion}]";
			}
		}

		if (hasChanges) {
			if (Args.IsReadOnly) {
				Echo($"  {Yellow("Updates are available. No changes were made")}");
			}
			else {
				proj.Save();
				Echo($"  {Green("Packages have been updated")}");
			}
		}
		else {
			Echo($"  {Green("All packages have been checked and no updates where necessary.")}");
		}
	}

	private static string Green(string s) => $"\u001b[32m{s}\u001b[0m";
	private static string Blue(string s) => $"\u001b[94m{s}\u001b[0m";
	private static string Gray(string s, int padding=0) => Color(s, "90", padding);
	private static string Yellow(string s, int padding=0) => Color(s, "33", padding);
	private static string Red(string s, int padding=0) => Color(s, "31", padding);

	private static string Color(string s, string color, int padding = 0)
		=> $"\u001b[{color}m{(padding < 0 ? s.PadRight(-padding) : padding > 0 ? s.PadLeft(padding) : s)}\u001b[0m";
	
	private static string? Limit(string version, VersionFilter filter) {
		var semVer = NuGetVersion.Parse(version);
		return filter switch {
			VersionFilter.Major or VersionFilter.None => "",
			VersionFilter.Minor => $"{semVer.Major}",
			VersionFilter.Patch => $"{semVer.Major}.{semVer.Minor}",
			_ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
		};
	}
	
	private static void Echo(string text) {
		if (Console.IsOutputRedirected) {
			// [32m
			text = Regex.Replace(text, @"\x1B\[[0-9;]*[A-Za-z]", string.Empty);
		}
		Console.WriteLine(text);
	}
}

public class CmdLineArguments {	
	public List<string> InputPaths{get; set; } = [];
	public bool ReferenceSwitcher { get; set; }
	public VersionFilter VersionFilter { get; set; } = VersionFilter.Major;
	public bool PreRelease { get; set; }
	public bool IsReadOnly { get; set; }

}

public enum VersionFilter {
	None,Major,Minor,Patch
}