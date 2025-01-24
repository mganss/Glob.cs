using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Ganss.IO.Tests;

public class Tests
{
    public MockFileSystem FileSystem { get; set; }

    static string TestDir => Path.GetFullPath(FixPath("/test"));

    public Tests()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [$@"{TestDir}/d"] = new MockFileData(""),
            [$@"{TestDir}/dir1/abc"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir1/123"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir1/456"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir2/file1"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir2/file2"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir2/file3"] = new MockFileData(""),
            [$@"{TestDir}/dir2/dir2/xyz"] = new MockFileData(""),
            [$@"{TestDir}/dir2/file1"] = new MockFileData(""),
            [$@"{TestDir}/dir2/file2"] = new MockFileData(""),
            [$@"{TestDir}/dir2/file3"] = new MockFileData(""),
            [$@"{TestDir}/dir3/file1"] = new MockFileData(""),
            [$@"{TestDir}/dir3/xyz"] = new MockFileData(""),
            [$@"{TestDir}/file1"] = new MockFileData(""),
            [$@"{TestDir}/[dir"] = new MockFileData(""),
            [$@"{TestDir}/[dir]"] = new MockFileData(""),
            [$@"{TestDir}/{{dir"] = new MockFileData(""),
            [$@"{TestDir}/{{dir1"] = new MockFileData(""),
            [$@"{TestDir}/{{dir1}}"] = new MockFileData(""),
        });

        fileSystem.Directory.SetCurrentDirectory(Path.GetFullPath(FixPath($@"{TestDir}/dir2/dir1")));

        FileSystem = fileSystem;
    }

    private static string FixPath(string v)
    {
        return Regex.Replace(v, @"[/\\]", Path.DirectorySeparatorChar.ToString());
    }

    IEnumerable<string> ExpandNames(string pattern, bool ignoreCase = true, bool dirOnly = false)
    {
        pattern = Path.GetFullPath(FixPath(TestDir + pattern));
        return new Glob(new GlobOptions { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }, FileSystem) { Pattern = pattern }.ExpandNames();
    }

    static void AssertEqual(IEnumerable<string> actual, params string[] expected)
    {
        var exp = expected.Select(f => Path.GetFullPath(FixPath(TestDir + f))).ToList();
        var act = actual.ToList();
        Assert.Equal(exp, act);
    }

    [Fact]
    public void CanExpandSimpleCases()
    {
        AssertEqual(ExpandNames(@"/file1"), @"/file1");
        AssertEqual(ExpandNames(@"/dir3/file*"), @"/dir3/file1");
        AssertEqual(ExpandNames(@"/dir2/*/*1*"), @"/dir2/dir1/123", @"/dir2/dir2/file1");
        AssertEqual(ExpandNames(@"/**/file1"), @"/file1", @"/dir2/file1", @"/dir2/dir2/file1", @"/dir3/file1");
        AssertEqual(ExpandNames(@"/**/*xxx"));
        AssertEqual(ExpandNames(@"/dir2/file[13]"), @"/dir2/file1", @"/dir2/file3");
        AssertEqual(ExpandNames(@"/dir3/???"), @"/dir3/xyz");
        AssertEqual(ExpandNames(@"/dir3/[^f]*"), @"/dir3/xyz");
        AssertEqual(ExpandNames(@"/dir3/[g-z]*"), @"/dir3/xyz");
        AssertEqual(ExpandNames(@"/[dir]"), @"/d");
        AssertEqual(ExpandNames(@"/**/d"), @"/d");
        AssertEqual(ExpandNames(""), "");
    }

    [Fact]
    public void CanExpandGroups()
    {
        AssertEqual(ExpandNames(@"\dir{1,3}\???"), @"\dir1\abc", @"\dir3\xyz");
        AssertEqual(ExpandNames(@"\{dir1,dir3}\???"), @"\dir1\abc", @"\dir3\xyz");
        AssertEqual(ExpandNames(@"\dir2\{file*,dir1\1?3}"), @"\dir2\file1", @"\dir2\file2", @"\dir2\file3", @"\dir2\dir1\123");
        AssertEqual(ExpandNames(@"\{dir3,dir2\{dir1,dir2}}\file1"), @"\dir3\file1", @"\dir2\dir2\file1");
        AssertEqual(ExpandNames(@"\dir{3,2\dir{1,2}}\[fgh]*1"), @"\dir3\file1", @"\dir2\dir2\file1");
        AssertEqual(ExpandNames(@"\{d,e}ir1\???"), @"\dir1\abc");
    }

    [Fact]
    public void CanExpandStrangeCases()
    {
        AssertEqual(ExpandNames(@"\*******\[aaaaaaaaaaaa]?c"), @"\dir1\abc");
        AssertEqual(ExpandNames(@"\******\xyz"), @"\dir3\xyz");
        AssertEqual(ExpandNames(@"\**\**\**\**\4?[0-9]*"), @"\dir2\dir1\456");
        AssertEqual(ExpandNames(@"\**\*******xyz*******"), @"\dir2\dir2\xyz", @"\dir3\xyz");
    }

    [Fact]
    public void CanExpandNonGlobParts()
    {
        AssertEqual(ExpandNames(@"\[dir"), @"\[dir");
        AssertEqual(ExpandNames(@"\{dir"), @"\{dir");
        AssertEqual(ExpandNames(@"\{dir}"));
        AssertEqual(ExpandNames(@"\>"));
    }

    [Fact]
    public void CanMatchCase()
    {
        AssertEqual(ExpandNames(@"\Dir2\file[13]", ignoreCase: false));
        AssertEqual(ExpandNames(@"\dir2\file[13]"), @"\dir2\file1", @"\dir2\file3");
    }

    [Fact]
    public void CanMatchDirOnly()
    {
        AssertEqual(ExpandNames(@"\**\dir*", ignoreCase: true, dirOnly: true), @"\dir1", @"\dir2", @"\dir3",
            @"\dir2\dir1", @"\dir2\dir2");
    }

    [Fact]
    public void CanMatchRelativePaths()
    {
        AssertEqual(Glob.ExpandNames(FixPath(@"..\..\dir3\file*"), ignoreCase: true, dirOnly: false, fileSystem: FileSystem), @"\dir3\file1");
        AssertEqual(Glob.ExpandNames(FixPath(@".\..\..\.\.\dir3\file*"), ignoreCase: true, dirOnly: false, fileSystem: FileSystem), @"\dir3\file1");
    }

    [Fact]
    public void CanCancel()
    {
        var glob = new Glob(FileSystem) { Pattern = TestDir + @"\dir1\*" };
        glob.Cancel();
        var fs = glob.Expand().ToList();
        Assert.Empty(fs);
    }

    [Fact]
    public void CanLog()
    {
        var log = "";
        var glob = new Glob(new GlobOptions { IgnoreCase = true, ErrorLog = s => log += s }, new TestFileSystem()) { Pattern = @"test" };
        var fs = glob.ExpandNames().ToList();
        Assert.False(string.IsNullOrEmpty(log));
    }

    [Fact]
    public void CanUseStaticMethods()
    {
        var fs = Glob.Expand(FixPath(TestDir + @"\dir1\abc"), ignoreCase: true, dirOnly: false, fileSystem: FileSystem).Select(f => f.FullName).ToList();
        AssertEqual(fs, @"\dir1\abc");
    }

    [Fact]
    public void CanUseUncachedRegex()
    {
        var fs = new Glob(new GlobOptions { CacheRegexes = false }, FileSystem) { Pattern = FixPath(TestDir + @"\dir1\*") }.ExpandNames().ToList();
        AssertEqual(fs, @"\dir1\abc");
    }

    [Fact]
    public void CanMatchRelativeChildren()
    {
        AssertEqual(ExpandNames(@"\dir1\.", ignoreCase: false, dirOnly: true), @"\dir1");
        AssertEqual(ExpandNames(@"\dir2\dir1\..", ignoreCase: false, dirOnly: true), @"\dir2");
    }

    [Fact]
    public void ReturnsStringAndHash()
    {
        var glob = new Glob(FileSystem) { Pattern = "abc" };
        Assert.Equal("abc", glob.ToString());
        Assert.Equal("abc".GetHashCode(), glob.GetHashCode());
    }

    [Fact]
    public void CanCompareInstances()
    {
        var glob = new Glob(FileSystem) { Pattern = "abc" };
        Assert.False(glob.Equals(4711));
        Assert.True(glob.Equals(new Glob() { Pattern = "abc" }));
    }

    [Fact]
    public void CanThrow()
    {
        var fs = new TestFileSystem
        {
            DirectoryInfo = new TestDirectoryInfoFactory{ FromDirectoryNameFunc = n => throw new ArgumentException("", "1") }
        };

        var g = new Glob(new GlobOptions { ThrowOnError = true }, fs) { Pattern = TestDir + @"\>" };
        Assert.Throws<ArgumentException>("1", () => g.ExpandNames().ToList());

        fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => throw new ArgumentException("", "2") };

        g = new Glob(fs) { Pattern = "*" };
        Assert.Empty(g.ExpandNames());
        g.Options.ThrowOnError = true;
        Assert.Throws<ArgumentException>("2", () => g.ExpandNames().ToList());

        fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => null };
        fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => throw new ArgumentException("", "3") };

        g.Options.ThrowOnError = false;
        Assert.Empty(g.ExpandNames());
        g.Options.ThrowOnError = true;
        Assert.Throws<ArgumentException>("3", () => g.ExpandNames().ToList());

        fs.Path = new TestPath(FileSystem) { GetDirectoryNameFunc = n => "" };
        fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => null };
        fs.Directory = new TestDirectory(FileSystem, null, "") { GetCurrentDirectoryFunc = () => throw new ArgumentException("", "4") };

        g.Options.ThrowOnError = false;
        Assert.Empty(g.ExpandNames());
        g.Options.ThrowOnError = true;
        Assert.Throws<ArgumentException>("4", () => g.ExpandNames().ToList());

        fs.Directory = new TestDirectory(FileSystem, null, "") { GetCurrentDirectoryFunc = () => "5" };
        var d = new TestDirectoryInfo(FileSystem, TestDir) { GetFileSystemInfosFunc = () => throw new ArgumentException("", "5") };
        fs.DirectoryInfo = new TestDirectoryInfoFactory { FromDirectoryNameFunc = n => d };

        g.Options.ThrowOnError = false;
        Assert.Empty(g.ExpandNames());
        g.Options.ThrowOnError = true;
        Assert.Throws<ArgumentException>("5", () => g.ExpandNames().ToList());
    }

    [Fact]
    public void HonorsMaxDepth()
    {
        var g = new Glob(FileSystem);
        g.Options.MaxDepth = 1;
        g.Pattern = Path.GetFullPath(FixPath(TestDir + @"/**/file1"));
        AssertEqual(g.ExpandNames(), @"/file1", @"/dir2/file1", @"/dir3/file1");
        g.Options.MaxDepth = 2;
        AssertEqual(g.ExpandNames(), @"/file1", @"/dir2/file1", @"/dir2/dir2/file1", @"/dir3/file1");
        g.Options.MaxDepth = 0;
        AssertEqual(g.ExpandNames(), @"/file1");
    }

    [Fact]
    public void ExistingDirOnly()
    {
        AssertEqual(ExpandNames(@"/dir1", dirOnly: false), @"/dir1");
    }

    [Fact]
    public void CanMatch()
    {
        var g = new Glob("**/dir1/dir2/file*");

        var match = g.IsMatch("c:/dir0/dir1/dir2/file");
        Assert.True(match);

        match = g.IsMatch("c:/dir1/dir2/xyz");
        Assert.False(match);

        match = g.IsMatch("/a/b/c/dir1/dir2/file.txt");
        Assert.True(match);

        match = g.IsMatch(@"c:\dir1\dir2\file.txt");
        Assert.True(match);

        match = g.IsMatch("dir0/dir1/dir2/file");
        Assert.True(match);

        match = g.IsMatch("dir0/dir1/dir2/xyz");
        Assert.False(match);

        g.Pattern = "/dir{1,2}/file_[abc].txt";

        match = g.IsMatch("/dir1/file_a.txt");
        Assert.True(match);

        match = g.IsMatch("/dir2/file_x.txt");
        Assert.False(match);

        g.Pattern = "first/*";

        match = g.IsMatch("first/second/third");
        Assert.False(match);

        match = g.IsMatch("first/second");
        Assert.True(match);

        g.Pattern = "first/**/third";

        match = g.IsMatch("first/second/third");
        Assert.True(match);

        match = g.IsMatch("first/second/x");
        Assert.False(match);
    }

    [Fact]
    public void CanSwitchCaseSensitivity()
    {
        var match = Glob.IsMatch("/d*r/file*", "/dir/File");
        Assert.True(match);

        match = Glob.IsMatch("/d*r/file*", "/dir/File", ignoreCase: false);
        Assert.False(match);
    }

    [Fact]
    public void CanUseConstructorOverloads()
    {
        var g = new Glob("f*", new GlobOptions { IgnoreCase = false });
        Assert.True(g.IsMatch("file"));
        Assert.False(g.IsMatch("File"));
        Assert.False(g.IsMatch("xyz"));

        g = new Glob(FixPath(TestDir + "/f*"), FileSystem);
        AssertEqual(g.ExpandNames(), @"/file1");

        g = new Glob(new GlobOptions { IgnoreCase = false })
        {
            Pattern = "f*"
        };
        Assert.True(g.IsMatch("file"));
        Assert.False(g.IsMatch("File"));
        Assert.False(g.IsMatch("xyz"));
    }
}
