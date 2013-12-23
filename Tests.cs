using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 1591
namespace Glob
{
    [TestFixture]
    public class Tests
    {
        public string TestDir { get; set; }

        [SetUp]
        public void Setup()
        {
            var cwd = Directory.GetCurrentDirectory();
            var testDir = Directory.GetParent(cwd).Parent.GetDirectories("test").Single();
            TestDir = testDir.FullName;
        }

        IEnumerable<string> ExpandNames(string pattern, bool ignoreCase = true, bool dirOnly = false)
        {
            return new Glob(TestDir + pattern) { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }.ExpandNames();
        }

        void AssertEqual(IEnumerable<string> actual, params string[] expected)
        {
            var exp = expected.Select(f => TestDir + f).ToList();
            var act = actual.ToList();
            CollectionAssert.AreEqual(exp, act);
        }

        [Test]
        public void CanExpandSimpleCases()
        {
            AssertEqual(ExpandNames(@"\file1"), @"\file1");
            AssertEqual(ExpandNames(@"\dir3\file*"), @"\dir3\file1");
            AssertEqual(ExpandNames(@"\dir2\*\*1*"), @"\dir2\dir1\123", @"\dir2\dir2\file1");
            AssertEqual(ExpandNames(@"\**\file1"), @"\file1", @"\dir2\file1", @"\dir2\dir2\file1", @"\dir3\file1");
            AssertEqual(ExpandNames(@"\**\*xxx"));
            AssertEqual(ExpandNames(@"\dir2\file[13]"), @"\dir2\file1", @"\dir2\file3");
            AssertEqual(ExpandNames(@"\dir3\???"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir3\[^f]*"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir3\[g-z]*"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\[dir]"), @"\d");
            AssertEqual(ExpandNames(@"\**\d"), @"\d");
        }

        [Test]
        public void CanExpandGroups()
        {
            AssertEqual(ExpandNames(@"\dir{1,3}\???"), @"\dir1\abc", @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\{dir1,dir3}\???"), @"\dir1\abc", @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\dir2\{file*,dir1\1?3}"), @"\dir2\file1", @"\dir2\file2", @"\dir2\file3", @"\dir2\dir1\123");
            AssertEqual(ExpandNames(@"\{dir3,dir2\{dir1,dir2}}\file1"), @"\dir3\file1", @"\dir2\dir2\file1");
            AssertEqual(ExpandNames(@"\dir{3,2\dir{1,2}}\[fgh]*1"), @"\dir3\file1", @"\dir2\dir2\file1");
        }

        [Test]
        public void CanExpandStrangeCases()
        {
            AssertEqual(ExpandNames(@"\*******\[aaaaaaaaaaaa]?c"), @"\dir1\abc");
            AssertEqual(ExpandNames(@"\******\xyz"), @"\dir3\xyz");
            AssertEqual(ExpandNames(@"\**\**\**\**\4?[0-9]*"), @"\dir2\dir1\456");
            AssertEqual(ExpandNames(@"\**\*******xyz*******"), @"\dir2\dir2\xyz", @"\dir3\xyz");
        }

        [Test]
        public void CanExpandNonGlobParts()
        {
            AssertEqual(ExpandNames(@"\[dir"), @"\[dir");
            AssertEqual(ExpandNames(@"\{dir"), @"\{dir");
            AssertEqual(ExpandNames(@"\{dir}"));
            AssertEqual(ExpandNames(@"\>"));
        }

        [Test]
        public void CanMatchCase()
        {
            AssertEqual(ExpandNames(@"\Dir2\file[13]", ignoreCase: false));
            AssertEqual(ExpandNames(@"\dir2\file[13]"), @"\dir2\file1", @"\dir2\file3");
        }

        [Test]
        public void CanMatchDirOnly()
        {
            AssertEqual(ExpandNames(@"\**\dir*", ignoreCase: true, dirOnly: true), @"\dir1", @"\dir2", @"\dir3",
                @"\dir2\dir1", @"\dir2\dir2");
        }

        [Test]
        public void CanMatchDotDot()
        {
            AssertEqual(Glob.ExpandNames(@"..\..\test\dir3\file*"), @"\dir3\file1");
        }
    }
}
#pragma warning restore 1591
