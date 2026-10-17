import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { wayModule } from '@kododo-dev/way-shell/vite'

// `npm run build`: src/way.tsx as a Way.Shell module (dist/way.js, dist/way.css), embedded by ConfigWay.UI.
// `npm run dev`: src/main.tsx with the module in WayDevHost; the API goes to the demo app (dotnet run in
// ConfigWay.Demo.Web).
export default defineConfig({
  plugins: [react(), wayModule()],
  server: { proxy: { '/config/api': process.env.CONFIGWAY_BACKEND ?? 'http://localhost:5089' } },
})
