import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { helpSections, HELP_IMG_BASE } from './helpContent';
import './HelpPage.css';

// One searchable string per section (title + tagline + intro + every step's text) so the jump-to
// dropdown can match any word that appears anywhere in that section, not just its title.
function sectionHaystack(section) {
  const parts = [section.title, section.tagline, section.intro];
  section.steps.forEach((s) => {
    parts.push(s.title, s.description, ...(s.instructions || []));
  });
  return parts.join(' \n ').toLowerCase();
}

function HelpPage() {
  const location = useLocation();
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const comboRef = useRef(null);
  const inputRef = useRef(null);
  const [lightbox, setLightbox] = useState(null);
  const [showTop, setShowTop] = useState(false);

  const q = query.trim().toLowerCase();

  // Show a floating "back to top" button once the reader has scrolled past the hero. The help
  // content is long (multiple sections/screenshots), so a quick way back to the search box is handy.
  useEffect(() => {
    const onScroll = () => setShowTop(window.scrollY > 600);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  // The dropdown lists every section, filtered by whatever the user has typed.
  const options = useMemo(
    () => (q ? helpSections.filter((s) => sectionHaystack(s).includes(q)) : helpSections),
    [q],
  );

  // Keep the keyboard highlight in range as the list changes.
  useEffect(() => {
    setActiveIndex(0);
  }, [q]);

  // Close the dropdown when clicking outside of it.
  useEffect(() => {
    function onDocClick(event) {
      if (comboRef.current && !comboRef.current.contains(event.target)) setOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  // Close the full-screen image viewer on Escape.
  useEffect(() => {
    if (!lightbox) return undefined;
    const onKey = (event) => {
      if (event.key === 'Escape') setLightbox(null);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [lightbox]);

  // Scroll a section to the top of the viewport and keep it there while the page settles. The step
  // screenshots are lazy-loaded, so sections above the target grow as their images load and reflow
  // the page after the first scroll — which would otherwise leave the reader parked on the wrong
  // section. Re-align on every image load (the exact reflow trigger) until the reader scrolls on
  // their own; alignCleanup lets a newer jump cancel an in-flight one.
  const alignCleanup = useRef(null);
  const alignToSection = useCallback((id) => {
    if (alignCleanup.current) alignCleanup.current();
    const el = document.getElementById(`help-${id}`);
    if (!el) return;

    let active = true;
    let safety;
    const align = (behavior) => {
      if (active) el.scrollIntoView({ behavior, block: 'start' });
    };
    const pending = Array.from(document.querySelectorAll('.help-content img')).filter((img) => !img.complete);
    const onLoad = () => align('auto');
    const stop = () => {
      active = false;
      pending.forEach((img) => img.removeEventListener('load', onLoad));
      window.removeEventListener('wheel', stop);
      window.removeEventListener('touchmove', stop);
      clearTimeout(safety);
      if (alignCleanup.current === stop) alignCleanup.current = null;
    };

    align('smooth');
    pending.forEach((img) => img.addEventListener('load', onLoad));
    window.addEventListener('wheel', stop, { passive: true });
    window.addEventListener('touchmove', stop, { passive: true });
    safety = setTimeout(stop, 4000);
    alignCleanup.current = stop;
  }, []);

  // Deep-link support: scroll to the section named in the URL hash (e.g. /help#track).
  useEffect(() => {
    const id = location.hash.replace('#', '');
    if (!id) {
      window.scrollTo({ top: 0 });
      return undefined;
    }
    alignToSection(id);
    return () => {
      if (alignCleanup.current) alignCleanup.current();
    };
  }, [location.hash, alignToSection]);

  const chooseSection = (section) => {
    if (!section) return;
    setQuery('');
    setOpen(false);
    inputRef.current?.blur();
    alignToSection(section.id);
  };

  const onKeyDown = (event) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setActiveIndex((i) => Math.min(i + 1, options.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      chooseSection(options[activeIndex]);
    } else if (event.key === 'Escape') {
      setOpen(false);
    }
  };

  return (
    <div className="page-shell help-page">
      <div className="panel help-hero">
        <p className="help-hero__eyebrow">Help &amp; How-To</p>
        <h1 className="page-title">Using the Well Permit portal</h1>
        <p className="page-subtitle">
          Step-by-step instructions, with screenshots, for applying, tracking, and uploading a site
          map. Search below and pick a topic to jump straight to it.
        </p>

        <div className="help-jump" ref={comboRef}>
          <div className="help-jump__field">
            <svg className="help-jump__icon" viewBox="0 0 24 24" aria-hidden="true">
              <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
              <line x1="21" y1="21" x2="16.5" y2="16.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            <input
              ref={inputRef}
              type="text"
              role="combobox"
              aria-expanded={open}
              aria-controls="help-jump-list"
              aria-autocomplete="list"
              className="help-jump__input"
              placeholder="Search topics and jump to a section…"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setOpen(true);
              }}
              onFocus={() => setOpen(true)}
              onKeyDown={onKeyDown}
              aria-label="Search help topics and jump to a section"
            />
            <svg className={`help-jump__caret${open ? ' is-open' : ''}`} viewBox="0 0 24 24" aria-hidden="true">
              <polyline points="6 9 12 15 18 9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>

          {open && (
            <ul className="help-jump__list" id="help-jump-list" role="listbox">
              {options.length === 0 && (
                <li className="help-jump__empty">
                  No topics match “{query}”.
                </li>
              )}
              {options.map((section, i) => (
                <li
                  key={section.id}
                  role="option"
                  aria-selected={i === activeIndex}
                  className={`help-jump__option${i === activeIndex ? ' is-active' : ''}`}
                  onMouseEnter={() => setActiveIndex(i)}
                  onMouseDown={(e) => {
                    e.preventDefault();
                    chooseSection(section);
                  }}
                >
                  <span className="help-jump__option-title">{section.title}</span>
                  {section.tagline && <span className="help-jump__option-tagline">{section.tagline}</span>}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="help-content">
        {helpSections.map((section) => (
          <section key={section.id} id={`help-${section.id}`} className="help-section panel">
            <header className="help-section__header">
              <div>
                <h2 className="help-section__title">{section.title}</h2>
                {section.tagline && <p className="help-section__tagline">{section.tagline}</p>}
              </div>
              {section.cta && (
                <Link to={section.cta.to} className="help-cta">
                  {section.cta.label}
                  <span aria-hidden="true"> →</span>
                </Link>
              )}
            </header>

            {section.intro && <p className="help-section__intro">{section.intro}</p>}

            <ol className="help-steps">
              {section.steps.map((step) => (
                <li key={step.title} id={step.anchor ? `help-${step.anchor}` : undefined} className="help-step">
                  {step.image && (
                    <figure className="help-step__figure">
                      <button
                        type="button"
                        className="help-step__zoom"
                        onClick={() =>
                          setLightbox({ src: `${HELP_IMG_BASE}/${step.image}`, alt: step.alt || step.title })
                        }
                        aria-label={`View full screen: ${step.alt || step.title}`}
                      >
                        <img src={`${HELP_IMG_BASE}/${step.image}`} alt={step.alt || step.title} loading="lazy" />
                        <span className="help-step__zoom-hint" aria-hidden="true">Click to enlarge</span>
                      </button>
                    </figure>
                  )}
                  <div className="help-step__body">
                    <h3 className="help-step__title">{step.title}</h3>
                    {step.description && <p className="help-step__desc">{step.description}</p>}
                    {step.instructions && (
                      <ul className="help-step__list">
                        {step.instructions.map((line, i) => (
                          <li key={i}>{line}</li>
                        ))}
                      </ul>
                    )}
                  </div>
                </li>
              ))}
            </ol>
          </section>
        ))}

        <p className="help-foot">
          Still need help? Contact Alameda County Public Works at{' '}
          <a href="tel:+15106706601">(510) 670-6601</a> or visit the{' '}
          <a href="https://www.acpwa.org/" target="_blank" rel="noopener noreferrer">
            Public Works Agency
          </a>{' '}
          website.
        </p>
      </div>

      {showTop && (
        <button
          type="button"
          className="help-to-top"
          onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
          aria-label="Back to top"
          title="Back to top"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <polyline points="6 15 12 9 18 15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>
      )}

      {lightbox && (
        <div
          className="help-lightbox"
          role="dialog"
          aria-modal="true"
          aria-label={lightbox.alt}
          onClick={() => setLightbox(null)}
        >
          <button
            type="button"
            className="help-lightbox__close"
            onClick={() => setLightbox(null)}
            aria-label="Close full-screen image"
          >
            ×
          </button>
          <img
            className="help-lightbox__img"
            src={lightbox.src}
            alt={lightbox.alt}
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}
    </div>
  );
}

export default HelpPage;
