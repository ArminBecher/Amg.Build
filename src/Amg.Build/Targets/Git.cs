﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Amg.Build.Builtin
{
    /// <summary>
    /// Targets for git repositories
    /// </summary>
    public class Git : Targets
    {
        private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructor. RootDirectory is set to "." 
        /// </summary>
        public Git()
            : this(".")
        {

        }

        /// <summary />
        public Git(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            GitTool = new Tool("git.exe").WithArguments("-C", RootDirectory);
        }

        /// <summary>
        /// Git command line tool
        /// </summary>
        public ITool GitTool { get; private set; }

        /// <summary>
        /// Use GitVersion to extract the application version
        /// </summary>
        [Once]
        public virtual Task<GitVersion.VersionVariables> GetVersion()
        {
            return Task.Factory.StartNew(() =>
            {
                var gitVersionExecuteCore = new GitVersion.ExecuteCore(new GitVersion.Helpers.FileSystem());
                string templateString = @"GitVersion: {message}";
                GitVersion.Logger.SetLoggers(
                _ => Logger.Debug(templateString, _),
                _ => Logger.Debug(templateString, _),
                _ => Logger.Warning(templateString, _),
                _ => Logger.Error(templateString, _));
                if (!gitVersionExecuteCore.TryGetVersion(RootDirectory, out var versionVariables, true, null))
                {
                    throw new System.Exception("Cannot read version");
                }
                Logger.Information("GitVersion: {InformationalVersion}", versionVariables.InformationalVersion);
                return versionVariables;
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        ///  Fails if the git repository contains uncommited changes.
        /// </summary>
        /// Use this target to prevent the release of binaries built with local, uncommitted changes.
        [Once]
        public virtual async Task EnsureNoPendingChanges()
        {
            var r = await GitTool.Run("ls-files", "--modified", "--others", "--exclude-standard");
            if (!String.IsNullOrEmpty(r.Output))
            {
                throw new Exception($@"The build requires that all git has no uncommitted changes.
Commit following files:

{r.Output}
            ");
            }
        }

        /// <summary>
        /// Repository root directory
        /// </summary>
        public string RootDirectory { get; }

        /// <summary>
        /// Execute buildStep only if commit hash was changed since last execution
        /// </summary>
        /// <param name="buildStep">build step to execute</param>
        /// <param name="stateFile">path to the state file</param>
        /// <returns></returns>
        public async Task RebuildIfCommitHashChanged(Func<Task> buildStep, string stateFile)
        {
            var resultCommitHash = await stateFile.ReadAllTextAsync();
            var sourceHash = (await GitTool.Run("log", "-1", "--pretty=format:%H")).Output.Trim();

            if (!string.Equals(resultCommitHash, sourceHash))
            {
                await buildStep();
                await stateFile.WriteAllTextAsync(sourceHash);
            }
        }
    }
}