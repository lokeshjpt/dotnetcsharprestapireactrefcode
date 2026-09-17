import { useState } from 'react';
import Modal from '../../../../components/UI/Modal';
import Button from '../../../../components/UI/Button';
import InputField from '../../../../components/UI/InputField';
import SelectField from '../../../../components/UI/SelectField';
import { useToast } from '../../../../components/UI/Toaster/ToastProvider';
import { updateHazardInfo } from '../../../../api/applicationApi';

const KNOWN_CONTAMINANTS = ['Gasoline', 'Diesel', 'Waste Oil'];

const EQUIP_ITEMS = [
  { flag: 'equipHardHatFlag', label: 'Hard Hat' },
  { flag: 'equipSafetyShoesFlag', label: 'Safety Shoes' },
  { flag: 'equipOrangeVestFlag', label: 'Orange Traffic Vest' },
  { flag: 'equipHearingProtFlag', label: 'Hearing Protection' },
  { flag: 'equipSafetyEyewearFlag', label: 'Safety Eye Wear' },
  { flag: 'equipClothingFlag', label: 'Clothing (Type):', desc: 'equipClothingDesc' },
  { flag: 'equipRespiratorFlag', label: 'Respirator (Type):', desc: 'equipRespiratorDesc' },
  { flag: 'equipCartridgeFlag', label: 'Cartridge (Type):', desc: 'equipCartridgeDesc' },
  { flag: 'equipGlovesFlag', label: 'Gloves (Type):', desc: 'equipGlovesDesc' },
  { flag: 'equipOtherFlag', label: 'Other:', desc: 'equipOtherDesc' },
];

const HOURS = Array.from({ length: 12 }, (_, i) => String(i + 1).padStart(2, '0'));
const MINUTES = Array.from({ length: 60 }, (_, i) => String(i).padStart(2, '0'));

const pad = (n) => String(n).padStart(2, '0');

function parseMeeting(iso) {
  if (!iso) return { date: '', hour: '', minute: '', shift: 'AM' };
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return { date: '', hour: '', minute: '', shift: 'AM' };
  const date = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const h = d.getHours();
  const shift = h >= 12 ? 'PM' : 'AM';
  let h12 = h % 12;
  if (h12 === 0) h12 = 12;
  return { date, hour: pad(h12), minute: pad(d.getMinutes()), shift };
}

function combineMeeting(m) {
  if (!m.date) return null;
  let h = parseInt(m.hour || '12', 10);
  if (m.shift === 'PM' && h < 12) h += 12;
  if (m.shift === 'AM' && h === 12) h = 0;
  return `${m.date}T${pad(h)}:${m.minute || '00'}:00`;
}

function EditHazardModal({ application, onClose, onSaved }) {
  const showToast = useToast();
  const hazard = application.hazard || {};
  const [saving, setSaving] = useState(false);

  const [form, setForm] = useState(() => ({
    consultantFirstName: hazard.consultantFirstName || '',
    consultantLastName: hazard.consultantLastName || '',
    consultantPhone: hazard.consultantPhone || '',
    consultantCell: hazard.consultantCell || '',
    safetyOfficerFirstName: hazard.safetyOfficerFirstName || '',
    safetyOfficerLastName: hazard.safetyOfficerLastName || '',
    safetyOfficerPhone: hazard.safetyOfficerPhone || '',
    safetyOfficerCell: hazard.safetyOfficerCell || '',
    facilityType: hazard.facilityType || '',
    ppeLevelA: hazard.ppeLevelA || '',
    ppeLevelB: hazard.ppeLevelB || '',
    ppeLevelC: hazard.ppeLevelC || '',
    ppeLevelD: hazard.ppeLevelD || '',
    equipHardHatFlag: hazard.equipHardHatFlag || '',
    equipSafetyShoesFlag: hazard.equipSafetyShoesFlag || '',
    equipOrangeVestFlag: hazard.equipOrangeVestFlag || '',
    equipHearingProtFlag: hazard.equipHearingProtFlag || '',
    equipSafetyEyewearFlag: hazard.equipSafetyEyewearFlag || '',
    equipClothingFlag: hazard.equipClothingFlag || '',
    equipClothingDesc: hazard.equipClothingDesc || '',
    equipRespiratorFlag: hazard.equipRespiratorFlag || '',
    equipRespiratorDesc: hazard.equipRespiratorDesc || '',
    equipCartridgeFlag: hazard.equipCartridgeFlag || '',
    equipCartridgeDesc: hazard.equipCartridgeDesc || '',
    equipGlovesFlag: hazard.equipGlovesFlag || '',
    equipGlovesDesc: hazard.equipGlovesDesc || '',
    equipOtherFlag: hazard.equipOtherFlag || '',
    equipOtherDesc: hazard.equipOtherDesc || '',
    infoProvidedByFirstName: hazard.infoProvidedByFirstName || '',
    infoProvidedByLastName: hazard.infoProvidedByLastName || '',
    infoProvidedByTitle: hazard.infoProvidedByTitle || '',
    infoProvidedByPhone: hazard.infoProvidedByPhone || '',
  }));

  const [meeting, setMeeting] = useState(() => parseMeeting(hazard.siteSafetyMeetingDateTime));

  const existingContaminants = hazard.contaminants || [];
  const [contaminants, setContaminants] = useState(() => {
    const known = {};
    KNOWN_CONTAMINANTS.forEach((c) => { known[c] = existingContaminants.includes(c); });
    return known;
  });
  const [otherContaminants, setOtherContaminants] = useState(() =>
    existingContaminants.filter((c) => !KNOWN_CONTAMINANTS.includes(c)));

  const [substances, setSubstances] = useState(() => {
    const rows = (hazard.substances || []).map((s) => ({
      concentration: s.concentration || '',
      pelPpm: s.pelPpm || '',
      healthEffects: s.healthEffects || '',
    }));
    return rows.length ? rows : [{ concentration: '', pelPpm: '', healthEffects: '' }];
  });

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setMeet = (key) => (e) => setMeeting((m) => ({ ...m, [key]: e.target.value }));

  const togglePpe = (key) => (e) =>
    setForm((f) => ({ ...f, [key]: e.target.checked ? 'Y' : '' }));

  // Required/Available are mutually exclusive per the legacy JS (doCheckValidation).
  const toggleEquip = (flagKey, value) => (e) =>
    setForm((f) => ({ ...f, [flagKey]: e.target.checked ? value : (f[flagKey] === value ? '' : f[flagKey]) }));

  const toggleContam = (name) => (e) =>
    setContaminants((c) => ({ ...c, [name]: e.target.checked }));

  const setOther = (idx) => (e) =>
    setOtherContaminants((rows) => rows.map((r, i) => (i === idx ? e.target.value : r)));
  const addOther = () => setOtherContaminants((rows) => [...rows, '']);
  const removeOther = (idx) => setOtherContaminants((rows) => rows.filter((_, i) => i !== idx));

  const setSub = (idx, key) => (e) =>
    setSubstances((rows) => rows.map((r, i) => (i === idx ? { ...r, [key]: e.target.value } : r)));
  const addSub = () => setSubstances((rows) => [...rows, { concentration: '', pelPpm: '', healthEffects: '' }]);
  const removeSub = (idx) => setSubstances((rows) => rows.filter((_, i) => i !== idx));

  async function handleSave() {
    setSaving(true);
    try {
      const selectedContaminants = [
        ...KNOWN_CONTAMINANTS.filter((c) => contaminants[c]),
        ...otherContaminants.map((c) => c.trim()).filter(Boolean),
      ];
      const payload = {
        ...form,
        siteSafetyMeetingDateTime: combineMeeting(meeting),
        contaminants: selectedContaminants,
        substances: substances
          .filter((s) => s.concentration || s.pelPpm || s.healthEffects)
          .map((s) => ({
            concentration: s.concentration,
            pelPpm: s.pelPpm,
            healthEffects: s.healthEffects,
          })),
      };
      const updated = await updateHazardInfo(application.appId, payload);
      onSaved(updated);
      showToast('Site hazard information updated.', 'success');
      onClose();
    } catch {
      showToast('Failed to update site hazard information.', 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="lg"
      title="Edit Site Hazard Information"
      onClose={saving ? undefined : onClose}
      footer={(
        <>
          <Button variant="default" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button variant="primary" onClick={handleSave} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>
      )}
    >
      <div className="modal-form-grid">
        <div className="modal-subhead">On-Site Consultant</div>
        <InputField label="First Name" id="consultantFirstName" value={form.consultantFirstName} onChange={set('consultantFirstName')} />
        <InputField label="Last Name" id="consultantLastName" value={form.consultantLastName} onChange={set('consultantLastName')} />
        <InputField label="Phone" id="consultantPhone" value={form.consultantPhone} onChange={set('consultantPhone')} />
        <InputField label="Cell" id="consultantCell" value={form.consultantCell} onChange={set('consultantCell')} />

        <div className="modal-subhead">Site Safety Officer</div>
        <InputField label="First Name" id="safetyOfficerFirstName" value={form.safetyOfficerFirstName} onChange={set('safetyOfficerFirstName')} />
        <InputField label="Last Name" id="safetyOfficerLastName" value={form.safetyOfficerLastName} onChange={set('safetyOfficerLastName')} />
        <InputField label="Phone" id="safetyOfficerPhone" value={form.safetyOfficerPhone} onChange={set('safetyOfficerPhone')} />
        <InputField label="Cell" id="safetyOfficerCell" value={form.safetyOfficerCell} onChange={set('safetyOfficerCell')} />

        <div className="modal-subhead">Facility &amp; Safety Meeting</div>
        <InputField label="Type of Facility" id="facilityType" value={form.facilityType} onChange={set('facilityType')} />
        <InputField label="Site Safety Meeting Date" id="meetingDate" type="date" value={meeting.date} onChange={setMeet('date')} />
        <div className="modal-span-2 haz-time-row">
          <span className="form-label">Meeting Time</span>
          <SelectField label={null} id="meetingHour" placeholder="HH" options={HOURS.map((h) => ({ code: h, label: h }))} value={meeting.hour} onChange={setMeet('hour')} />
          <SelectField label={null} id="meetingMinute" placeholder="MM" options={MINUTES.map((m) => ({ code: m, label: m }))} value={meeting.minute} onChange={setMeet('minute')} />
          <SelectField label={null} id="meetingShift" options={[{ code: 'AM', label: 'AM' }, { code: 'PM', label: 'PM' }]} value={meeting.shift} onChange={setMeet('shift')} />
        </div>

        <div className="modal-subhead">Level of Personal Protection Equipment</div>
        <div className="modal-span-2 haz-ppe-levels">
          <label><input type="checkbox" checked={form.ppeLevelA === 'Y'} onChange={togglePpe('ppeLevelA')} /> A (Highest)</label>
          <label><input type="checkbox" checked={form.ppeLevelB === 'Y'} onChange={togglePpe('ppeLevelB')} /> B (High)</label>
          <label><input type="checkbox" checked={form.ppeLevelC === 'Y'} onChange={togglePpe('ppeLevelC')} /> C (Medium)</label>
          <label><input type="checkbox" checked={form.ppeLevelD === 'Y'} onChange={togglePpe('ppeLevelD')} /> D (Low)</label>
        </div>

        <div className="modal-subhead">Personal Protective Equipment</div>
        <table className="modal-spec-table haz-equip-table modal-span-2">
          <thead>
            <tr>
              <th>Equipment</th>
              <th>Required</th>
              <th>Available</th>
              <th>Type / Description</th>
            </tr>
          </thead>
          <tbody>
            {EQUIP_ITEMS.map((item) => (
              <tr key={item.flag}>
                <td>{item.label}</td>
                <td><input type="checkbox" aria-label={`${item.label} required`} checked={form[item.flag] === 'R'} onChange={toggleEquip(item.flag, 'R')} /></td>
                <td><input type="checkbox" aria-label={`${item.label} available`} checked={form[item.flag] === 'A'} onChange={toggleEquip(item.flag, 'A')} /></td>
                <td>
                  {item.desc
                    ? <input className="form-control" aria-label={`${item.label} description`} value={form[item.desc]} onChange={set(item.desc)} />
                    : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="modal-subhead">Anticipated Hazardous Substances</div>
        <div className="modal-span-2 haz-ppe-levels">
          {KNOWN_CONTAMINANTS.map((c) => (
            <label key={c}><input type="checkbox" checked={!!contaminants[c]} onChange={toggleContam(c)} /> {c}</label>
          ))}
        </div>
        <div className="modal-span-2">
          {otherContaminants.map((val, idx) => (
            <div key={idx} className="haz-inline-row">
              <input className="form-control" aria-label={`Other contaminant ${idx + 1}`} placeholder="Other contaminant" value={val} onChange={setOther(idx)} />
              <Button variant="default" onClick={() => removeOther(idx)}>Remove</Button>
            </div>
          ))}
          <Button variant="default" onClick={addOther}>Insert more Contaminants</Button>
        </div>

        <div className="modal-subhead">Expected Concentrations</div>
        <table className="modal-spec-table modal-span-2">
          <thead>
            <tr>
              <th>Concentration (ppm) — medium (soil/water/air)</th>
              <th>PEL (ppm)</th>
              <th>Health Effects</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {substances.map((row, idx) => (
              <tr key={idx}>
                <td><input className="form-control" aria-label={`Concentration ${idx + 1}`} value={row.concentration} onChange={setSub(idx, 'concentration')} /></td>
                <td><input className="form-control" aria-label={`PEL ${idx + 1}`} value={row.pelPpm} onChange={setSub(idx, 'pelPpm')} /></td>
                <td><input className="form-control" aria-label={`Health effects ${idx + 1}`} value={row.healthEffects} onChange={setSub(idx, 'healthEffects')} /></td>
                <td>{substances.length > 1 ? <Button variant="default" onClick={() => removeSub(idx)}>Rem</Button> : null}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="modal-span-2">
          <Button variant="default" onClick={addSub}>Insert more Substances</Button>
        </div>

        <div className="modal-subhead">Information Provided By</div>
        <InputField label="Preparer First Name" id="infoProvidedByFirstName" value={form.infoProvidedByFirstName} onChange={set('infoProvidedByFirstName')} />
        <InputField label="Last Name" id="infoProvidedByLastName" value={form.infoProvidedByLastName} onChange={set('infoProvidedByLastName')} />
        <InputField label="Title" id="infoProvidedByTitle" value={form.infoProvidedByTitle} onChange={set('infoProvidedByTitle')} />
        <InputField label="Phone" id="infoProvidedByPhone" value={form.infoProvidedByPhone} onChange={set('infoProvidedByPhone')} />
      </div>
    </Modal>
  );
}

export default EditHazardModal;
