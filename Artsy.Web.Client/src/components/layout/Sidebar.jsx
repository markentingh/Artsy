import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useSession } from '@/context/session';
import { useDashboard } from '@/context/dashboard';
import ThemeToggle from '@/components/ui/theme-toggle';

export default function Sidebar() {
  const { logout, user } = useSession();
  const { tokens: availableTokens } = useDashboard();
  const location = useLocation();

  const isAdmin = user?.roles?.includes('admin') ?? false;

  const navItems = [
    { path: '/dashboard', label: 'Projects', match: ['/dashboard/projects', '/dashboard/project'] },
    { path: '/dashboard/orders', label: 'Orders' },
    { path: '/dashboard/connections', label: 'Connections' },
    ...(isAdmin ? [
      { path: '/dashboard/printify', label: 'Printify' },
      { path: '/dashboard/openai', label: 'OpenAI' },
      { path: '/dashboard/services', label: 'Services' },
      { path: '/dashboard/billing', label: 'Billing' },
      { path: '/dashboard/users', label: 'Users' },
      { path: '/dashboard/hangfire', label: 'Hangfire' }
    ] : [])
  ];

  return (
    <aside className="w-64 h-screen fixed left-0 top-0 flex flex-col bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700">
      <div className="p-4 border-b border-gray-200 dark:border-gray-700">
        <Link to="/">
          <img src="/logo-inline.svg" alt="Artsy" className="h-10 mb-4 w-auto" />
        </Link>
        {user && <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">{user.displayName}</p>}
      </div>
      <nav className="flex-1 p-4">
        <ul className="space-y-2">
          {navItems.map((item) => {
            const isActive = item.match
              ? item.match.some((path) => location.pathname === path || location.pathname.startsWith(`${path}/`))
              : location.pathname === item.path || location.pathname.startsWith(`${item.path}/`);
            return (
              <li key={item.path}>
                <Link
                  to={item.path}
                  className={`block px-4 py-2 rounded transition ${isActive
                      ? 'bg-primary-100 text-primary-700 dark:bg-primary-900 dark:text-primary-300'
                      : 'hover:bg-gray-100 dark:hover:bg-gray-700'
                    }`}
                >
                  {item.label}
                </Link>
              </li>
            );
          })}
          <li>
            <button
              onClick={logout}
              className="w-full py-2 px-4 text-left text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition"
            >
              Log out
            </button>
          </li>
        </ul>
      </nav>
      <div className="p-4 border-t border-gray-200 dark:border-gray-700">
        {availableTokens != null && (
          <div className="mb-4 text-sm">
            <span className="text-gray-600 dark:text-gray-400">Available Tokens: </span>
            <span className="font-semibold text-primary-600 dark:text-primary-500">{availableTokens.toLocaleString()}</span>
          </div>
        )}
        <div className="mb-4">
          <ThemeToggle />
        </div>
      </div>
    </aside>
  );
}
