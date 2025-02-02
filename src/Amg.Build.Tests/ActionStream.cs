﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Amg.Build
{
    [TestFixture]
    internal class ActionStreamTests : TestBase
    {
        [Test]
        public void ActionIsCalledForEveryLine()
        {
            var lines = new List<String>();
            var s = new ActionStream(_ => lines.Add(_));
            s.WriteLine("Hello");
            s.Write("W");
            s.WriteLine("orld");
            s.Write("!");
            s.Flush();
            Assert.That(lines.SequenceEqual(new[]
            {
                "Hello",
                "World",
                "!"
            }));

        }
    }
}