import { createRouter, createWebHistory } from 'vue-router';

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: { name: 'flows' }
    },
    {
      path: '/flows',
      name: 'flows',
      component: () => import('@/features/flows/views/FlowListView.vue')
    },
    {
      path: '/flows/:flowId',
      name: 'flow-designer',
      component: () => import('@/features/flows/views/FlowDesignerView.vue'),
      props: (route) => ({ flowId: String(route.params.flowId) })
    }
  ]
});

export default router;
