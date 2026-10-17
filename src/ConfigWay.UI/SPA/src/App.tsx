import { Route, Routes } from 'react-router-dom';
import { SectionsProvider } from './context/SectionsContext';
import SideMenu from './components/layout/SideMenu';
import ToWayRoot from './components/common/ToWayRoot';
import AllSettingsPage from './pages/AllSettingsPage';
import SectionPage from './pages/SectionPage';

// The editor inside Way.Shell: the shell provides the router, theme, language and layout; the sections go into the
// shell's side menu.
function App() {
  return (
    <SectionsProvider>
      <SideMenu />
      <Routes>
        <Route index element={<AllSettingsPage />} />
        <Route path="section/:sectionKey" element={<SectionPage />} />
        <Route path="*" element={<ToWayRoot />} />
      </Routes>
    </SectionsProvider>
  );
}

export default App;
