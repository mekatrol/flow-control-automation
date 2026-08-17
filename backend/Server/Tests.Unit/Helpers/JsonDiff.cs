using System.Text.Json.Nodes;

namespace Tests.Unit.Helpers;

public static class JsonDiff
{
    public static IEnumerable<string> FindDifferences(
        JsonNode? actual,
        JsonNode? expected,
        string path = "$")
    {
        if (JsonNode.DeepEquals(actual, expected))
        {
            yield break;
        }

        if (actual is null || expected is null)
        {
            yield return $"{path}: actual={actual?.ToJsonString() ?? "null"}, " +
                         $"expected={expected?.ToJsonString() ?? "null"}";
            yield break;
        }

        if (actual is JsonObject actualObject &&
            expected is JsonObject expectedObject)
        {
            var keys = actualObject.Select(x => x.Key)
                .Union(expectedObject.Select(x => x.Key));

            foreach (var key in keys)
            {
                var hasActual = actualObject.TryGetPropertyValue(key, out var actualValue);
                var hasExpected = expectedObject.TryGetPropertyValue(key, out var expectedValue);

                if (!hasActual)
                {
                    yield return $"{path}.{key}: missing from actual; " +
                                 $"expected={expectedValue?.ToJsonString() ?? "null"}";
                }
                else if (!hasExpected)
                {
                    yield return $"{path}.{key}: actual={actualValue?.ToJsonString() ?? "null"}; " +
                                 "missing from expected";
                }
                else
                {
                    foreach (var diff in FindDifferences(
                                 actualValue,
                                 expectedValue,
                                 $"{path}.{key}"))
                    {
                        yield return diff;
                    }
                }
            }

            yield break;
        }

        if (actual is JsonArray actualArray &&
            expected is JsonArray expectedArray)
        {
            var count = Math.Max(actualArray.Count, expectedArray.Count);

            for (var i = 0; i < count; i++)
            {
                if (i >= actualArray.Count)
                {
                    yield return $"{path}[{i}]: missing from actual; " +
                                 $"expected={expectedArray[i]?.ToJsonString() ?? "null"}";
                }
                else if (i >= expectedArray.Count)
                {
                    yield return $"{path}[{i}]: actual={actualArray[i]?.ToJsonString() ?? "null"}; " +
                                 "missing from expected";
                }
                else
                {
                    foreach (var diff in FindDifferences(
                                 actualArray[i],
                                 expectedArray[i],
                                 $"{path}[{i}]"))
                    {
                        yield return diff;
                    }
                }
            }

            yield break;
        }

        // Different node types or different scalar values.
        yield return $"{path}: actual={actual.ToJsonString()}, expected={expected.ToJsonString()}";
    }
}