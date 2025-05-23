﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ganss.IO.Tests;

public class TestFileSystem : IFileSystem
{
    public IFile File => throw new NotImplementedException();

    public IDirectory Directory { get; set; }

    public IFileInfoFactory FileInfo { get; set; }

    public IFileStreamFactory FileStream => throw new NotImplementedException();

    public IPath Path { get; set; }

    public IDirectoryInfoFactory DirectoryInfo { get; set; }

    public IDriveInfoFactory DriveInfo => throw new NotImplementedException();

    public IFileSystemWatcherFactory FileSystemWatcher => throw new NotImplementedException();

    public IFileVersionInfoFactory FileVersionInfo => throw new NotImplementedException();
}

public class TestDirectoryInfoFactory : IDirectoryInfoFactory
{
    public IDirectoryInfo FromDirectoryName(string directoryName)
    {
        return FromDirectoryNameFunc(directoryName);
    }

    public IDirectoryInfo New(string path)
    {
        return FromDirectoryNameFunc(path);
    }

    [return: NotNullIfNotNull(nameof(directoryInfo))]
    public IDirectoryInfo Wrap(DirectoryInfo directoryInfo)
    {
        throw new NotImplementedException();
    }

    public Func<string, IDirectoryInfo> FromDirectoryNameFunc { get; set; }

    public IFileSystem FileSystem => throw new NotImplementedException();
}

public class TestFileInfoFactory : IFileInfoFactory
{
    public IFileInfo FromFileName(string fileName)
    {
        return FromFileNameFunc(fileName);
    }

    public IFileInfo New(string fileName)
    {
        return FromFileNameFunc(fileName);
    }

    [return: NotNullIfNotNull(nameof(fileInfo))]
    public IFileInfo Wrap(FileInfo fileInfo)
    {
        throw new NotImplementedException();
    }

    public Func<string, IFileInfo> FromFileNameFunc { get; set; }

    public IFileSystem FileSystem => throw new NotImplementedException();
}

public class TestPath(IMockFileDataAccessor m) : MockPath(m)
{
    public override string GetDirectoryName(string path)
    {
        return GetDirectoryNameFunc(path);
    }

    public Func<string, string> GetDirectoryNameFunc { get; set; }
}

public class TestDirectory(IMockFileDataAccessor m, FileBase b, string cwd) : MockDirectory(m, b, cwd)
{
    public override string GetCurrentDirectory()
    {
        return GetCurrentDirectoryFunc();
    }

    public Func<string> GetCurrentDirectoryFunc { get; set; }
}

public class TestDirectoryInfo(IMockFileDataAccessor m, string p) : MockDirectoryInfo(m, p)
{
    public override IFileSystemInfo[] GetFileSystemInfos()
    {
        return GetFileSystemInfosFunc();
    }

    public Func<IFileSystemInfo[]> GetFileSystemInfosFunc { get; set; }
}
