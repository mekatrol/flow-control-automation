# Test Documentation and Commenting Standard

## Scope

This standard applies to all new and modified automated tests in this repository, including:

- C# unit, integration, and component tests.
- TypeScript unit, integration, component, and end-to-end tests.

Existing tests should be brought into compliance whenever they are modified. A test is not complete until its test-level documentation and assertion-level acceptance criteria comply with this standard.

## Required Test-Level Documentation

Every individual test must have its own documentation immediately above the test declaration. A file-level or test-suite-level comment does not replace per-test documentation.

Each test-level documentation block must have exactly one blank line before its
opening comment. The documentation lines within the block remain contiguous, and
the test attribute must immediately follow the closing comment without a blank line.

Use this exact structure:

```text
Purpose: <Why this behavior matters and what requirement or risk the test covers.>
Description: <The scenario, action, and observable result exercised by this test.>
```

The two fields have different roles:

- **Purpose** explains why the test exists. Describe the business rule, contract, regression risk, or behavior being protected.
- **Description** explains what the test does. Identify the relevant starting conditions, the action under test, and the result that will be observed.

Comments must describe the specific test rather than restating its method or case name. Include meaningful boundary values, roles, states, or error conditions when they affect the scenario.

### C# format

Use XML documentation comments so the documentation is attached to the test method:

```csharp
/// <summary>
/// Purpose: Verifies that an unauthorised user cannot update a protected workflow.
/// Description: Submits an update without the required permission and verifies that
/// the operation is rejected without changing the persisted workflow.
/// </summary>
[Fact]
public async Task UpdateWorkflow_WithoutPermission_IsRejected()
{
    // Test implementation.
}
```

The same format applies regardless of the C# test attribute, including `[Fact]`, `[Theory]`, and `[Test]`. Each parameterized test method requires documentation that explains the behavior represented by its data rows.

### TypeScript format

Use a documentation block immediately above each `it(...)` or `test(...)` declaration:

```typescript
/**
 * Purpose: Verifies that an unauthorised user cannot update a protected workflow.
 * Description: Submits an update without the required permission and verifies that
 * the operation is rejected without changing the displayed workflow.
 */
test("rejects a workflow update when permission is missing", async () => {
  // Test implementation.
});
```

Comments on `describe(...)` blocks provide useful suite context but do not replace the required comment on each test.

## Required Assertion Documentation

Every assertion statement must have an immediately preceding comment that states both:

```text
Expected outcome: <The observable result that must occur.>
Acceptance criteria: <The precise pass condition and why that condition proves the
expected behavior for this test scenario.>
```

Each assertion-level documentation block must have exactly one blank line before
its `Expected outcome` comment. The `Expected outcome` and `Acceptance criteria`
comment lines remain contiguous, and the assertion must immediately follow the
documentation block without a blank line.

The assertion comment must:

- Describe the outcome in domain or behavioral terms.
- State the exact accepted value, state, relationship, exception, call count, or tolerance.
- Explain why that exact condition is correct for the test's arrangement and action.
- Connect the asserted result to the requirement, contract, security rule, state
  transition, or other behavior that the assertion proves.
- Match the assertion that immediately follows it.
- Be updated whenever the assertion changes.
- Stand on its own; do not rely on a distant test-level comment to explain the assertion.

A compliant acceptance criterion contains both the **what** and the **why**:

```text
Acceptance criteria: <What must be true>, because <why it proves the expected
behavior in this scenario>.
```

Merely translating code into prose is not sufficient. For example,
`The status code is 403` restates `Assert.Equal(HttpStatusCode.Forbidden, ...)`;
it does not explain why `403 Forbidden` is correct. A compliant comment identifies
the relevant cause, such as the request being made anonymously or by a user without
the required permission.

A chained fluent assertion counts as one assertion statement and requires one comment. If a statement verifies several independent conditions, split it into separate assertions so each condition has its own acceptance criteria.

Avoid vague comments such as `Verify the result`, `Check success`, or
`Should be correct`, as well as comments that only repeat assertion operands.

### C# assertion examples

```csharp
// Expected outcome: The update is rejected as forbidden.
// Acceptance criteria: The response status is HTTP 403 Forbidden because the
// anonymous caller has no permission to update a protected workflow.
Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

// Expected outcome: A rejected update leaves the stored workflow unchanged.
// Acceptance criteria: The persisted name remains "Original workflow" because an
// update rejected during authorization must not modify protected workflow data.
Assert.Equal("Original workflow", persistedWorkflow.Name);

// Expected outcome: The service reports the missing workflow.
// Acceptance criteria: The operation throws WorkflowNotFoundException because ID 42
// was not present in the arranged repository and must not be treated as a valid workflow.
var exception = await Assert.ThrowsAsync<WorkflowNotFoundException>(
    () => service.GetWorkflowAsync(42));

// Expected outcome: The exception identifies the workflow that was not found.
// Acceptance criteria: WorkflowId is 42 because consumers need the rejected request's
// identifier to diagnose which lookup failed.
Assert.Equal(42, exception.WorkflowId);
```

### TypeScript assertion examples

```typescript
// Expected outcome: The update is rejected as forbidden.
// Acceptance criteria: The response status is 403 because the anonymous caller lacks
// the permission required to update a protected workflow.
expect(response.status).toBe(403);

// Expected outcome: A rejected update leaves the workflow name unchanged.
// Acceptance criteria: The displayed name remains "Original workflow" because a
// rejected update must not replace the workflow data already visible to the user.
await expect(page.getByTestId("workflow-name")).toHaveText("Original workflow");

// Expected outcome: Saving a valid workflow invokes the persistence operation.
// Acceptance criteria: saveWorkflow is called once because one valid save action
// must create one persistence request without duplicates.
expect(saveWorkflow).toHaveBeenCalledTimes(1);

// Expected outcome: Persistence receives the workflow being edited.
// Acceptance criteria: The saved object has ID 42 because that is the workflow
// selected in the arranged editing scenario.
expect(saveWorkflow).toHaveBeenCalledWith(
  expect.objectContaining({ id: 42 }),
);
```

When multiple assertions collectively express one expected outcome, each assertion still needs its own acceptance-criteria comment:

```typescript
// Expected outcome: Saving a valid workflow invokes persistence.
// Acceptance criteria: saveWorkflow is called once because one valid save action
// must create one persistence request without duplicates.
expect(saveWorkflow).toHaveBeenCalledTimes(1);

// Expected outcome: Persistence receives the workflow being edited.
// Acceptance criteria: The saved object has ID 42 because that is the workflow
// selected in the arranged editing scenario.
expect(saveWorkflow).toHaveBeenCalledWith(
  expect.objectContaining({ id: 42 }),
);
```

## Arrange, Act, Assert Structure

Use `Arrange`, `Act`, and `Assert` section comments when they improve readability. These organizational comments are optional and do not replace test-level or assertion-level documentation.

```csharp
/// <summary>
/// Purpose: Protects the rule that disabled workflows cannot be executed.
/// Description: Loads a disabled workflow, requests execution, and verifies that
/// execution is rejected before any job is queued.
/// </summary>
[Fact]
public async Task ExecuteWorkflow_WhenDisabled_DoesNotQueueJob()
{
    // Arrange

    // Act

    // Assert
    // Expected outcome: The disabled workflow is rejected.
    // Acceptance criteria: The result is WorkflowDisabled because the arranged
    // workflow is disabled and therefore ineligible for execution.
    Assert.Equal(ExecutionResult.WorkflowDisabled, result);

    // Expected outcome: Rejected execution does not create background work.
    // Acceptance criteria: The queue receives no enqueue calls because rejection
    // must stop processing before any executable job is created.
    queue.Verify(x => x.EnqueueAsync(It.IsAny<Job>()), Times.Never);
}
```

## Review Checklist

Before a new or modified test is accepted, confirm that:

- Every test method or test case has its own `Purpose` and `Description`.
- The purpose explains why the behavior matters.
- The description identifies the scenario, action, and observable result.
- Every assertion has an immediately preceding `Expected outcome` and `Acceptance criteria`.
- Each acceptance criterion states both the precise pass condition and why that
  condition proves the behavior for the arranged scenario.
- No acceptance criterion merely translates its assertion into prose.
- Comments and assertions agree with one another.
- Independent conditions use separate assertions and separate assertion comments.
- Suite-level, file-level, and Arrange/Act/Assert comments are not being used as substitutes for the required per-test and per-assertion documentation.
