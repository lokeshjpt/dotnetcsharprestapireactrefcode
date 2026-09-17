import './Loader.css';

function Loader() {
  return (
    <div className="loader-overlay" role="status" aria-live="polite">
      <div className="spinner">
        {[...Array(8)].map((_, i) => (
          <div key={i} className={`spinner-dot spinner-dot-${i + 1}`}></div>
        ))}
      </div>
    </div>
  );
}

export default Loader;
