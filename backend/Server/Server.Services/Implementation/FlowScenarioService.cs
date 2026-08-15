using Server.Data.Context;
using Server.Data.Entities;
using Server.Services.Contracts;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class FlowScenarioService(
    IFlowControlDbContext context,
    IFlowSimulatorService simulator,
    TimeProvider timeProvider) : IFlowScenarioService
{
    private const int MaxScenariosPerFlow = 100;
    private const int MaxSteps = 1000;
    private const int MaxExpectations = 1000;
    private const int MaxExecutionScans = 10_000;
    private static readonly TimeSpan MaximumExecutionTime = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<FlowScenario>> ListAsync(string flowId, CancellationToken cancellationToken) =>
        (await context.FlowScenarios.AsNoTracking()
            .Where(item => item.Key == flowId)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken))
        .Select(Deserialize).ToList();

    public async Task<FlowScenario> GetAsync(string flowId, string scenarioId, CancellationToken cancellationToken)
    {
        var entity = await context.FlowScenarios.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == scenarioId && item.Key == flowId, cancellationToken);
        return entity is null
            ? throw new FlowScenarioException("scenario_not_found", "The scenario was not found.")
            : Deserialize(entity);
    }

    public async Task<FlowScenario> SaveAsync(FlowScenario scenario, CancellationToken cancellationToken)
    {
        Validate(scenario);
        var entity = await context.FlowScenarios.SingleOrDefaultAsync(item => item.Id == scenario.Id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (entity is null)
        {
            var count = await context.FlowScenarios.CountAsync(item => item.Key == scenario.FlowId, cancellationToken);
            if (count >= MaxScenariosPerFlow)
            {
                throw new FlowScenarioException("scenario_limit_exceeded", $"A flow can contain at most {MaxScenariosPerFlow} scenarios.");
            }

            entity = new FlowScenarioEntity { Id = scenario.Id, Key = scenario.FlowId, Created = now };
            context.FlowScenarios.Add(entity);
        }
        else if (!string.Equals(entity.Key, scenario.FlowId, StringComparison.Ordinal))
        {
            throw new FlowScenarioException("scenario_id_conflict", "The scenario ID belongs to another flow.");
        }

        entity.Json = JsonSerializer.Serialize(scenario, FlowControlJson.Options);
        entity.Updated = now;
        await context.SaveChangesAsync(cancellationToken);
        return scenario;
    }

    public async Task DeleteAsync(string flowId, string scenarioId, CancellationToken cancellationToken)
    {
        var entity = await context.FlowScenarios.SingleOrDefaultAsync(item => item.Id == scenarioId && item.Key == flowId, cancellationToken)
            ?? throw new FlowScenarioException("scenario_not_found", "The scenario was not found.");
        context.FlowScenarios.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FlowScenarioRunResult> RunAsync(FlowScenario scenario, ExecutableFlowSource source, CancellationToken cancellationToken)
    {
        Validate(scenario);
        if (!string.Equals(scenario.FlowId, source.Id, StringComparison.Ordinal) || scenario.FlowRevision != source.Revision)
        {
            throw new FlowScenarioException("scenario_stale_revision", "The scenario targets another flow revision.", "/flowRevision");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MaximumExecutionTime);
        var executionToken = timeout.Token;
        FlowSimulatorSession? session = null;
        try
        {
            session = await simulator.StartAsync(source, true, executionToken);
            ulong clock = 0;
            foreach (var step in scenario.Steps)
            {
                executionToken.ThrowIfCancellationRequested();
                if (step.AtMilliseconds > clock)
                {
                    session = await simulator.AdvanceAsync(source.Id, session.SessionId, step.AtMilliseconds - clock, executionToken);
                    clock = step.AtMilliseconds;
                }
                session = step.Action switch
                {
                    "apply" => await simulator.ApplyInputsAsync(source.Id, session.SessionId, step.Inputs, executionToken),
                    "step" => step.Inputs.Count == 0
                        ? await simulator.StepTickAsync(source.Id, session.SessionId, executionToken)
                        : await simulator.ApplyInputsAndStepAsync(source.Id, session.SessionId, step.Inputs, executionToken),
                    "advance" => session,
                    "reset" => await simulator.ResetIoAsync(source.Id, session.SessionId, step.PowerCycle, executionToken),
                    _ => throw new InvalidOperationException()
                };
                if ((session.Io?.ScanNumber ?? 0) > MaxExecutionScans)
                {
                    throw new FlowScenarioException("scenario_limit_exceeded", $"Scenario execution cannot exceed {MaxExecutionScans} scans.");
                }
            }

            var results = scenario.Expectations.Select(expectation => Evaluate(expectation, session.Io?.OutputHistory ?? [])).ToList();
            return new FlowScenarioRunResult
            {
                ScenarioId = scenario.Id,
                Passed = results.All(item => item.Passed),
                ScanNumber = session.Io?.ScanNumber ?? 0,
                Expectations = results
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FlowScenarioException("scenario_limit_exceeded", "Scenario execution exceeded 30 seconds.");
        }
        finally
        {
            if (session is not null)
            {
                try { await simulator.StopAsync(source.Id, session.SessionId, CancellationToken.None); }
                catch (FlowSimulatorException) { }
            }
        }
    }

    private static FlowScenarioExpectationResult Evaluate(FlowScenarioExpectation expectation, IReadOnlyList<EmulatorOutputSample> history)
    {
        var samples = history.Where(item => item.OutputId == expectation.OutputId && (!expectation.Scan.HasValue || item.ScanNumber == expectation.Scan)).ToList();
        var actual = samples.LastOrDefault();
        var passed = expectation.Operator switch
        {
            "equals" => actual is not null && Equal(actual.EffectiveValue, expectation.ExpectedValue, 0),
            "approximately" => actual is not null && Equal(actual.EffectiveValue, expectation.ExpectedValue, expectation.Tolerance ?? 0),
            "changes" => samples.Select(item => ValueKey(item.EffectiveValue)).Distinct(StringComparer.Ordinal).Skip(1).Any(),
            "remains" => samples.Count > 0 && samples.Select(item => ValueKey(item.EffectiveValue)).Distinct(StringComparer.Ordinal).Count() == 1,
            _ => false
        };
        return new FlowScenarioExpectationResult
        {
            Passed = passed,
            OutputId = expectation.OutputId,
            Operator = expectation.Operator,
            Scan = expectation.Scan,
            ExpectedValue = expectation.ExpectedValue,
            ActualValue = actual?.EffectiveValue,
            Quality = actual?.Quality,
            DiagnosticCode = passed ? null : actual is null ? "scenario_output_missing" : "scenario_expectation_failed"
        };
    }

    private static bool Equal(FlowVmValue actual, FlowVmValue? expected, double tolerance) => expected is not null
        && actual.Type == expected.Type && actual.Quality == expected.Quality
        && (actual.Type == "boolean" ? actual.Boolean == expected.Boolean : Math.Abs(actual.Number - expected.Number) <= tolerance);
    private static string ValueKey(FlowVmValue value) => JsonSerializer.Serialize(value, FlowControlJson.Options);
    private static FlowScenario Deserialize(FlowScenarioEntity entity) =>
        JsonSerializer.Deserialize<FlowScenario>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored scenario {entity.Id} is null.");

    private static void Validate(FlowScenario scenario)
    {
        if (scenario.SchemaVersion != 1)
        {
            throw Invalid("scenario_version_unsupported", "Only scenario schema version 1 is supported.", "/schemaVersion");
        }

        if (string.IsNullOrWhiteSpace(scenario.Id) || scenario.Id.Length > 100)
        {
            throw Invalid("scenario_invalid", "Scenario ID is required and limited to 100 characters.", "/id");
        }

        if (string.IsNullOrWhiteSpace(scenario.Name) || scenario.Name.Length > 200)
        {
            throw Invalid("scenario_invalid", "Scenario name is required and limited to 200 characters.", "/name");
        }

        if (scenario.Description?.Length > 2000)
        {
            throw Invalid("scenario_invalid", "Scenario description is limited to 2000 characters.", "/description");
        }

        if (string.IsNullOrWhiteSpace(scenario.FlowId))
        {
            throw Invalid("scenario_invalid", "Flow ID is required.", "/flowId");
        }

        if (scenario.Steps.Count > MaxSteps)
        {
            throw Invalid("scenario_limit_exceeded", $"A scenario can contain at most {MaxSteps} steps.", "/steps");
        }

        if (scenario.Expectations.Count > MaxExpectations)
        {
            throw Invalid("scenario_limit_exceeded", $"A scenario can contain at most {MaxExpectations} expectations.", "/expectations");
        }

        ulong previous = 0;
        for (var index = 0; index < scenario.Steps.Count; index++)
        {
            var step = scenario.Steps[index];
            if (index > 0 && step.AtMilliseconds < previous)
            {
                throw Invalid("scenario_invalid", "Steps must be ordered by time.", $"/steps/{index}/atMilliseconds");
            }

            if (step.Action is not ("apply" or "step" or "advance" or "reset"))
            {
                throw Invalid("scenario_invalid", "Scenario action is unsupported.", $"/steps/{index}/action");
            }

            previous = step.AtMilliseconds;
        }
        for (var index = 0; index < scenario.Expectations.Count; index++)
        {
            var item = scenario.Expectations[index];
            if (string.IsNullOrWhiteSpace(item.OutputId))
            {
                throw Invalid("scenario_invalid", "Output ID is required.", $"/expectations/{index}/outputId");
            }

            if (item.Operator is not ("equals" or "approximately" or "changes" or "remains"))
            {
                throw Invalid("scenario_invalid", "Expectation operator is unsupported.", $"/expectations/{index}/operator");
            }

            if (item.Operator is ("equals" or "approximately") && item.ExpectedValue is null)
            {
                throw Invalid("scenario_invalid", "This operator requires an expected value.", $"/expectations/{index}/expectedValue");
            }

            if (item.Tolerance.HasValue && (!double.IsFinite(item.Tolerance.Value) || item.Tolerance.Value < 0))
            {
                throw Invalid("scenario_invalid", "Tolerance must be finite and non-negative.", $"/expectations/{index}/tolerance");
            }
        }
    }

    private static FlowScenarioException Invalid(string code, string message, string path) => new(code, message, path);
}