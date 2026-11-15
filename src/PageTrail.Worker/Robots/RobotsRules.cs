namespace PageTrail.Worker.Robots;

public sealed class RobotsRules
{
    public const string UserAgentToken = "PageTrailBot";
    private const string AnyAgent = "*";
    private const char EndAnchor = '$';
    private const char Wildcard = '*';

    private readonly IReadOnlyList<RobotsRule> _rules;

    private RobotsRules(IReadOnlyList<RobotsRule> rules) => _rules = rules;

    public static RobotsRules AllowAll { get; } = new([]);

    public static RobotsRules Parse(string content)
    {
        var groups = ParseGroups(content);
        var ours = groups.Where(g => g.Agents.Contains(UserAgentToken, StringComparer.OrdinalIgnoreCase)).ToList();
        var chosen = ours.Count > 0 ? ours : groups.Where(g => g.Agents.Contains(AnyAgent)).ToList();
        return new RobotsRules(chosen.SelectMany(g => g.Rules).ToList());
    }

    public bool Allows(string pathAndQuery) =>
        _rules
            .Where(rule => Matches(rule.Pattern, pathAndQuery))
            .OrderByDescending(rule => rule.Pattern.Length)
            .ThenByDescending(rule => rule.Allow)
            .Select(rule => rule.Allow)
            .DefaultIfEmpty(true)
            .First();

    private static List<RobotsGroup> ParseGroups(string content)
    {
        var groups = new List<RobotsGroup>();
        RobotsGroup? current = null;
        foreach (var line in content.Split('\n'))
        {
            if (ParseLine(line) is not { } parsed)
            {
                continue;
            }

            var (key, value) = parsed;
            if (key.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (current is null || current.Rules.Count > 0)
                {
                    current = new RobotsGroup([], []);
                    groups.Add(current);
                }

                current.Agents.Add(value);
            }
            else if (current is not null && value.Length > 0 && IsRuleKey(key, out var allow))
            {
                current.Rules.Add(new RobotsRule(value, allow));
            }
        }

        return groups;
    }

    private static (string Key, string Value)? ParseLine(string line)
    {
        var content = line.Split('#', 2)[0];
        var separator = content.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? null : (content[..separator].Trim(), content[(separator + 1)..].Trim());
    }

    private static bool IsRuleKey(string key, out bool allow)
    {
        allow = key.Equals("allow", StringComparison.OrdinalIgnoreCase);
        return allow || key.Equals("disallow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(string pattern, string path)
    {
        var glob = pattern.EndsWith(EndAnchor) ? pattern[..^1] : pattern + Wildcard;
        return GlobMatches(glob, path);
    }

    private static bool GlobMatches(string glob, string text)
    {
        int g = 0, t = 0, star = -1, resume = 0;
        while (t < text.Length)
        {
            if (g < glob.Length && glob[g] == Wildcard)
            {
                star = g++;
                resume = t;
            }
            else if (g < glob.Length && glob[g] == text[t])
            {
                g++;
                t++;
            }
            else if (star >= 0)
            {
                g = star + 1;
                t = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (g < glob.Length && glob[g] == Wildcard)
        {
            g++;
        }

        return g == glob.Length;
    }

    private sealed record RobotsRule(string Pattern, bool Allow);

    private sealed record RobotsGroup(List<string> Agents, List<RobotsRule> Rules);
}
