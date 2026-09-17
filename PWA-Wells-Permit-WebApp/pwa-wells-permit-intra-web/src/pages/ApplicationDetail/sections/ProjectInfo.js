import { useEffect, useState } from 'react';
import DetailSection, { DetailField } from '../../../components/DetailSection/DetailSection';
import Button from '../../../components/UI/Button';
import { getCities } from '../../../api/referenceApi';
import EditProjectModal from './edit/EditProjectModal';
import EditApplicantModal from './edit/EditApplicantModal';
import EditHazardModal from './edit/EditHazardModal';

function fullName(first, last) {
  return `${first || ''} ${last || ''}`.trim();
}

function address(street, city, state, zip, street2) {
  const line1 = [street, street2].filter(Boolean).join(', ');
  const line2 = [city, [state, zip].filter(Boolean).join(' ')].filter(Boolean).join(', ');
  return [line1, line2].filter(Boolean).join('\n');
}

function emailLink(email) {
  if (!email) return null;
  return <a href={`mailto:${email}`}>{email}</a>;
}

function ProjectInfo({ application, onUpdated }) {
  const hazard = application.hazard;
  const [openModal, setOpenModal] = useState(null);
  const [cityNames, setCityNames] = useState({});

  // Resolve the project-site city code to its name (the record stores only the code).
  useEffect(() => {
    let active = true;
    getCities()
      .then((cities) => {
        if (!active) return;
        const map = {};
        (cities || []).forEach((city) => { if (city.code) map[city.code] = city.label; });
        setCityNames(map);
      })
      .catch(() => {});
    return () => { active = false; };
  }, []);

  const siteCityName = cityNames[application.siteCityCode]
    || application.siteCityName
    || application.siteCityCode
    || null;

  const handleSaved = (updated) => {
    if (updated && onUpdated) onUpdated(updated);
  };

  const ownerName = fullName(application.ownerFirstName, application.ownerLastName);
  const clientName = fullName(application.clientFirstName, application.clientLastName);
  const applicantName = fullName(application.appFirstName, application.appLastName);
  const applicantLabel = application.appBusinessName
    ? `${application.appBusinessName}${applicantName ? ` - ${applicantName}` : ''}`
    : applicantName;
  const contactName = fullName(application.contactFirstName, application.contactLastName);

  const latLong = (application.siteLat || application.siteLong)
    ? `${application.siteLat || '—'} / ${application.siteLong || '—'}`
    : null;

  return (
    <>
      <DetailSection title="Project Information" actions={<Button variant="default" onClick={() => setOpenModal('project')}>Edit</Button>}>
        <div className="detail-col">
          <DetailField label="Project Site City">{siteCityName}</DetailField>
          <DetailField label="Project Start Date">
            {application.projStartDate ? new Date(application.projStartDate).toLocaleDateString() : null}
          </DetailField>
          <DetailField label="Property Owner">{ownerName}</DetailField>
          <DetailField label="Owner Address" wrap>
            {address(application.ownerAddrStreet, application.ownerAddrCity, application.ownerAddrState, application.ownerAddrZip)}
          </DetailField>
          <DetailField label="Client">{clientName}</DetailField>
          <DetailField label="Client Address" wrap>
            {address(application.clientAddrStreet, application.clientAddrCity, application.clientAddrState, application.clientAddrZip)}
          </DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Site Location">{application.siteLocation}</DetailField>
          <DetailField label="Lat/Long">{latLong}</DetailField>
          <DetailField label="Completion Date">
            {application.projEndDate ? new Date(application.projEndDate).toLocaleDateString() : null}
          </DetailField>
          {application.extendCount != null && (
            <>
              <DetailField label="Extension Start Date">
                {application.extendStartDate ? new Date(application.extendStartDate).toLocaleDateString() : null}
              </DetailField>
              <DetailField label="Extension End Date">
                {application.extendEndDate ? new Date(application.extendEndDate).toLocaleDateString() : null}
              </DetailField>
            </>
          )}
          <DetailField label="Owner Phone">{application.ownerPhone}</DetailField>
          <DetailField label="Owner E-Mail">{emailLink(application.ownerEmail)}</DetailField>
          <DetailField label="Client Phone">{application.clientPhone}</DetailField>
          <DetailField label="Client E-Mail">{emailLink(application.clientEmail)}</DetailField>
        </div>
      </DetailSection>

      <DetailSection title="Applicant Information" actions={<Button variant="default" onClick={() => setOpenModal('applicant')}>Edit</Button>}>
        <div className="detail-col">
          <DetailField label="Applicant">{applicantLabel}</DetailField>
          <DetailField label="Address" wrap>
            {address(application.appAddrStreet, application.appAddrCity, application.appAddrState, application.appAddrZip, application.appAddrStreet2)}
          </DetailField>
          <DetailField label="Contact Name">{contactName}</DetailField>
          <DetailField label="Contact Phone">{application.contactPhone}</DetailField>
        </div>
        <div className="detail-col">
          <DetailField label="Phone Number">{application.appPhone}</DetailField>
          <DetailField label="E-Mail">{emailLink(application.appEmailAddr)}</DetailField>
          <DetailField label="Contact E-Mail">{emailLink(application.contactEmail)}</DetailField>
          <DetailField label="Contact Cell">{application.contactCell}</DetailField>
        </div>
      </DetailSection>

      {hazard && (
        <DetailSection
          title="Site Hazard Information"
          actions={(
            <>
              <Button variant="default" onClick={() => window.open(`${window.location.pathname}#/hazard/${application.appId}`, '_blank', 'noopener')}>Print Site Hazard</Button>
              <Button variant="default" onClick={() => setOpenModal('hazard')}>Edit</Button>
            </>
          )}
        >
          <div className="detail-col">
            <DetailField label="Consultant Name">
              {fullName(hazard.consultantFirstName, hazard.consultantLastName)}
            </DetailField>
          </div>
          <div className="detail-col">
            <DetailField label="Site Safety Officer Name">
              {fullName(hazard.safetyOfficerFirstName, hazard.safetyOfficerLastName)}
            </DetailField>
          </div>
        </DetailSection>
      )}

      {openModal === 'project' && (
        <EditProjectModal application={application} onClose={() => setOpenModal(null)} onSaved={handleSaved} />
      )}
      {openModal === 'applicant' && (
        <EditApplicantModal application={application} onClose={() => setOpenModal(null)} onSaved={handleSaved} />
      )}
      {openModal === 'hazard' && hazard && (
        <EditHazardModal application={application} onClose={() => setOpenModal(null)} onSaved={handleSaved} />
      )}
    </>
  );
}

export default ProjectInfo;
