﻿using System.Threading.Tasks;

namespace build
{
    class Program
    {
        static int Main(string[] args) => Amg.Build.Runner.Run<BuildTargets>(args);
    }
}
