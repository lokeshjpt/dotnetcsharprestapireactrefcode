// Lightweight Google Maps JavaScript API script loader (no extra npm dependency).
// Resolves with the google.maps namespace once the script is ready. A single
// shared promise guarantees the script tag is only injected once.

let loadPromise = null;

// Google invokes window.gm_authFailure when the API key is rejected (e.g.
// RefererNotAllowedMapError). We surface that as a flag so the UI can fall back
// to manual coordinate entry instead of showing a broken map.
export function onGoogleMapsAuthFailure(cb) {
  if (typeof window === 'undefined') return;
  window.gm_authFailure = () => {
    window.__gmAuthFailed = true;
    try { cb && cb(); } catch (_) { /* no-op */ }
  };
}

export function googleMapsAuthFailed() {
  return typeof window !== 'undefined' && window.__gmAuthFailed === true;
}

export function loadGoogleMaps(apiKey) {
  if (!apiKey) return Promise.reject(new Error('No Google Maps API key configured.'));
  if (typeof window !== 'undefined' && window.google && window.google.maps) {
    return Promise.resolve(window.google.maps);
  }
  if (loadPromise) return loadPromise;

  loadPromise = new Promise((resolve, reject) => {
    const existing = document.getElementById('google-maps-script');
    if (existing) {
      existing.addEventListener('load', () => resolve(window.google.maps));
      existing.addEventListener('error', () => {
        loadPromise = null;
        reject(new Error('Failed to load Google Maps.'));
      });
      return;
    }
    const script = document.createElement('script');
    script.id = 'google-maps-script';
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`;
    script.async = true;
    script.defer = true;
    script.onload = () => resolve(window.google.maps);
    script.onerror = () => {
      loadPromise = null;
      reject(new Error('Failed to load Google Maps.'));
    };
    document.head.appendChild(script);
  });

  return loadPromise;
}
