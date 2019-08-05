using System.Threading.Tasks;
using System;
using System.IO;
using Amg.Build;
using System.Diagnostics;
using System.ComponentModel;

partial class BuildTargets : Targets
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    string productName => name;
    string year => DateTime.UtcNow.ToString("yyyy");
    string copyright => $"Copyright (c) {company} {year}";
    string configuration => "Debug";

    string Root { get; set; } = ".".Absolute();
    string OutDir => Root.Combine("out", configuration);
    string PackagesDir => OutDir.Combine("packages");
    string SrcDir => Root.Combine("src");
    string CommonAssemblyInfoFile => OutDir.Combine("CommonAssemblyInfo.cs");
    string VersionPropsFile => OutDir.Combine("Version.props");

    string SlnFile => SrcDir.Combine($"{name}.sln");
    string LibDir => SrcDir.Combine(name);

    Dotnet Dotnet => DefineTargets(() => new Dotnet());
    Git Git => DefineTargets(() => new Git());

    [Description("Build")]
    public Target Build => DefineTarget(async () =>
    {
        await WriteAssemblyInformationFile();
        await WriteVersionPropsFile();
        await (await Dotnet.Tool()).Run("build", SlnFile);
    });

    Target<string> WriteAssemblyInformationFile => DefineTarget(async () =>
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
    });

    Target<string> WriteVersionPropsFile => DefineTarget(async () =>
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
    });

    [Description("run unit tests")]
	public Target Test => DefineTarget(async () =>
    {
        await Build();
        await (await Dotnet.Tool()).Run("test", SlnFile, "--no-build");
    });

    [Description("measure code coverage")]
    public Target CodeCoverage => DefineTarget(async () =>
    {
        await Build();
        await (await Dotnet.Tool()).Run("test", SlnFile, "--no-build",
            "--collect:Code Coverage"
            );
    });

    [Description("pack nuget package")]
    public Target<string> Pack => DefineTarget(async () =>
    {
        var version = (await Git.GetVersion()).NuGetVersionV2;
        await Build();
        await (await Dotnet.Tool()).Run("pack",
            LibDir,
            "--configuration", configuration,
            "--no-build",
            "--include-source",
            "--include-symbols",
            "--output", PackagesDir.EnsureDirectoryExists()
            );

        return PackagesDir.Combine($"{name}.{version}.nupkg");
    });

    [Description("push nuget package")]
    Target Push => DefineTarget(async () =>
    {
        await Git.EnsureNoPendingChanges();
        await Task.WhenAll(Test(), Pack());
        var nupkgFile = await Pack();
        var nuget = new Tool("nuget.exe");
        await nuget.Run(
            "push",
            nupkgFile
            );
    });

    Target OpenInVisualStudio => DefineTarget(async () =>
    {
        await Build();
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(SlnFile),
            UseShellExecute = true
        });
    });
	
	Target Default => DefineTarget(async () =>
	{
		await Test();
	});
}

