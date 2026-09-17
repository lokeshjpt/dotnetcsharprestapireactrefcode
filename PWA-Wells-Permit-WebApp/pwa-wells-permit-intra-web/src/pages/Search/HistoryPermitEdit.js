import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import Loader from '../../components/Loader/Loader';
import Button from '../../components/UI/Button';
import InputField from '../../components/UI/InputField';
import SelectField from '../../components/UI/SelectField';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { getHistoryPermit, updateHistoryPermit, createHistoryPermit } from '../../api/historyApi';
import { toHistDateInput } from '../../utils/historyFormat';
import './SearchPage.css';
import './HistoryDetailEdit.css';

const EXEMPT_OPTIONS = [
  { code: 'Y', label: 'Yes' },
  { code: 'N', label: 'No' },
];

const EMPTY_FORM = {
  permitNum: '',
  workType: '',
  cityName: '',
  addrStreet: '',
  bldgNum: '',
  consultant: '',
  permitDate: '',
  startDate: '',
  endDate: '',
  startNoticeDate: '',
  sealDate: '',
  wellComplRptExmpt: '',
  wellComplRptRecvdt: '',
  wellComplRptNum: '',
  stateWellNumber: '',
  drillerName: '',
  drillerLicenseNum: '',
};

// Legacy search_hist_detail.jsp form (UpdateAppServlet proc=updhist). In edit mode the Permit
// Number is the key and read-only; in add mode (legacy a=add) it is entered and the record is
// INSERTed. Everything else is shared.
function HistoryPermitEdit({ mode }) {
  const isAdd = mode === 'add';
  const { permitNum: routePermitNum } = useParams();
  const navigate = useNavigate();
  const showToast = useToast();
  const [loading, setLoading] = useState(!isAdd);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(isAdd ? { ...EMPTY_FORM } : null);

  useEffect(() => {
    if (isAdd) return undefined;
    let active = true;
    setLoading(true);
    getHistoryPermit(routePermitNum)
      .then((p) => {
        if (!active) return;
        setForm({
          permitNum: p.permitNum || routePermitNum,
          workType: p.workType || '',
          cityName: p.cityName || '',
          addrStreet: p.addrStreet || '',
          bldgNum: p.bldgNum || '',
          consultant: p.consultant || '',
          permitDate: toHistDateInput(p.permitDate),
          startDate: toHistDateInput(p.startDate),
          endDate: toHistDateInput(p.endDate),
          startNoticeDate: toHistDateInput(p.startNoticeDate),
          sealDate: toHistDateInput(p.sealDate),
          wellComplRptExmpt: p.wellComplRptExmpt || '',
          wellComplRptRecvdt: toHistDateInput(p.wellComplRptRecvdt),
          wellComplRptNum: p.wellComplRptNum || '',
          stateWellNumber: p.stateWellNumber || '',
          drillerName: p.drillerName || '',
          drillerLicenseNum: p.drillerLicenseNum || '',
        });
        setError('');
      })
      .catch((err) => { if (active) setError(err?.response?.status === 404 ? 'notfound' : 'error'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [isAdd, routePermitNum]);

  const set = (key) => (event) => setForm((current) => ({ ...current, [key]: event.target.value }));

  const detailPath = isAdd ? '/search/history-permits' : `/search/history-permits/${encodeURIComponent(routePermitNum)}`;

  const handleSave = async (event) => {
    if (event) event.preventDefault();
    if (isAdd && !form.permitNum.trim()) {
      showToast('Permit Number is required.', 'error');
      return;
    }
    setSaving(true);
    try {
      if (isAdd) {
        const created = await createHistoryPermit(form);
        const newPermitNum = (created?.permitNum || form.permitNum).trim();
        showToast('History permit added.', 'success');
        navigate(`/search/history-permits/${encodeURIComponent(newPermitNum)}`);
      } else {
        await updateHistoryPermit(routePermitNum, form);
        showToast('History permit updated.', 'success');
        navigate(detailPath);
      }
    } catch (err) {
      if (isAdd && err?.response?.status === 409) {
        showToast('Add failed: a history permit with that permit number already exists.', 'error');
      } else {
        showToast(`Failed to ${isAdd ? 'add' : 'update'} the history permit.`, 'error');
      }
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="page-shell"><Loader message="Loading history permit…" /></div>;
  }

  if (error === 'notfound' || !form) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-permits">← Back to search</Link>
        <div className="alert alert-error">History permit not found.</div>
      </div>
    );
  }

  const title = isAdd ? 'Add History Permit' : `Edit History Permit #${routePermitNum}`;

  return (
    <div className="page-shell">
      <Link className="history-back-link" to={detailPath}>← Back to {isAdd ? 'search' : 'detail'}</Link>
      <div className="panel">
        <h1 className="page-title">{title}</h1>
        <form onSubmit={handleSave}>
          <div className="history-edit__grid">
            <div className="modal-subhead">Permit</div>
            {isAdd
              ? <InputField label="Permit Number" id="permitNum" required value={form.permitNum} onChange={set('permitNum')} />
              : <InputField label="Permit Number" id="permitNum" value={routePermitNum} disabled readOnly />}
            <InputField label="Work Type" id="workType" value={form.workType} onChange={set('workType')} />
            <InputField label="Building #" id="bldgNum" value={form.bldgNum} onChange={set('bldgNum')} />
            <InputField label="Address" id="addrStreet" value={form.addrStreet} onChange={set('addrStreet')} />
            <InputField label="City Name" id="cityName" value={form.cityName} onChange={set('cityName')} />
            <InputField label="Consultant" id="consultant" value={form.consultant} onChange={set('consultant')} />
            <InputField label="Driller Name" id="drillerName" value={form.drillerName} onChange={set('drillerName')} />
            <InputField label="Driller License #" id="drillerLicenseNum" value={form.drillerLicenseNum} onChange={set('drillerLicenseNum')} />
            <InputField label="State Well Number" id="stateWellNumber" value={form.stateWellNumber} onChange={set('stateWellNumber')} />

            <div className="modal-subhead">Dates</div>
            <InputField label="Permit Date" id="permitDate" type="date" value={form.permitDate} onChange={set('permitDate')} />
            <InputField label="Start Date" id="startDate" type="date" value={form.startDate} onChange={set('startDate')} />
            <InputField label="End Date" id="endDate" type="date" value={form.endDate} onChange={set('endDate')} />
            <InputField label="Start Notice Date" id="startNoticeDate" type="date" value={form.startNoticeDate} onChange={set('startNoticeDate')} />
            <InputField label="Seal Date" id="sealDate" type="date" value={form.sealDate} onChange={set('sealDate')} />

            <div className="modal-subhead">Well Completion Report</div>
            <InputField label="Well Compl Rpt #" id="wellComplRptNum" value={form.wellComplRptNum} onChange={set('wellComplRptNum')} />
            <InputField label="Well Compl Rpt Received" id="wellComplRptRecvdt" type="date" value={form.wellComplRptRecvdt} onChange={set('wellComplRptRecvdt')} />
            <SelectField label="Well Compl Rpt Exempt" id="wellComplRptExmpt" placeholder="— Select —" options={EXEMPT_OPTIONS} value={form.wellComplRptExmpt} onChange={set('wellComplRptExmpt')} />
          </div>

          <div className="history-edit__form-actions">
            <Button type="submit" variant="primary" disabled={saving}>{saving ? 'Saving…' : (isAdd ? 'Add' : 'Save')}</Button>
            <Button type="button" variant="default" onClick={() => navigate(detailPath)} disabled={saving}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default HistoryPermitEdit;
