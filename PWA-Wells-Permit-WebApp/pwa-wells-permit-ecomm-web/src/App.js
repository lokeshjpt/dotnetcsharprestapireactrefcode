import { Routes, Route } from 'react-router-dom';
import Header from './components/Header/Header';
import Footer from './components/Footer/Footer';
import MainContainer from './components/MainContainer/MainContainer';
import Home from './pages/Home/Home';
import ApplyPage from './pages/Apply/ApplyPage';
import ConfirmationPage from './pages/Confirmation/ConfirmationPage';
import TrackPage from './pages/Track/TrackPage';
import PaymentPage from './pages/Payment/PaymentPage';
import HelpPage from './pages/Help/HelpPage';
import { ToastProvider } from './components/UI/Toaster/ToastProvider';

function App() {
  return (
    <ToastProvider>
    <div className="app-shell">
      <MainContainer>
        <Header />
        <main id="main-content" tabIndex={-1}>
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/apply" element={<ApplyPage />} />
            <Route path="/confirmation/:appId?" element={<ConfirmationPage />} />
            <Route path="/track" element={<TrackPage />} />
            <Route path="/payment/:appId" element={<PaymentPage />} />
            <Route path="/help" element={<HelpPage />} />
          </Routes>
        </main>
      </MainContainer>
      <Footer />
    </div>
    </ToastProvider>
  );
}

export default App;
