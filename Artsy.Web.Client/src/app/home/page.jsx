import React, { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useSession } from '@/context/session';
import ShinyImage from '@/components/ui/shiny-image';

function LearnMoreButton() {
  return (
    <Link
      to="/learn-more"
      className="inline-flex items-center justify-center px-6 py-3 border border-white/40 text-white rounded-full hover:bg-white/10 transition"
    >
      Learn More
    </Link>
  );
}

function Placeholder({ label }) {
  return (
    <div className="h-full min-h-[20rem] bg-white/5 border-2 border-dashed border-white/20 rounded-2xl flex items-center justify-center text-white/40 text-sm">
      {label}
    </div>
  );
}

const makeGlow = (h, v) =>
  `radial-gradient(circle at ${h} ${v}%, rgba(109,40,217,0.9) 0%, rgba(91,33,182,0.5) 30%, rgba(76,29,149,0.2) 55%, transparent 85%)`;

const PurpleGlow = React.forwardRef(({ position, vertical = 50 }, ref) => {
  return (
    <div
      ref={ref}
      className="absolute inset-0 pointer-events-none"
      style={{ backgroundImage: makeGlow(position, vertical) }}
    />
  );
});

export default function Home() {
  const { isAuthenticated } = useSession();
  const logoRef = useRef(null);
  const heroTextRef = useRef(null);
  const glow1Ref = useRef(null);
  const glow2Ref = useRef(null);
  const heroRef = useRef(null);
  const [showTopLink, setShowTopLink] = useState(true);

  useEffect(() => {
    const onScroll = () => {
      const y = window.scrollY;
      if (logoRef.current) {
        logoRef.current.style.transform = `translateY(${y * 0.5}px)`;
      }
      if (heroTextRef.current) {
        heroTextRef.current.style.transform = `translateY(${y * 0.15}px)`;
      }
      if (glow1Ref.current) {
        const v = Math.min(100, Math.max(0, 50 - y * 0.02));
        glow1Ref.current.style.backgroundImage = makeGlow('60%', v);
      }
      if (glow2Ref.current) {
        const v = Math.min(100, Math.max(0, 50 - y * 0.03));
        glow2Ref.current.style.backgroundImage = makeGlow('100%', v);
      }
    };
    window.addEventListener('scroll', onScroll);
    onScroll();
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    if (!heroRef.current) return;
    const observer = new IntersectionObserver(
      ([entry]) => setShowTopLink(entry.isIntersecting),
      { threshold: 0 }
    );
    observer.observe(heroRef.current);
    return () => observer.disconnect();
  }, []);

  return (
    <div className="font-serif bg-slate-950 text-white">
      <section
        ref={heroRef}
        className="relative min-h-screen overflow-hidden bg-cover bg-center bg-no-repeat"
        style={{
          backgroundImage: 'url(/home/hero-01.webp)'
        }}
      >
        <div
          className={`fixed top-6 right-6 z-20 transition-opacity duration-300 ${
            showTopLink ? 'opacity-100' : 'opacity-0 pointer-events-none'
          }`}>
          <Link
            to={isAuthenticated ? '/dashboard' : '/login'}
            className="font-medium text-white/80 hover:text-white transition"
          >
            {isAuthenticated ? 'Dashboard' : 'Log In'}
          </Link>
        </div>
        <div className="relative z-10 max-w-7xl mx-auto px-6 min-h-screen flex flex-col">
          <div className="flex-1 flex items-center justify-center ml-20">
            <img
              ref={logoRef}
              src="/logo-inline.svg"
              alt="artship.ai"
              className="w-3/4 sm:w-1/2 md:w-1/3 h-auto"
            />
          </div>

          <div className="flex-1 flex items-center justify-end">
            <div
              ref={heroTextRef}
              className="w-full lg:w-1/2 bg-slate-950/30 backdrop-blur-md rounded-2xl p-6 md:p-8 border border-white/10"
            >
              <h1 className="text-4xl sm:text-6xl md:text-7xl font-semibold leading-tight tracking-tight mb-6">
              Where Creative Ideas Become a Scalable Business
            </h1>
            <p className="text-lg md:text-xl text-white/80 leading-relaxed mb-8 max-w-xl">
              The intelligent creative suite for entrepreneurs. Generate, publish, and promote
              custom products from one place — built for creators who want to turn their vision
              into commerce without the busywork.
            </p>
            <div className="flex flex-wrap items-center gap-4">
              <Link
                to="/subscriptions"
                className="inline-flex items-center justify-center px-8 py-3 bg-white text-slate-950 rounded-full font-medium hover:bg-white/90 transition"
              >
                Sign Up
              </Link>
              <LearnMoreButton />
            </div>
          </div>
        </div>
      </div>
    </section>

      <section className="relative py-24 lg:py-32 bg-slate-950 overflow-hidden border-t border-white/5">


        <div className="relative z-10 max-w-7xl mx-auto px-6 grid lg:grid-cols-2 gap-12 items-center">
          <ShinyImage
            src="/home/flow-diagram.png"
            alt="Concept to campaign workflow"
            className="w-full h-auto rounded-2xl overflow-hidden"
          />
          <div className="lg:pl-12">
            <h2 className="text-2xl sm:text-4xl md:text-5xl font-semibold tracking-tight mb-6">
              From Concept to Campaign on Autopilot
            </h2>
            <p className="text-lg text-white/80 leading-relaxed mb-8 max-w-xl">
              artship.ai automates the entire journey: generate original artwork, publish product
              collections, and schedule social media posts from a single workflow. Every step is
              connected, so you can move from idea to launched campaign in minutes instead of days.
            </p>
            <LearnMoreButton />
          </div>
        </div>
      </section>

      <section className="relative py-24 lg:py-32 bg-slate-950 overflow-hidden border-t border-white/5">
        <PurpleGlow ref={glow1Ref} position="60%" />

        <div className="relative z-10 max-w-7xl mx-auto px-6 grid lg:grid-cols-2 gap-12 items-center">
          <div className="flex justify-center w-full mb-8">
            <img src="/home/printify-logo.svg" alt="Printify" className="w-1/2 sm:w-2/5 md:w-1/4 h-auto rounded-2xl" />
          </div>
          <div className="lg:pl-12">
            <h2 className="text-2xl sm:text-4xl md:text-5xl font-semibold tracking-tight mb-6">
              1,790+ Premium Products at Your Fingertips
            </h2>
            <p className="text-lg text-white/80 leading-relaxed mb-8 max-w-xl">
              We leverage the Printify catalog of over 1,790 printable, high-quality products.
              From apparel and home goods to accessories and stationery, choose from a global
              network of print providers and publish directly to your online store — no inventory,
              no upfront costs.
            </p>
            <div className="flex flex-wrap items-center gap-4">
              <LearnMoreButton />
              <a
                href="https://printify.com"
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center px-6 py-3 border border-white/40 text-white rounded-full hover:bg-white/10 transition"
              >
                Visit Printify
              </a>
            </div>
          </div>
        </div>
      </section>

      <section className="relative py-24 lg:py-32 bg-slate-950 overflow-hidden border-t border-white/5">


        <div className="relative z-10 max-w-7xl mx-auto px-6 grid lg:grid-cols-2 gap-12 items-center">
          <Placeholder label="AI artwork placeholder" />
          <div className="lg:pl-12">
            <h2 className="text-2xl sm:text-4xl md:text-5xl font-semibold tracking-tight mb-6">
              Generate Any Artwork You Can Imagine
            </h2>
            <p className="text-lg text-white/80 leading-relaxed mb-8 max-w-xl">
              Use the latest AI image models on the market to bring your ideas to life. Describe a
              concept, pick a style, and receive unique, high-resolution artwork ready for print.
              No design experience required — just your imagination and a few words.
            </p>
            <LearnMoreButton />
          </div>
        </div>
      </section>

      <section className="relative py-24 lg:py-32 bg-slate-900/40 overflow-hidden border-t border-white/5">
        <PurpleGlow ref={glow2Ref} position="100%" />

        <div className="relative z-10 max-w-7xl mx-auto px-6 grid lg:grid-cols-2 gap-12 items-center">
          <Placeholder label="Social media placeholder" />
          <div className="lg:pl-12">
            <h2 className="text-2xl sm:text-4xl md:text-5xl font-semibold tracking-tight mb-6">
              Promote Collections Where Your Audience Lives
            </h2>
            <p className="text-lg text-white/80 leading-relaxed mb-8 max-w-xl">
              Launch your products on the social platforms that matter. artship.ai helps you share your
              collections on Facebook, Instagram, and TikTok with content that feels native to each
              channel, turning followers into customers while you stay focused on creating.
            </p>
            <LearnMoreButton />
          </div>
        </div>
      </section>

      <section className="relative py-24 lg:py-32 bg-slate-950 overflow-hidden border-t border-white/5">


        <div className="relative z-10 max-w-7xl mx-auto px-6 grid lg:grid-cols-2 gap-12 items-center">
          <Placeholder label="WhatsApp bot placeholder" />
          <div className="lg:pl-12">
            <h2 className="text-2xl sm:text-4xl md:text-5xl font-semibold tracking-tight mb-6">
              Publish Unlimited Collections From Your Phone
            </h2>
            <p className="text-lg text-white/80 leading-relaxed mb-8 max-w-xl">
              Our WhatsApp bot walks you through a few simple questions that you set up in advance,
              then uses your answers to generate artwork and publish products straight to your
              online store. It is the fastest way to go from idea to live product — all without
              opening a laptop.
            </p>
            <LearnMoreButton />
          </div>
        </div>
      </section>
    </div>
  );
}
