<template>
  <form v-if="!authenticated" class="api-login" @submit.prevent="authenticate">
    <h1>Flow Control</h1>
    <label
      ><span>API key</span
      ><input v-model="apiKey" type="password" autocomplete="current-password" required
    /></label>
    <button type="submit">Continue</button>
    <p v-if="authenticationError" role="alert">{{ authenticationError }}</p>
  </form>
  <div v-else class="app-content" :inert="isWaiting || undefined">
    <RouterView />
  </div>
  <AppSpinnerOverlay />
</template>

<script setup lang="ts">
import AppSpinnerOverlay from '@/components/AppSpinnerOverlay.vue';
import { useWait } from '@/composables/useWait';
import { getApiKey, removeStoredApiKey, storeApiKey } from '@/config/apiAccess';
import { ref } from 'vue';

const { isWaiting } = useWait();
const authenticated = ref(Boolean(getApiKey()));
const apiKey = ref('');
const authenticationError = ref('');
const authenticate = async (): Promise<void> => {
  storeApiKey(apiKey.value);
  const response = await fetch('/api/execution-contexts', {
    headers: { 'X-Api-Key': apiKey.value }
  });
  if (response.ok) {
    authenticated.value = true;
    authenticationError.value = '';
  } else {
    removeStoredApiKey();
    authenticationError.value =
      response.status === 401 ? 'The API key is invalid.' : 'The API key does not have access.';
  }
};
</script>

<style scoped>
.api-login {
  max-width: 24rem;
  margin: 15vh auto;
  display: grid;
  gap: 1rem;
}
.api-login label {
  display: grid;
  gap: 0.4rem;
}
</style>
