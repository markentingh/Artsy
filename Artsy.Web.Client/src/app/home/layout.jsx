import React, { useState, useEffect } from 'react';
import Navbar from '@/components/layout/Navbar';

export default function HomeLayout({ children }) {
  const [showOnScroll, setShowOnScroll] = useState(0);

  useEffect(() => {
    const update = () => setShowOnScroll(Math.floor(window.innerHeight * 0.8));
    update();
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

  return (
    <>
      <Navbar showOnScroll={showOnScroll} />
      {children}
    </>
  );
}
