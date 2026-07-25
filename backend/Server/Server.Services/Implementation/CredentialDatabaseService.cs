using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Server.Data.Context;
using Server.Data.Entities;
using Server.Services.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

internal sealed partial class CredentialDatabaseService :
    ICredentialStore,
    ICredentialResolver
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IFlowControlDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly byte[] _key;

    public CredentialDatabaseService(
        IFlowControlDbContext context,
        TimeProvider timeProvider,
        IOptions<ServerOptions> options)
    {
        _context = context;
        _timeProvider = timeProvider;
        _key = Convert.FromBase64String(options.Value.CredentialEncryptionKey!);
    }

    public async Task<IReadOnlyList<CredentialMetadata>> ListAsync(
        CancellationToken cancellationToken) =>
        (await _context.Credentials
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Select(Deserialize)
            .Select(credential => credential.Metadata)
            .OrderBy(metadata => metadata.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(metadata => metadata.Id, StringComparer.Ordinal)
            .ToList();

    public async Task<CredentialMetadata> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await _context.Credentials
            .AsNoTracking()
            .SingleOrDefaultAsync(credential => credential.Id == id, cancellationToken);
        return entity is null
            ? throw new CredentialNotFoundException(id)
            : Deserialize(entity).Metadata;
    }

    public async Task<CredentialMetadata> CreateAsync(
        CredentialInput input,
        CancellationToken cancellationToken)
    {
        Validate(input, update: false);
        await EnsureNameAvailable(input.Name, exceptId: null, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var metadata = new CredentialMetadata(
            input.Id,
            input.Name.Trim(),
            input.Kind,
            input.Username,
            1,
            Timestamp(now),
            Timestamp(now));
        var stored = new StoredCredential(metadata, Encrypt(SecretValue(input)));
        _context.Credentials.Add(new CredentialEntity
        {
            Id = metadata.Id,
            Key = NormalizeName(metadata.Name),
            Json = Serialize(stored),
            Created = now,
            Updated = now,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new CredentialConflictException(
                "credential ID or name already exists",
                exception);
        }

        return metadata;
    }

    public async Task<CredentialMetadata> UpdateAsync(
        string id,
        CredentialInput input,
        CancellationToken cancellationToken)
    {
        var entity = await FindTracked(id, cancellationToken);
        var previous = Deserialize(entity);
        if (input.Id != id || input.Revision != previous.Metadata.Revision)
        {
            throw new CredentialConflictException("stale revision or mismatched ID");
        }

        Validate(input, update: true);
        await EnsureNameAvailable(input.Name, id, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var metadata = new CredentialMetadata(
            id,
            input.Name.Trim(),
            input.Kind,
            input.Username,
            previous.Metadata.Revision + 1,
            previous.Metadata.CreatedAt,
            Timestamp(now));
        var secret = string.IsNullOrEmpty(input.Password) && string.IsNullOrEmpty(input.Token)
            ? previous.Secret
            : Encrypt(SecretValue(input));
        entity.Key = NormalizeName(metadata.Name);
        entity.Json = Serialize(new StoredCredential(metadata, secret));
        entity.Updated = now;

        try
        {
            await SaveWithConcurrencyMapping(entity, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new CredentialConflictException(
                "credential name already exists",
                exception);
        }

        return metadata;
    }

    public async Task DeleteAsync(
        string id,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindTracked(id, cancellationToken);
        if (Deserialize(entity).Metadata.Revision != revision)
        {
            throw new CredentialConflictException("stale revision");
        }

        var reference = $"secret://{id}";
        var pointSources = await _context.PointSources
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var referencingSource = pointSources
            .Select(DeserializePointSource)
            .FirstOrDefault(source => source.CredentialRef == reference);
        if (referencingSource is not null)
        {
            throw new CredentialConflictException(
                $"credential is referenced by point source \"{referencingSource.Id}\"");
        }

        _context.Credentials.Remove(entity);
        await SaveWithConcurrencyMapping(entity: null, cancellationToken);
    }

    public async Task<string> ResolveAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        if (reference.Length == 0)
        {
            return string.Empty;
        }

        if (reference.StartsWith("env:", StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(reference["env:".Length..])
                ?? throw new CredentialResolutionException(
                    "referenced credential is unavailable");
        }

        if (!reference.StartsWith("secret://", StringComparison.Ordinal))
        {
            throw new CredentialResolutionException(
                "credential reference is unavailable in this deployment");
        }

        var id = reference["secret://".Length..];
        var entity = await _context.Credentials
            .AsNoTracking()
            .SingleOrDefaultAsync(credential => credential.Id == id, cancellationToken) ?? throw new CredentialResolutionException("referenced credential is unavailable");
        try
        {
            var credential = Deserialize(entity);
            var secret = Decrypt(credential.Secret);
            return credential.Metadata.Kind == "mqtt"
                ? JsonSerializer.Serialize(
                    new Dictionary<string, string?>
                    {
                        ["username"] = credential.Metadata.Username,
                        ["password"] = secret,
                    },
                    FlowControlJson.Options)
                : secret;
        }
        catch (Exception exception) when (
            exception is FormatException or CryptographicException or JsonException)
        {
            // Resolution errors intentionally hide ciphertext and key details.
            throw new CredentialResolutionException(
                "referenced credential could not be resolved");
        }
    }

    private async Task EnsureNameAvailable(
        string name,
        string? exceptId,
        CancellationToken cancellationToken)
    {
        var credentials = await _context.Credentials
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (credentials.Any(entity =>
            entity.Id != exceptId
            && string.Equals(
                Deserialize(entity).Metadata.Name,
                name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new CredentialConflictException("credential name already exists");
        }
    }

    private async Task<CredentialEntity> FindTracked(
        string id,
        CancellationToken cancellationToken) =>
        await _context.Credentials.SingleOrDefaultAsync(
            credential => credential.Id == id,
            cancellationToken)
        ?? throw new CredentialNotFoundException(id);

    private async Task SaveWithConcurrencyMapping(
        CredentialEntity? entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (entity is not null)
            {
                await _context.ReloadAsync(entity, cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CredentialConflictException("stale revision", exception);
        }
    }

    private string Encrypt(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = System.Text.Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var sealedValue = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(sealedValue, 0);
        ciphertext.CopyTo(sealedValue, nonce.Length);
        tag.CopyTo(sealedValue, nonce.Length + ciphertext.Length);
        return Convert.ToBase64String(sealedValue).TrimEnd('=');
    }

    private string Decrypt(string encoded)
    {
        var padding = (4 - (encoded.Length % 4)) % 4;
        var sealedValue = Convert.FromBase64String(encoded + new string('=', padding));
        if (sealedValue.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid encrypted credential.");
        }

        var ciphertextLength = sealedValue.Length - NonceSize - TagSize;
        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            sealedValue.AsSpan(0, NonceSize),
            sealedValue.AsSpan(NonceSize, ciphertextLength),
            sealedValue.AsSpan(NonceSize + ciphertextLength, TagSize),
            plaintext);
        return System.Text.Encoding.UTF8.GetString(plaintext);
    }

    private static void Validate(CredentialInput input, bool update)
    {
        if (!Identifier().IsMatch(input.Id) || string.IsNullOrWhiteSpace(input.Name))
        {
            throw new CredentialValidationException(
                "id and name are required; id must be lowercase and hyphenated");
        }

        if (input.Kind is not ("mqtt" or "token"))
        {
            throw new CredentialValidationException("kind must be mqtt or token");
        }

        if (input.Kind == "mqtt" && string.IsNullOrWhiteSpace(input.Username))
        {
            throw new CredentialValidationException(
                "username is required for MQTT credentials");
        }

        if (!update && string.IsNullOrEmpty(SecretValue(input)))
        {
            throw new CredentialValidationException("a password or token is required");
        }

        if ((input.Kind == "mqtt" && !string.IsNullOrEmpty(input.Token))
            || (input.Kind == "token" && !string.IsNullOrEmpty(input.Password)))
        {
            throw new CredentialValidationException(
                "credential contains fields for a different kind");
        }
    }

    private static string SecretValue(CredentialInput input) =>
        input.Kind == "mqtt" ? input.Password ?? string.Empty : input.Token ?? string.Empty;

    private static string Serialize(StoredCredential credential) =>
        JsonSerializer.Serialize(credential, FlowControlJson.Options);

    private static StoredCredential Deserialize(CredentialEntity entity) =>
        JsonSerializer.Deserialize<StoredCredential>(entity.Json, FlowControlJson.Options)
        ?? throw new JsonException("Stored credential is null.");

    private static PointSource DeserializePointSource(PointSourceEntity entity) =>
        JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
        ?? throw new JsonException("Stored point source is null.");

    private static string NormalizeName(string name) =>
        name.Trim().ToUpperInvariant();

    private static string Timestamp(DateTimeOffset value) =>
        value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            CultureInfo.InvariantCulture);

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.Ordinal) == true;

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    private sealed record StoredCredential(
        CredentialMetadata Metadata,
        string Secret);
}