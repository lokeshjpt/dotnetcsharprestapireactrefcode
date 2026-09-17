import { useEffect, useRef, useState } from 'react';
import config from '../../config';
import { loadGoogleMaps, onGoogleMapsAuthFailure, googleMapsAuthFailed } from '../../utils/googleMapsLoader';
import InputField from '../UI/InputField';
import './MapPicker.css';

// Alameda County bounding box, matching legacy app_proj_locmap.jsp.
const DEFAULT_CENTER = { lat: 37.6879, lng: -122.0038 };

function toNum(value) {
  const n = parseFloat(value);
  return Number.isFinite(n) ? n : null;
}

// Pull a human-readable city name out of a Google geocoder result.
function extractCity(components = []) {
  const byType = (type) => {
    const match = components.find((c) => (c.types || []).includes(type));
    return match ? (match.long_name || match.short_name) : '';
  };
  return byType('locality') || byType('sublocality') || byType('administrative_area_level_3') || '';
}

/**
 * Reusable location picker. When a Google Maps API key is present it renders an
 * interactive map: click or drag the marker to set coordinates. Without a key
 * (or if the map fails to load) it gracefully degrades to manual lat/long inputs.
 *
 * Props:
 *  - lat, lng: current values (strings)
 *  - onChange(lat, lng): called with the new coordinate pair
 *  - onPlace({ address, city, lat, lng }): called when a place is searched or the
 *    map is clicked (with a reverse-geocoded address + city). Faithful to the
 *    legacy app_proj_locmap.jsp "Search Address" + click-to-place behavior.
 *  - showSearch: render the "Search Address" autocomplete box (default false)
 *  - idPrefix: unique id prefix for the inputs
 *  - showInputs: render manual lat/long inputs (default true)
 *  - compact: render a shorter map (used for per-well pins)
 */
function MapPicker({
  lat,
  lng,
  onChange,
  onPlace,
  showSearch = false,
  idPrefix = 'map',
  showInputs = true,
  compact = false,
}) {
  const apiKey = config.mapApiKey;
  const mapRef = useRef(null);
  const searchRef = useRef(null);
  const mapObj = useRef(null);
  const markerObj = useRef(null);
  const geocoderRef = useRef(null);
  const placeMarkerRef = useRef(null);
  const onChangeRef = useRef(onChange);
  const onPlaceRef = useRef(onPlace);
  const [status, setStatus] = useState(apiKey ? 'loading' : 'nokey');

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    onPlaceRef.current = onPlace;
  }, [onPlace]);

  useEffect(() => {
    if (!apiKey) return undefined;
    let cancelled = false;

    onGoogleMapsAuthFailure(() => {
      if (!cancelled) setStatus('error');
    });
    if (googleMapsAuthFailed()) setStatus('error');

    loadGoogleMaps(apiKey)
      .then((maps) => {
        if (cancelled || !mapRef.current) return;
        const hasCoords = toNum(lat) !== null && toNum(lng) !== null;
        const center = hasCoords ? { lat: toNum(lat), lng: toNum(lng) } : DEFAULT_CENTER;
        const map = new maps.Map(mapRef.current, {
          center,
          zoom: hasCoords ? 16 : 11,
          // Default to satellite imagery; users can switch to the regular road map
          // via the tabbed (Map | Satellite) map-type control.
          mapTypeId: maps.MapTypeId.SATELLITE,
          streetViewControl: false,
          mapTypeControl: true,
          mapTypeControlOptions: {
            style: maps.MapTypeControlStyle.HORIZONTAL_BAR,
            mapTypeIds: [maps.MapTypeId.ROADMAP, maps.MapTypeId.SATELLITE],
          },
          fullscreenControl: false,
          gestureHandling: 'cooperative',
        });
        mapObj.current = map;
        geocoderRef.current = new maps.Geocoder();

        const placeMarker = (position) => {
          if (markerObj.current) {
            markerObj.current.setPosition(position);
          } else {
            markerObj.current = new maps.Marker({ position, map, draggable: true });
            markerObj.current.addListener('dragend', (ev) => {
              onChangeRef.current(ev.latLng.lat().toFixed(6), ev.latLng.lng().toFixed(6));
              reverseGeocode(ev.latLng);
            });
          }
        };
        placeMarkerRef.current = placeMarker;

        // Reverse-geocode a LatLng into address + city and fire onPlace.
        const reverseGeocode = (latLng) => {
          if (!onPlaceRef.current || !geocoderRef.current) return;
          geocoderRef.current.geocode({ location: latLng }, (results, gcStatus) => {
            if (gcStatus === 'OK' && results && results[0]) {
              onPlaceRef.current({
                address: results[0].formatted_address,
                city: extractCity(results[0].address_components),
                lat: latLng.lat().toFixed(6),
                lng: latLng.lng().toFixed(6),
              });
            }
          });
        };

        if (hasCoords) placeMarker(center);

        map.addListener('click', (ev) => {
          placeMarker(ev.latLng);
          onChangeRef.current(ev.latLng.lat().toFixed(6), ev.latLng.lng().toFixed(6));
          reverseGeocode(ev.latLng);
        });

        // "Search Address" autocomplete — matches legacy #pac-input.
        if (showSearch && searchRef.current && maps.places) {
          const autocomplete = new maps.places.Autocomplete(searchRef.current, {
            fields: ['geometry', 'formatted_address', 'address_components', 'name'],
          });
          autocomplete.bindTo('bounds', map);
          autocomplete.addListener('place_changed', () => {
            const place = autocomplete.getPlace();
            if (!place.geometry || !place.geometry.location) return;
            const loc = place.geometry.location;
            map.setCenter(loc);
            map.setZoom(16);
            placeMarker(loc);
            const latStr = loc.lat().toFixed(6);
            const lngStr = loc.lng().toFixed(6);
            onChangeRef.current(latStr, lngStr);
            if (onPlaceRef.current) {
              onPlaceRef.current({
                address: place.formatted_address || place.name || '',
                city: extractCity(place.address_components),
                lat: latStr,
                lng: lngStr,
              });
            }
          });
        }

        setStatus('ready');
      })
      .catch(() => {
        if (!cancelled) setStatus('error');
      });

    return () => {
      cancelled = true;
    };
    // Intentionally only re-run when the key changes; marker seeding uses the
    // initial value and further updates come from user interaction/inputs.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiKey]);

  // Keep the marker/center in sync when lat/lng arrive or change from outside
  // the map (e.g. coordinates preset from the project site, or typed into the
  // lat/long inputs). Recenters only when the marker first appears so dragging
  // the pin doesn't cause a jarring re-center.
  useEffect(() => {
    if (status !== 'ready' || !mapObj.current || !placeMarkerRef.current) return;
    const nlat = toNum(lat);
    const nlng = toNum(lng);
    if (nlat === null || nlng === null) return;
    const firstPlace = !markerObj.current;
    const position = { lat: nlat, lng: nlng };
    placeMarkerRef.current(position);
    if (firstPlace) {
      mapObj.current.setCenter(position);
      mapObj.current.setZoom(16);
    }
  }, [lat, lng, status]);

  const showMap = status === 'loading' || status === 'ready';

  // Nothing to render when there is no key/map and inputs are suppressed
  // (per-well pins fall back to the existing table lat/long columns).
  if (!showInputs && !showSearch && (status === 'nokey' || status === 'error')) return null;

  return (
    <div className="map-picker">
      {showSearch && showMap && (
        <input
          ref={searchRef}
          id={`${idPrefix}Search`}
          type="text"
          className="form-control map-picker__search"
          placeholder="Search Address"
          aria-label="Search address to position the map"
        />
      )}
      {showMap && (
        <div
          ref={mapRef}
          className={`map-picker__canvas${compact ? ' map-picker__canvas--compact' : ''}`}
          role="group"
          aria-label="Location map"
        />
      )}
      {status === 'loading' && <p className="text-muted">Loading map…</p>}
      {status === 'error' && (
        <p className="text-muted">The map could not be loaded. Please enter the coordinates manually.</p>
      )}
      {status === 'ready' && (
        <p className="text-muted">
          Click on the map or enter an address in the &lsquo;Search Address&rsquo; box to position the marker.
        </p>
      )}
      {showInputs || status === 'nokey' || status === 'error' ? (
        <div className="map-picker__coords">
          <InputField
            id={`${idPrefix}Lat`}
            label="Latitude"
            value={lat || ''}
            inputMode="decimal"
            onChange={(e) => onChange(e.target.value, lng)}
            hint={status === 'nokey' ? 'Enter the site latitude (decimal degrees).' : undefined}
          />
          <InputField
            id={`${idPrefix}Lng`}
            label="Longitude"
            value={lng || ''}
            inputMode="decimal"
            onChange={(e) => onChange(lat, e.target.value)}
            hint={status === 'nokey' ? 'Enter the site longitude (decimal degrees).' : undefined}
          />
        </div>
      ) : null}
    </div>
  );
}

export default MapPicker;
