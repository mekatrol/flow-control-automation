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
    },
    {
      path: '/points',
      name: 'points',
      component: () => import('@/features/catalogues/views/PointsCatalogueView.vue')
    },
    {
      path: '/point-groups',
      name: 'point-groups',
      component: () => import('@/features/catalogues/views/PointGroupsCatalogueView.vue')
    },
    {
      path: '/controller-templates',
      name: 'controller-templates',
      component: () => import('@/features/catalogues/views/ControllerTemplatesCatalogueView.vue')
    },
    {
      path: '/point-sources',
      name: 'point-sources',
      component: () => import('@/features/pointSources/views/PointSourceListView.vue')
    },
    {
      path: '/point-sources/new',
      name: 'point-source-new',
      component: () => import('@/features/pointSources/views/PointSourceEditorView.vue')
    },
    {
      path: '/point-sources/:sourceId',
      name: 'point-source-detail',
      component: () => import('@/features/pointSources/views/PointSourceEditorView.vue'),
      props: (route) => ({ sourceId: String(route.params.sourceId) })
    },
    {
      path: '/credentials',
      name: 'credentials',
      component: () => import('@/features/credentials/views/CredentialManagerView.vue')
    }
  ]
});

export default router;
