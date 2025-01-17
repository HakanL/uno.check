﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Claunia.PropertyList;
using DotNetCheck.Models;
using NuGet.Versioning;

namespace DotNetCheck.Checkups
{
	public class XCodeCheckup : Checkup
	{
		const string BugCommandLineToolsPath = "/Library/Developer/CommandLineTools";

		public override bool IsPlatformSupported(Platform platform)
			=> platform == Platform.OSX;

		public NuGetVersion MinimumVersion
			=> Extensions.ParseVersion(Manifest?.Check?.XCode?.MinimumVersion);

		public string MinimumVersionName
			=> Manifest?.Check?.XCode?.MinimumVersionName;

		public NuGetVersion ExactVersion
			=> Extensions.ParseVersion(Manifest?.Check?.XCode?.ExactVersion);

		public string ExactVersionName
			=> Manifest?.Check?.XCode?.ExactVersionName;

		public string VersionName
			=> ExactVersionName ?? MinimumVersionName ?? ExactVersion?.ToString() ?? MinimumVersion?.ToString();

		public override string Id => "xcode";

		public override string Title => $"XCode {VersionName}";

		public override bool ShouldExamine(SharedState history)
			=> Manifest?.Check?.XCode != null;

		public override Task<DiagnosticResult> Examine(SharedState history)
		{
			try
			{
				var selected = GetSelectedXCode();

				if (selected.Version.IsCompatible(MinimumVersion, ExactVersion))
				{
					// Selected version is good
					ReportStatus($"Xcode.app ({VersionName})", Status.Ok);
					return Task.FromResult(DiagnosticResult.Ok(this));
				}

				XCodeInfo eligibleXcode = null;

				var xcodes = FindXCodeInstalls();

				foreach (var x in xcodes)
				{
					if (x.Version.IsCompatible(MinimumVersion, ExactVersion))
					{
						eligibleXcode = x;
						break;
					}
				}

				if (eligibleXcode != null)
				{
					// If this is the case, they need to run xcode-select -s
					ReportStatus($"No Xcode.app or an incompatible Xcode.app version is selected, but one was found at ({eligibleXcode.Path})", Status.Error);

					return Task.FromResult(new DiagnosticResult(
						Status.Error,
						this,
						new Suggestion("Run xcode-select -s <Path>",
							new Solutions.ActionSolution((sln, cancelToken) =>
							{
								ShellProcessRunner.Run("xcode-select", "-s " + eligibleXcode.Path);
								return Task.CompletedTask;
							}))));
				}


				ReportStatus($"Xcode.app ({VersionName}) not installed.", Status.Error);

				return Task.FromResult(new DiagnosticResult(
					Status.Error,
					this,
					new Suggestion($"Download XCode {VersionName}")));
			}
			catch(InvalidDataException)
			{
				return Task.FromResult(new DiagnosticResult(
						Status.Error,
						this,
						new Suggestion("Run xcode-select --install",
							new Solutions.ActionSolution((sln, cancelToken) =>
							{	
								var result = ShellProcessRunner.Run("xcode-select", "--install");

								if(result.ExitCode == 0)
								{
									this.Examine(history);
								}

								return Task.CompletedTask;
							}))));
			}
		}

		XCodeInfo GetSelectedXCode()
		{
			var r = ShellProcessRunner.Run("xcode-select", "-p");

			var xcodeSelectedPath = r.GetOutput().Trim();

			if (!string.IsNullOrEmpty(xcodeSelectedPath))
			{
				if (xcodeSelectedPath.Equals(BugCommandLineToolsPath))
					throw new InvalidDataException();

				var infoPlist = Path.Combine(xcodeSelectedPath, "..", "Info.plist");
				if (File.Exists(infoPlist))
				{
					return GetXcodeInfo(
						Path.GetFullPath(
							Path.Combine(xcodeSelectedPath, "..", "..")), true);
				}
			}

			return null;
		}

		public static readonly string[] LikelyPaths = new []
		{
			"/Applications/Xcode.app",
			"/Applications/Xcode-beta.app",
		};

		IEnumerable<XCodeInfo> FindXCodeInstalls()
		{
			foreach (var p in LikelyPaths)
			{
				var i = GetXcodeInfo(p, false);
				if (i != null)
					yield return i;
			}
		}

		XCodeInfo GetXcodeInfo(string path, bool selected)
		{
			var versionPlist = Path.Combine(path, "Contents", "version.plist");

			if (File.Exists(versionPlist))
			{
				NSDictionary rootDict = (NSDictionary)PropertyListParser.Parse(versionPlist);
				string cfBundleVersion = rootDict.ObjectForKey("CFBundleVersion")?.ToString();
				string cfBundleShortVersion = rootDict.ObjectForKey("CFBundleShortVersionString")?.ToString();
				string productBuildVersion = rootDict.ObjectForKey("ProductBuildVersion")?.ToString();

				if (NuGetVersion.TryParse(cfBundleVersion, out var v))
					return new XCodeInfo(v, cfBundleShortVersion, productBuildVersion, path, selected);
			}
			else
			{
				var infoPlist = Path.Combine(path, "Contents", "Info.plist");

				if (File.Exists(infoPlist))
				{
					NSDictionary rootDict = (NSDictionary)PropertyListParser.Parse(infoPlist);
					string cfBundleVersion = rootDict.ObjectForKey("CFBundleVersion")?.ToString();
					string cfBundleShortVersion = rootDict.ObjectForKey("CFBundleShortVersionString")?.ToString();
					if (NuGetVersion.TryParse(cfBundleVersion, out var v))
						return new XCodeInfo(v, cfBundleShortVersion, string.Empty, path, selected);
				}
			}
			return null;
		}
	}

	public record XCodeInfo(NuGetVersion Version, string VersionString, string BuildVersion, string Path, bool Selected);
}
