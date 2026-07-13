import { lazy } from 'react';

const routes = [
  { path: '/dashboard',            Element: lazy(() => import('@/app/dashboard/home')) },
  { path: '/dashboard/users',      Element: lazy(() => import('@/app/dashboard/users')) },
  { path: '/dashboard/connections', Element: lazy(() => import('@/app/dashboard/connections')) }
];

export default routes;
