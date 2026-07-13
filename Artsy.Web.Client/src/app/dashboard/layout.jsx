import React from 'react';
import Sidebar from '@/components/layout/Sidebar';

export default function DashboardLayout({ children }) {
  return (
    <div className="flex min-h-screen bg-gray-50 dark:bg-gray-900 text-gray-900 dark:text-gray-100">
      <Sidebar />
      <main className="flex-1 p-8">
        {children}
      </main>
    </div>
  );
}
