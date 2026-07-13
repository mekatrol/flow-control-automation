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
      // Lazy view imports keep designer-only code out of the initial flow-list
      // download and let the browser fetch it when the route is first visited.
      component: () => import('@/features/flows/views/FlowListView.vue')
    },
    {
      path: '/flows/:flowId',
      name: 'flow-designer',
      component: () => import('@/features/flows/views/FlowDesignerView.vue'),
      // Convert the route parameter at the boundary so the view receives a plain
      // string prop and does not need to understand router parameter shapes.
      props: (route) => ({ flowId: String(route.params.flowId) })
    }
  ]
});

export default router;
