Glob.cs
=======

Single-source-file path <a href="http://en.wikipedia.org/wiki/Glob_(programming)">globbing</a> for .NET.

Syntax
------

* `?` matches a single character
* `*` matches zero or more characters
* `**` matches zero or more recursive directories, e.g. `a\**\x` matches `a\x`, `a\b\x`, `a\b\c\x`, etc.
* `[...]` matches a set of characters, syntax is the same as [http://msdn.microsoft.com/en-us/library/20bw873z.aspx#PositiveGroup](character groups) in Regex.
* `{group1,group2,...}` matches any of the pattern groups. Groups can contain groups and patterns, e.g. `{a\b,{c,d}*}`.
