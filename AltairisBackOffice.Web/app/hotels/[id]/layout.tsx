import type { ReactNode } from "react";
import { HotelWorkspace } from "@/components/hotel-workspace";

export default function HotelLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <HotelWorkspace>{children}</HotelWorkspace>;
}
