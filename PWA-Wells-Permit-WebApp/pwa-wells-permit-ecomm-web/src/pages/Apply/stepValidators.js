import {
  validateText,
  validateEmail,
  validateZip,
  validateNumeric,
  validateDecimal,
  validateStartDate,
  validateEndDate,
  validateDate,
  isEmpty,
} from './validation';
import { effectiveWorks } from './works';
import { isOtherDrillMethod, isBoreholeCategory } from './referenceData';


function validatePhone(fd, prefix, label, required, errors) {
  const p1 = fd[`${prefix}1`];
  const p2 = fd[`${prefix}2`];
  const p3 = fd[`${prefix}3`];
  const anyEntered = !isEmpty(p1) || !isEmpty(p2) || !isEmpty(p3);
  if (!anyEntered) {
    if (required) errors[`${prefix}1`] = `${label} is required.`;
    return;
  }
  if (isEmpty(p1) || !/^\d{3}$/.test(String(p1))) errors[`${prefix}1`] = `${label} area code must be 3 digits.`;
  else if (isEmpty(p2) || !/^\d{3}$/.test(String(p2))) errors[`${prefix}2`] = `${label} prefix must be 3 digits.`;
  else if (isEmpty(p3) || !/^\d{4}$/.test(String(p3))) errors[`${prefix}3`] = `${label} line number must be 4 digits.`;
}

function applicantErrors(fd) {
  const e = {};
  e.appBusinessName = validateText(fd.appBusinessName, { label: 'Applicant Business Name', required: true, maxLen: 100 });
  e.appLastName = validateText(fd.appLastName, { label: 'Applicant Last Name', required: true, maxLen: 50 });
  e.appFirstName = validateText(fd.appFirstName, { label: 'First Name', required: true, maxLen: 50 });
  e.appAddr = validateText(fd.appAddr, { label: 'Mailing Address', required: true, maxLen: 50 });
  e.appAddr2 = validateText(fd.appAddr2, { label: 'Address Line 2', required: false, maxLen: 50 });
  e.appCity = validateText(fd.appCity, { label: 'City', required: true, maxLen: 50 });
  e.appState = isEmpty(fd.appState) ? 'State is required.' : null;
  e.appZip = validateZip(fd.appZip, { label: 'Zip Code', required: true });
  validatePhone(fd, 'appPhone', 'Phone', true, e);
  if (!isEmpty(fd.appPhoneX)) e.appPhoneX = validateNumeric(fd.appPhoneX, { label: 'Extension', required: false, maxLen: 5 });
  e.appEmail = validateEmail(fd.appEmail, { label: 'Email Address', required: true });
  if (!isEmpty(fd.conEmail)) e.conEmail = validateEmail(fd.conEmail, { label: 'Contact Email', required: false });
  (fd.ccEmails || []).forEach((email, i) => {
    if (!isEmpty(email)) {
      const msg = validateEmail(email, { label: 'CC Email', required: false });
      if (msg) e[`ccEmails.${i}`] = msg;
    }
  });
  return e;
}

function locationErrors(fd) {
  const e = {};
  e.siteLoc = validateText(fd.siteLoc, { label: 'Location Address / Description', required: true, maxLen: 200 });
  e.siteCity = isEmpty(fd.siteCity) ? 'Please select the location city.' : null;
  e.siteLat = (isEmpty(fd.siteLat) || isEmpty(fd.siteLong))
    ? 'Please identify the project location on the map.'
    : null;
  return e;
}

function projectErrors(fd) {
  const e = {};
  e.startDate = validateStartDate(fd.startDate, { label: 'Project Start Date', required: true });
  e.endDate = validateEndDate(fd.endDate, fd.startDate, { label: 'Project Completion Date', required: true });
  e.sitehazardrequired = isEmpty(fd.sitehazardrequired) ? 'Please indicate whether there are site hazards.' : null;

  e.ownLastName = validateText(fd.ownLastName, { label: 'Owner Last Name', required: true, maxLen: 50 });
  e.ownFirstName = validateText(fd.ownFirstName, { label: 'Owner First Name', required: true, maxLen: 50 });
  e.ownAddr = validateText(fd.ownAddr, { label: 'Owner Mail Address', required: true, maxLen: 50 });
  e.ownCity = validateText(fd.ownCity, { label: 'Owner City', required: true, maxLen: 50 });
  e.ownState = isEmpty(fd.ownState) ? 'Owner State is required.' : null;
  e.ownZip = validateZip(fd.ownZip, { label: 'Owner Zip Code', required: true });
  if (!isEmpty(fd.ownEmail)) e.ownEmail = validateEmail(fd.ownEmail, { label: 'Owner Email', required: false });

  // Client is optional ("Required if different from Property Owner"). Matches Java BeanApp:
  // only when a Client Last Name is entered do the remaining client fields become required.
  const clientProvided = !isEmpty(fd.cliLastName);
  e.cliLastName = validateText(fd.cliLastName, { label: 'Client Last Name', required: false, maxLen: 50 });
  e.cliFirstName = validateText(fd.cliFirstName, { label: 'Client First Name', required: clientProvided, maxLen: 50 });
  e.cliAddr = validateText(fd.cliAddr, { label: 'Client Mail Address', required: clientProvided, maxLen: 50 });
  e.cliCity = validateText(fd.cliCity, { label: 'Client City', required: clientProvided, maxLen: 50 });
  e.cliState = clientProvided && isEmpty(fd.cliState) ? 'Client State is required.' : null;
  e.cliZip = validateZip(fd.cliZip, { label: 'Client Zip Code', required: clientProvided });
  if (!isEmpty(fd.cliEmail)) e.cliEmail = validateEmail(fd.cliEmail, { label: 'Client Email', required: false });
  return e;
}

function workTypeErrors(fd) {
  const e = {};
  e.workCat = isEmpty(fd.workCat) ? 'Please select a work category.' : null;
  e.workType = isEmpty(fd.workType) ? 'Please select a work type.' : null;
  return e;
}

function workInfoErrors(fd) {
  const e = {};
  const cat = (fd.workCat || '').toLowerCase();
  if (cat.startsWith('con')) {
    e.wUse = isEmpty(fd.wUse) ? 'Well Use is required.' : null;
  }
  e.drillerName = validateText(fd.drillerName, { label: 'Driller Name', required: true, maxLen: 100 });
  e.drillerLic = validateText(fd.drillerLic, { label: 'Driller License #', required: true, maxLen: 50 });
  e.dmeth = isEmpty(fd.dmeth) ? 'Drilling Method is required.' : null;
  if (isOtherDrillMethod(fd.dmeth, fd.dmethName)) {
    e.dmethOth = validateText(fd.dmethOth, { label: 'Other Method', required: true, maxLen: 50 });
  }
  if (isBoreholeCategory(fd.workCat)) {
    // Borehole (investigation / geo-probe) works collect a single set of Borehole Specifications
    // instead of a per-well table — validate only those and skip the well-spec rows.
    e.numbore = validateNumeric(fd.numbore, { label: 'Number of Boreholes', required: true, maxLen: 5 });
    e.holediam = validateNumeric(fd.holediam, { label: 'Hole Diameter', required: true, maxLen: 5 });
    e.maxdepth = validateNumeric(fd.maxdepth, { label: 'Maximum Depth', required: true, maxLen: 5 });
    return e;
  }
  const specs = fd.wellSpecs || [];
  if (specs.length === 0) {
    e.wellSpecs = 'At least one well specification row is required.';
  }
  specs.forEach((row, i) => {
    const owMsg = validateText(row.owellnum, { label: 'Owner Well Id', required: true, maxLen: 10 });
    if (owMsg) e[`wellSpecs.${i}.owellnum`] = owMsg;
    const hd = validateDecimal(row.holediam, { label: 'Drill Hole Diameter', required: true });
    if (hd) e[`wellSpecs.${i}.holediam`] = hd;
    const cd = validateDecimal(row.casediam, { label: 'Casing Diameter', required: true });
    if (cd) e[`wellSpecs.${i}.casediam`] = cd;
    const sd = validateDecimal(row.sealdepth, { label: 'Surface Seal Depth', required: true });
    if (sd) e[`wellSpecs.${i}.sealdepth`] = sd;
    const md = validateDecimal(row.maxdepth, { label: 'Max Depth', required: true });
    if (md) e[`wellSpecs.${i}.maxdepth`] = md;
  });
  return e;
}

function paymentErrors(fd) {
  const e = {};
  e.paymentType = isEmpty(fd.paymentType) ? 'Please select a payment type.' : null;
  if (fd.paymentType === 'CHECK') {
    e.acctName = validateText(fd.acctName, { label: 'Name on Account', required: true, maxLen: 50 });
  }
  return e;
}

function hazardErrors(fd) {
  const e = {};
  if (fd.sitehazardrequired !== 'Y') return e;

  e.hazConsultantFirstName = validateText(fd.hazConsultantFirstName, { label: 'Site Consultant First Name', required: true, maxLen: 50 });
  e.hazConsultantLastName = validateText(fd.hazConsultantLastName, { label: 'Site Consultant Last Name', required: true, maxLen: 50 });
  validatePhone(fd, 'hazConsultantPhone', 'Site Consultant Phone', false, e);
  validatePhone(fd, 'hazConsultantCell', 'Site Consultant Cell Phone', false, e);

  e.hazSafetyFirstName = validateText(fd.hazSafetyFirstName, { label: 'Site Safety Officer First Name', required: true, maxLen: 50 });
  e.hazSafetyLastName = validateText(fd.hazSafetyLastName, { label: 'Site Safety Officer Last Name', required: true, maxLen: 50 });
  validatePhone(fd, 'hazSafetyPhone', 'Site Safety Officer Phone', false, e);
  validatePhone(fd, 'hazSafetyCell', 'Site Safety Officer Cell Phone', false, e);

  e.hazFacilityType = validateText(fd.hazFacilityType, { label: 'Type of Facility', required: false, maxLen: 100 });
  e.hazMeetingDate = validateDate(fd.hazMeetingDate, { label: 'Site Safety Meeting Date', required: false });

  e.hazEquipClothingDesc = validateText(fd.hazEquipClothingDesc, { label: 'Clothing Description', required: false, maxLen: 150 });
  e.hazEquipRespiratorDesc = validateText(fd.hazEquipRespiratorDesc, { label: 'Respirator Description', required: false, maxLen: 150 });
  e.hazEquipCartridgeDesc = validateText(fd.hazEquipCartridgeDesc, { label: 'Cartridge Description', required: false, maxLen: 150 });
  e.hazEquipGlovesDesc = validateText(fd.hazEquipGlovesDesc, { label: 'Gloves Description', required: false, maxLen: 150 });
  e.hazEquipOtherDesc = validateText(fd.hazEquipOtherDesc, { label: 'Other Equipment Description', required: false, maxLen: 150 });

  e.hazProviderLastName = validateText(fd.hazProviderLastName, { label: 'Preparer Last Name', required: true, maxLen: 50 });
  e.hazProviderFirstName = validateText(fd.hazProviderFirstName, { label: 'Preparer First Name', required: true, maxLen: 50 });
  e.hazProviderTitle = validateText(fd.hazProviderTitle, { label: 'Provider Title', required: false, maxLen: 50 });
  validatePhone(fd, 'hazProviderPhone', 'Provider Phone', false, e);

  // Legacy requires at least one Personal Protection Equipment level (validateHazardEquipment).
  const anyPpe = fd.hazPpeA || fd.hazPpeB || fd.hazPpeC || fd.hazPpeD;
  e.hazPpe = anyPpe ? null : 'Please select at least one level of protection equipment.';

  // Legacy requires at least one anticipated Contaminant (saveHazardSubs).
  const anyContaminant = fd.hazContamGasoline || fd.hazContamDiesel || fd.hazContamWasteOil
    || (fd.hazContamOthers || []).some((name) => !isEmpty(name));
  e.hazContam = anyContaminant ? null : 'Please select at least one contaminant.';

  // Legacy marks the acknowledgement checkbox required (asterisk) when hazards are present.
  e.hazAcknowledgement = fd.hazAcknowledgement ? null : 'You must acknowledge the hazardous-materials certification.';

  (fd.hazContamOthers || []).forEach((name, i) => {
    if (!isEmpty(name)) {
      const msg = validateText(name, { label: 'Other Contaminant', required: false, maxLen: 100 });
      if (msg) e[`hazContamOthers.${i}`] = msg;
    }
  });

  (fd.hazSubstances || []).forEach((row, i) => {
    const c = validateText(row.concentration, { label: 'Expected Concentration', required: false, maxLen: 100 });
    if (c) e[`hazSubstances.${i}.concentration`] = c;
    const p = validateText(row.pelPpm, { label: 'PEL (ppm)', required: false, maxLen: 100 });
    if (p) e[`hazSubstances.${i}.pelPpm`] = p;
    const h = validateText(row.healthEffects, { label: 'Health Effects', required: false, maxLen: 100 });
    if (h) e[`hazSubstances.${i}.healthEffects`] = h;
  });

  return e;
}

function worksReviewErrors(fd) {
  const e = {};
  if (effectiveWorks(fd).length === 0) {
    e.works = 'Please add at least one type of work to the project.';
  }
  return e;
}

export const stepValidators = {
  location: locationErrors,
  applicant: applicantErrors,
  project: projectErrors,
  workType: workTypeErrors,
  workInfo: workInfoErrors,
  works: worksReviewErrors,
  hazard: hazardErrors,
  payment: paymentErrors,
  verify: () => ({}),
};

export function validateStepByKey(key, formData) {
  const fn = stepValidators[key] || (() => ({}));
  const raw = fn(formData);
  const errors = {};
  Object.keys(raw).forEach((field) => {
    if (raw[field]) errors[field] = raw[field];
  });
  return errors;
}