using System.Text.Json.Nodes;

namespace Server.Api.Contracts;

public sealed record MappingTestRequest(
    string? SourceYaml,
    string? MappingId,
    string Operation,
    JsonNode? Payload);
