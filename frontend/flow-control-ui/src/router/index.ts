import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';

import AppLayout from '@/layouts/AppLayout.vue';
import { WorkspaceMode } from '@/features/flows/types/flowDesigner';

export const ROUTE_NAMES = {
  home: 'home',
  flows: 'flows',
  flowDesigner: 'flow-designer',
  flowSimulator: 'flow-simulator',
  flowDebugger: 'flow-debugger',
  points: 'points',
  pointNew: 'point-new',
  pointDetail: 'point-detail',
  controllerTemplates: 'controller-templates',
  controllerTemplateNew: 'controller-template-new',
  controllerTemplateDetail: 'controller-template-detail',
  pointSources: 'point-sources',
  pointSourceNew: 'point-source-new',
  pointSourceDetail: 'point-source-detail',
  credentials: 'credentials'
};

export type RouteName = (typeof ROUTE_NAMES)[keyof typeof ROUTE_NAMES];

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: AppLayout,
    children: [
      {
        path: '',
        name: ROUTE_NAMES.home,
        redirect: { name: ROUTE_NAMES.flows }
      },
      {
        path: 'flows',
        name: ROUTE_NAMES.flows,
        // Lazy view imports keep designer-only code out of the initial flow-list
        // download and let the browser fetch it when the route is first visited.
        component: () => import('@/features/flows/views/AppFlowListView.vue')
      },
      {
        path: 'flows/:flowId',
        redirect: (route) => ({
          name: ROUTE_NAMES.flowDesigner,
          params: { flowId: route.params.flowId }
        })
      },
      {
        path: 'flows/:flowId/design',
        name: ROUTE_NAMES.flowDesigner,
        component: () => import('@/features/flows/views/AppFlowDesignerView.vue'),
        // Convert the route parameter at the boundary so the view receives a plain
        // string prop and does not need to understand router parameter shapes.
        props: (route) => ({
          flowId: String(route.params.flowId),
          workspaceMode: WorkspaceMode.Design
        })
      },
      {
        path: 'flows/:flowId/simulator',
        name: ROUTE_NAMES.flowSimulator,
        component: () => import('@/features/flows/views/AppFlowDesignerView.vue'),
        props: (route) => ({
          flowId: String(route.params.flowId),
          workspaceMode: WorkspaceMode.Simulator
        })
      },
      {
        path: 'flows/:flowId/debugger',
        name: ROUTE_NAMES.flowDebugger,
        component: () => import('@/features/flows/views/AppFlowDesignerView.vue'),
        props: (route) => ({
          flowId: String(route.params.flowId),
          workspaceMode: WorkspaceMode.Debugger
        })
      },
      {
        path: 'points',
        name: ROUTE_NAMES.points,
        component: () => import('@/features/catalogues/views/AppPointsCatalogueView.vue')
      },
      {
        path: 'points/new',
        name: ROUTE_NAMES.pointNew,
        component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
        props: { kind: 'point' }
      },
      {
        path: 'points/:resourceId',
        name: ROUTE_NAMES.pointDetail,
        component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
        props: (route) => ({ kind: 'point', resourceId: String(route.params.resourceId) })
      },
      {
        path: 'controller-templates',
        name: ROUTE_NAMES.controllerTemplates,
        component: () =>
          import('@/features/catalogues/views/AppControllerTemplatesCatalogueView.vue')
      },
      {
        path: 'controller-templates/new',
        name: ROUTE_NAMES.controllerTemplateNew,
        component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
        props: { kind: 'controller' }
      },
      {
        path: 'controller-templates/:resourceId',
        name: ROUTE_NAMES.controllerTemplateDetail,
        component: () => import('@/features/configuration/views/AppYamlResourceEditorView.vue'),
        props: (route) => ({ kind: 'controller', resourceId: String(route.params.resourceId) })
      },
      {
        path: 'point-sources',
        name: ROUTE_NAMES.pointSources,
        component: () => import('@/features/pointSources/views/AppPointSourceListView.vue')
      },
      {
        path: 'point-sources/new',
        name: ROUTE_NAMES.pointSourceNew,
        component: () => import('@/features/pointSources/views/AppPointSourceEditorView.vue')
      },
      {
        path: 'point-sources/:sourceId',
        name: ROUTE_NAMES.pointSourceDetail,
        component: () => import('@/features/pointSources/views/AppPointSourceEditorView.vue'),
        props: (route) => ({ sourceId: String(route.params.sourceId) })
      },
      {
        path: 'credentials',
        name: ROUTE_NAMES.credentials,
        component: () => import('@/features/credentials/views/AppCredentialManagerView.vue')
      }
    ]
  }
];

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: routes
});

export default router;
