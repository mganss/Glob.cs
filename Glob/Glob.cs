using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Security;
using System.IO.Abstractions.TestingHelpers;

namespace Ganss.IO
{
    /// <summary>
    /// Provides options for glob matching using <see cref="Glob"/>.
    /// </summary>
    public class GlobOptions
    {
        /// <summary>
        /// Gets or sets a value indicating an action to be performed when an error occurs during pattern matching.
        /// </summary>
        public Action<string>? ErrorLog { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether exceptions that occur during matching should be rethrown. Default is false.
        /// </summary>
        public bool ThrowOnError { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether case should be ignored in file and directory names. Default is true.
        /// </summary>
        public bool IgnoreCase { get; set; } = true;
        /// <summary>
        /// Gets or sets a value indicating whether only directories should be matched. Default is false.
        /// </summary>
        public bool DirectoriesOnly { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Regex"/> objects should be cached. Default is true.
        /// </summary>
        public bool CacheRegexes { get; set; } = true;
        /// <summary>
        /// Gets or sets the maximum number of levels of directories up to which a <c>**</c> wildcard will be expanded.
        /// Negative values indicate no limit. Default is -1.
        /// </summary>
        public int MaxDepth { get; set; } = -1;
    }

    /// <summary>
    /// Finds files and directories by matching their path names against a pattern.
    /// </summary>
    public class Glob
    {
        /// <summary>
        /// Gets or sets a value indicating the pattern to match file and directory names against.
        /// The pattern can contain the following special characters:
        /// <list type="table">
        /// <item>
        /// <term>?</term>
        /// <description>Matches any single character in a file or directory name.</description>
        /// </item>
        /// <item>
        /// <term>*</term>
        /// <description>Matches zero or more characters in a file or directory name.</description>
        /// </item>
        /// <item>
        /// <term>**</term>
        /// <description>Matches zero or more recursve directories.</description>
        /// </item>
        /// <item>
        /// <term>[...]</term>
        /// <description>Matches a set of characters in a name. Syntax is equivalent to character groups in <see cref="System.Text.RegularExpressions.Regex"/>.</description>
        /// </item>
        /// <item>
        /// <term>{group1,group2,...}</term>
        /// <description>Matches any of the pattern groups. Groups can contain groups and patterns.</description>
        /// </item>
        /// </list>
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Gets or sets the options that define glob expansion behavior.
        /// </summary>
        public GlobOptions Options { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating that a running pattern match should be cancelled.
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// Cancels a running pattern match.
        /// </summary>
        public void Cancel()
        {
            Cancelled = true;
        }

        private void Log(string s, params object[] args)
        {
            Options.ErrorLog?.Invoke(string.Format(s, args));
        }

        readonly IFileSystem _fileSystem;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public Glob() : this(new GlobOptions(), new FileSystem())
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="pattern">The pattern to match.</param>
        public Glob(string pattern) : this(pattern, new GlobOptions(), new FileSystem())
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="pattern">The pattern to match.</param>
        /// <param name="options">The options that define glob expansion behavior.</param>
        public Glob(string pattern, GlobOptions options) : this(pattern, options, new FileSystem())
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="pattern">The pattern to match.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use.</param>
        public Glob(string pattern, IFileSystem fileSystem) : this(pattern, new GlobOptions(), fileSystem)
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use.</param>
        public Glob(IFileSystem fileSystem) : this(new GlobOptions(), fileSystem)
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="options">The options that define glob expansion behavior.</param>
        public Glob(GlobOptions options) : this(options, new FileSystem())
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="options">The options that define glob expansion behavior.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use.</param>
        public Glob(GlobOptions options, IFileSystem fileSystem)
        {
            Pattern = "";
            Options = options;
            this._fileSystem = fileSystem;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="pattern">The pattern to match.</param>
        /// <param name="options">The options that define glob expansion behavior.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use.</param>
        public Glob(string pattern, GlobOptions options, IFileSystem fileSystem)
        {
            Pattern = pattern;
            Options = options;
            this._fileSystem = fileSystem;
        }

        /// <summary>
        /// Determines whether the specified path matches the pattern.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        ///   <c>true</c> if the specified path matches; otherwise, <c>false</c>.
        /// </returns>
        public bool IsMatch(string path)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                [path] = new MockFileData("")
            });
            var isMatch = ExpandInternal(Pattern, false, mockFileSystem).Any();

            return isMatch;
        }

        /// <summary>
        /// Determines whether the specified path matches the pattern.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pattern">The pattern to be matched.</param>
        /// <param name="ignoreCase">true if case should be ignored; false, otherwise.</param>
        /// <param name="dirOnly">true if only directories shoud be matched; false, otherwise.</param>
        /// <returns>
        ///   <c>true</c> if the specified path matches; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsMatch(string pattern, string path, bool ignoreCase = true, bool dirOnly = false)
        {
            return new Glob(pattern, new GlobOptions { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }).IsMatch(path);
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <param name="pattern">The pattern to be matched.</param>
        /// <param name="ignoreCase">true if case should be ignored; false, otherwise.</param>
        /// <param name="dirOnly">true if only directories shoud be matched; false, otherwise.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use. The default (null) is the physical file system.</param>
        /// <returns>The matched path names</returns>
        public static IEnumerable<string> ExpandNames(string pattern, bool ignoreCase = true, bool dirOnly = false, IFileSystem? fileSystem = null)
        {
            return new Glob(new GlobOptions { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }, fileSystem ?? new FileSystem()) { Pattern = pattern }.ExpandNames();
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <returns>The matched path names</returns>
        public IEnumerable<string> ExpandNames() => ExpandInternal(Pattern, Options.DirectoriesOnly, _fileSystem).Select(f => f.FullName);

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <param name="pattern">The pattern to be matched.</param>
        /// <param name="ignoreCase">true if case should be ignored; false, otherwise.</param>
        /// <param name="dirOnly">true if only directories shoud be matched; false, otherwise.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> implementation to use. The default (null) is the physical file system.</param>
        /// <returns>The matched <see cref="IFileSystemInfo"/> objects</returns>
        public static IEnumerable<IFileSystemInfo> Expand(string pattern, bool ignoreCase = true, bool dirOnly = false, IFileSystem? fileSystem = null)
        {
            return new Glob(new GlobOptions { IgnoreCase = ignoreCase, DirectoriesOnly = dirOnly }, fileSystem ?? new FileSystem()) { Pattern = pattern }.Expand();
        }

        /// <summary>
        /// Performs a pattern match.
        /// </summary>
        /// <returns>The matched <see cref="FileSystemInfo"/> objects</returns>
        public IEnumerable<IFileSystemInfo> Expand() => ExpandInternal(Pattern, Options.DirectoriesOnly, _fileSystem);

        private IEnumerable<IFileSystemInfo> ExpandInternal(string path, bool dirOnly, IFileSystem fileSystem)
        {
            if (Cancelled) yield break;

            if (string.IsNullOrEmpty(path))
            {
                yield break;
            }

            // stop looking if there are no more glob characters in the path.
            // but only if ignoring case because FileSystemInfo.Exists always ignores case.
            if (Options.IgnoreCase && path.IndexOfAny(GlobCharacters) < 0)
            {
                IFileSystemInfo? fsi = null;
                bool exists = false;

                try
                {
                    fsi = (IFileSystemInfo)fileSystem.DirectoryInfo.New(path);
                    if (!fsi.Exists && !dirOnly) fsi = fileSystem.FileInfo.New(path);
                    exists = fsi.Exists;
                }
                catch (Exception ex)
                {
                    Log("Error getting FileSystemInfo for '{0}': {1}", path, ex);
                    if (Options.ThrowOnError) throw;
                }

                if (fsi != null && exists) yield return fsi;
                yield break;
            }

            string? parent;

            try
            {
                parent = fileSystem.Path.GetDirectoryName(path);
            }
            catch (Exception ex)
            {
                Log("Error getting directory name for '{0}': {1}", path, ex);
                if (Options.ThrowOnError) throw;
                yield break;
            }

            if (parent == null)
            {
                IDirectoryInfo? dir = null;

                try
                {
                    dir = fileSystem.DirectoryInfo.New(path);
                }
                catch (Exception ex)
                {
                    Log("Error getting DirectoryInfo for '{0}': {1}", path, ex);
                    if (Options.ThrowOnError) throw;
                }

                if (dir != null) yield return dir;
                yield break;
            }

            if (parent.Length == 0)
            {
                try
                {
                    parent = fileSystem.Directory.GetCurrentDirectory();
                }
                catch (Exception ex)
                {
                    Log("Error getting current working directory: {1}", ex);
                    if (Options.ThrowOnError) throw;
                }
            }

            var child = fileSystem.Path.GetFileName(path);

            // handle groups that contain folders
            // child will contain unmatched closing brace
            if (child.Count(c => c == '}') > child.Count(c => c == '{'))
            {
                foreach (var group in Ungroup(path))
                {
                    foreach (var item in ExpandInternal(group, dirOnly, fileSystem))
                    {
                        yield return item;
                    }
                }

                yield break;
            }

            if (child == "**")
            {
                foreach (IDirectoryInfo dir in ExpandInternal(parent, true, fileSystem).DistinctBy(d => d.FullName).Cast<IDirectoryInfo>())
                {
                    yield return dir;

                    foreach (var subDir in GetDirectories(dir, 1, Options.MaxDepth))
                    {
                        yield return subDir;
                    }
                }

                yield break;
            }

            var childRegexes = Ungroup(child).Select(s => CreateRegexOrString(s)).ToList();

            foreach (IDirectoryInfo parentDir in ExpandInternal(parent, true, fileSystem).DistinctBy(d => d.FullName).Cast<IDirectoryInfo>())
            {
                IEnumerable<IFileSystemInfo> fileSystemEntries;

                try
                {
                    fileSystemEntries = dirOnly ? parentDir.EnumerateDirectories() : parentDir.EnumerateFileSystemInfos();
                }
                catch (Exception ex)
                {
                    Log("Error finding file system entries in {0}: {1}.", parentDir, ex);
                    if (Options.ThrowOnError) throw;
                    continue;
                }

                foreach (var fileSystemEntry in fileSystemEntries.Where(e => childRegexes.Exists(r => r.IsMatch(e.Name))))
                {
                    yield return fileSystemEntry;
                }

                if (childRegexes.Exists(r => r.Pattern == @"^\.\.$")) yield return parentDir.Parent ?? parentDir;
                if (childRegexes.Exists(r => r.Pattern == @"^\.$")) yield return parentDir;
            }
        }

        class RegexOrString
        {
            public Regex? Regex { get; set; }
            public string Pattern { get; set; }
            public bool IgnoreCase { get; set; }

            static readonly TimeSpan matchTimeout = TimeSpan.FromMilliseconds(1000);

            public RegexOrString(string pattern, string rawString, bool ignoreCase, bool compileRegex)
            {
                IgnoreCase = ignoreCase;

                try
                {
                    Regex = new Regex(pattern, RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : 0)
                        | (compileRegex ? RegexOptions.Compiled : 0), matchTimeout);
                    Pattern = pattern;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException || ex is ArgumentOutOfRangeException)
                {
                    Pattern = rawString;
                }
            }

            public bool IsMatch(string input)
            {
                if (Regex != null) return Regex.IsMatch(input);
                return Pattern.Equals(input, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
        }

        private static readonly ConcurrentDictionary<string, RegexOrString> RegexOrStringCache =
            new(StringComparer.Ordinal);

        private RegexOrString CreateRegexOrString(string pattern)
        {
            if (!Options.CacheRegexes) return new RegexOrString(GlobToRegex(pattern), pattern, Options.IgnoreCase, compileRegex: false);

            var cacheKey = pattern + (Options.IgnoreCase ? "/i" : "/c");

            if (!RegexOrStringCache.TryGetValue(cacheKey, out RegexOrString regexOrString))
            {
                regexOrString = new RegexOrString(GlobToRegex(pattern), pattern, Options.IgnoreCase, compileRegex: true);
                RegexOrStringCache[cacheKey] = regexOrString;
            }

            return regexOrString;
        }

        private static readonly char[] GlobCharacters = "*?[]{}".ToCharArray();

        private static readonly HashSet<char> RegexSpecialChars = new(['[', '\\', '^', '$', '.', '|', '?', '*', '+', '(', ')']);

        private static string GlobToRegex(string glob)
        {
            var regex = new StringBuilder();
            var characterClass = false;

            regex.Append("^");

            foreach (var c in glob)
            {
                if (characterClass)
                {
                    if (c == ']') characterClass = false;
                    regex.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '*':
                        regex.Append(".*");
                        break;
                    case '?':
                        regex.Append(".");
                        break;
                    case '[':
                        characterClass = true;
                        regex.Append(c);
                        break;
                    default:
                        if (RegexSpecialChars.Contains(c)) regex.Append('\\');
                        regex.Append(c);
                        break;
                }
            }

            regex.Append("$");

            return regex.ToString();
        }

        private static IEnumerable<string> Ungroup(string path)
        {
            if (!path.Contains('{'))
            {
                yield return path;
                yield break;
            }

            var level = 0;
            var option = new StringBuilder();
            var prefix = "";
            var postfix = "";
            var options = new List<string>();

            for (int i = 0; i < path.Length; i++)
            {
                var c = path[i];

                switch (c)
                {
                    case '{':
                        level++;
                        if (level == 1)
                        {
                            prefix = option.ToString();
                            option.Clear();
                        }
                        else option.Append(c);
                        break;
                    case ',':
                        if (level == 1)
                        {
                            options.Add(option.ToString());
                            option.Clear();
                        }
                        else option.Append(c);
                        break;
                    case '}':
                        level--;
                        if (level == 0)
                        {
                            options.Add(option.ToString());
                            break;
                        }
                        else option.Append(c);
                        break;
                    default:
                        option.Append(c);
                        break;
                }

                if (level == 0 && c == '}' && (i + 1) < path.Length)
                {
                    postfix = path.Substring(i + 1);
                    break;
                }
            }

            if (level > 0) // invalid grouping
            {
                yield return path;
                yield break;
            }

            var postGroups = Ungroup(postfix);

            foreach (var opt in options.SelectMany(o => Ungroup(o)))
            {
                foreach (var postGroup in postGroups)
                {
                    var s = prefix + opt + postGroup;
                    yield return s;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString() => Pattern;

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode() => Pattern.GetHashCode();

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if (obj == null || GetType() != obj.GetType()) return false;

            var g = (Glob)obj;
            return Pattern == g.Pattern;
        }

        private static IEnumerable<IDirectoryInfo> GetDirectories(IDirectoryInfo root, int level, int maxDepth)
        {
            IEnumerable<IDirectoryInfo> subDirs;

            if (maxDepth >= 0 && level > maxDepth)
                yield break;

            try
            {
                subDirs = root.EnumerateDirectories();
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException || ex is SecurityException)
            {
                yield break;
            }

            foreach (IDirectoryInfo dirInfo in subDirs)
            {
                yield return dirInfo;

                foreach (var recursiveDir in GetDirectories(dirInfo, level + 1, maxDepth))
                {
                    yield return recursiveDir;
                }
            }
        }
    }

    static class Extensions
    {
        internal static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            var knownKeys = new HashSet<TKey>();
            foreach (TSource element in source.Where(e => knownKeys.Add(keySelector(e))))
            {
                yield return element;
            }
        }
    }
}
