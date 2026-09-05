using Server.Common.Types;

namespace Server.Common.Models;

public sealed record FlowConnector(
    string Id,
    string Label,
    DataDirectionType Direction,
    DataType DataType,
    string Side);