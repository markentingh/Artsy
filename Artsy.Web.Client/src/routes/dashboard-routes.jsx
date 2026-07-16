import { lazy } from 'react';

const routes = [
  { path: '/dashboard',            Element: lazy(() => import('@/app/dashboard/projects/page')) },
  { path: '/dashboard/projects',    Element: lazy(() => import('@/app/dashboard/projects/page')) },
  { path: '/dashboard/project/:projectId', Element: lazy(() => import('@/app/dashboard/project/page')) },
  { path: '/dashboard/users',      Element: lazy(() => import('@/app/dashboard/users/page')) },
  { path: '/dashboard/connections', Element: lazy(() => import('@/app/dashboard/connections/page')) },
  { path: '/dashboard/services',    Element: lazy(() => import('@/app/dashboard/services/page')) },
  { path: '/dashboard/openai',      Element: lazy(() => import('@/app/dashboard/openai/page')) }
];

export default routes;
