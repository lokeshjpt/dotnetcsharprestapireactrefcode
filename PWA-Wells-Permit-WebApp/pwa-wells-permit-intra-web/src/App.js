import { useEffect, useState } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import Header from './components/Header/Header';
import Footer from './components/Footer/Footer';
import MainContainer from './components/MainContainer/MainContainer';
import SideBar from './components/SideBar/SideBar';
import Home from './pages/Home/Home';
import PendingList from './pages/PendingList/PendingList';
import ApplicationDetail from './pages/ApplicationDetail/ApplicationDetail';
import ApprovalPage from './pages/Approval/ApprovalPage';
import PaymentInfoPage from './pages/PaymentInfo/PaymentInfoPage';
import InspectionDetailPage from './pages/Inspection/InspectionDetailPage';
import InspectionsList from './pages/Inspections/InspectionsList';
import PendingWcrList from './pages/Inspections/PendingWcrList';
import PendingGeoLogList from './pages/Inspections/PendingGeoLogList';
import HoldList from './pages/Inspections/HoldList';
import InspectionsCalendar from './pages/Inspections/InspectionsCalendar';
import FileUploadPage from './pages/FileUpload/FileUploadPage';
import SearchPage from './pages/Search/SearchPage';
import CancelledSearch from './pages/Search/CancelledSearch';
import HistoryPermits from './pages/Search/HistoryPermits';
import HistoryPermitDetail from './pages/Search/HistoryPermitDetail';
import HistoryPermitEdit from './pages/Search/HistoryPermitEdit';
import HistoryWells from './pages/Search/HistoryWells';
import HistoryWellDetail from './pages/Search/HistoryWellDetail';
import HistoryWellEdit from './pages/Search/HistoryWellEdit';
import ReportsPage from './pages/Reports/ReportsPage';
import Reconciliation from './pages/Reports/Reconciliation';
import CompletedWork from './pages/Reports/CompletedWork';
import CompletedInspections from './pages/Reports/CompletedInspections';
import Extract from './pages/Reports/Extract';
import SsrsReport from './pages/Reports/SsrsReport';
import QueueList from './pages/Queue/QueueList';
import MaintenancePage from './pages/Maintenance/MaintenancePage';
import MaintenanceEntity from './pages/Maintenance/MaintenanceEntity';
import PermitPage from './pages/Permit/PermitPage';
import SiteHazardPage from './pages/Hazard/SiteHazardPage';
import HelpPage from './pages/Help/HelpPage';
import NotAuthorized from './components/NotAuthorized/NotAuthorized';
import { useAuth } from './context/AuthContext';
import { ToastProvider } from './components/UI/Toaster/ToastProvider';
import './App.css';

function App() {
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const location = useLocation();
  const { accessDenied, user } = useAuth();

  // Close the mobile drawer whenever the route changes.
  useEffect(() => {
    setMobileNavOpen(false);
  }, [location.pathname]);

  // Application allowlist gate: an authenticated Entra user who is not on the allowlist is blocked
  // from the entire intra app (including the standalone print pages) with a full-page banner.
  if (accessDenied) {
    return (
      <ToastProvider>
        <NotAuthorized user={user} />
      </ToastProvider>
    );
  }

  // The printable permit and the printable site hazard form are standalone pages (opened in their
  // own tab) rendered without the staff header/sidebar/footer chrome so they print cleanly to PDF.
  if (location.pathname.startsWith('/permit/') || location.pathname.startsWith('/hazard/')) {
    return (
      <ToastProvider>
        <Routes>
          <Route path="/permit/:appId" element={<PermitPage />} />
          <Route path="/hazard/:appId" element={<SiteHazardPage />} />
        </Routes>
      </ToastProvider>
    );
  }

  return (
    <ToastProvider>
    <div className="app-shell">
      <MainContainer>
        <Header onMenuToggle={() => setMobileNavOpen((open) => !open)} />
        <div className="staff-layout">
          <SideBar mobileOpen={mobileNavOpen} onClose={() => setMobileNavOpen(false)} />
          <main id="main-content" className="staff-content" tabIndex={-1}>
            <div className="staff-content__inner">
            <Routes>
              <Route path="/" element={<Home />} />
              <Route path="/applications" element={<PendingList />} />
              <Route path="/applications/:appId" element={<ApplicationDetail />} />
              <Route path="/applications/:appId/approval" element={<ApprovalPage />} />
              <Route path="/payments/:appId" element={<PaymentInfoPage />} />
              <Route path="/inspections/list" element={<InspectionsList />} />
              <Route path="/inspections/pending-wcr" element={<PendingWcrList />} />
              <Route path="/inspections/pending-geolog" element={<PendingGeoLogList />} />
              <Route path="/inspections/hold" element={<HoldList />} />
              <Route path="/inspections/calendar" element={<InspectionsCalendar />} />
              <Route path="/inspections/:appId" element={<InspectionDetailPage />} />
              <Route path="/files" element={<FileUploadPage />} />
              <Route path="/search" element={<SearchPage />} />
              <Route path="/search/history-permits" element={<HistoryPermits />} />
              <Route path="/search/history-permits/new" element={<HistoryPermitEdit mode="add" />} />
              <Route path="/search/history-permits/:permitNum" element={<HistoryPermitDetail />} />
              <Route path="/search/history-permits/:permitNum/edit" element={<HistoryPermitEdit />} />
              <Route path="/search/history-wells" element={<HistoryWells />} />
              <Route path="/search/history-wells/new" element={<HistoryWellEdit mode="add" />} />
              <Route path="/search/history-wells/:wellKey" element={<HistoryWellDetail />} />
              <Route path="/search/history-wells/:wellKey/edit" element={<HistoryWellEdit />} />
              <Route path="/search/cancelled" element={<CancelledSearch />} />
              <Route path="/queue/:statusCode" element={<QueueList />} />
              <Route path="/reports" element={<ReportsPage />} />
              <Route path="/reports/reconciliation" element={<Reconciliation />} />
              <Route path="/reports/completed-works" element={<CompletedWork />} />
              <Route path="/reports/completed-inspections" element={<CompletedInspections />} />
              <Route path="/reports/extract" element={<Extract />} />
              <Route path="/reports/ssrs/:reportKey" element={<SsrsReport />} />
              <Route path="/maintenance" element={<MaintenancePage />} />
              <Route path="/maintenance/:entityKey" element={<MaintenanceEntity />} />
              <Route path="/help" element={<HelpPage />} />
            </Routes>
            </div>
          </main>
        </div>
      </MainContainer>
      <Footer />
    </div>
    </ToastProvider>
  );
}

export default App;
