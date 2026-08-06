import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';

export default function Navbar({ showOnScroll }) {
  const [isVisible, setIsVisible] = useState(showOnScroll === undefined);

  useEffect(() => {
    if (showOnScroll === undefined) {
      setIsVisible(true);
      return;
    }

    const onScroll = () => setIsVisible(window.scrollY > showOnScroll);
    window.addEventListener('scroll', onScroll);
    onScroll();
    return () => window.removeEventListener('scroll', onScroll);
  }, [showOnScroll]);

  return (
    <nav
      className={`fixed top-0 left-0 w-full z-50 transition-transform duration-300 ${
        isVisible ? 'translate-y-0' : '-translate-y-full'
      } bg-slate-950/90 backdrop-blur-md border-b border-white/10`}
    >
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
        <Link to="/">
          <img src="/logo-inline.svg" alt="artship.ai" className="h-8 w-auto" />
        </Link>

        <div className="flex items-center gap-6">
          <div className="hidden md:flex items-center gap-6 text-sm font-medium">
            <Link to="/how-it-works" className="text-white/80 hover:text-white transition">How It Works</Link>
            <Link to="/about" className="text-white/80 hover:text-white transition">About</Link>
            <Link to="/contact" className="text-white/80 hover:text-white transition">Contact Us</Link>
          </div>

          <div className="flex items-center gap-3">
            <Link to="/login" className="px-4 py-2 text-sm text-white/80 hover:text-white transition">Log In</Link>
            <Link
              to="/subscriptions"
              className="px-4 py-2 text-sm bg-white text-slate-950 rounded-full font-medium hover:bg-white/90 transition"
            >
              Sign Up
            </Link>
          </div>
        </div>
      </div>
    </nav>
  );
}
