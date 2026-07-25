<template>
  <section class="configuration-page credential-page">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Secure configuration</p>
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
          <button type="button" @click="beginCreate">New credential</button>
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
              <td><button type="button" @click="beginEdit(credential)">Edit</button></td>
            </tr>
          </tbody>
        </table>
      </section>

      <form class="credential-form" @submit.prevent="save">
        <p class="eyebrow">{{ editing ? 'Update credential' : 'New credential' }}</p>
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
          <input
            id="credential-username"
            v-model="form.username"
            required
            autocomplete="username"
          />
          <label for="credential-password">{{
            editing ? 'Replacement password' : 'Password'
          }}</label>
          <input
            id="credential-password"
            v-model="form.password"
            type="password"
            :required="!editing"
            autocomplete="new-password"
          />
          <p v-if="editing" class="field-help">Leave blank to keep the existing password.</p>
        </template>
        <template v-else>
          <label for="credential-token">{{ editing ? 'Replacement token' : 'Token' }}</label>
          <input
            id="credential-token"
            v-model="form.token"
            type="password"
            :required="!editing"
            autocomplete="new-password"
          />
          <p v-if="editing" class="field-help">Leave blank to keep the existing token.</p>
        </template>
        <p class="secret-notice">
          Sensitive values are encrypted when saved and are never returned to this screen.
        </p>
        <div class="editor-actions">
          <button type="submit" :disabled="saving">
            {{ saving ? 'Saving…' : editing ? 'Save changes' : 'Create credential' }}
          </button>
          <button v-if="editing" type="button" @click="beginCreate">Cancel</button>
          <button v-if="editing" type="button" class="danger-button" @click="remove">Delete</button>
        </div>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import {
  credentialApi,
  type CredentialInput,
  type CredentialKind,
  type CredentialMetadata
} from '@/features/credentials/api/credentialApi';

const credentials = ref<CredentialMetadata[]>([]);
const loading = ref(false);
const saving = ref(false);
const editing = ref(false);
const error = ref('');
const status = ref('');
const errorSummary = ref<HTMLElement>();
let controller: AbortController | undefined;
const form = reactive<CredentialInput>({
  id: '',
  name: '',
  kind: 'mqtt',
  username: '',
  password: '',
  token: ''
});
watch(error, async (value) => {
  if (value) {
    await nextTick();
    errorSummary.value?.focus();
  }
});
const resetSecrets = (): void => {
  form.password = '';
  form.token = '';
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
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to delete credential';
  }
};
onMounted(() => void load());
onBeforeUnmount(() => controller?.abort());
</script>
