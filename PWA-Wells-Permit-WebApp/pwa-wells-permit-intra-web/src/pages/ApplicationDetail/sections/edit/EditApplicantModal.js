import { useEffect, useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import SelectField from '../../../../components/UI/SelectField';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateApplicantInfo } from '../../../../api/applicationApi';
import { getStates } from '../../../../api/referenceApi';

function EditApplicantModal({ application, onClose, onSaved }) {
  const showToast = useToast();
  const [saving, setSaving] = useState(false);
  const [states, setStates] = useState([]);
  const [form, setForm] = useState(() => ({
    appBusinessName: application.appBusinessName || '',
    appFirstName: application.appFirstName || '',
    appLastName: application.appLastName || '',
    appEmailAddr: application.appEmailAddr || '',
    appAddrStreet: application.appAddrStreet || '',
    appAddrStreet2: application.appAddrStreet2 || '',
    appAddrCity: application.appAddrCity || '',
    appAddrState: application.appAddrState || '',
    appAddrZip: application.appAddrZip || '',
    appPhone: application.appPhone || '',
    appFax: application.appFax || '',
    contactFirstName: application.contactFirstName || '',
    contactLastName: application.contactLastName || '',
    contactEmail: application.contactEmail || '',
    contactPhone: application.contactPhone || '',
    contactCell: application.contactCell || '',
  }));
  const [emailCcs, setEmailCcs] = useState(() => [...(application.emailCcs || [])]);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setCc = (idx) => (e) =>
    setEmailCcs((list) => list.map((v, i) => (i === idx ? e.target.value : v)));
  const addCc = () => setEmailCcs((list) => [...list, '']);
  const removeCc = (idx) => setEmailCcs((list) => list.filter((_, i) => i !== idx));

  useEffect(() => {
    let active = true;
    getStates().then((s) => { if (active) setStates(s || []); }).catch(() => {});
    return () => { active = false; };
  }, []);

  async function handleSave() {
    setSaving(true);
    try {
      const updated = await updateApplicantInfo(application.appId, {
        ...form,
        emailCcs: emailCcs.map((e) => e.trim()).filter(Boolean),
      });
      onSaved(updated);
      showToast('Applicant information updated.', 'success');
      onClose();
    } catch {
      showToast('Failed to update applicant information.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="lg"
      title="Edit Applicant Information"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>
      )}
    >
      <div className="modal-form-grid">
        <div className="modal-subhead">Applicant</div>
        <div className="modal-span-2">
          <InputField label="Business Name" id="appBusinessName" value={form.appBusinessName} onChange={set('appBusinessName')} />
        </div>
        <InputField label="First Name" id="appFirstName" value={form.appFirstName} onChange={set('appFirstName')} />
        <InputField label="Last Name" id="appLastName" value={form.appLastName} onChange={set('appLastName')} />
        <InputField label="Street" id="appAddrStreet" value={form.appAddrStreet} onChange={set('appAddrStreet')} />
        <InputField label="Street 2" id="appAddrStreet2" value={form.appAddrStreet2} onChange={set('appAddrStreet2')} />
        <InputField label="City" id="appAddrCity" value={form.appAddrCity} onChange={set('appAddrCity')} />
        <SelectField label="State" id="appAddrState" placeholder="— Select —" options={states} value={form.appAddrState} onChange={set('appAddrState')} />
        <InputField label="Zip" id="appAddrZip" value={form.appAddrZip} onChange={set('appAddrZip')} />
        <InputField label="Phone Number" id="appPhone" value={form.appPhone} onChange={set('appPhone')} />
        <InputField label="Fax" id="appFax" value={form.appFax} onChange={set('appFax')} />
        <InputField label="E-Mail" id="appEmailAddr" type="email" value={form.appEmailAddr} onChange={set('appEmailAddr')} />

        <div className="modal-subhead">Contact</div>
        <InputField label="First Name" id="contactFirstName" value={form.contactFirstName} onChange={set('contactFirstName')} />
        <InputField label="Last Name" id="contactLastName" value={form.contactLastName} onChange={set('contactLastName')} />
        <InputField label="Phone" id="contactPhone" value={form.contactPhone} onChange={set('contactPhone')} />
        <InputField label="Cell" id="contactCell" value={form.contactCell} onChange={set('contactCell')} />
        <InputField label="E-Mail" id="contactEmail" type="email" value={form.contactEmail} onChange={set('contactEmail')} />

        <div className="modal-subhead">Other E-Mails (CC)</div>
        <div className="modal-span-2">
          {emailCcs.length === 0 && (
            <p className="text-muted" style={{ margin: '2px 0 6px' }}>No additional CC recipients.</p>
          )}
          {emailCcs.map((val, idx) => (
            <div key={idx} className="haz-inline-row">
              <input
                className="form-control"
                type="email"
                aria-label={`CC Email ${idx + 1}`}
                placeholder="name@example.com"
                value={val}
                onChange={setCc(idx)}
              />
              <Button variant="default" onClick={() => removeCc(idx)}>Remove</Button>
            </div>
          ))}
          <Button variant="default" onClick={addCc}>Add CC Email</Button>
        </div>
      </div>
    </Modal>
  );
}

export default EditApplicantModal;
