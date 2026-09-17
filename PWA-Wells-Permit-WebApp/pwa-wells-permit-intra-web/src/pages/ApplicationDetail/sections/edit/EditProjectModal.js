import { useEffect, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import SelectField from '../../../../components/UI/SelectField';
import MapPicker from '../../../../components/MapPicker/MapPicker';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateProjectInfo } from '../../../../api/applicationApi';
import { getCities, getStates } from '../../../../api/referenceApi';

function toDateInput(value) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return d.toISOString().slice(0, 10);
}

function EditProjectModal({ application, onClose, onSaved }) {
  const showToast = useToast();
  const [cities, setCities] = useState([]);
  const [states, setStates] = useState([]);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(() => ({
    siteCityCode: application.siteCityCode || '',
    siteLocation: application.siteLocation || '',
    siteLat: application.siteLat || '',
    siteLong: application.siteLong || '',
    projStartDate: toDateInput(application.projStartDate),
    projEndDate: toDateInput(application.projEndDate),
    ownerFirstName: application.ownerFirstName || '',
    ownerLastName: application.ownerLastName || '',
    ownerAddrStreet: application.ownerAddrStreet || '',
    ownerAddrCity: application.ownerAddrCity || '',
    ownerAddrState: application.ownerAddrState || '',
    ownerAddrZip: application.ownerAddrZip || '',
    ownerPhone: application.ownerPhone || '',
    ownerEmail: application.ownerEmail || '',
    clientFirstName: application.clientFirstName || '',
    clientLastName: application.clientLastName || '',
    clientAddrStreet: application.clientAddrStreet || '',
    clientAddrCity: application.clientAddrCity || '',
    clientAddrState: application.clientAddrState || '',
    clientAddrZip: application.clientAddrZip || '',
    clientPhone: application.clientPhone || '',
    clientEmail: application.clientEmail || '',
  }));

  useEffect(() => {
    let active = true;
    getCities().then((c) => { if (active) setCities(c || []); }).catch(() => {});
    getStates().then((s) => { if (active) setStates(s || []); }).catch(() => {});
    return () => { active = false; };
  }, []);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  // Called by the MapPicker when the marker moves (click/drag/search).
  const setSiteCoords = (lat, lng) => setForm((f) => ({ ...f, siteLat: lat, siteLong: lng }));
  const handlePlace = ({ address }) =>
    setForm((f) => ({ ...f, siteLocation: address || f.siteLocation }));

  async function handleSave() {
    setSaving(true);
    try {
      const payload = {
        ...form,
        projStartDate: form.projStartDate || null,
        projEndDate: form.projEndDate || null,
      };
      const updated = await updateProjectInfo(application.appId, payload);
      onSaved(updated);
      showToast('Project information updated.', 'success');
      onClose();
    } catch {
      showToast('Failed to update project information.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="xl"
      title="Edit Project Information"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>
      )}
    >
      <div className="modal-form-grid">
        <div className="modal-subhead">Project / Site</div>
        <SelectField label="Project Site City" id="siteCityCode" placeholder="— Select —" options={cities} value={form.siteCityCode} onChange={set('siteCityCode')} />
        <InputField label="Site Location" id="siteLocation" value={form.siteLocation} onChange={set('siteLocation')} />
        <div className="modal-span-2">
          <MapPicker
            idPrefix="site"
            lat={form.siteLat}
            lng={form.siteLong}
            onChange={setSiteCoords}
            onPlace={handlePlace}
            showSearch
          />
        </div>
        <InputField label="Project Start Date" id="projStartDate" type="date" value={form.projStartDate} onChange={set('projStartDate')} />
        <InputField label="Completion Date" id="projEndDate" type="date" value={form.projEndDate} onChange={set('projEndDate')} />

        <div className="modal-subhead">Property Owner</div>
        <InputField label="First Name" id="ownerFirstName" value={form.ownerFirstName} onChange={set('ownerFirstName')} />
        <InputField label="Last Name" id="ownerLastName" value={form.ownerLastName} onChange={set('ownerLastName')} />
        <InputField label="Street" id="ownerAddrStreet" value={form.ownerAddrStreet} onChange={set('ownerAddrStreet')} />
        <InputField label="City" id="ownerAddrCity" value={form.ownerAddrCity} onChange={set('ownerAddrCity')} />
        <SelectField label="State" id="ownerAddrState" placeholder="— Select —" options={states} value={form.ownerAddrState} onChange={set('ownerAddrState')} />
        <InputField label="Zip" id="ownerAddrZip" value={form.ownerAddrZip} onChange={set('ownerAddrZip')} />
        <InputField label="Phone" id="ownerPhone" value={form.ownerPhone} onChange={set('ownerPhone')} />
        <InputField label="E-Mail" id="ownerEmail" type="email" value={form.ownerEmail} onChange={set('ownerEmail')} />

        <div className="modal-subhead">Client</div>
        <InputField label="First Name" id="clientFirstName" value={form.clientFirstName} onChange={set('clientFirstName')} />
        <InputField label="Last Name" id="clientLastName" value={form.clientLastName} onChange={set('clientLastName')} />
        <InputField label="Street" id="clientAddrStreet" value={form.clientAddrStreet} onChange={set('clientAddrStreet')} />
        <InputField label="City" id="clientAddrCity" value={form.clientAddrCity} onChange={set('clientAddrCity')} />
        <SelectField label="State" id="clientAddrState" placeholder="— Select —" options={states} value={form.clientAddrState} onChange={set('clientAddrState')} />
        <InputField label="Zip" id="clientAddrZip" value={form.clientAddrZip} onChange={set('clientAddrZip')} />
        <InputField label="Phone" id="clientPhone" value={form.clientPhone} onChange={set('clientPhone')} />
        <InputField label="E-Mail" id="clientEmail" type="email" value={form.clientEmail} onChange={set('clientEmail')} />
      </div>
    </Modal>
  );
}

export default EditProjectModal;
