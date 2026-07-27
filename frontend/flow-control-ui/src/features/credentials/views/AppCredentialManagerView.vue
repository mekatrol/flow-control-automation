<template>
  <section class="configuration-page credential-page">
    <div class="page-heading">
      <div>
        <p>Secure configuration</p>
        <h1>Credentials</h1>
        <p>
          Create write-only credentials for point sources. Saved passwords and tokens cannot be
          viewed again.
        </p>
      </div>
    </div>

    <div
      v-if="error"
      ref="errorSummary"
      class="request-error error-summary"
      role="alert"
      tabindex="-1"
    >
      <strong>There is a problem</strong><span>{{ error }}</span>
    </div>
    <p class="visually-hidden" role="status" aria-live="polite">{{ status }}</p>

    <div class="credential-layout">
      <section aria-labelledby="saved-credentials-heading">
        <div class="section-heading">
          <div>
            <h2 id="saved-credentials-heading">Saved credentials</h2>
            <p>Use the displayed reference in point-source YAML.</p>
          </div>
          <AppButton
            automation="credential-new"
            text="New credential"
            :icon="createIcon"
            @click="openCreateDialog"
          />
        </div>
        <p v-if="loading" role="status">Loading credentials…</p>
        <p v-else-if="credentials.length === 0" class="empty-state">
          No credentials have been created.
        </p>
        <table v-else>
          <caption class="visually-hidden">
            Saved credential references
          </caption>
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Type</th>
              <th scope="col">Reference</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="credential in credentials" :key="credential.id">
              <th scope="row">
                {{ credential.name
                }}<small v-if="credential.username">Username: {{ credential.username }}</small>
              </th>
              <td>{{ credential.kind === 'mqtt' ? 'MQTT login' : 'API token' }}</td>
              <td>
                <code>secret://{{ credential.id }}</code>
              </td>
              <td>
                <AppButton
                  :automation="`credential-edit-${credential.id}`"
                  text="Edit"
                  :icon="editIcon"
                  @click="openEditDialog(credential)"
                />
              </td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>
  </section>

  <AppDialog
    id="credential-form-dialog"
    ref="credentialDialog"
    :content-label="editing ? `Edit ${form.name}` : 'Create new credential'"
    automation="credential-dialog"
    :dismissible="false"
    @cancel="handleCredentialDialogCancel"
  >
    <AppForm class="credential-form" automation="credential-form" @submit.prevent="save">
      <p>{{ editing ? 'Update credential' : 'New credential' }}</p>
      <h2>{{ editing ? `Edit ${form.name}` : 'Credential details' }}</h2>
      <label for="credential-name">Display name</label>
      <input id="credential-name" v-model="form.name" required autocomplete="off" />
      <label for="credential-id">Reference ID</label>
      <input
        id="credential-id"
        v-model="form.id"
        required
        pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
        :disabled="editing"
        autocomplete="off"
      />
      <p class="field-help">
        Point sources refer to this as <code>secret://{{ form.id || 'reference-id' }}</code
        >.
      </p>
      <label for="credential-kind">Credential type</label>
      <select id="credential-kind" v-model="form.kind" :disabled="editing">
        <option value="mqtt">MQTT username and password</option>
        <option value="token">API bearer token</option>
      </select>
      <template v-if="form.kind === 'mqtt'">
        <label for="credential-username">Username</label>
        <input id="credential-username" v-model="form.username" required autocomplete="username" />
        <label for="credential-password">{{ editing ? 'Replacement password' : 'Password' }}</label>
        <div class="secret-input">
          <input
            id="credential-password"
            v-model="form.password"
            :type="passwordVisible ? 'text' : 'password'"
            :required="!editing"
            autocomplete="new-password"
          />
          <AppButton
            v-if="form.password"
            automation="credential-password-visibility"
            class="secret-visibility-button"
            :text="passwordVisible ? 'Hide password' : 'Show password'"
            :icon="visibilityIcon"
            hide-text
            :aria-pressed="passwordVisible"
            @click="passwordVisible = !passwordVisible"
          />
        </div>
        <p v-if="editing" class="field-help">Leave blank to keep the existing password.</p>
      </template>
      <template v-else>
        <label for="credential-token">{{ editing ? 'Replacement token' : 'Token' }}</label>
        <div class="secret-input">
          <input
            id="credential-token"
            v-model="form.token"
            :type="tokenVisible ? 'text' : 'password'"
            :required="!editing"
            autocomplete="new-password"
          />
          <AppButton
            v-if="form.token"
            automation="credential-token-visibility"
            class="secret-visibility-button"
            :text="tokenVisible ? 'Hide token' : 'Show token'"
            :icon="visibilityIcon"
            hide-text
            :aria-pressed="tokenVisible"
            @click="tokenVisible = !tokenVisible"
          />
        </div>
        <p v-if="editing" class="field-help">Leave blank to keep the existing token.</p>
      </template>
      <p class="secret-notice">
        Sensitive values are encrypted when saved and are never returned to this screen.
      </p>
      <div class="editor-actions">
        <AppButton
          automation="credential-save"
          type="submit"
          :text="saving ? 'Saving…' : editing ? 'Save changes' : 'Create credential'"
          :icon="editing ? saveIcon : createIcon"
          :disabled="saving"
        />
        <AppButton
          automation="credential-cancel"
          text="Cancel"
          :icon="cancelIcon"
          :disabled="saving"
          @click="cancelCredentialDialog"
        />
        <AppButton
          v-if="editing"
          automation="credential-delete"
          text="Delete"
          :icon="deleteIcon"
          @click="remove"
        />
      </div>
    </AppForm>
  </AppDialog>

  <AppPromptDialog
    id="credential-discard-dialog"
    ref="credentialDiscardDialog"
    content-label="Discard unsaved credential changes"
    automation="credential-discard-dialog"
    message="Your credential changes have not been saved and will be lost."
    @cancel="keepEditing"
    @confirm="discardCredentialChanges"
  />
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import {
  credentialApi,
  type CredentialInput,
  type CredentialKind,
  type CredentialMetadata
} from '@/features/credentials/api/credentialApi';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import createIcon from '@/assets/icons/new-flow-icon.svg';
import editIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import visibilityIcon from '@/assets/icons/visibility-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import AppForm from '@/components/AppForm.vue';
import AppPromptDialog from '@/components/AppPromptDialog.vue';

const credentials = ref<CredentialMetadata[]>([]);
const loading = ref(false);
const saving = ref(false);
const editing = ref(false);
const error = ref('');
const status = ref('');
const passwordVisible = ref(false);
const tokenVisible = ref(false);
const errorSummary = ref<HTMLElement>();
const credentialDialog = ref<InstanceType<typeof AppDialog>>();
const credentialDiscardDialog = ref<InstanceType<typeof AppPromptDialog>>();
let controller: AbortController | undefined;
const form = reactive<CredentialInput>({
  id: '',
  name: '',
  kind: 'mqtt',
  username: '',
  password: '',
  token: ''
});
const formBaseline = ref('');
const formSnapshot = (): string => JSON.stringify(form);
const formDirty = computed(() => formSnapshot() !== formBaseline.value);
const discardConfirmationOpen = ref(false);

watch(error, async (value) => {
  if (value) {
    await nextTick();
    errorSummary.value?.focus();
  }
});

watch(
  () => form.password,
  (value) => {
    if (!value) passwordVisible.value = false;
  }
);

watch(
  () => form.token,
  (value) => {
    if (!value) tokenVisible.value = false;
  }
);

const resetSecrets = (): void => {
  form.password = '';
  form.token = '';
  passwordVisible.value = false;
  tokenVisible.value = false;
};

const closeCredentialDialog = (): void => {
  credentialDialog.value?.close();
};

const beginCreate = (): void => {
  editing.value = false;
  Object.assign(form, {
    id: '',
    name: '',
    kind: 'mqtt' satisfies CredentialKind,
    username: '',
    revision: undefined
  });
  resetSecrets();
  error.value = '';
  formBaseline.value = formSnapshot();
};

const beginEdit = (credential: CredentialMetadata): void => {
  editing.value = true;
  Object.assign(form, {
    id: credential.id,
    name: credential.name,
    kind: credential.kind,
    username: credential.username ?? '',
    revision: credential.revision
  });
  resetSecrets();
  error.value = '';
  formBaseline.value = formSnapshot();
};

const openCreateDialog = async (): Promise<void> => {
  beginCreate();
  await nextTick();
  credentialDialog.value?.showModal();
};

const openEditDialog = async (credential: CredentialMetadata): Promise<void> => {
  beginEdit(credential);
  await nextTick();
  credentialDialog.value?.showModal();
};

const cancelCredentialDialog = (): void => {
  if (formDirty.value) {
    if (discardConfirmationOpen.value) return;
    discardConfirmationOpen.value = true;
    credentialDiscardDialog.value?.showModal();
    return;
  }

  closeCredentialDialog();
  beginCreate();
};

const handleCredentialDialogCancel = (): void => {
  cancelCredentialDialog();
};

const keepEditing = (): void => {
  discardConfirmationOpen.value = false;
};

const discardCredentialChanges = (): void => {
  discardConfirmationOpen.value = false;
  closeCredentialDialog();
  beginCreate();
};

const load = async (): Promise<void> => {
  controller?.abort();
  controller = new AbortController();
  loading.value = true;
  try {
    credentials.value = await credentialApi.list(controller.signal);
  } catch (reason) {
    if (!controller.signal.aborted)
      error.value = reason instanceof Error ? reason.message : 'Unable to load credentials';
  } finally {
    loading.value = false;
  }
};

const save = async (): Promise<void> => {
  saving.value = true;
  error.value = '';
  try {
    const saved = editing.value
      ? await credentialApi.update({ ...form })
      : await credentialApi.create({ ...form });
    status.value = editing.value
      ? 'Credential updated. Sensitive values remain hidden.'
      : 'Credential created. Sensitive values are now hidden.';
    await load();
    beginEdit(saved);
    closeCredentialDialog();
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to save credential';
  } finally {
    saving.value = false;
    resetSecrets();
  }
};

const remove = async (): Promise<void> => {
  if (!form.revision || !window.confirm(`Delete credential “${form.name}”?`)) return;
  try {
    await credentialApi.delete(form.id, form.revision);
    status.value = 'Credential deleted.';
    beginCreate();
    await load();
    closeCredentialDialog();
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to delete credential';
  }
};

onMounted(() => void load());
onBeforeUnmount(() => controller?.abort());
</script>

<style lang="css" scoped>
.credential-form :deep(> form) {
  display: grid;
  padding: 22px;
  background: var(--color-surface-subtle);
  border: 1px solid var(--color-border-default);
  border-radius: 12px;
}

.credential-form :deep(h2) {
  margin: 5px 0 20px;
}

.credential-form :deep(form label) {
  margin: 13px 0 6px;
  font-weight: 700;
}

.credential-form :deep(input),
.credential-form :deep(select) {
  min-width: 0;
  padding: 10px 11px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 7px;
}
</style>
