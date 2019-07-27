using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System;

namespace Amg.Build
{
    [TestFixture]
    public class TargetsTests : TestBase
    {
        [Test]
        public void Run()
        {
            var exitCode = Targets.Run<MyTargets>(new string[] { });
            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void Fail()
        {
            var exitCode = Targets.Run<MyTargets>(new string[] { "AlwaysFails", "-vq"});
            Assert.That(exitCode, Is.Not.EqualTo(0));
        }

        [Test]
        public void RunQuiet()
        {
            var exitCode = Targets.Run<MyTargets>(new string[] { "-vq" });
            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunTargetThatReturnsAResult()
        {
            var exitCode = Targets.Run<MyTargets>(new string[] { "SayHello" });
            Assert.That(exitCode, Is.EqualTo(0));
        }

        [Test]
        public void GetTargetThatReturnsAResult()
        {
            var t = new MyTargets();
            var target = t.GetTarget(t.GetType().GetProperty("SayHello"));
        }

        [Test]
        public void IsTarget()
        {
            var t = typeof(MyTargets);
            Assert.That(Targets.IsPublicTargetProperty(t.GetProperty("SayHello")));
        }

        [Test]
        public void ErrorMessageForMissingDefaultTarget()
        {
            var t = new MyTargetsNoDefault();
            var e = Assert.Throws<AggregateException>(() =>
            {
                t.RunTargets(new string[] { }).Wait();
            });
            Assert.That(e.InnerException.Message.Contains("Default"));
        }

        [Test]
        public async Task DefaultTarget()
        {
            var t = new MyTargets();
            await t.RunTargets(Enumerable.Empty<String>());
            var r = t.result;
            Assert.AreEqual("CompileLinkPack", r);
        }

        [Test]
        public async Task TargetsCanBeAbbreviated()
        {
            var t = new MyTargets();
            await t.RunTargets(new string[] { "Co" });
            Assert.AreEqual("Compile", t.result);
        }

        [Test]
        public async Task TargetsAreRunOnlyOncePerInputArgument()
        {
            var t = new MyTargets();
            const int aCount = 10;
            foreach (var i in Enumerable.Range(0,3))
            {
                foreach (var a in Enumerable.Range(0, aCount))
                {
                    await t.Div2(a);
                }
            }
            Assert.AreEqual(aCount, t.args.Count);
        }

        [Test]
        public void TargetsAreRunWithCommandLineArgs()
        {
            var exitCode = Targets.Run<MyTargets>(new[] { "Pack", "-vd", "--configuration", "MyConfiguration" });
        }

        [Test]
        public void Help()
        {
            var o = TestUtil.CaptureOutput(() =>
            {
                var exitCode = Targets.Run<MyTargets>(new[] { "--help" });
                Assert.That(exitCode, Is.EqualTo(1));
            });
            Assert.AreEqual(@"Usage: build <targets> [options]

Targets:
  Link     Link object files      
  SayHello Say hello              
  Pack     Pack nuget package     
  Default  Compile, link, and pack

Options:
  --configuration=<string>                Release or Debug                                                
  -h | --help                             Show help and exit                                              
  -v<verbosity> | --verbosity=<verbosity> Set the verbosity level. verbosity=quiet|minimal|normal|detailed
", o.Out);

            Assert.AreEqual(String.Empty, o.Error);
        }
    }
}
