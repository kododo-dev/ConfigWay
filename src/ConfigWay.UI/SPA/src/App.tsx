import { HashRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { ThemeContextProvider, useThemeMode } from './context/ThemeContext';
import { I18nProvider } from './i18n/I18nContext';
import { SectionsProvider } from './context/SectionsContext';
import MainLayout from './components/layout/MainLayout';
import AllSettingsPage from './pages/AllSettingsPage';
import SectionPage from './pages/SectionPage';

function AppInner() {
  const { muiTheme } = useThemeMode();
  return (
    <ThemeProvider theme={muiTheme}>
      <CssBaseline />
      <SectionsProvider>
        <HashRouter>
          <MainLayout>
            <Routes>
              <Route path="/" element={<AllSettingsPage />} />
              <Route path="/all" element={<Navigate to="/" replace />} />
              <Route path="/section/:sectionKey" element={<SectionPage />} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </MainLayout>
        </HashRouter>
      </SectionsProvider>
    </ThemeProvider>
  );
}

function App() {
  return (
    <ThemeContextProvider>
      <I18nProvider>
        <AppInner />
      </I18nProvider>
    </ThemeContextProvider>
  );
}

export default App;
