using System.Threading.Tasks;
using System;
using System.IO;
using Amg.Build;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;

public partial class BuildTargets
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    string productName => name;
    string year => DateTime.UtcNow.ToString("yyyy");
    string copyright => $"Copyright (c) {company} {year}";

    [Description("Release or Debug. Default: Release")]
    public string Configuration { get; set; } = ConfigurationRelease;

    const string ConfigurationRelease = "Release";
    const string ConfigurationDebug = "Debug";

    string Root { get; set; } = ".".Absolute();
    string OutDir => Root.Combine("out", Configuration.ToString());
    string PackagesDir => OutDir.Combine("packages");
    string SrcDir => Root.Combine("src");
    string CommonAssemblyInfoFile => OutDir.Combine("CommonAssemblyInfo.cs");
    string VersionPropsFile => OutDir.Combine("Version.props");

    string SlnFile => SrcDir.Combine($"{name}.sln");
    string LibDir => SrcDir.Combine(name);

    [Once]
    protected virtual Dotnet Dotnet => Runner.Once<Dotnet>();

    [Once]
    protected virtual Git Git => Runner.Once<Git>(_ => _.RootDirectory = Root);

    [Once]
    protected virtual async Task PrepareBuild()
    {
        await WriteAssemblyInformationFile();
        await WriteVersionPropsFile();
    }

    [Once]
    [Description("Build")]
    public virtual async Task Build()
    {
        await WriteAssemblyInformationFile();
        await WriteVersionPropsFile();
        await (await Dotnet.Tool()).Run("build", SlnFile, 
            "--configuration", this.Configuration);
    }

    [Once]
    protected virtual async Task<string> WriteAssemblyInformationFile()
    {
        var v = await Git.GetVersion();
        return await CommonAssemblyInfoFile.WriteAllTextIfChangedAsync(
$@"// Generated. Changes will be lost.
[assembly: System.Reflection.AssemblyCopyright({copyright.Quote()})]
[assembly: System.Reflection.AssemblyCompany({company.Quote()})]
[assembly: System.Reflection.AssemblyProduct({productName.Quote()})]
[assembly: System.Reflection.AssemblyVersion({v.AssemblySemVer.Quote()})]
[assembly: System.Reflection.AssemblyFileVersion({v.AssemblySemFileVer.Quote()})]
[assembly: System.Reflection.AssemblyInformationalVersion({v.InformationalVersion.Quote()})]
");
    }

    [Once]
    protected virtual async Task<string> WriteVersionPropsFile()
    {
        var v = await Git.GetVersion();
        return await VersionPropsFile.WriteAllTextIfChangedAsync(
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <VersionPrefix>{v.MajorMinorPatch}</VersionPrefix>
        <VersionSuffix>{v.NuGetPreReleaseTagV2}</VersionSuffix>
    </PropertyGroup>
</Project>

");
    }

    [Once] [Description("run unit tests")]
	public virtual async Task Test()
    {
        await Build();
        await (await Dotnet.Tool()).Run("test", 
            SlnFile, 
            "--no-build",
            "--configuration", Configuration
            );
    }

    [Once] [Description("measure code coverage")]
    public virtual async Task CodeCoverage()
    {
        await Build();
        await (await Dotnet.Tool()).Run("test", SlnFile, "--no-build",
            "--collect:Code Coverage"
            );
    }

    [Once] [Description("pack nuget package")]
    public virtual async Task<string> Pack()
    {
        var version = (await Git.GetVersion()).NuGetVersionV2;
        await Build();
        await (await Dotnet.Tool()).Run("pack",
            LibDir,
            "--configuration", Configuration,
            "--no-build",
            "--include-source",
            "--include-symbols",
            "--output", PackagesDir.EnsureDirectoryExists()
            );

        return PackagesDir.Combine($"{name}.{version}.nupkg");
    }

    [Once][Description("Complete test with .cmd bootstrapper file")]
    public virtual async Task EndToEndTest()
    {
        await Git.EnsureNoPendingChanges();
        await Pack();

        var testDir = OutDir.Combine("EndToEndTest");
        await testDir.EnsureNotExists();
        testDir.EnsureDirectoryExists();

        var nugetConfigFile = testDir.Combine("nuget.config");
        await CreateNugetConfigFile(nugetConfigFile, PackagesDir);
        var nuget = new Tool("nuget.exe").WithWorkingDirectory(testDir);
        await nuget.Run("source");

        var script = testDir.Combine("build.cmd");
        await Root.Combine("examples", "skeleton").CopyTree(testDir);
        foreach (var d in new[] { "obj", "bin"})
        {
            await testDir.Combine("build", d).EnsureNotExists();
        }

        var version = await Git.GetVersion();

        var build = new Tool(script).DoNotCheckExitCode()
            .WithEnvironment(new Dictionary<string, string> { { "AmgBuildVersion", version.NuGetVersion } })
            ;

        {
            var result = await build.Run();
            if (!result.ExitCode.Equals(0))
            {
                throw new Exception();
            }
            if (!result.Output.Contains(version.InformationalVersion))
            {
                throw new Exception();
            }
            if (!String.IsNullOrEmpty(result.Error))
            {
                throw new Exception(result.Error);
            }
        }

        {
            var result = await build.Run();
            if (!result.ExitCode.Equals(0))
            {
                throw new Exception();
            }
            if (!String.IsNullOrEmpty(result.Error))
            {
                throw new Exception(result.Error);
            }
        }

        {
            var result = await build.Run("--help");
            if (!result.ExitCode.Equals(3))
            {
                throw new Exception();
            }
        }

        {
            var result = await build.Run("--clean");
            if (!result.ExitCode.Equals(0))
            {
                throw new Exception();
            }
            if (!result.Output.Contains("Build script requires rebuild."))
            {
                throw new Exception();
            }
        }

        {
            var outdated = DateTime.UtcNow.AddDays(-1);
            foreach (var f in testDir.Combine("build", "bin").Glob("**/*").EnumerateFileInfos())
            {
                f.LastWriteTimeUtc = outdated;
            }

            var result = await build.Run();

            if (!result.ExitCode.Equals(0))
            {
                throw new Exception();
            }
            if (!result.Output.Contains("Build script requires rebuild."))
            {
                throw new Exception();
            }
        }
    }

    private static async Task CreateEmptyNugetConfigFile(string nugetConfigFile)
    {
        await nugetConfigFile.WriteAllTextAsync(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");
    }

    private static async Task CreateNugetConfigFile(string nugetConfigFile, string packageSource)
    {
        await nugetConfigFile.WriteAllTextAsync($@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear /> 
    <add key=""EndToEndTestDefault"" value={packageSource.Quote()} />
  </packageSources>
</configuration>");
    }

    [Once] [Description("push nuget package")]
    protected virtual async Task Push()
    {
        await Push(nugetPushSource);
    }

    [Once]
    [Description("push nuget package")]
    protected virtual async Task Push(string nugetPushSource)
    {
        await Git.EnsureNoPendingChanges();
        await Task.WhenAll(Test(), Pack(), EndToEndTest());
        var nupkgFile = await Pack();
        var nuget = new Tool("nuget.exe");
        await nuget.Run(
            "push",
            nupkgFile,
            "-Source", nugetPushSource
            );
    }

    [Once] [Description("Open in Visual Studio")]
    public virtual async Task OpenInVisualStudio()
    {
        foreach (var configuration in new[] { ConfigurationRelease, ConfigurationDebug})
        {
            var b = Runner.Once<BuildTargets>();
            b.Configuration = configuration;
            await b.PrepareBuild();
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(SlnFile),
            UseShellExecute = true
        });
    }
	
    [Once][Description("Test")][Default]
    public virtual async Task Default()
	{
		await Test();
	}

    static string IncreasePatchVersion(string version)
    {
        var i = version.Split('.').Select(_ => Int32.Parse(_)).ToArray();
        ++i[i.Length - 1];
        return i.Select(_ => _.ToString()).Join(".");
    }

    [Once][Description("Build a release version and push to nuget.org")]
    public virtual async Task Release()
    {
        await Git.EnsureNoPendingChanges();
        var git = Runner.Once<Git>(_ => _.RootDirectory = this.Root);
        var v = await git.GetVersion();
        Logger.Information("Tagging with {version}", v.MajorMinorPatch);
        var gitTool = Git.GitTool;
        try
        {
            await gitTool.Run("tag", v.MajorMinorPatch);
        }
        catch (ToolException te)
        {
            if (te.Result.Error.Contains("already exists"))
            {
                await gitTool.Run("tag", IncreasePatchVersion(v.MajorMinorPatch));
            }
        }
        await gitTool.Run("push", "--tags");
        await Push("https://api.nuget.org/v3/index.json");
    }
}

