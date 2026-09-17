import './Footer.css';

function Footer() {
  return (
    <footer className="app-footer">
      <div className="app-footer__inner">
        <span>
          For assistance, contact the PWA Help Desk at (510) 670-5755
        </span>
        <span>&copy; {new Date().getFullYear()} County of Alameda Public Works Agency</span>
      </div>
    </footer>
  );
}

export default Footer;