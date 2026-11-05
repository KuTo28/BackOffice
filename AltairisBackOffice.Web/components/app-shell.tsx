"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

const links = [
  { href: "/", label: "Overview", icon: "◫" },
  { href: "/hotels", label: "Hotels", icon: "⌂" },
];

export function AppShell({ children }: Readonly<{ children: ReactNode }>) {
  const pathname = usePathname();
  return (
    <div className="app-frame">
      <aside className="sidebar">
        <Link className="brand" href="/">
          <span className="brand-mark">A</span>
          <span><strong>ALTAIRIS</strong><small>HOTEL OPERATIONS</small></span>
        </Link>
        <div className="nav-caption">WORKSPACE</div>
        <nav className="main-nav" aria-label="Main navigation">
          {links.map((link) => {
            const active = link.href === "/" ? pathname === "/" : pathname === link.href || pathname.startsWith("/hotels/");
            return (
              <Link className={`nav-link${active ? " active" : ""}`} href={link.href} key={link.href}>
                <span className="nav-icon" aria-hidden="true">{link.icon}</span>
                {link.label}
                {active && <span className="nav-indicator" />}
              </Link>
            );
          })}
        </nav>
        <div className="sidebar-bottom">
          <span className="status-dot" /> API-connected backoffice
          <small>Hotel management</small>
        </div>
      </aside>
      <div className="main-area">
        <header className="topbar">
          <div className="breadcrumb"><Link href="/">Altairis</Link><span className="crumb-slash">/</span><strong>{pathname === "/" ? "Overview" : pathname.startsWith("/hotels/") ? "Hotel workspace" : "Hotels"}</strong></div>
          <div className="topbar-right">
            <span className="topbar-date">{new Intl.DateTimeFormat("en-GB", { dateStyle: "medium" }).format(new Date())}</span>
            <span className="avatar">A</span>
          </div>
        </header>
        <main className="page-content">{children}</main>
      </div>
    </div>
  );
}
