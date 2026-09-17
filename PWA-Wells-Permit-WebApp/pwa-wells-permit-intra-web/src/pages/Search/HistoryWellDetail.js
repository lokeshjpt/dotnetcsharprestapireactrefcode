import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import Loader from '../../components/Loader/Loader';
import Button from '../../components/UI/Button';
import DetailSection, { DetailField } from '../../components/DetailSection/DetailSection';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { getHistoryWell, deleteHistoryWell } from '../../api/historyApi';
import { fmtHistDate } from '../../utils/historyFormat';
import './SearchPage.css';
import './HistoryDetailEdit.css';

// Legacy search_hist_wells_detail.jsp (DisplaySearchServlet s=HWD) — read-only view of a single
// history well location, with Edit / Delete and a cross-link to the matching history permit.
function HistoryWellDetail() {
  const { wellKey } = useParams();
  const navigate = useNavigate();
  const showToast = useToast();
  const [well, setWell] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getHistoryWell(wellKey)
      .then((data) => { if (active) { setWell(data); setError(''); } })
      .catch((err) => {
        if (active) setError(err?.response?.status === 404 ? 'notfound' : 'error');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [wellKey]);

  const handleDelete = async () => {
    if (!window.confirm('Delete this history well location? This cannot be undone.')) return;
    setDeleting(true);
    try {
      await deleteHistoryWell(wellKey);
      showToast('History well location deleted.', 'success');
      navigate('/search/history-wells');
    } catch {
      showToast('Failed to delete the history well location.', 'error');
      setDeleting(false);
    }
  };

  if (loading) {
    return <div className="page-shell"><Loader message="Loading well location…" /></div>;
  }

  if (error === 'notfound' || !well) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-wells">← Back to search</Link>
        <div className="alert alert-error">History well location not found.</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-wells">← Back to search</Link>
        <div className="alert alert-error">The well location could not be loaded.</div>
      </div>
    );
  }

  return (
    <div className="page-shell">
      <Link className="history-back-link" to="/search/history-wells">← Back to search</Link>
      <div className="history-page__header">
        <h1 className="page-title">Well Location #{well.wellKey}</h1>
        <div className="history-page__actions">
          <Button variant="primary" onClick={() => navigate(`/search/history-wells/${well.wellKey}/edit`)}>Edit</Button>
          <Button variant="danger" onClick={handleDelete} disabled={deleting}>{deleting ? 'Deleting…' : 'Delete'}</Button>
        </div>
      </div>

      <DetailSection title="Well Location">
        <div className="detail-col">
          <DetailField label="Permit Number">{well.permitNum}</DetailField>
          <DetailField label="Tract #">{well.tractNum}</DetailField>
          <DetailField label="Section #">{well.sectNum}</DetailField>
          <DetailField label="Street Address">{well.addrStreet}</DetailField>
          <DetailField label="City">{well.addrCity}</DetailField>
          <DetailField label="Owner Name">{well.ownerName}</DetailField>
          <DetailField label="Coordinate X">{well.coordX}</DetailField>
          <DetailField label="Coordinate Y">{well.coordY}</DetailField>
          <DetailField label="Match Level">{well.matchLevel}</DetailField>
          <DetailField label="TSR/QQ">{well.tsrQq}</DetailField>
          <DetailField label="Rec Code">{well.recCode}</DetailField>
          <DetailField label="Phone Number">{well.phoneNum}</DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Drill Date">{fmtHistDate(well.drillDate)}</DetailField>
          <DetailField label="Elevation">{well.elevation}</DetailField>
          <DetailField label="Total Depth">{well.totalDepth}</DetailField>
          <DetailField label="Water Depth">{well.waterDepth}</DetailField>
          <DetailField label="Diameter">{well.diameter}</DetailField>
          <DetailField label="Well Use">{well.wellUse}</DetailField>
          <DetailField label="Log Code">{well.logCode}</DetailField>
          <DetailField label="Water Quality (WQ)">{well.wq}</DetailField>
          <DetailField label="Water Injection (WI)">{well.wi}</DetailField>
          <DetailField label="Yield">{well.yield}</DetailField>
          <DetailField label="DTW Calc">{well.dtwCalc}</DetailField>
        </div>
      </DetailSection>

      <DetailSection title="Linked History Permit">
        <div className="detail-col">
          <DetailField label="History Permit">
            {well.historyPermitNum
              ? <Link to={`/search/history-permits/${encodeURIComponent(well.historyPermitNum.trim())}`}>View history permit #{well.historyPermitNum.trim()}</Link>
              : 'No matching history permit.'}
          </DetailField>
        </div>
        <div className="detail-col" />
      </DetailSection>

      <DetailSection title="Audit">
        <div className="detail-col">
          <DetailField label="Added By">{well.addBy}</DetailField>
          <DetailField label="Added Date">{fmtHistDate(well.addDate)}</DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Updated By">{well.updateBy}</DetailField>
          <DetailField label="Updated Date">{fmtHistDate(well.updateDate)}</DetailField>
        </div>
      </DetailSection>
    </div>
  );
}

export default HistoryWellDetail;
