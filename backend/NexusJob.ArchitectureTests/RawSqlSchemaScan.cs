using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace NexusJob.ArchitectureTests;

/// <summary>
/// AD-2 rule 4 (best-effort backstop for AD-5): a raw-SQL call
/// (<c>FromSql</c> / <c>FromSqlRaw</c> / <c>FromSqlInterpolated</c> /
/// <c>ExecuteSqlRaw</c> / <c>ExecuteSqlInterpolated</c> / <c>ExecuteSql</c> /
/// <c>SqlQueryRaw</c> / <c>SqlQuery</c>, each with or without an <c>Async</c>
/// suffix) whose literal SQL names a schema other than the caller module's own
/// schema fails the build. Comments are stripped before matching - respecting
/// string literals, so a <c>//</c> or <c>/*</c> inside SQL text is not mistaken
/// for a comment - so a commented-out call does not break the build. Today the
/// tree has no raw SQL, so the source scan passes vacuously; the fixture tests
/// prove the detector still catches the known-bad path.
/// </summary>
public sealed class RawSqlSchemaScan
{
    internal const string Rule4 =
        "AD-2 rule 4: raw SQL (FromSqlRaw / ExecuteSql*) must not name a schema outside the caller module's own schema.";

    /// <summary>Schema owned by each module implementation project (AD-7).</summary>
    internal static readonly IReadOnlyDictionary<string, string> ModuleSchemas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NexusJob.Modules.Identity"] = "identity",
            ["NexusJob.Modules.JobPostings"] = "job_postings",
            ["NexusJob.Modules.Applications"] = "applications",
        };

    /// <summary>Every schema name in the system, including the Host-owned <c>public</c> schema.</summary>
    private static readonly IReadOnlySet<string> KnownSchemas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "identity", "job_postings", "applications", "public",
        };

    private static readonly Regex RawSqlCall = new(
        @"\b(?<call>(?:FromSqlInterpolated|FromSqlRaw|FromSql|ExecuteSqlInterpolated|ExecuteSqlRaw|ExecuteSql|SqlQueryRaw|SqlQuery)(?:Async)?)\s*(?:<[^>]*>)?\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex StringLiteral = new(
        "\"(?<body>(?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.Compiled);

    private static readonly Regex SchemaQualifiedName = new(
        @"\b(?<schema>[A-Za-z_][A-Za-z0-9_]*)\.(?<object>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    [Fact]
    public void No_module_contains_a_cross_schema_raw_sql_call()
    {
        var violations = new List<string>();

        foreach (var (moduleName, ownSchema) in ModuleSchemas)
        {
            var moduleDirectory = Path.Combine(ModuleAssemblies.BackendDirectory, moduleName);
            if (!Directory.Exists(moduleDirectory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(moduleDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                if (ContainsCrossSchemaRawSql(source, ownSchema, out var offending))
                {
                    violations.Add($"{Path.GetFileName(file)}: {offending}");
                }
            }
        }

        Assert.True(violations.Count == 0, $"{Rule4} Offending call(s): {string.Join(" | ", violations)}");
    }

    [Fact]
    public void Detector_flags_a_cross_schema_raw_sql_call_fixture()
    {
        // Known-bad path: Identity module code reaching into the job_postings schema.
        const string ownSchema = "identity";
        const string source =
            "public sealed class Bad { public void Run(DbContext db) { " +
            "db.Database.ExecuteSqlRaw(\"UPDATE job_postings.job_posting SET title = 'x' WHERE id = 1\"); } }";

        Assert.True(
            ContainsCrossSchemaRawSql(source, ownSchema, out var offending),
            "The detector must flag a raw-SQL call that names another module's schema.");
        Assert.Contains("job_postings", offending, StringComparison.Ordinal);
    }

    [Fact]
    public void Detector_flags_the_async_raw_sql_forms()
    {
        const string ownSchema = "identity";

        Assert.True(ContainsCrossSchemaRawSql(
            "await db.Database.ExecuteSqlRawAsync(\"DELETE FROM job_postings.job_posting\");", ownSchema, out var a));
        Assert.Contains("job_postings", a, StringComparison.Ordinal);

        Assert.True(ContainsCrossSchemaRawSql(
            "var q = db.Database.SqlQueryRaw<int>(\"SELECT id FROM applications.application\");", ownSchema, out _));

        Assert.True(ContainsCrossSchemaRawSql(
            "var rows = db.Set<Foo>().FromSqlInterpolatedAsync($\"SELECT * FROM public.data_protection_keys\");",
            ownSchema, out _));

        // EF Core's interpolated Database.SqlQuery (no "Raw" suffix).
        Assert.True(ContainsCrossSchemaRawSql(
            "var ids = db.Database.SqlQuery<int>($\"SELECT id FROM job_postings.job_posting WHERE id = {id}\");",
            ownSchema, out var q));
        Assert.Contains("job_postings", q, StringComparison.Ordinal);
    }

    [Fact]
    public void Detector_still_flags_a_cross_schema_name_after_an_inline_sql_comment_marker()
    {
        const string ownSchema = "identity";

        // A "//" inside the SQL string (here, a URL) must not be treated as the
        // start of a C# line comment and swallow the rest of the literal.
        Assert.True(ContainsCrossSchemaRawSql(
            "db.Database.ExecuteSqlRaw(\"UPDATE identity.foo SET url = 'http://x' FROM job_postings.bar\");",
            ownSchema, out var a));
        Assert.Contains("job_postings", a, StringComparison.Ordinal);

        // Likewise a "/* ... */" inside the SQL string (an inline SQL comment).
        Assert.True(ContainsCrossSchemaRawSql(
            "db.Database.ExecuteSqlRaw(\"UPDATE identity.foo /* n/a */ SET x = 1 FROM job_postings.bar\");",
            ownSchema, out var b));
        Assert.Contains("job_postings", b, StringComparison.Ordinal);
    }

    [Fact]
    public void Detector_ignores_commented_out_raw_sql_calls()
    {
        const string ownSchema = "identity";

        Assert.False(ContainsCrossSchemaRawSql(
            "// db.Database.ExecuteSqlRaw(\"UPDATE job_postings.job_posting SET title = 'x'\");", ownSchema, out _));

        Assert.False(ContainsCrossSchemaRawSql(
            "/* legacy:\n db.Database.ExecuteSqlRawAsync(\"DELETE FROM job_postings.job_posting\");\n */",
            ownSchema, out _));
    }

    [Fact]
    public void Detector_ignores_same_schema_and_non_raw_sql_fixtures()
    {
        const string ownSchema = "identity";

        // Same-schema raw SQL is allowed.
        Assert.False(ContainsCrossSchemaRawSql(
            "db.Database.ExecuteSqlRaw(\"SELECT * FROM identity.company_account\");", ownSchema, out _));

        // A schema-qualified name that is not inside a raw-SQL call is not our concern.
        Assert.False(ContainsCrossSchemaRawSql(
            "var x = config.GetSection(\"job_postings.enabled\");", ownSchema, out _));

        // Raw SQL with only unqualified names is fine.
        Assert.False(ContainsCrossSchemaRawSql(
            "db.Database.ExecuteSqlRaw(\"SELECT 1\");", ownSchema, out _));
    }

    /// <summary>
    /// True when <paramref name="source"/> contains a raw-SQL call whose literal
    /// SQL text names a known schema other than <paramref name="ownSchema"/>.
    /// </summary>
    internal static bool ContainsCrossSchemaRawSql(string source, string ownSchema, out string offending)
    {
        offending = string.Empty;

        // Strip line and block comments so a commented-out call is not treated as
        // real code - but skip over string literals verbatim, so a "//" or "/*"
        // inside SQL text (e.g. a URL, or an inline SQL comment) is never mistaken
        // for the start of a C# comment and does not truncate the literal.
        source = StripCommentsPreservingStringLiterals(source);

        foreach (Match call in RawSqlCall.Matches(source))
        {
            // Take the argument list from the opening paren to a balanced close.
            var argsStart = call.Index + call.Length - 1; // index of '('
            var argsText = ExtractBalancedParenthesised(source, argsStart);
            if (argsText is null)
            {
                continue;
            }

            foreach (Match literal in StringLiteral.Matches(argsText))
            {
                var sql = literal.Groups["body"].Value;
                foreach (Match qualified in SchemaQualifiedName.Matches(sql))
                {
                    var schema = qualified.Groups["schema"].Value;
                    if (KnownSchemas.Contains(schema)
                        && !string.Equals(schema, ownSchema, StringComparison.OrdinalIgnoreCase))
                    {
                        offending = $"{call.Groups["call"].Value}(... \"{qualified.Value}\" ...)";
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <paramref name="source"/> with <c>//</c> line comments and
    /// <c>/* */</c> block comments blanked, while copying C# string literals
    /// (including <c>$"</c>, <c>@"</c> and <c>$@"</c> / <c>@$"</c> forms) through
    /// verbatim so comment markers inside SQL text are left intact.
    /// </summary>
    internal static string StripCommentsPreservingStringLiterals(string source)
    {
        var result = new StringBuilder(source.Length);

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];

            if (c == '"')
            {
                var verbatim = (i >= 1 && source[i - 1] == '@')
                    || (i >= 2 && source[i - 1] == '$' && source[i - 2] == '@')
                    || (i >= 2 && source[i - 1] == '@' && source[i - 2] == '$');

                result.Append(c);
                i++;
                while (i < source.Length)
                {
                    var d = source[i];
                    if (verbatim)
                    {
                        if (d == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"')
                            {
                                result.Append("\"\"");
                                i += 2;
                                continue;
                            }

                            result.Append('"');
                            break;
                        }

                        result.Append(d);
                    }
                    else
                    {
                        if (d == '\\' && i + 1 < source.Length)
                        {
                            result.Append(d).Append(source[i + 1]);
                            i += 2;
                            continue;
                        }

                        result.Append(d);
                        if (d == '"')
                        {
                            break;
                        }
                    }

                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                i += 2;
                while (i < source.Length && source[i] != '\n' && source[i] != '\r')
                {
                    i++;
                }

                i--; // let the loop's i++ land on the newline (or end)
                result.Append(' ');
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }

                i++; // skip past the closing '/'
                result.Append(' ');
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static string? ExtractBalancedParenthesised(string source, int openParenIndex)
    {
        if (openParenIndex < 0 || openParenIndex >= source.Length || source[openParenIndex] != '(')
        {
            return null;
        }

        var depth = 0;
        for (var i = openParenIndex; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openParenIndex, i - openParenIndex + 1);
                }
            }
        }

        // Unbalanced (call spans past our window); scan what we have.
        return source[openParenIndex..];
    }
}
