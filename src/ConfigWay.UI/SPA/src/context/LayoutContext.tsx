import { createContext, useContext } from 'react';

interface LayoutContextValue {
  openMobileMenu: () => void;
}

export const LayoutContext = createContext<LayoutContextValue>({
  openMobileMenu: () => {},
});

export const useLayout = () => useContext(LayoutContext);
