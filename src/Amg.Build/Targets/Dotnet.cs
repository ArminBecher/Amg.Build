using System.Threading.Tasks;

namespace Amg.Build.Builtin
{
    /// <summary>
    /// Subtargets for the dotnet core framework
    /// </summary>
    public class Dotnet
    {
        /// <summary>
        /// Dotnet tool
        /// </summary>
        [Once]
        public virtual Task<Tool> Tool() => Task.FromResult(new Tool("dotnet"));

        /// <summary>
        /// dotnet version
        /// </summary>
        [Once]
        public virtual async Task<string> Version()
        {
            var d = await Tool();
            return (await d.Run("--version")).Output.Trim();
        }
    }
}