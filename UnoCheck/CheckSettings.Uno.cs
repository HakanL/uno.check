﻿#nullable enable

using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DotNetCheck
{
	partial class CheckSettings
	{
		[CommandOption("--target <TARGET_PLATFORM_ID>")]
		[Description(
@"Run checks for a specific target platform. Use the --target option multiple times to run checks for multiple platforms, or omit it to run checks for all supported platforms.
Targets: webassembly ios android macos linux uwp win32desktop"
			)]
		public string[]? TargetPlatforms { get; set; }
	}
}
