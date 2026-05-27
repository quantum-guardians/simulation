using System.Collections.Generic;
using System.Globalization;

public static class GraphInputParser
{
    public static bool TryParseUndirected(string input, out GraphData data, out string error, List<string> warnings = null)
    {
        data = new GraphData();
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "입력이 비어 있습니다.";
            return false;
        }

        var edgeByPair = new Dictionary<(int min, int max), GraphEdgeData>();
        foreach (var rawLine in input.Split(new[] { '\r', '\n' }, System.StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (!TryParseEdgeLine(line, out var from, out var to, out var weight, out error))
                return false;

            if (from == to)
            {
                warnings?.Add($"자기 자신과의 간선 무시: \"{line}\"");
                continue;
            }

            (int min, int max) key = from < to ? (from, to) : (to, from);
            if (edgeByPair.ContainsKey(key))
                warnings?.Add($"중복 간선 ({key.min}-{key.max}), 마지막 가중치로 덮어씁니다.");

            data.EnsureNode(from);
            data.EnsureNode(to);
            edgeByPair[key] = new GraphEdgeData(from, to, weight);
        }

        foreach (var e in edgeByPair.Values)
            data.Edges.Add(e);

        if (data.Nodes.Count == 0)
        {
            error = "유효한 간선이 없습니다.";
            return false;
        }

        return true;
    }

    public static bool TryParseDirected(string input, out GraphData data, out string error, List<string> warnings = null)
    {
        data = new GraphData { Directed = true };
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "입력이 비어 있습니다.";
            return false;
        }

        foreach (var rawLine in input.Split(new[] { '\r', '\n' }, System.StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (!TryParseEdgeLine(line, out var from, out var to, out var weight, out error))
                return false;

            if (from == to)
            {
                warnings?.Add($"자기 자신으로의 간선 무시: \"{line}\"");
                continue;
            }

            data.EnsureNode(from);
            data.EnsureNode(to);
            data.Edges.Add(new GraphEdgeData(from, to, weight));
        }

        if (data.Nodes.Count == 0)
        {
            error = "유효한 간선이 없습니다.";
            return false;
        }

        return true;
    }

    static bool TryParseEdgeLine(string line, out int from, out int to, out float weight, out string error)
    {
        from = default;
        to = default;
        weight = default;
        error = null;

        var parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            error = $"한 줄에 숫자 3개가 필요합니다: \"{line}\"";
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out from) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out to))
        {
            error = $"노드 ID는 정수여야 합니다: \"{line}\"";
            return false;
        }

        if (!float.TryParse(parts[2], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out weight))
        {
            error = $"가중치를 읽을 수 없습니다: \"{line}\"";
            return false;
        }

        return true;
    }
}
