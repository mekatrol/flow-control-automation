namespace Server.Common.Contracts.Points;

/// <summary>Enforces the canonical schema and protocol-specific constraints for reusable point sources.</summary>
public interface IPointSourceValidator
{
    /// <summary>Validates a point source without contacting or mutating the external system it describes.</summary>
    /// <param name="source">The source definition to validate; it must be non-null and use the current schema and a supported source type.</param>
    /// <exception cref="ArgumentException">Thrown when a field, range, credential reference, or protocol combination is invalid.</exception>
    void Validate(PointSource source);

    /// <summary>Validates the aggregate and point identifiers against other persisted aggregates.</summary>
    void Validate(PointSource source, IEnumerable<PointSource> existingSources);
}