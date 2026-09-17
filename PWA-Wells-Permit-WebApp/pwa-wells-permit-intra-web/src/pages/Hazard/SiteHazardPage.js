import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getApplication, getPermitInfo } from '../../api/applicationApi';
import './SiteHazard.css';

const LOGO_SRC = `${process.env.PUBLIC_URL}/assets/permit-logo.png`;

function joinName(...parts) {
  return parts.map((p) => (p || '').trim()).filter(Boolean).join(' ');
}

function fmtDate(value) {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString();
}

function fmtTime(value) {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

// R = Required, A = As Needed. Renders the |x| / |  | check boxes the legacy PDF drew.
function EquipRow({ flag, label, desc }) {
  const f = String(flag || '').toUpperCase();
  return (
    <div className="haz-equip-row">
      <span className="haz-box">{f === 'R' ? 'x' : '\u00a0'}</span>
      <span className="haz-box">{f === 'A' ? 'x' : '\u00a0'}</span>
      <span className="haz-equip-label">{label}</span>
      {desc !== undefined && <span className="haz-equip-desc">{desc || ''}</span>}
    </div>
  );
}

function PpeBox({ on, label }) {
  return <span className="haz-ppe"><span className="haz-box">{on ? 'x' : '\u00a0'}</span> {label}</span>;
}

// Printable Site Hazard Information form — mirrors the legacy intra DisplayPdf.printSiteHazard
// layout. Opened in its own tab from Application Detail and printed to PDF via the browser.
function SiteHazardPage() {
  const { appId } = useParams();
  const [state, setState] = useState({ loading: true, error: '', application: null, permit: null });

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [application, permit] = await Promise.all([
          getApplication(appId),
          getPermitInfo(appId).catch(() => null),
        ]);
        if (active) setState({ loading: false, error: '', application, permit });
      } catch (err) {
        if (active) setState({ loading: false, error: 'Unable to load the site hazard information.', application: null, permit: null });
      }
    })();
    return () => { active = false; };
  }, [appId]);

  const { loading, error, application, permit } = state;

  useEffect(() => {
    document.title = application ? `Site Hazard ${application.appId}` : 'Site Hazard';
  }, [application]);

  if (loading) {
    return <div className="haz-shell"><p>Loading…</p></div>;
  }
  if (error || !application) {
    return <div className="haz-shell"><p className="haz-error">{error || 'Application not found.'}</p></div>;
  }

  const hazard = application.hazard || {};

  const permitNumbers = (permit?.permits || []).map((p) => p.permitNumber).filter(Boolean);
  let permitSummary = '';
  if (permitNumbers.length > 1) {
    permitSummary = `${permitNumbers[0]} to ${permitNumbers[permitNumbers.length - 1]}`;
  } else if (permitNumbers.length === 1) {
    permitSummary = permitNumbers[0];
  }

  const contaminants = hazard.contaminants || [];
  const substances = hazard.substances || [];

  return (
    <main className="haz-shell">
      <div className="haz-actions">
        <button type="button" className="btn btn-primary" onClick={() => window.print()}>Print / Save as PDF</button>
        <button type="button" className="btn btn-default" onClick={() => window.close()}>Close</button>
      </div>

      <div className="haz-doc">
        <header className="haz-head">
          <h1 className="haz-head-title">Alameda County Public Works Agency</h1>
          <div className="haz-subtitle">Water Resources — Site Hazard Information</div>
          <div className="haz-masthead">
            <img className="haz-logo" src={LOGO_SRC} alt="Alameda County Public Works Agency" />
            <div className="haz-office">
              <div>399 Elmhurst Street, Hayward, CA 94544-1395</div>
              <div>Telephone: (510) 670-6633 &nbsp;·&nbsp; Fax: (510) 782-1939</div>
            </div>
          </div>
        </header>

        <h2 className="haz-title">S I T E &nbsp; H A Z A R D &nbsp; I N F O R M A T I O N</h2>

        <section className="haz-grid">
          <div><span className="lbl">Owner's Name:</span> {joinName(application.ownerFirstName, application.ownerLastName)}</div>
          <div className="haz-right"><span className="lbl">Application ID:</span> {application.appId}</div>

          <div className="haz-span2"><span className="lbl">Site Address:</span> {application.siteLocation || ''}</div>

          <div>{application.siteCityName || application.siteCityCode || ''}</div>
          <div className="haz-right"><span className="lbl">Permit Numbers:</span> {permitSummary}</div>

          <div className="haz-span2"><span className="lbl">Consultant on Site:</span> {joinName(hazard.consultantFirstName, hazard.consultantLastName)}</div>
          <div><span className="lbl">Phone No.:</span> {hazard.consultantPhone || ''}</div>
          <div className="haz-right"><span className="lbl">Cell No.:</span> {hazard.consultantCell || ''}</div>

          <div className="haz-span2"><span className="lbl">Site Safety Officer:</span> {joinName(hazard.safetyOfficerFirstName, hazard.safetyOfficerLastName)}</div>
          <div><span className="lbl">Phone No.:</span> {hazard.safetyOfficerPhone || ''}</div>
          <div className="haz-right"><span className="lbl">Cell No.:</span> {hazard.safetyOfficerCell || ''}</div>

          <div className="haz-span2"><span className="lbl">Type of Facility:</span> {hazard.facilityType || ''}</div>
        </section>

        <section className="haz-section">
          <h3>Contaminant Name</h3>
          {contaminants.length === 0 ? (
            <p className="haz-empty">None reported.</p>
          ) : (
            <div className="haz-contaminants">
              {contaminants.map((name, idx) => (
                <div key={`${name}-${idx}`}>{idx + 1}. {name}</div>
              ))}
            </div>
          )}
        </section>

        <section className="haz-section">
          <h3>Contaminant Substances</h3>
          <table className="haz-table">
            <thead>
              <tr>
                <th>Expected Concentrations (PPM)</th>
                <th>PEL</th>
                <th>Health Effects</th>
              </tr>
            </thead>
            <tbody>
              {substances.length === 0 ? (
                <tr><td colSpan={3} className="haz-empty">None reported.</td></tr>
              ) : (
                substances.map((s, idx) => (
                  <tr key={idx}>
                    <td>{s.concentration || ''}</td>
                    <td>{s.pelPpm || ''}</td>
                    <td>{s.healthEffects || ''}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          <div className="haz-meeting">
            <span className="lbl">Site Safety Meeting:</span>
            <span>Date: {fmtDate(hazard.siteSafetyMeetingDateTime)}</span>
            <span>Time: {fmtTime(hazard.siteSafetyMeetingDateTime)}</span>
          </div>
        </section>

        <section className="haz-section">
          <h3>Level of Personal Protection</h3>
          <div className="haz-ppe-row">
            <PpeBox on={String(hazard.ppeLevelA || '').toUpperCase() === 'Y'} label="A (Highest)" />
            <PpeBox on={String(hazard.ppeLevelB || '').toUpperCase() === 'Y'} label="B (High)" />
            <PpeBox on={String(hazard.ppeLevelC || '').toUpperCase() === 'Y'} label="C (Medium)" />
            <PpeBox on={String(hazard.ppeLevelD || '').toUpperCase() === 'Y'} label="D (Low)" />
          </div>
        </section>

        <section className="haz-section">
          <h3>Personal Protective Equipment</h3>
          <p className="haz-legend">R = Required &nbsp; A = As Needed (with description of action concentrations)</p>
          <div className="haz-equip-head"><span className="haz-box haz-box--head">R</span><span className="haz-box haz-box--head">A</span></div>
          <div className="haz-equip-grid">
            <EquipRow flag={hazard.equipHardHatFlag} label="Hard Hat" />
            <EquipRow flag={hazard.equipClothingFlag} label="Clothing (Type):" desc={hazard.equipClothingDesc} />
            <EquipRow flag={hazard.equipSafetyShoesFlag} label="Safety Shoes" />
            <EquipRow flag={hazard.equipRespiratorFlag} label="Respirator (Type):" desc={hazard.equipRespiratorDesc} />
            <EquipRow flag={hazard.equipOrangeVestFlag} label="Orange Traffic Vest" />
            <EquipRow flag={hazard.equipCartridgeFlag} label="Cartridge (Type):" desc={hazard.equipCartridgeDesc} />
            <EquipRow flag={hazard.equipHearingProtFlag} label="Hearing Protection" />
            <EquipRow flag={hazard.equipGlovesFlag} label="Gloves (Type):" desc={hazard.equipGlovesDesc} />
            <EquipRow flag={hazard.equipSafetyEyewearFlag} label="Safety Eyewear" />
            <EquipRow flag={hazard.equipOtherFlag} label="Other:" desc={hazard.equipOtherDesc} />
          </div>
        </section>

        <section className="haz-provided">
          <div className="haz-provided-row">
            <span className="lbl">Site Hazard Information Provided By:</span>
            <span className="haz-underline">{joinName(hazard.infoProvidedByFirstName, hazard.infoProvidedByLastName)}</span>
            <span className="lbl">Phone:</span>
            <span className="haz-underline">{hazard.infoProvidedByPhone || ''}</span>
          </div>
          <div className="haz-provided-row">
            <span className="haz-underline haz-underline--title">{hazard.infoProvidedByTitle || ''}<em>Title</em></span>
            <span className="lbl">Date:</span>
            <span className="haz-underline">{fmtDate(hazard.addTs)}</span>
          </div>
        </section>
      </div>
    </main>
  );
}

export default SiteHazardPage;
