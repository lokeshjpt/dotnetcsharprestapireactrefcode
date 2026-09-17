import { useEffect, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import Loader from '../../components/Loader/Loader';
import Button from '../../components/UI/Button';
import InputField from '../../components/UI/InputField';
import SelectField from '../../components/UI/SelectField';
import { useToast } from '../../components/UI/Toaster/ToastProvider';
import { getHistoryWell, updateHistoryWell, createHistoryWell, getHistoryCities } from '../../api/historyApi';
import { toHistDateInput } from '../../utils/historyFormat';
import './SearchPage.css';
import './HistoryDetailEdit.css';

const EMPTY_FORM = {
  permitNum: '',
  tractNum: '',
  sectNum: '',
  addrStreet: '',
  addrCityCode: '',
  ownerName: '',
  coordX: '',
  coordY: '',
  matchLevel: '',
  tsrQq: '',
  recCode: '',
  phoneNum: '',
  drillDate: '',
  elevation: '',
  totalDepth: '',
  waterDepth: '',
  diameter: '',
  wellUse: '',
  logCode: '',
  wq: '',
  wi: '',
  yield: '',
  dtwCalc: '',
};

// Legacy search_hist_wells_detail.jsp form (UpdateAppServlet proc=updHistWell). In edit mode the
// Well Key is the identity and read-only; in add mode (legacy a=add) there is no Well Key and the
// record is INSERTed (SCOPE_IDENTITY returns the new key). The City select mirrors the legacy
// BeanHistoryCities dropdown; addr_city is derived from the selected code on save.
function HistoryWellEdit({ mode }) {
  const isAdd = mode === 'add';
  const { wellKey: routeWellKey } = useParams();
  const navigate = useNavigate();
  const showToast = useToast();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [cities, setCities] = useState([]);
  const [form, setForm] = useState(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    const cityPromise = getHistoryCities().catch(() => []);

    if (isAdd) {
      cityPromise
        .then((cityList) => {
          if (!active) return;
          setCities(cityList || []);
          setForm({ ...EMPTY_FORM });
          setError('');
        })
        .finally(() => { if (active) setLoading(false); });
      return () => { active = false; };
    }

    Promise.all([getHistoryWell(routeWellKey), cityPromise])
      .then(([w, cityList]) => {
        if (!active) return;
        setCities(cityList || []);
        setForm({
          permitNum: w.permitNum || '',
          tractNum: w.tractNum || '',
          sectNum: w.sectNum || '',
          addrStreet: w.addrStreet || '',
          addrCityCode: w.addrCityCode ? w.addrCityCode.trim() : '',
          ownerName: w.ownerName || '',
          coordX: w.coordX || '',
          coordY: w.coordY || '',
          matchLevel: w.matchLevel || '',
          tsrQq: w.tsrQq || '',
          recCode: w.recCode || '',
          phoneNum: w.phoneNum || '',
          drillDate: toHistDateInput(w.drillDate),
          elevation: w.elevation || '',
          totalDepth: w.totalDepth ?? '',
          waterDepth: w.waterDepth ?? '',
          diameter: w.diameter || '',
          wellUse: w.wellUse || '',
          logCode: w.logCode || '',
          wq: w.wq || '',
          wi: w.wi || '',
          yield: w.yield ?? '',
          dtwCalc: w.dtwCalc ?? '',
        });
        setError('');
      })
      .catch((err) => { if (active) setError(err?.response?.status === 404 ? 'notfound' : 'error'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [isAdd, routeWellKey]);

  const set = (key) => (event) => setForm((current) => ({ ...current, [key]: event.target.value }));

  const cityOptions = cities.map((c) => ({ code: (c.code || '').trim(), label: c.name }));

  const detailPath = isAdd ? '/search/history-wells' : `/search/history-wells/${routeWellKey}`;

  const handleSave = async (event) => {
    if (event) event.preventDefault();
    if (isAdd && (!form.tractNum.trim() || !form.sectNum.trim())) {
      showToast('Tract Number and Section Number are required.', 'error');
      return;
    }
    setSaving(true);
    try {
      const selectedCity = cityOptions.find((c) => c.code === form.addrCityCode);
      const payload = {
        ...form,
        addrCity: selectedCity ? selectedCity.label : '',
      };
      if (isAdd) {
        const created = await createHistoryWell(payload);
        showToast('History well location added.', 'success');
        navigate(`/search/history-wells/${created.wellKey}`);
      } else {
        await updateHistoryWell(routeWellKey, payload);
        showToast('History well location updated.', 'success');
        navigate(detailPath);
      }
    } catch {
      showToast(`Failed to ${isAdd ? 'add' : 'update'} the history well location.`, 'error');
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="page-shell"><Loader message="Loading well location…" /></div>;
  }

  if (error === 'notfound' || !form) {
    return (
      <div className="page-shell">
        <Link className="history-back-link" to="/search/history-wells">← Back to search</Link>
        <div className="alert alert-error">History well location not found.</div>
      </div>
    );
  }

  const title = isAdd ? 'Add History Well Location' : `Edit Well Location #${routeWellKey}`;

  return (
    <div className="page-shell">
      <Link className="history-back-link" to={detailPath}>← Back to {isAdd ? 'search' : 'detail'}</Link>
      <div className="panel">
        <h1 className="page-title">{title}</h1>
        <form onSubmit={handleSave}>
          <div className="history-edit__grid">
            <div className="modal-subhead">Location</div>
            <InputField label="Permit Number" id="permitNum" value={form.permitNum} onChange={set('permitNum')} />
            <InputField label="Tract #" id="tractNum" required value={form.tractNum} onChange={set('tractNum')} />
            <InputField label="Section #" id="sectNum" required value={form.sectNum} onChange={set('sectNum')} />
            <InputField label="Street Address" id="addrStreet" value={form.addrStreet} onChange={set('addrStreet')} />
            <SelectField label="City" id="addrCityCode" placeholder="— Select —" options={cityOptions} value={form.addrCityCode} onChange={set('addrCityCode')} />
            <InputField label="Owner Name" id="ownerName" value={form.ownerName} onChange={set('ownerName')} />
            <InputField label="Coordinate X" id="coordX" value={form.coordX} onChange={set('coordX')} />
            <InputField label="Coordinate Y" id="coordY" value={form.coordY} onChange={set('coordY')} />
            <InputField label="Match Level" id="matchLevel" value={form.matchLevel} onChange={set('matchLevel')} />
            <InputField label="TSR/QQ" id="tsrQq" value={form.tsrQq} onChange={set('tsrQq')} />
            <InputField label="Rec Code" id="recCode" value={form.recCode} onChange={set('recCode')} />
            <InputField label="Phone Number" id="phoneNum" value={form.phoneNum} onChange={set('phoneNum')} />

            <div className="modal-subhead">Well Details</div>
            <InputField label="Drill Date" id="drillDate" type="date" value={form.drillDate} onChange={set('drillDate')} />
            <InputField label="Elevation" id="elevation" value={form.elevation} onChange={set('elevation')} />
            <InputField label="Total Depth" id="totalDepth" type="number" value={form.totalDepth} onChange={set('totalDepth')} />
            <InputField label="Water Depth" id="waterDepth" type="number" step="any" value={form.waterDepth} onChange={set('waterDepth')} />
            <InputField label="Diameter" id="diameter" value={form.diameter} onChange={set('diameter')} />
            <InputField label="Well Use" id="wellUse" value={form.wellUse} onChange={set('wellUse')} />
            <InputField label="Log Code" id="logCode" value={form.logCode} onChange={set('logCode')} />
            <InputField label="Water Quality (WQ)" id="wq" value={form.wq} onChange={set('wq')} />
            <InputField label="Water Injection (WI)" id="wi" value={form.wi} onChange={set('wi')} />
            <InputField label="Yield" id="yield" type="number" value={form.yield} onChange={set('yield')} />
            <InputField label="DTW Calc" id="dtwCalc" type="number" value={form.dtwCalc} onChange={set('dtwCalc')} />
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

export default HistoryWellEdit;
