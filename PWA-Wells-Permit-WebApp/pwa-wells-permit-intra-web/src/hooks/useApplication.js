import { useEffect, useState } from 'react';
import { getApplication } from '../api/applicationApi';
import { getPayment } from '../api/paymentApi';
import { getInspections } from '../api/inspectionApi';

function useApplication(appId) {
  const [application, setApplication] = useState(null);
  const [payment, setPayment] = useState(null);
  const [inspections, setInspections] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!appId) return;

    async function load() {
      setLoading(true);
      setError('');
      try {
        const [applicationData, paymentData, inspectionData] = await Promise.all([
          getApplication(appId),
          getPayment(appId).catch(() => null),
          getInspections(appId).catch(() => []),
        ]);
        setApplication(applicationData);
        setPayment(paymentData);
        setInspections(inspectionData || []);
      } catch {
        setError('Unable to load application details.');
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [appId]);

  return { application, payment, inspections, loading, error, setApplication };
}

export default useApplication;
