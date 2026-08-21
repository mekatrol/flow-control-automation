using Server.Common;
using Server.Common.Contracts;
using Server.Common.Services;
using Server.Data.Context;
using Server.Data.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.Services.Implementation;

internal sealed partial class ExecutionConfigurationService(
    IFlowControlDbContext context,
    TimeProvider timeProvider,
    IControllerTemplateStore controllerTemplates,
    IPointDefinitionStore pointDefinitions) : IExecutionConfigurationService
{
    public async Task<IReadOnlyList<ExecutionContextDefinition>> ListContextsAsync(CancellationToken cancellationToken) =>
        (await context.ExecutionContexts.AsNoTracking().OrderBy(item => item.Key).ToListAsync(cancellationToken))
            .Select(Deserialize<ExecutionContextDefinition>).ToList();

    public async Task<ExecutionContextDefinition> GetContextAsync(string id, CancellationToken cancellationToken) =>
        Deserialize<ExecutionContextDefinition>(await Find(context.ExecutionContexts, id, "execution context", cancellationToken));

    public async Task<ExecutionContextDefinition> SaveContextAsync(ExecutionContextDefinition definition, bool create, CancellationToken cancellationToken)
    {
        ValidateId(definition.Id, "id");
        if (string.IsNullOrWhiteSpace(definition.Name)) Fail("name must be non-empty");
        if (definition.Programs.Select(item => item.FlowId).Distinct(StringComparer.Ordinal).Count() != definition.Programs.Count)
            Fail("programs must contain unique flow ids");
        if (definition.Programs.Any(item => item.FlowRevision < 1)) Fail("program revisions must be positive");

        var declarations = new List<VirtualPointDeclaration>();
        foreach (var program in definition.Programs)
        {
            var flowEntity = await context.Flows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == program.FlowId, cancellationToken)
                ?? throw new ExecutionConfigurationException($"flow '{program.FlowId}' not found", 422);
            var flow = Deserialize<Flow>(flowEntity);
            if (flow.Revision != program.FlowRevision)
                throw new ExecutionConfigurationException($"flow '{program.FlowId}' revision {program.FlowRevision} is not available", 409);
            declarations.AddRange(flow.VirtualPointDeclarations);
        }

        var contracts = MergeContracts(declarations);
        if (definition.PointContracts.Count > 0 && !ContractsEqual(definition.PointContracts, contracts))
            Fail("pointContracts must equal the contracts merged from the selected flow revisions");
        var normalized = definition with { PointContracts = contracts };
        return await Save(context.ExecutionContexts, normalized.Id, normalized, create, cancellationToken);
    }

    public async Task DeleteContextAsync(string id, CancellationToken cancellationToken)
    {
        if (await context.ExecutionContextDeployments.AnyAsync(item => item.ExecutionContextId == id, cancellationToken))
            throw new ExecutionConfigurationException("execution context has deployments", 409);
        await Delete(context.ExecutionContexts, id, "execution context", cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionInstance>> ListInstancesAsync(CancellationToken cancellationToken) =>
        (await context.ExecutionInstances.AsNoTracking().OrderBy(item => item.Key).ToListAsync(cancellationToken))
            .Select(Deserialize<ExecutionInstance>).ToList();

    public async Task<ExecutionInstance> GetInstanceAsync(string id, CancellationToken cancellationToken) =>
        Deserialize<ExecutionInstance>(await Find(context.ExecutionInstances, id, "execution instance", cancellationToken));

    public async Task<ExecutionInstance> SaveInstanceAsync(ExecutionInstance instance, bool create, CancellationToken cancellationToken)
    {
        ValidateId(instance.Id, "id");
        if (string.IsNullOrWhiteSpace(instance.Name)) Fail("name must be non-empty");
        if (instance.Kind == ExecutionInstanceKind.Server && (instance.ControllerTemplateId is not null || instance.ControllerTemplateRevision is not null))
            Fail("server instances cannot reference a controller template");
        if (instance.Kind == ExecutionInstanceKind.Controller && (string.IsNullOrWhiteSpace(instance.ControllerTemplateId) || instance.ControllerTemplateRevision is null or < 1))
            Fail("controller instances require a controller template id and revision");
        if (instance.Kind == ExecutionInstanceKind.Controller)
        {
            ControllerTemplate template;
            try { template = await controllerTemplates.GetAsync(instance.ControllerTemplateId!, cancellationToken); }
            catch (ControllerTemplateNotFoundException)
            {
                throw new ExecutionConfigurationException($"controller template '{instance.ControllerTemplateId}' not found", 422);
            }
            if (template.Revision != instance.ControllerTemplateRevision)
                throw new ExecutionConfigurationException($"controller template '{template.Id}' revision is stale", 409);
        }
        if (instance.Id == "server" && (!create || instance.Kind != ExecutionInstanceKind.Server))
            throw new ExecutionConfigurationException("the built-in server instance cannot be changed", 409);
        return await Save(context.ExecutionInstances, instance.Id, instance, create, cancellationToken);
    }

    public async Task DeleteInstanceAsync(string id, CancellationToken cancellationToken)
    {
        if (id == "server") throw new ExecutionConfigurationException("the built-in server instance cannot be deleted", 409);
        if (await context.ExecutionContextDeployments.AnyAsync(item => item.ExecutionInstanceId == id, cancellationToken))
            throw new ExecutionConfigurationException("execution instance has deployments", 409);
        await Delete(context.ExecutionInstances, id, "execution instance", cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionContextDeployment>> ListDeploymentsAsync(string contextId, CancellationToken cancellationToken)
    {
        _ = await Find(context.ExecutionContexts, contextId, "execution context", cancellationToken);
        return (await context.ExecutionContextDeployments.AsNoTracking()
            .Where(item => item.ExecutionContextId == contextId).OrderBy(item => item.Key).ToListAsync(cancellationToken))
            .Select(Deserialize<ExecutionContextDeployment>).ToList();
    }

    public async Task<ExecutionContextDeployment> SaveDeploymentAsync(ExecutionContextDeployment deployment, bool create, CancellationToken cancellationToken)
    {
        ValidateId(deployment.Id, "id");
        var definition = await GetContextAsync(deployment.ExecutionContextId, cancellationToken);
        var instance = await GetInstanceAsync(deployment.ExecutionInstanceId, cancellationToken);
        if (deployment.ExecutionContextRevision != definition.Revision)
            throw new ExecutionConfigurationException("execution context revision is stale", 409);
        if (deployment.Generation < 1) Fail("generation must be positive");
        if (deployment.PhysicalPointBindings.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != deployment.PhysicalPointBindings.Count)
            Fail("physical point binding roles must be unique");
        if (deployment.PhysicalPointBindings.Any(item => string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.PointId)))
            Fail("physical point bindings require non-empty roles and point ids");
        if (deployment.Status == ExecutionContextDeploymentStatus.Active)
            await ValidateActiveDeploymentAsync(deployment, definition, instance, cancellationToken);

        var entity = new ExecutionContextDeploymentEntity
        {
            Id = deployment.Id,
            Key = deployment.Id,
            ExecutionContextId = deployment.ExecutionContextId,
            ExecutionInstanceId = deployment.ExecutionInstanceId,
            Json = Serialize(deployment),
            Created = timeProvider.GetUtcNow(),
            Updated = timeProvider.GetUtcNow()
        };
        return await SaveDeployment(entity, deployment, create, cancellationToken);
    }

    public async Task DeleteDeploymentAsync(string contextId, string deploymentId, CancellationToken cancellationToken)
    {
        var entity = await Find(context.ExecutionContextDeployments, deploymentId, "deployment", cancellationToken);
        if (entity.ExecutionContextId != contextId) throw new ExecutionConfigurationException("deployment not found", 404);
        context.ExecutionContextDeployments.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VirtualPointAllocation>> ListAllocationsAsync(string instanceId, CancellationToken cancellationToken)
    {
        _ = await GetInstanceAsync(instanceId, cancellationToken);
        var deployments = await context.ExecutionContextDeployments.AsNoTracking()
            .Where(item => item.ExecutionInstanceId == instanceId && item.Json.Contains("\"status\":\"active\""))
            .ToListAsync(cancellationToken);
        var contexts = await context.ExecutionContexts.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        var declarations = deployments.Select(Deserialize<ExecutionContextDeployment>)
            .Select(item => contexts.TryGetValue(item.ExecutionContextId, out var entity) ? Deserialize<ExecutionContextDefinition>(entity) : null)
            .Where(item => item is not null).SelectMany(item => item!.PointContracts);
        return MergeContracts(declarations).Select(contract => new VirtualPointAllocation(instanceId, contract.Key, contract)).ToList();
    }

    private async Task<T> Save<T, TEntity>(DbSet<TEntity> set, string id, T value, bool create, CancellationToken cancellationToken)
        where T : class where TEntity : BaseEntity, new()
    {
        var entity = await set.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (create && entity is not null) throw new ExecutionConfigurationException($"'{id}' already exists", 409);
        if (!create && entity is null) throw new ExecutionConfigurationException($"'{id}' not found", 404);
        var revision = (int)(typeof(T).GetProperty("Revision")?.GetValue(value) ?? 1);
        if (entity is not null)
        {
            var current = Deserialize<T>(entity);
            var currentRevision = (int)(typeof(T).GetProperty("Revision")?.GetValue(current) ?? 1);
            if (revision != currentRevision) throw new ExecutionConfigurationException($"'{id}' revision is stale", 409);
            typeof(T).GetProperty("Revision")?.SetValue(value, checked(currentRevision + 1));
            entity.Json = Serialize(value); entity.Updated = timeProvider.GetUtcNow();
        }
        else
        {
            entity = new TEntity { Id = id, Key = id, Json = Serialize(value), Created = timeProvider.GetUtcNow(), Updated = timeProvider.GetUtcNow() };
            set.Add(entity);
        }
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) { throw new ExecutionConfigurationException(exception.Message, 409); }
        return value;
    }

    private Task<ExecutionContextDefinition> Save(DbSet<ExecutionContextEntity> set, string id, ExecutionContextDefinition value, bool create, CancellationToken token) => Save<ExecutionContextDefinition, ExecutionContextEntity>(set, id, value, create, token);
    private Task<ExecutionInstance> Save(DbSet<ExecutionInstanceEntity> set, string id, ExecutionInstance value, bool create, CancellationToken token) => Save<ExecutionInstance, ExecutionInstanceEntity>(set, id, value, create, token);

    private async Task<ExecutionContextDeployment> SaveDeployment(ExecutionContextDeploymentEntity proposed, ExecutionContextDeployment value, bool create, CancellationToken token)
    {
        var existing = await context.ExecutionContextDeployments.SingleOrDefaultAsync(item => item.Id == value.Id, token);
        if (create && existing is not null) throw new ExecutionConfigurationException($"'{value.Id}' already exists", 409);
        if (!create && existing is null) throw new ExecutionConfigurationException($"'{value.Id}' not found", 404);
        if (existing is null) context.ExecutionContextDeployments.Add(proposed);
        else
        {
            var current = Deserialize<ExecutionContextDeployment>(existing);
            if (current.Revision != value.Revision) throw new ExecutionConfigurationException($"'{value.Id}' revision is stale", 409);
            value = value with { Revision = checked(value.Revision + 1) };
            existing.ExecutionContextId = value.ExecutionContextId; existing.ExecutionInstanceId = value.ExecutionInstanceId;
            existing.Json = Serialize(value); existing.Updated = timeProvider.GetUtcNow();
        }
        try { await context.SaveChangesAsync(token); }
        catch (DbUpdateException) { throw new ExecutionConfigurationException("a deployment already exists for this context and instance", 409); }
        return value;
    }

    private static async Task<TEntity> Find<TEntity>(DbSet<TEntity> set, string id, string kind, CancellationToken token) where TEntity : BaseEntity =>
        await set.SingleOrDefaultAsync(item => item.Id == id, token) ?? throw new ExecutionConfigurationException($"{kind} not found", 404);

    private async Task Delete<TEntity>(DbSet<TEntity> set, string id, string kind, CancellationToken token) where TEntity : BaseEntity
    { var entity = await Find(set, id, kind, token); set.Remove(entity); await context.SaveChangesAsync(token); }

    internal static IReadOnlyList<VirtualPointDeclaration> MergeContracts(IEnumerable<VirtualPointDeclaration> source)
    {
        var result = new Dictionary<string, VirtualPointDeclaration>(StringComparer.Ordinal);
        foreach (var declaration in source)
        {
            ValidateDeclaration(declaration);
            if (result.TryGetValue(declaration.Key, out var current))
            {
                if (!Compatible(current, declaration)) Fail($"virtual point '{declaration.Key}' has conflicting declarations");
                result[declaration.Key] = current with { Readable = current.Readable || declaration.Readable, Commandable = current.Commandable || declaration.Commandable };
            }
            else result.Add(declaration.Key, declaration);
        }
        return result.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToList();
    }

    private static bool Compatible(VirtualPointDeclaration left, VirtualPointDeclaration right) =>
        left.ValueType == right.ValueType && left.Units == right.Units && left.Persistence == right.Persistence
        && JsonSerializer.Serialize(left.RelinquishDefault) == JsonSerializer.Serialize(right.RelinquishDefault);
    private static bool ContractsEqual(IReadOnlyList<VirtualPointDeclaration> left, IReadOnlyList<VirtualPointDeclaration> right) =>
        left.Count == right.Count && left.OrderBy(item => item.Key).Zip(right.OrderBy(item => item.Key)).All(pair => pair.First == pair.Second);
    private static void ValidateDeclaration(VirtualPointDeclaration item)
    {
        ValidateId(item.Key, "virtual point key");
        if (item.ValueType is not (FlowPointValueType.Analog or FlowPointValueType.Digital)) Fail($"virtual point '{item.Key}' must be analog or digital");
        if (!item.Readable && !item.Commandable) Fail($"virtual point '{item.Key}' must be readable or commandable");
        if (item.ValueType == FlowPointValueType.Digital && item.Units is not null) Fail($"digital virtual point '{item.Key}' cannot have units");
        if (item.RelinquishDefault is { } value && (item.ValueType == FlowPointValueType.Analog ? value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number) : value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            Fail($"virtual point '{item.Key}' default does not match its type");
    }

    private async Task ValidateActiveDeploymentAsync(
        ExecutionContextDeployment deployment,
        ExecutionContextDefinition definition,
        ExecutionInstance instance,
        CancellationToken cancellationToken)
    {
        if (!instance.Enabled)
            throw new ExecutionConfigurationException($"execution instance '{instance.Id}' is disabled", 409);

        if (instance.Kind == ExecutionInstanceKind.Controller)
        {
            ControllerTemplate template;
            try { template = await controllerTemplates.GetAsync(instance.ControllerTemplateId!, cancellationToken); }
            catch (ControllerTemplateNotFoundException)
            {
                throw new ExecutionConfigurationException($"controller template '{instance.ControllerTemplateId}' not found", 422);
            }
            if (template.Revision != instance.ControllerTemplateRevision)
                throw new ExecutionConfigurationException($"controller template '{template.Id}' revision is stale", 409);
            ValidateTemplateCapabilities(template, definition.PointContracts);
        }

        var programs = await LoadProgramsAsync(definition, cancellationToken);
        await ValidatePhysicalBindingsAsync(deployment, programs, cancellationToken);

        var otherDeployments = (await context.ExecutionContextDeployments.AsNoTracking()
                .Where(item => item.ExecutionInstanceId == instance.Id && item.Id != deployment.Id)
                .ToListAsync(cancellationToken))
            .Select(Deserialize<ExecutionContextDeployment>)
            .Where(item => item.Status == ExecutionContextDeploymentStatus.Active)
            .ToList();
        var contextEntities = await context.ExecutionContexts.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        var activeDefinitions = otherDeployments
            .Select(item => contextEntities.TryGetValue(item.ExecutionContextId, out var entity) ? Deserialize<ExecutionContextDefinition>(entity) : null)
            .Where(item => item is not null)
            .Cast<ExecutionContextDefinition>()
            .ToList();
        _ = MergeContracts(activeDefinitions.SelectMany(item => item.PointContracts).Concat(definition.PointContracts));

        var writers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var active in activeDefinitions)
        {
            foreach (var flow in await LoadProgramsAsync(active, cancellationToken))
                AddWriters(writers, flow);
        }
        foreach (var flow in programs)
            AddWriters(writers, flow);
    }

    private async Task<IReadOnlyList<Flow>> LoadProgramsAsync(ExecutionContextDefinition definition, CancellationToken cancellationToken)
    {
        var result = new List<Flow>();
        foreach (var program in definition.Programs)
        {
            var entity = await context.Flows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == program.FlowId, cancellationToken)
                ?? throw new ExecutionConfigurationException($"flow '{program.FlowId}' not found", 422);
            var flow = Deserialize<Flow>(entity);
            if (flow.Revision != program.FlowRevision)
                throw new ExecutionConfigurationException($"flow '{flow.Id}' revision {program.FlowRevision} is stale", 409);
            result.Add(flow);
        }
        return result;
    }

    private async Task ValidatePhysicalBindingsAsync(
        ExecutionContextDeployment deployment,
        IReadOnlyList<Flow> programs,
        CancellationToken cancellationToken)
    {
        var bindings = deployment.PhysicalPointBindings.ToDictionary(item => item.Role, StringComparer.Ordinal);
        var points = (await pointDefinitions.ListPointsAsync(cancellationToken)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        var requiredRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var flow in programs)
        {
            var virtualKeys = flow.VirtualPointDeclarations.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var node in flow.Nodes.Where(item => item.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput or FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput))
            {
                var role = node.Configuration.TryGetValue("pointId", out var value) ? value.GetString() : null;
                if (role is null || virtualKeys.Contains(role)) continue;
                requiredRoles.Add(role);
                if (!bindings.TryGetValue(role, out var binding))
                    throw new ExecutionConfigurationException($"physical point role '{role}' has no deployment binding", 422);
                if (!points.TryGetValue(binding.PointId, out var point) || !point.Enabled)
                    throw new ExecutionConfigurationException($"physical point role '{role}' resolves to a missing or disabled point", 422);
                var analog = node.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput;
                var input = node.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.DigitalInput;
                if (point.ValueType != (analog ? FlowPointValueType.Analog : FlowPointValueType.Digital)
                    || (input ? !point.Readable : !point.Commandable))
                    throw new ExecutionConfigurationException($"physical point role '{role}' resolves to an incompatible point", 422);
            }
        }
        var unexpected = bindings.Keys.Except(requiredRoles, StringComparer.Ordinal).FirstOrDefault();
        if (unexpected is not null)
            throw new ExecutionConfigurationException($"physical point binding role '{unexpected}' is not used by the context", 422);
    }

    private static void AddWriters(IDictionary<string, string> writers, Flow flow)
    {
        var virtualKeys = flow.VirtualPointDeclarations.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var node in flow.Nodes.Where(item => item.Kind is FlowNodeKind.AnalogOutput or FlowNodeKind.DigitalOutput))
        {
            var key = node.Configuration.TryGetValue("pointId", out var value) ? value.GetString() : null;
            if (key is null || !virtualKeys.Contains(key)) continue;
            if (writers.TryGetValue(key, out var owner) && owner != flow.Id)
                throw new ExecutionConfigurationException($"virtual point '{key}' already has writer flow '{owner}' on this execution instance", 409);
            writers[key] = flow.Id;
        }
    }

    private static void ValidateTemplateCapabilities(ControllerTemplate template, IReadOnlyList<VirtualPointDeclaration> contracts)
    {
        if (contracts.Count == 0) return;
        if (!template.Capabilities.RuntimeFeatures.Contains(ControllerRuntimeFeature.VirtualPoints))
            throw new ExecutionConfigurationException($"controller template '{template.Id}' does not support virtual points", 422);
        foreach (var contract in contracts)
        {
            if (!template.Capabilities.PointTypes.Contains(contract.ValueType))
                throw new ExecutionConfigurationException($"controller template '{template.Id}' does not support {contract.ValueType} virtual point '{contract.Key}'", 422);
            if (contract.Persistence == VirtualPointPersistence.Retained
                && !template.Capabilities.PointFeatures.Contains(ControllerPointFeature.Retain))
                throw new ExecutionConfigurationException($"controller template '{template.Id}' cannot retain virtual point '{contract.Key}'", 422);
        }
    }
    private static void ValidateId(string id, string path) { if (string.IsNullOrWhiteSpace(id) || !Identifier().IsMatch(id)) Fail($"{path} has invalid syntax"); }
    private static void Fail(string message) => throw new ExecutionConfigurationException(message);
    private static T Deserialize<T>(BaseEntity entity) => JsonSerializer.Deserialize<T>(entity.Json, FlowControlJson.Options) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is null.");
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, FlowControlJson.Options);
    [GeneratedRegex("^[a-zA-Z0-9](?:[a-zA-Z0-9._-]{0,126}[a-zA-Z0-9])?$")]
    private static partial Regex Identifier();
}