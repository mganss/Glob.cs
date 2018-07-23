using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text;

namespace Ganss.IO.Tests
{
    public class TestFileSystem : IFileSystem
    {
        public FileBase File => throw new NotImplementedException();

        public DirectoryBase Directory { get; set; }

        public IFileInfoFactory FileInfo { get; set; }

        public IFileStreamFactory FileStream => throw new NotImplementedException();

        public PathBase Path { get; set; }

        public IDirectoryInfoFactory DirectoryInfo { get; set; }

        public IDriveInfoFactory DriveInfo => throw new NotImplementedException();

        public IFileSystemWatcherFactory FileSystemWatcher => throw new NotImplementedException();
    }

    public class TestDirectoryInfoFactory : IDirectoryInfoFactory
    {
        public DirectoryInfoBase FromDirectoryName(string directoryName)
        {
            return FromDirectoryNameFunc(directoryName);
        }

        public Func<string, DirectoryInfoBase> FromDirectoryNameFunc { get; set; }
    }

    public class TestFileInfoFactory : IFileInfoFactory
    {
        public FileInfoBase FromFileName(string fileName)
        {
            return FromFileNameFunc(fileName);
        }

        public Func<string, FileInfoBase> FromFileNameFunc { get; set; }
    }

    public class TestPath : MockPath
    {
        public TestPath(IMockFileDataAccessor m) : base(m)
        { }

        public override string GetDirectoryName(string path)
        {
            return GetDirectoryNameFunc(path);
        }

        public Func<string, string> GetDirectoryNameFunc { get; set; }
    }

    public class TestDirectory: MockDirectory
    {
        public TestDirectory(IMockFileDataAccessor m, FileBase b, string cwd): base(m, b, cwd)
        { }

        public override string GetCurrentDirectory()
        {
            return GetCurrentDirectoryFunc();
        }

        public Func<string> GetCurrentDirectoryFunc { get; set; }
    }

    public class TestDirectoryInfo: MockDirectoryInfo
    {
        public TestDirectoryInfo(IMockFileDataAccessor m, string p): base(m, p)
        { }

        public override FileSystemInfoBase[] GetFileSystemInfos()
        {
            return GetFileSystemInfosFunc();
        }

        public Func<FileSystemInfoBase[]> GetFileSystemInfosFunc { get; set; }
    }
}
