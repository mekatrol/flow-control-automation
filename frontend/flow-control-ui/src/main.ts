import { createApp } from 'vue';
import { createPinia } from 'pinia';

import App from './App.vue';
import router from './router';

import './assets/styles/theme.css';
import './assets/styles/reset.css';
import './assets/styles/popover.css';
import './assets/styles/layout.css';
import './assets/styles/main.css';

const app = createApp(App);

app.use(createPinia());
app.use(router);

app.mount('#app');
