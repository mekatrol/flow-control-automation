using System.Text.Json.Nodes;

namespace Server.Api.Contracts;

public sealed record PointTestRequest(
    string SourceYaml,
    string PointYaml,
    string Operation,
    JsonNode? Value);