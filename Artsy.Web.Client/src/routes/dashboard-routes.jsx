import { lazy } from 'react';

const routes = [
  { path: '/dashboard',            Element: lazy(() => import('@/app/dashboard/projects/page')) },
  { path: '/dashboard/projects',    Element: lazy(() => import('@/app/dashboard/projects/page')) },
  { path: '/dashboard/project/:projectId', Element: lazy(() => import('@/app/dashboard/project/page')) },
  { path: '/dashboard/orders',            Element: lazy(() => import('@/app/dashboard/orders/page')) },
  { path: '/dashboard/printify',           Element: lazy(() => import('@/app/dashboard/printify/page')) },
  { path: '/dashboard/users',      Element: lazy(() => import('@/app/dashboard/users/page')) },
  { path: '/dashboard/connections', Element: lazy(() => import('@/app/dashboard/connections/page')) },
  { path: '/dashboard/services',    Element: lazy(() => import('@/app/dashboard/services/page')) },
  { path: '/dashboard/openai',      Element: lazy(() => import('@/app/dashboard/openai/page')) },
  { path: '/dashboard/billing',     Element: lazy(() => import('@/app/dashboard/billing/page')) },
  { path: '/dashboard/hangfire',    Element: lazy(() => import('@/app/dashboard/hangfire/page')) }
];

export default routes;
