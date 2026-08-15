namespace Server.Services.Implementation;

public static class FlowNodeRegistry
{
    public static IReadOnlySet<string> Functions { get; } =
        new SortedSet<string>(
            [
                "and", "average", "calculator", "calendar", "clamp", "comparator",
                "delay", "if", "level-shifter", "line", "max", "min",
                "nand", "nor", "not", "or", "override", "point-changed", "pulse",
                "read-point", "release-point-command", "schedule", "selector",
                "sequence", "split", "timer", "write-point", "xnor", "xor",
            ],
            StringComparer.Ordinal);
}