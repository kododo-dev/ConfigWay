import { defineWay } from '@kododo-dev/way-shell';
import App from './App';
import { configWayTheme } from './theme';

// The module Way.Shell loads (built by `npm run build` into dist/way.js).
export default defineWay({ App, theme: configWayTheme });
