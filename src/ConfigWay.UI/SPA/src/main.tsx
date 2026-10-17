// `npm run dev`: the editor inside WayDevHost, with the API of the demo app through Vite's proxy.
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { WayDevHost } from '@kododo-dev/way-shell';
import way from './way';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <WayDevHost way={way} id="config" name="Configuration" apiBase="/config/api" />
  </StrictMode>,
);
