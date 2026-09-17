import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import Loader from '../../components/Loader/Loader';
import Button from '../../components/UI/Button';
import DetailSection, { DetailField } from '../../components/DetailSection/DetailSection';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { getHistoryPermit, deleteHistoryPermit, downloadHistoryPermitFile } from '../../api/historyApi';
import { fmtHistDate } from '../../utils/historyFormat';
import './SearchPage.css';
import './HistoryDetailEdit.css';

// Legacy search_hist_detail.jsp (DisplaySearchServlet s=HD) — the read-only view of a single
// pre-system history permit, with Edit / Delete and a cross-link to the linked well location.
function HistoryPermitDetail() {
  const { permitNum } = useParams();
  const navigate = useNavigate();
  const showToast = useToast();
  const [permit, setPermit] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getHistoryPermit(permitNum)
      .then((data) => { if (active) { setPermit(data); setError(''); } })
      .catch((err) => {
        if (active) setError(err?.response?.status === 404 ? 'notfound' : 'error');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [permitNum]);

  const handleDelete = async () => {
    if (!window.confirm('Delete this history permit? This cannot be undone.')) return;
    setDeleting(true);
    try {
      await deleteHistoryPermit(permitNum);
      showToast('History permit deleted.', 'success');
      navigate('/search/history-permits');
    } catch {
      showToast('Failed to delete the history permit.', 'error');
      setDeleting(false);
    }
  };

  const handleViewFile = async (fileType, label) => {
    try {
      const blob = await downloadHistoryPermitFile(permit.permitNum.trim(), fileType);
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener');
      setTimeout(() => window.URL.revokeObjectURL(url), 60000);
    } catch {
      showToast(`Unable to open the ${label}.`, 'error');
    }
  };

  if (loading) {
    return <div className="page-shell"><Loader message="Loading history permit…" /></div>;
  }

  if (error === 'notfound' || !permit) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-permits">← Back to search</Link>
        <div className="alert alert-error">History permit not found.</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-permits">← Back to search</Link>
        <div className="alert alert-error">The history permit could not be loaded.</div>
      </div>
    );
  }

  const address = `${permit.bldgNum ? `${permit.bldgNum} ` : ''}${permit.addrStreet || ''}`.trim();
  const hasFile = (name) => Boolean(name && name.trim());

  return (
    <div className="page-shell">
      <Link className="history-back-link" to="/search/history-permits">← Back to search</Link>
      <div className="history-page__header">
        <h1 className="page-title">History Permit #{permit.permitNum}</h1>
        <div className="history-page__actions">
          <Button variant="primary" onClick={() => navigate(`/search/history-permits/${encodeURIComponent(permit.permitNum)}/edit`)}>Edit</Button>
          <Button variant="danger" onClick={handleDelete} disabled={deleting}>{deleting ? 'Deleting…' : 'Delete'}</Button>
        </div>
      </div>

      <DetailSection title="Permit Information">
        <div className="detail-col">
          <DetailField label="Permit Number">{permit.permitNum}</DetailField>
          <DetailField label="Work Type">{permit.workType}</DetailField>
          <DetailField label="Address">{address}</DetailField>
          <DetailField label="City Name">{permit.cityName}</DetailField>
          <DetailField label="Consultant">{permit.consultant}</DetailField>
          <DetailField label="Driller Name">{permit.drillerName}</DetailField>
          <DetailField label="Driller License #">{permit.drillerLicenseNum}</DetailField>
          <DetailField label="State Well Number">{permit.stateWellNumber}</DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Permit Date">{fmtHistDate(permit.permitDate)}</DetailField>
          <DetailField label="Start Date">{fmtHistDate(permit.startDate)}</DetailField>
          <DetailField label="End Date">{fmtHistDate(permit.endDate)}</DetailField>
          <DetailField label="Start Notice Date">{fmtHistDate(permit.startNoticeDate)}</DetailField>
          <DetailField label="Seal Date">{fmtHistDate(permit.sealDate)}</DetailField>
          <DetailField label="Well Compl Rpt #">{permit.wellComplRptNum}</DetailField>
          <DetailField label="Well Compl Rpt Received">{fmtHistDate(permit.wellComplRptRecvdt)}</DetailField>
          <DetailField label="Well Compl Rpt Exempt">{permit.wellComplRptExmpt}</DetailField>
        </div>
      </DetailSection>

      <DetailSection title="Linked Well Location">
        <div className="detail-col">
          <DetailField label="Well Location">
            {permit.wellLocationWellKey
              ? <Link to={`/search/history-wells/${permit.wellLocationWellKey}`}>View well location #{permit.wellLocationWellKey}</Link>
              : 'No linked well location.'}
          </DetailField>
        </div>
        <div className="detail-col" />
      </DetailSection>

      <DetailSection title="Uploaded Image Files">
        <div className="detail-col">
          <DetailField label="Document Image">
            {hasFile(permit.documentImageFilename)
              ? <Button variant="link" onClick={() => handleViewFile('document', 'document image')}>View Document</Button>
              : 'No Document'}
          </DetailField>
          <DetailField label="Sitemap/Permit Image">
            {hasFile(permit.permitImageFilename)
              ? <Button variant="link" onClick={() => handleViewFile('permit', 'sitemap/permit image')}>View Sitemap/Permit</Button>
              : 'No Sitemap/Permit'}
          </DetailField>
          <DetailField label="Well Compl Rpt Image">
            {hasFile(permit.wellComplRptFilename)
              ? <Button variant="link" onClick={() => handleViewFile('wellrpt', 'well completion report')}>View Compl Rpt</Button>
              : 'No DWR'}
          </DetailField>
        </div>
        <div className="detail-col" />
      </DetailSection>

      <DetailSection title="Audit">
        <div className="detail-col">
          <DetailField label="Added By">{permit.addBy}</DetailField>
          <DetailField label="Added Date">{fmtHistDate(permit.addDate)}</DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Updated By">{permit.updateBy}</DetailField>
          <DetailField label="Updated Date">{fmtHistDate(permit.updateDate)}</DetailField>
        </div>
      </DetailSection>
    </div>
  );
}

export default HistoryPermitDetail;
