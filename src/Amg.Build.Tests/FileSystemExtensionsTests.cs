﻿using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Amg.Build
{
    [TestFixture]
    public class FileSystemExtensionsTests : TestBase
    {
        private static readonly Serilog.ILogger Logger = Serilog.Log.Logger.ForContext(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [Test]
        public async Task EnsureDirectoryIsEmpty()
        {
            var testDir = CreateEmptyTestDirectory();
            var git = new Tool("git.exe");
            await git.Run("init", testDir);
            Assert.That(testDir.EnumerateFileSystemEntries().Any());
            testDir.EnsureDirectoryIsEmpty();
            Assert.That(testDir.EnumerateFileSystemEntries().Any(), Is.Not.True);
        }

        [Test]
        public async Task MoveToRecyclingBin()
        {
            var testDir = CreateEmptyTestDirectory();
            var repoDir = testDir.Combine("repo");
            var git = new Tool("git.exe");
            await git.Run("init", repoDir);
            Assert.That(repoDir.IsDirectory());
            Assert.That(repoDir.EnumerateFileSystemEntries().Any());
            testDir.EnsureDirectoryIsEmpty();
            Assert.That(repoDir.Exists(), Is.Not.True);
        }

        [Test]
        public void ParentDirectory()
        {
            var testDir = CreateEmptyTestDirectory();
            var d = Path.Combine(testDir, "a", "b");
            var f = d.Combine("c");
            f.EnsureParentDirectoryExists();
            Assert.That(Directory.Exists(d));
            Assert.That(f.Parent(), Is.EqualTo(d));
        }

        [Test]
        public void LastModified()
        {
            Console.WriteLine(".".Glob().LastWriteTimeUtc());
        }

        static string GetThisSourceFile([CallerFilePath] string path = null) => path;

        [Test]
        public void OutOfDate()
        {
            var thisDll = Assembly.GetExecutingAssembly().Location;
            var sources = GetThisSourceFile().Parent()
                .Glob("**")
                .Exclude("obj")
                .Exclude("bin");

            Assert.That(thisDll.IsOutOfDate(sources), Is.Not.True);
        }

        bool IsValidFilename(string f)
        {
            try
            {
                var d = this.CreateEmptyTestDirectory();
                var p = d.Combine(f);
                p.WriteAllTextAsync("a").Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        [Test]
        public void Combine()
        {
            var combined = @"C:\temp\a\b\c";
            Assert.That(@"C:\temp".Combine("a", "b", "c"), Is.EqualTo(combined));
            Assert.That(@"C:\temp".Combine(@"a\b", "c"), Is.EqualTo(combined));
            Assert.That(@"C:\temp".Combine(@"a\b\c"), Is.EqualTo(combined));
            Assert.That(@"C:\temp".Combine(@"a/b/c"), Is.EqualTo(combined));
        }

        [Test]
        public void CombineChecksForValidFileNames()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var c = @"C:\temp".Combine("a", "b", new string('c', 1024));
            });
            Logger.Information("{exception}", e);
        }

        [Test]
        public void MakeValidFilename()
        {
            Assert.That("x".MakeValidFileName(), Is.EqualTo("x"));

            var invalid = new string(Path.GetInvalidFileNameChars());
            Assert.That(invalid.MakeValidFileName(), Is.EqualTo(new string('_', invalid.Length)));

            var tooLong = new string('a', 1024) + ".ext";
            Assert.That(tooLong.IsValidFileName(), Is.False);
            var shortened = tooLong.MakeValidFileName();
            Assert.That(shortened.IsValidFileName(), Is.True);
            Logger.Information("{shortened}", shortened);
            Assert.That(IsValidFilename(shortened));
            Assert.That(shortened.Extension(), Is.EqualTo(tooLong.Extension()));
        }

        [Test]
        public void ChangeRoot()
        {
            Assert.AreEqual(@"C:\newRoot\a\b\c\d", @"C:\oldRoot\a\b\c\d".ChangeRoot(@"C:\oldRoot", @"C:\newRoot"));
        }

        static string TestFileName(int i)
        {
            return i.ToString("X").Select(_ => new string(_, 1)).Join("\\") + ".txt";
        }

        static async Task SetupTree(string root)
        {
            var files = Enumerable.Range(0, 100).Select(_ => root.Combine(TestFileName(_))).ToList();
            foreach (var i in files)
            {
                await i.WriteAllTextAsync(new string('a', 100 * 1024));
            }
        }

        static async Task SetupFile(string root)
        {
            await root.WriteAllTextAsync("hello");
        }

        [Test, TestCase(true), TestCase(false)]
        public async Task CopyTree(bool useHardlinks)
        {
            var testDir = CreateEmptyTestDirectory();
            Logger.Information(testDir);
            var source = testDir.Combine("source");
            await SetupTree(source);

            var dest = testDir.Combine("dest");

            var time = Enumerable.Range(0, 3)
                .Select(_ => MeasureTime(() => source.CopyTree(dest, useHardlinks: useHardlinks)))
                .ToList();
            Assert.That(time.Skip(1).All(_ => _.TotalSeconds < time.First().TotalSeconds*0.5));

            Logger.Information("{0}", time.Select(_ => new { _.TotalSeconds }).ToTable());
        }

        [Test, TestCase(true), TestCase(false)]
        public async Task CopyTreeSingleFile(bool useHardlinks)
        {
            var testDir = CreateEmptyTestDirectory();
            Logger.Information(testDir);
            var source = testDir.Combine("source");

            var dest = testDir.Combine("dest");

            await SetupFile(source);

            var time = Enumerable.Range(0, 3)
                .Select(_ => MeasureTime(() => source.CopyTree(dest, useHardlinks: useHardlinks)))
                .ToList();

            Logger.Information("{0}", time.Select(_ => new { _.TotalSeconds }).ToTable());
            Assert.That(await source.IsContentEqual(dest));
        }

        [Test]
        public async Task Hardlinks()
        {
            var testDir = CreateEmptyTestDirectory();
            var source = await 
                testDir.Combine("original.txt")
                .WriteAllTextAsync("hello");

            var dest = source.CreateHardlink(testDir.Combine("copy.txt"));

            Assert.That(dest.IsFile());

            var info = dest.HardlinkInfo();
            Assert.That(info.HardLinks.Count(), Is.EqualTo(2));

            source.EnsureFileNotExists();
            info = dest.HardlinkInfo();
            Assert.That(info.HardLinks.SequenceEqual(new[] { dest }));
        }
    }
}