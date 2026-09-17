import { useEffect, useState } from 'react';
import { getInspectors } from '../../api/referenceApi';

// Loads the active inspector dropdown options ([{ code, label }]) once for the Inspections list
// filters. Failures degrade to an empty list so the "All Inspectors" option still renders.
export default function useInspectors() {
  const [inspectors, setInspectors] = useState([]);

  useEffect(() => {
    let active = true;
    getInspectors()
      .then((data) => { if (active) setInspectors(Array.isArray(data) ? data : []); })
      .catch(() => { if (active) setInspectors([]); });
    return () => { active = false; };
  }, []);

  return inspectors;
}

// Strips empty/null values so blank filters are never sent as query params.
export function cleanParams(obj) {
  const out = {};
  Object.entries(obj).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) out[key] = value;
  });
  return out;
}
