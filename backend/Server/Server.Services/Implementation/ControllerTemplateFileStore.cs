using Microsoft.Extensions.Options;
using Server.Services.Contracts;
using System.Globalization;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class ControllerTemplateFileStore(
    IOptions<ServerOptions> options,
    IControllerTemplateValidator validator,
    TimeProvider timeProvider) : IControllerTemplateStore
{
    private readonly string _path = Path.GetFullPath(options.Value.ControllerDataFile);
    private readonly IControllerTemplateValidator _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ControllerTemplateDocument? _document;

    public async Task<IReadOnlyList<ControllerTemplate>> ListAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await Load(cancellationToken);
            return [BuiltInControllerTemplate.Default, .. document.Templates
                .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(template => template.Id, StringComparer.Ordinal)];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ControllerTemplate> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (id == BuiltInControllerTemplate.Id)
        {
            return BuiltInControllerTemplate.Default;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await Load(cancellationToken)).Templates.FirstOrDefault(
                template => template.Id == id)
                ?? throw new ControllerTemplateNotFoundException(id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ControllerTemplate> CreateAsync(
        ControllerTemplate template,
        CancellationToken cancellationToken)
    {
        _validator.Validate(template);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await Load(cancellationToken);
            EnsureAvailable(document, template, exceptId: null);
            var now = Timestamp(_timeProvider.GetUtcNow());
            var created = template with
            {
                ReadOnly = false,
                Revision = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            await Persist(
                document with
                {
                    Revision = document.Revision + 1,
                    Templates = [.. document.Templates, created],
                },
                cancellationToken);
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ControllerTemplate> UpdateAsync(
        string id,
        ControllerTemplate template,
        int revision,
        CancellationToken cancellationToken)
    {
        EnsureMutable(id);
        if (template.Id != id)
        {
            throw new ControllerTemplateValidationException(
                [new("id_mismatch", "id", "id must match request path")]);
        }

        _validator.Validate(template);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await Load(cancellationToken);
            var previous = document.Templates.FirstOrDefault(item => item.Id == id)
                ?? throw new ControllerTemplateNotFoundException(id);
            if (previous.Revision != revision)
            {
                throw new ControllerTemplateConflictException("stale revision");
            }

            EnsureAvailable(document, template, id);
            var updated = template with
            {
                ReadOnly = false,
                Revision = previous.Revision + 1,
                CreatedAt = previous.CreatedAt,
                UpdatedAt = Timestamp(_timeProvider.GetUtcNow())
            };
            await Persist(
                document with
                {
                    Revision = document.Revision + 1,
                    Templates = [.. document.Templates.Select(item => item.Id == id ? updated : item)]
                },
                cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(
        string id,
        int revision,
        CancellationToken cancellationToken)
    {
        EnsureMutable(id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var document = await Load(cancellationToken);
            var existing = document.Templates.FirstOrDefault(item => item.Id == id)
                ?? throw new ControllerTemplateNotFoundException(id);
            if (existing.Revision != revision)
            {
                throw new ControllerTemplateConflictException("stale revision");
            }

            await Persist(
                document with
                {
                    Revision = document.Revision + 1,
                    Templates = [.. document.Templates.Where(item => item.Id != id)]
                },
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ControllerTemplateDocument> Load(CancellationToken cancellationToken)
    {
        if (_document is not null)
        {
            return _document;
        }

        if (!File.Exists(_path))
        {
            return _document = new();
        }

        await using var stream = File.OpenRead(_path);
        var document = await JsonSerializer.DeserializeAsync<ControllerTemplateDocument>(
            stream,
            FlowControlJson.Options,
            cancellationToken)
            ?? throw new InvalidDataException("Controller template data is empty.");
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported controller template schema version.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in document.Templates)
        {
            _validator.Validate(template);
            if (!ids.Add(template.Id) || !names.Add(template.Name))
            {
                throw new InvalidDataException("Controller template IDs and names must be unique.");
            }
        }

        return _document = document;
    }

    private async Task Persist(
        ControllerTemplateDocument document,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Controller data path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document with
                    {
                        Templates = [.. document.Templates.OrderBy(
                            item => item.Id,
                            StringComparer.Ordinal)]
                    },
                    FlowControlJson.Options,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _path, overwrite: true);
            _document = document;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void EnsureMutable(string id)
    {
        if (id == BuiltInControllerTemplate.Id)
        {
            throw new ControllerTemplateConflictException(
                "the built-in default controller template is read-only");
        }
    }

    private static void EnsureAvailable(
        ControllerTemplateDocument document,
        ControllerTemplate candidate,
        string? exceptId)
    {
        if (document.Templates.Any(item =>
            item.Id != exceptId
            && (item.Id == candidate.Id
                || string.Equals(item.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))))
        {
            throw new ControllerTemplateConflictException(
                "controller template ID or name already exists");
        }
    }

    private static string Timestamp(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture);
}