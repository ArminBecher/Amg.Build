partial class BuildTargets : Amg.Build.Targets
{
    string name => "Amg.Build";
    string company => "Amg";
    // string nugetPushSource => @"C:\src\local-nuget-repository";
	string nugetPushSource => @"https://pkgs.dev.azure.com/CommonHostPlatform/_packaging/chp/nuget/v3/index.json";
    string nugetPushSymbolSource => nugetPushSource;
}
