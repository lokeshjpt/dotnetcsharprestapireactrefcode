import { Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import './Home.css';

function Home() {
  return (
    <div className="page-shell home-page">
      <section className="home-hero panel">
        <div>
          <p className="home-hero__eyebrow">County of Alameda</p>
          <h1 className="page-title">Apply online for a well permit</h1>
          <p className="page-subtitle">
            Submit a new permit request, upload supporting information, and return later to track status updates.
          </p>
          <div className="home-hero__actions">
            <Link to="/apply"><Button>Start application</Button></Link>
            <Link to="/track"><Button variant="secondary">Track existing application</Button></Link>
          </div>
        </div>
        <div className="home-hero__card">
          <h2>Before you begin</h2>
          <ul>
            <li>Gather applicant contact information.</li>
            <li>Confirm the project location and expected construction dates.</li>
            <li>Have your sitemap ready for upload.</li>
            <li>Use a credit card, check, or fee exemption code for payment.</li>
          </ul>
        </div>
      </section>
    </div>
  );
}

export default Home;
