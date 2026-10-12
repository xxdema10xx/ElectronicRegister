// src/hooks/useLayout.js
import { Platform, useWindowDimensions } from "react-native";

export const DESKTOP_BREAKPOINT = 900;
export const WEB_BREAKPOINT = 600;
export const CONTENT_MAX_WIDTH = 1400;

// Layout responsive: sul web la sidebar è il layout predefinito (si passa alla tab bar solo su finestre
// molto strette); su nativo serve uno schermo largo (tablet).
export function useLayout() {
  const { width } = useWindowDimensions();
  const isWide = width >= DESKTOP_BREAKPOINT;
  const isDesktop = isWide || (Platform.OS === "web" && width >= WEB_BREAKPOINT);
  return { width, isDesktop, isWide };
}
