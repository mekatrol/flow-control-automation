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
      component: () => import('@/features/flows/views/AppFlowListView.vue')
    },
    {
      path: '/flows/:flowId',
      name: 'flow-designer',
      component: () => import('@/features/flows/views/AppFlowDesignerView.vue'),
      // Convert the route parameter at the boundary so the view receives a plain
      // string prop and does not need to understand router parameter shapes.
      props: (route) => ({ flowId: String(route.params.flowId) })
    },
    {
      path: '/points',
      name: 'points',
      component: () => import('@/features/catalogues/views/AppPointsCatalogueView.vue')
    },
    {
      path: '/points/new',
      name: 'point-new',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: { kind: 'point' }
    },
    {
      path: '/points/:resourceId',
      name: 'point-detail',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: (route) => ({ kind: 'point', resourceId: String(route.params.resourceId) })
    },
    {
      path: '/point-groups',
      name: 'point-groups',
      component: () => import('@/features/catalogues/views/AppPointGroupsCatalogueView.vue')
    },
    {
      path: '/point-groups/new',
      name: 'point-group-new',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: { kind: 'group' }
    },
    {
      path: '/point-groups/:resourceId',
      name: 'point-group-detail',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: (route) => ({ kind: 'group', resourceId: String(route.params.resourceId) })
    },
    {
      path: '/controller-templates',
      name: 'controller-templates',
      component: () => import('@/features/catalogues/views/AppControllerTemplatesCatalogueView.vue')
    },
    {
      path: '/controller-templates/new',
      name: 'controller-template-new',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: { kind: 'controller' }
    },
    {
      path: '/controller-templates/:resourceId',
      name: 'controller-template-detail',
      component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
      props: (route) => ({ kind: 'controller', resourceId: String(route.params.resourceId) })
    },
    {
      path: '/point-sources',
      name: 'point-sources',
      component: () => import('@/features/pointSources/views/AppPointSourceListView.vue')
    },
    {
      path: '/point-sources/new',
      name: 'point-source-new',
      component: () => import('@/features/pointSources/views/AppPointSourceEditorView.vue')
    },
    {
      path: '/point-sources/:sourceId',
      name: 'point-source-detail',
      component: () => import('@/features/pointSources/views/AppPointSourceEditorView.vue'),
      props: (route) => ({ sourceId: String(route.params.sourceId) })
    },
    {
      path: '/credentials',
      name: 'credentials',
      component: () => import('@/features/credentials/views/AppCredentialManagerView.vue')
    }
  ]
});

export default router;
