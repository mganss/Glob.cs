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
            AssertEqual(ExpandNames(@"\{d,e}ir1\???"), @"\dir1\abc");
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
        public void CanMatchRelativePaths()
        {
            AssertEqual(Glob.ExpandNames(@"..\..\test\dir3\file*"), @"\dir3\file1");
            AssertEqual(Glob.ExpandNames(@".\..\..\.\.\test\dir3\file*"), @"\dir3\file1");
            var cwd = Directory.GetCurrentDirectory();
            var dir = Directory.GetParent(cwd).Parent.FullName;
            dir = dir.Substring(2); // C:\xyz -> \xyz
            AssertEqual(Glob.ExpandNames(dir + @"\test\dir3\file*"), @"\dir3\file1");
        }

        [Test]
        public void CanCancel()
        {
            var glob = new Glob(TestDir + @"\dir1\*");
            glob.Cancel();
            var fs = glob.Expand().ToList();
            Assert.AreEqual(0, fs.Count);
        }

        [Test]
        public void CanLog()
        {
            var log = "";
            var glob = new Glob(TestDir + @"\>") { IgnoreCase = true, ErrorLog = s => log += s };
            var fs = glob.ExpandNames().ToList();
            Assert.IsNotNullOrEmpty(log);
        }

        [Test]
        public void CanUseStaticMethods()
        {
            var fs = Glob.Expand(TestDir + @"\dir1\abc").Select(f => f.FullName).ToList();
            AssertEqual(fs, @"\dir1\abc");
        }

        [Test]
        public void CanUseUncachedRegex()
        {
            var fs = new Glob(TestDir + @"\dir1\*") { CacheRegexes = false }.ExpandNames().ToList();
            AssertEqual(fs, @"\dir1\abc");
        }

        [Test]
        public void DetectsInvalidPaths()
        {
            ExpandNames(@"\>\xyz", ignoreCase: false).ToList();
            var n = new Glob(@"ü:\x") { IgnoreCase = false }.ExpandNames().ToList();
            CollectionAssert.IsEmpty(n);
        }

        [Test]
        public void DetectsDeniedCurrentWorkingDirectory()
        {
            Directory.SetCurrentDirectory(TestDir + @"\..");
            var cwd = Directory.GetCurrentDirectory();
            new System.Security.Permissions.FileIOPermission(System.Security.Permissions.FileIOPermissionAccess.PathDiscovery, TestDir).PermitOnly();
            var fs = new Glob("hallo") { IgnoreCase = false }.Expand().ToList();
            CollectionAssert.IsEmpty(fs);
        }

        [Test]
        public void CatchesFileSystemErrors()
        {
            var root = Path.GetPathRoot(TestDir);
            new System.Security.Permissions.FileIOPermission(System.Security.Permissions.FileIOPermissionAccess.PathDiscovery | System.Security.Permissions.FileIOPermissionAccess.Read, root).PermitOnly();
            var fs = new Glob(root + @"\*\*") { IgnoreCase = false }.Expand().ToList();
        }

        [Test]
        public void CanMatchRelativeChildren()
        {
            AssertEqual(ExpandNames(@"\dir1\.", ignoreCase: false, dirOnly: true), @"\dir1");
            AssertEqual(ExpandNames(@"\dir2\dir1\..", ignoreCase: false, dirOnly: true), @"\dir2");
        }

        [Test]
        public void ReturnsStringAndHash()
        {
            var glob = new Glob("abc");
            Assert.AreEqual("abc", glob.ToString());
            Assert.AreEqual("abc".GetHashCode(), glob.GetHashCode());
        }

        [Test]
        public void CanCompareInstances()
        {
            var glob = new Glob("abc");
            Assert.False(glob.Equals(4711));
            Assert.True(glob.Equals(new Glob("abc")));
        }
    }
}
#pragma warning restore 1591
