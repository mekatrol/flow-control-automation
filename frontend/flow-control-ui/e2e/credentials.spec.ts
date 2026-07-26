import { expect, test } from '@playwright/test';

/**
 * Purpose: Protects credential secrecy by allowing a password only during creation and
 * preventing the persisted UI or metadata response from exposing it afterward.
 * Description: Creates an MQTT credential with a masked password, briefly toggles local
 * visibility, saves it, and observes that only its secret reference and metadata remain.
 */
test('creates a write-only credential and never displays its secret again', async ({ page }) => {
  const credentials: Array<Record<string, unknown>> = [];
  await page.route('/api/credentials', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: { items: credentials } });
      return;
    }
    const input = route.request().postDataJSON() as Record<string, unknown>;

    // Expected outcome: Creation sends the password entered for this credential.
    // Acceptance criteria: The POST body password is `broker-secret` because the backend
    // needs the arranged secret once to create the write-only credential.
    expect(input.password).toBe('broker-secret');
    const metadata = {
      id: input.id,
      name: input.name,
      kind: input.kind,
      username: input.username,
      revision: 1,
      createdAt: '2026-07-25T00:00:00Z',
      updatedAt: '2026-07-25T00:00:00Z'
    };
    credentials.push(metadata);
    await route.fulfill({ status: 201, json: metadata });
  });

  await page.goto('/credentials');
  await page.getByRole('button', { name: 'New credential' }).click();
  await page.getByLabel('Display name').fill('Plant MQTT');
  await page.getByLabel('Reference ID').fill('plant-mqtt');
  await page.getByLabel('Username').fill('flow-reader');
  const password = page.getByLabel('Password', { exact: true });

  // Expected outcome: An empty password control starts masked.
  // Acceptance criteria: Its type is `password` because secret input must not be exposed
  // before the user has entered a value or requested temporary visibility.
  await expect(password).toHaveAttribute('type', 'password');

  // Expected outcome: Visibility cannot be toggled when there is no secret to reveal.
  // Acceptance criteria: There are zero "Show password" buttons because an empty field
  // needs no reveal action and should not imply that a stored secret is retrievable.
  await expect(page.getByRole('button', { name: 'Show password' })).toHaveCount(0);
  await password.fill('broker-secret');
  const showPassword = page.getByRole('button', { name: 'Show password' });

  // Expected outcome: A newly entered local password can be inspected before submission.
  // Acceptance criteria: "Show password" is visible because the user may verify their
  // current unsaved input without retrieving any persisted secret.
  await expect(showPassword).toBeVisible();
  await showPassword.click();

  // Expected outcome: Activating reveal temporarily displays the unsaved password.
  // Acceptance criteria: The input type is `text` because the explicit local reveal action
  // must make the currently entered value readable for verification.
  await expect(password).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'Hide password' }).click();

  // Expected outcome: Activating hide restores password masking before submission.
  // Acceptance criteria: The input type returns to `password` because ending temporary
  // visibility must conceal the unsaved secret again.
  await expect(password).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: 'Create credential' }).click();

  const savedCredentials = page.getByLabel('Saved credentials');
  const credentialDialog = page.getByRole('dialog', { name: 'Create new credential' });

  // Expected outcome: the credential form closes after the save succeeds.
  // Acceptance criteria: the dialog must not be visible, because a completed create no longer
  // requires input while a failed create must remain available for correction.
  await expect(credentialDialog).not.toBeVisible();

  // Expected outcome: The saved list exposes a usable reference instead of secret material.
  // Acceptance criteria: `secret://plant-mqtt` is visible because workflows address the
  // credential by its configured reference ID after persistence.
  await expect(savedCredentials.getByText('secret://plant-mqtt', { exact: true })).toBeVisible();

  // Expected outcome: Editing metadata does not preload the persisted password.
  // Acceptance criteria: Replacement password is empty because the backend response omits
  // write-only secret material and the UI must not reconstruct or cache it.
  await expect(page.getByLabel('Replacement password')).toHaveValue('');

  // Expected outcome: The saved credential offers no action to reveal its password.
  // Acceptance criteria: There are zero "Show password" buttons because persisted secrets
  // are write-only and cannot be read back through the metadata screen.
  await expect(page.getByRole('button', { name: 'Show password' })).toHaveCount(0);

  // Expected outcome: The submitted password never appears in rendered saved state.
  // Acceptance criteria: There are zero text nodes containing `broker-secret` because a
  // write-only secret must not leak into credential metadata or confirmation content.
  await expect(page.getByText('broker-secret')).toHaveCount(0);

  // Expected outcome: The UI explains the post-save secrecy behavior.
  // Acceptance criteria: The sensitive-values-hidden message is attached because users
  // must understand why the saved credential no longer displays the submitted password.
  await expect(page.getByText(/Sensitive values are now hidden/)).toBeAttached();
});

/**
 * Purpose: Protects credential data from accidental dismissal.
 * Description: Exercises backdrop, Escape, and Cancel interactions and verifies changed data requires discard confirmation.
 */
test('requires an explicit action to close the credential dialog', async ({ page }) => {
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');

  const dialog = page.getByRole('dialog', { name: 'Create new credential' });
  await page.getByRole('button', { name: 'New credential' }).click();

  // Expected outcome: Starting credential creation presents the protected form.
  // Acceptance criteria: The "Create new credential" dialog is visible because secret
  // input must occur in the explicit modal workflow rather than the background page.
  await expect(dialog).toBeVisible();

  await page.getByLabel('Display name').fill('Unsaved credential');
  await page.mouse.click(5, 5);

  // Expected outcome: A backdrop click cannot silently dismiss changed credential data.
  // Acceptance criteria: The credential dialog remains visible because the entered display
  // name is unsaved and requires an explicit keep-or-discard decision.
  await expect(dialog).toBeVisible();

  // Expected outcome: The blocked backdrop dismissal preserves the user's edit.
  // Acceptance criteria: The display name remains "Unsaved credential" because rejecting
  // implicit dismissal must leave all protected form state intact.
  await expect(page.getByLabel('Display name')).toHaveValue('Unsaved credential');

  const discardDialog = page.getByRole('dialog', {
    name: 'Discard unsaved credential changes'
  });

  await page.keyboard.press('Escape');

  // Expected outcome: Escape requests an explicit decision for the dirty form.
  // Acceptance criteria: The discard confirmation is visible because unsaved credential
  // metadata must not be abandoned directly through the native cancel gesture.
  await expect(discardDialog).toBeVisible();
  await page.getByRole('button', { name: 'Keep editing' }).click();

  // Expected outcome: Choosing to keep editing closes only the confirmation prompt.
  // Acceptance criteria: The discard dialog is not visible because its safe action resolves
  // the nested decision without ending credential creation.
  await expect(discardDialog).not.toBeVisible();

  // Expected outcome: Keeping edits returns the user to the credential form.
  // Acceptance criteria: The credential dialog remains visible because cancellation of the
  // discard decision must preserve the active creation workflow.
  await expect(dialog).toBeVisible();

  // Expected outcome: Keeping edits preserves the entered credential metadata.
  // Acceptance criteria: The display name remains "Unsaved credential" because no
  // destructive decision has authorized clearing the dirty form.
  await expect(page.getByLabel('Display name')).toHaveValue('Unsaved credential');

  await page.keyboard.press('Escape');

  // Expected outcome: A later Escape still protects the unchanged dirty form.
  // Acceptance criteria: The discard confirmation is visible again because choosing to keep
  // editing does not waive protection for subsequent dismissal attempts.
  await expect(discardDialog).toBeVisible();
  await page.getByRole('button', { name: 'Keep editing' }).click();

  // Expected outcome: Repeated safe cancellation closes the nested prompt again.
  // Acceptance criteria: The discard dialog is not visible because each "Keep editing"
  // action must independently resolve its current confirmation.
  await expect(discardDialog).not.toBeVisible();

  // Expected outcome: Repeated safe cancellation retains the creation modal.
  // Acceptance criteria: The credential dialog remains visible because the user has again
  // rejected discarding the unsaved credential.
  await expect(dialog).toBeVisible();

  // Expected outcome: Repeated dismissal attempts do not erode form state.
  // Acceptance criteria: The display name remains "Unsaved credential" because neither
  // Escape attempt was followed by destructive confirmation.
  await expect(page.getByLabel('Display name')).toHaveValue('Unsaved credential');

  await page.keyboard.press('Escape');

  // Expected outcome: The dirty-state prompt is available for an explicit destructive choice.
  // Acceptance criteria: The discard dialog is visible because the final Escape still must
  // not close the credential form on its own.
  await expect(discardDialog).toBeVisible();
  await page.getByRole('button', { name: 'Discard changes' }).click();

  // Expected outcome: Explicit discard ends the credential creation workflow.
  // Acceptance criteria: The credential dialog is not visible because the user has now
  // knowingly authorized removal of the unsaved form state.
  await expect(dialog).not.toBeVisible();
});
