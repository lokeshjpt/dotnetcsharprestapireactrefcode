import FormSection from '../../../components/FormSection/FormSection';
import { formatDollar, isOtherDrillMethod, isBoreholeCategory } from '../referenceData';
import { formatPaymentType } from '../../../constants/paymentTypes';
import { effectiveWorks, workFee, workWellCount } from '../works';

function Row({ label, value }) {
  return (
    <tr>
      <th scope="row" style={{ width: '40%' }}>{label}</th>
      <td>{value === undefined || value === null || value === '' ? 'Not provided' : value}</td>
    </tr>
  );
}

function phone(fd, prefix) {
  const p1 = fd[`${prefix}1`];
  const p2 = fd[`${prefix}2`];
  const p3 = fd[`${prefix}3`];
  if (!p1 && !p2 && !p3) return '';
  const ext = fd[`${prefix}X`] ? ` x${fd[`${prefix}X`]}` : '';
  return `(${p1}) ${p2}-${p3}${ext}`;
}

function Step6Verify({ formData, amountDue, onEdit }) {
  const fd = formData;
  const works = effectiveWorks(fd);
  const showHazard = fd.sitehazardrequired === 'Y';
  const contaminants = [];
  if (fd.hazContamGasoline) contaminants.push('Gasoline');
  if (fd.hazContamDiesel) contaminants.push('Diesel');
  if (fd.hazContamWasteOil) contaminants.push('Waste Oil');
  (fd.hazContamOthers || []).forEach((c) => { if (c && c.trim()) contaminants.push(c.trim()); });
  const substances = (fd.hazSubstances || []).filter((r) => r.concentration || r.pelPpm || r.healthEffects);

  const EditButton = ({ step }) =>
    onEdit ? (
      <button type="button" className="btn btn-default form-section__edit" onClick={() => onEdit(step)}>
        Edit
      </button>
    ) : null;

  return (
    <>
      <div className="alert alert-success">Review your application below, then submit.</div>

      <FormSection title="Applicant" borderColor="m-blue" headerRight={<EditButton step="applicant" />}>
        <table className="simple-table">
          <tbody>
            <Row label="Business Name" value={fd.appBusinessName} />
            <Row label="Name" value={`${fd.appFirstName} ${fd.appLastName}`.trim()} />
            <Row label="Mailing Address" value={[fd.appAddr, fd.appAddr2, `${fd.appCity}, ${fd.appState} ${fd.appZip}`].filter(Boolean).join(', ')} />
            <Row label="Phone" value={phone(fd, 'appPhone')} />
            <Row label="Fax" value={phone(fd, 'appFax')} />
            <Row label="Email" value={fd.appEmail} />
            <Row label="Contact Name" value={`${fd.conFirstName} ${fd.conLastName}`.trim()} />
            <Row label="Contact Email" value={fd.conEmail} />
            <Row label="Contact Phone" value={phone(fd, 'conPhone')} />
            <Row label="Contact Cell" value={phone(fd, 'conCell')} />
            <Row label="CC Emails" value={(fd.ccEmails || []).filter(Boolean).join(', ')} />
          </tbody>
        </table>
      </FormSection>

      <FormSection title="Project / Location" borderColor="orange" headerRight={<EditButton step="project" />}>
        <table className="simple-table">
          <tbody>
            <Row label="Location" value={fd.siteLoc} />
            <Row label="Site City" value={fd.siteCityName} />
            <Row label="Coordinates" value={fd.siteLat && fd.siteLong ? `${fd.siteLat}, ${fd.siteLong}` : ''} />
            <Row label="Start Date" value={fd.startDate} />
            <Row label="Completion Date" value={fd.endDate} />
            <Row label="Site Hazards" value={fd.sitehazardrequired === 'Y' ? 'Yes' : 'No'} />
            <Row label="Owner Name" value={`${fd.ownFirstName} ${fd.ownLastName}`.trim()} />
            <Row label="Owner Address" value={[fd.ownAddr, `${fd.ownCity}, ${fd.ownState} ${fd.ownZip}`].filter((s) => s && s.trim() && s !== ', ').join(', ')} />
            <Row label="Owner Phone" value={phone(fd, 'ownPhone')} />
            <Row label="Owner Email" value={fd.ownEmail} />
            {fd.cliLastName ? (
              <>
                <Row label="Client Name" value={`${fd.cliFirstName} ${fd.cliLastName}`.trim()} />
                <Row label="Client Address" value={[fd.cliAddr, `${fd.cliCity}, ${fd.cliState} ${fd.cliZip}`].filter((s) => s && s.trim() && s !== ', ').join(', ')} />
                <Row label="Client Phone" value={phone(fd, 'cliPhone')} />
                <Row label="Client Email" value={fd.cliEmail} />
              </>
            ) : (
              <Row label="Client" value="Same as Property Owner" />
            )}
          </tbody>
        </table>
      </FormSection>

      <FormSection title={`Work${works.length === 1 ? '' : 's'} Requesting Permit`} borderColor="l-blue" headerRight={<EditButton step="works" />}>
        {works.length === 0 ? (
          <p className="text-muted">No work types have been added.</p>
        ) : (
          works.map((work, wi) => {
            const specs = work.wellSpecs || [];
            const method = isOtherDrillMethod(work.dmeth, work.dmethName) ? (work.dmethOth || 'Other') : (work.dmethName || work.dmeth);
            const isBore = isBoreholeCategory(work.workCat);
            return (
              <div key={wi} className="verify-work" style={{ marginBottom: wi < works.length - 1 ? 20 : 0 }}>
                <table className="simple-table">
                  <tbody>
                    <Row label="Work Type" value={work.workDesc || work.workType} />
                    <Row label="Well Use" value={work.wUseDesc || work.wUse} />
                    <Row label="Driller" value={work.drillerName} />
                    <Row label="Driller License #" value={work.drillerLic} />
                    <Row label="Drilling Method" value={method} />
                    <Row label={isBore ? 'Number of Boreholes' : 'Number of Wells'} value={workWellCount(work)} />
                    {isBore && <Row label="Hole Diameter (in)" value={work.holediam} />}
                    {isBore && <Row label="Maximum Depth (ft)" value={work.maxdepth} />}
                    <Row label="Work Total" value={formatDollar(workFee(work))} />
                  </tbody>
                </table>
                {!isBore && specs.length > 0 && (
                  <table className="data-table" style={{ marginTop: 12 }}>
                    <thead>
                      <tr>
                        <th scope="col">Owner Well Id</th>
                        <th scope="col">Hole Diam</th>
                        <th scope="col">Casing Diam</th>
                        <th scope="col">Seal Depth</th>
                        <th scope="col">Max Depth</th>
                        <th scope="col">Latitude</th>
                        <th scope="col">Longitude</th>
                      </tr>
                    </thead>
                    <tbody>
                      {specs.map((row, i) => (
                        <tr key={i}>
                          <td>{row.owellnum}</td>
                          <td>{row.holediam}</td>
                          <td>{row.casediam}</td>
                          <td>{row.sealdepth}</td>
                          <td>{row.maxdepth}</td>
                          <td>{row.latitude}</td>
                          <td>{row.longitude}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            );
          })
        )}
      </FormSection>

      {showHazard && (
        <FormSection title="Site Hazard Information" borderColor="l-blue" headerRight={<EditButton step="hazard" />}>
          <table className="simple-table">
            <tbody>
              <Row label="Site Consultant" value={`${fd.hazConsultantFirstName} ${fd.hazConsultantLastName}`.trim()} />
              <Row label="Consultant Phone" value={phone(fd, 'hazConsultantPhone')} />
              <Row label="Consultant Cell" value={phone(fd, 'hazConsultantCell')} />
              <Row label="Site Safety Officer" value={`${fd.hazSafetyFirstName} ${fd.hazSafetyLastName}`.trim()} />
              <Row label="Safety Officer Phone" value={phone(fd, 'hazSafetyPhone')} />
              <Row label="Safety Officer Cell" value={phone(fd, 'hazSafetyCell')} />
              <Row label="Type of Facility" value={fd.hazFacilityType} />
              <Row label="Site Safety Meeting Date" value={fd.hazMeetingDate} />
              <Row label="Site Safety Meeting Time" value={`${fd.hazMeetingHour}:${fd.hazMeetingMinute} ${fd.hazMeetingShift}`} />
              <Row label="Contaminants" value={contaminants.join(', ')} />
              <Row label="PPE Level(s)" value={[fd.hazPpeA && 'A', fd.hazPpeB && 'B', fd.hazPpeC && 'C', fd.hazPpeD && 'D'].filter(Boolean).join(', ')} />
              <Row label="Provider" value={`${fd.hazProviderFirstName || ''} ${fd.hazProviderLastName || ''}`.trim()} />
              <Row label="Provider Title" value={fd.hazProviderTitle} />
              <Row label="Provider Phone" value={phone(fd, 'hazProviderPhone')} />
              <Row label="Acknowledgement" value={fd.hazAcknowledgement ? 'Acknowledged' : 'Not acknowledged'} />
            </tbody>
          </table>
          {substances.length > 0 && (
            <table className="data-table" style={{ marginTop: 12 }}>
              <thead>
                <tr>
                  <th scope="col">Expected Concentrations (ppm)</th>
                  <th scope="col">PEL (ppm)</th>
                  <th scope="col">Health Effects</th>
                </tr>
              </thead>
              <tbody>
                {substances.map((row, i) => (
                  <tr key={i}>
                    <td>{row.concentration}</td>
                    <td>{row.pelPpm}</td>
                    <td>{row.healthEffects}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </FormSection>
      )}

      <FormSection title="Payment" borderColor="m-blue" headerRight={<EditButton step="payment" />}>
        <table className="simple-table">
          <tbody>
            <Row label="Payment Type" value={formatPaymentType(fd.paymentType)} />
            {fd.paymentType === 'CHECK' && <Row label="Name on Account" value={fd.acctName} />}
            <Row label="Amount Due" value={formatDollar(amountDue)} />
          </tbody>
        </table>
      </FormSection>
    </>
  );
}

export default Step6Verify;