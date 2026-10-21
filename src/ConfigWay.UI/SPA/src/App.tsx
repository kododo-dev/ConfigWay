import { Route, Routes } from 'react-router-dom';
import { SectionsProvider } from './context/SectionsContext';
import { FlagsProvider, useFlags } from './context/FlagsContext';
import SideMenu from './components/layout/SideMenu';
import ToWayRoot from './components/common/ToWayRoot';
import AllSettingsPage from './pages/AllSettingsPage';
import SectionPage from './pages/SectionPage';
import FlagsPage from './pages/FlagsPage';
import FlagPage from './pages/FlagPage';

// The editor inside Way.Shell: the shell provides the router, theme, language and layout; the sections go into the
// shell's side menu. The flags views are there when the app added Kododo.ConfigWay.FeatureManagement.
function App() {
  return (
    <SectionsProvider>
      <FlagsProvider>
        <SideMenu />
        <AppRoutes />
      </FlagsProvider>
    </SectionsProvider>
  );
}

function AppRoutes() {
  const { available, loading } = useFlags();
  return (
    <Routes>
      <Route index element={<AllSettingsPage />} />
      <Route path="section/:sectionKey" element={<SectionPage />} />
      {available && <Route path="flags" element={<FlagsPage />} />}
      {available && <Route path="flags/new" element={<FlagPage isNew />} />}
      {available && <Route path="flag/:flagId" element={<FlagPage />} />}
      {/* Until the server says whether there are flags, a link to them waits instead of going to the root. */}
      <Route path="*" element={loading ? null : <ToWayRoot />} />
    </Routes>
  );
}

export default App;
