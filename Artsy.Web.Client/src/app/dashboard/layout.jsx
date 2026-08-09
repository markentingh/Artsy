import React from 'react';
import Sidebar from '@/components/layout/Sidebar';
import { DashboardProvider } from '@/context/dashboard';

export default function DashboardLayout({ children }) {
  return (
    <DashboardProvider>
      <div className="flex min-h-screen bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
        <Sidebar />
        <main className="flex-1 min-w-0 p-8 ml-64">
          {children}
        </main>
      </div>
    </DashboardProvider>
  );
}
