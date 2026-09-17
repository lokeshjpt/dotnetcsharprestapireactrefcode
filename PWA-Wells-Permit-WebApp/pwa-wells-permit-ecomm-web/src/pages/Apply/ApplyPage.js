import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import Button from '../../components/UI/Button';
import Loader from '../../components/Loader/Loader';
import InvisibleCaptcha, { captchaEnabled } from '../../components/Captcha/InvisibleCaptcha';
import useApplication, { STEP_STORAGE_KEY, SESSION_TIMEOUT_MS } from '../../hooks/useApplication';
import { submitApplication, uploadSitemap } from '../../api/applicationApi';
import {
  getStates,
  getCities,
  getPaymentTypes,
  getWorkCategories,
  getDrillMethods,
  getWellUseTypes,
} from '../../api/referenceApi';
import { STATE_CODES, WORK_CATEGORIES, DRILL_METHODS, WELL_USE_TYPES, isBoreholeCategory } from './referenceData';
import { validateStepByKey } from './stepValidators';
import { effectiveWorks } from './works';
import Step1Applicant from './steps/Step1Applicant';
import StepLocation from './steps/StepLocation';
import Step2ProjectInfo from './steps/Step2ProjectInfo';
import StepWorksReview from './steps/StepWorksReview';
import WorkFormModal from './steps/WorkFormModal';
import StepHazard from './steps/StepHazard';
import Step5Payment from './steps/Step5Payment';
import Step6Verify from './steps/Step6Verify';
import IntelliPayLightbox from '../Payment/IntelliPayLightbox';
import './ApplyPage.css';

export function buildHazardPayload(fd) {
  const present = fd.sitehazardrequired === 'Y';
  const phone = (prefix) => {
    const parts = [fd[`${prefix}1`], fd[`${prefix}2`], fd[`${prefix}3`]].filter(Boolean).join('-');
    const ext = fd[`${prefix}X`];
    return parts ? `${parts}${ext ? ` x${ext}` : ''}` : '';
  };
  const contaminants = [];
  if (fd.hazContamGasoline) contaminants.push('Gasoline');
  if (fd.hazContamDiesel) contaminants.push('Diesel');
  if (fd.hazContamWasteOil) contaminants.push('Waste Oil');
  (fd.hazContamOthers || []).forEach((c) => {
    if (c && String(c).trim()) contaminants.push(String(c).trim());
  });
  const substances = (fd.hazSubstances || [])
    .filter((row) => row.concentration || row.pelPpm || row.healthEffects)
    .map((row) => ({
      concentration: row.concentration,
      pelPpm: row.pelPpm,
      healthEffects: row.healthEffects,
    }));
  const time = present ? `${fd.hazMeetingHour}:${fd.hazMeetingMinute} ${fd.hazMeetingShift}` : '';
  return {
    present,
    consultantFirstName: fd.hazConsultantFirstName,
    consultantLastName: fd.hazConsultantLastName,
    consultantPhone: phone('hazConsultantPhone'),
    consultantCell: phone('hazConsultantCell'),
    safetyOfficerFirstName: fd.hazSafetyFirstName,
    safetyOfficerLastName: fd.hazSafetyLastName,
    safetyOfficerPhone: phone('hazSafetyPhone'),
    safetyOfficerCell: phone('hazSafetyCell'),
    facilityType: fd.hazFacilityType,
    siteSafetyMeetingDate: fd.hazMeetingDate || null,
    siteSafetyMeetingTime: time,
    ppeLevelA: fd.hazPpeA ? 'Y' : 'N',
    ppeLevelB: fd.hazPpeB ? 'Y' : 'N',
    ppeLevelC: fd.hazPpeC ? 'Y' : 'N',
    ppeLevelD: fd.hazPpeD ? 'Y' : 'N',
    equipHardHatFlag: fd.hazEquipHardHat || null,
    equipSafetyShoesFlag: fd.hazEquipSafetyShoes || null,
    equipOrangeVestFlag: fd.hazEquipOrangeVest || null,
    equipHearingProtFlag: fd.hazEquipHearing || null,
    equipSafetyEyewearFlag: fd.hazEquipEyewear || null,
    equipClothingFlag: fd.hazEquipClothing || null,
    equipClothingDesc: fd.hazEquipClothingDesc || null,
    equipRespiratorFlag: fd.hazEquipRespirator || null,
    equipRespiratorDesc: fd.hazEquipRespiratorDesc || null,
    equipCartridgeFlag: fd.hazEquipCartridge || null,
    equipCartridgeDesc: fd.hazEquipCartridgeDesc || null,
    equipGlovesFlag: fd.hazEquipGloves || null,
    equipGlovesDesc: fd.hazEquipGlovesDesc || null,
    equipOtherFlag: fd.hazEquipOther || null,
    equipOtherDesc: fd.hazEquipOtherDesc || null,
    infoProvidedByLastName: fd.hazProviderLastName || null,
    infoProvidedByFirstName: fd.hazProviderFirstName || null,
    infoProvidedByTitle: fd.hazProviderTitle || null,
    infoProvidedByPhone: phone('hazProviderPhone') || null,
    acknowledgement: !!fd.hazAcknowledgement,
    contaminants,
    substances,
  };
}

function joinPhone(fd, prefix) {
  const base = [fd[`${prefix}1`], fd[`${prefix}2`], fd[`${prefix}3`]].filter(Boolean).join('-');
  const ext = fd[`${prefix}X`];
  return base ? (ext ? `${base} x${ext}` : base) : '';
}

function numOrNull(value) {
  if (value === null || value === undefined || String(value).trim() === '') return null;
  const n = Number(value);
  return Number.isNaN(n) ? null : n;
}

function buildOneWork(work) {
  // Borehole (investigation / geo-probe) works carry a single spec built from the Borehole
  // Specifications fields (Number of Boreholes / Hole Diameter / Max Depth) collected on the
  // work-info form — there is no per-well table for these categories. All other works map each
  // row of the well-specifications table to a spec.
  const specs = isBoreholeCategory(work.workCat)
    ? [{
        ownerWellNum: null,
        drillCount: numOrNull(work.numbore),
        holeDiamIn: numOrNull(work.holediam),
        casingDiamIn: null,
        sealDepthFt: null,
        maxDepthFt: numOrNull(work.maxdepth),
        latitude: null,
        longitude: null,
        stateWellId: null,
        dwrNum: null,
        permitNum: null,
      }]
    : (work.wellSpecs || []).map((w) => ({
        ownerWellNum: w.owellnum || null,
        drillCount: numOrNull(work.numbore),
        holeDiamIn: numOrNull(w.holediam),
        casingDiamIn: numOrNull(w.casediam),
        sealDepthFt: numOrNull(w.sealdepth),
        maxDepthFt: numOrNull(w.maxdepth),
        latitude: w.latitude || null,
        longitude: w.longitude || null,
        stateWellId: w.swellid || null,
        dwrNum: w.dwr || null,
        permitNum: w.permit || null,
      }));
  return {
    workCategory: work.workCat || '',
    workType: work.workType || '',
    wellUseType: work.wUse || null,
    drillerName: work.drillerName || null,
    drillerLicenseNum: work.drillerLic || null,
    drillMethodType: work.dmeth || null,
    drillMethodOtherDesc: work.dmethOth || null,
    workFeeRate: numOrNull(work.workFeeRate) ?? 0,
    workFeeUnit: work.workFeeUnit || 'EA',
    workSiteMax: numOrNull(work.workSiteMax) ?? 0,
    specs,
  };
}

// Every work on the application (committed works + the in-progress draft), shaped for the API.
function buildWorks(fd) {
  return effectiveWorks(fd).map(buildOneWork);
}

function normalizeOptions(list, codeKeys, labelKeys) {
  if (!Array.isArray(list)) return [];
  return list.map((item) => {
    if (typeof item === 'string') return { code: item, label: item };
    const code = codeKeys.map((k) => item[k]).find((v) => v !== undefined);
    const label = labelKeys.map((k) => item[k]).find((v) => v !== undefined);
    return { code, label: label ?? code };
  });
}

// Human-readable inactivity window (mirrors SESSION_TIMEOUT_MS in useApplication), shown in the
// proactive "submit before you time out" warning banner below.
const SESSION_TIMEOUT_LABEL = (() => {
  const minutes = SESSION_TIMEOUT_MS / 60000;
  const rounded = minutes >= 1 ? Math.round(minutes) : minutes;
  return `${rounded} minute${rounded === 1 ? '' : 's'}`;
})();

function ApplyPage() {
  const navigate = useNavigate();
  const app = useApplication();
  const { formData, updateField, setFields, reset, amountDue, sessionExpired, acknowledgeSessionExpiry } = app;
  // Restore the last step the applicant was on (persisted in localStorage) so returning to /apply —
  // from another page, a new tab, or after closing and reopening the browser — resumes in place.
  const [currentStep, setCurrentStep] = useState(() => {
    try {
      const saved = parseInt(window.localStorage.getItem(STEP_STORAGE_KEY), 10);
      return Number.isInteger(saved) && saved >= 0 ? saved : 0;
    } catch {
      return 0;
    }
  });
  const [options, setOptions] = useState({
    states: STATE_CODES,
    paymentTypes: [],
    workCategories: WORK_CATEGORIES,
    drillMethods: DRILL_METHODS,
    wellUseTypes: WELL_USE_TYPES,
  });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  // When set (CC only), the IntelliPay lightbox is shown inline on the verify step after the
  // application record has been created, mirroring legacy app_verify.jsp "Pay Now and Submit".
  const [ccPay, setCcPay] = useState(null);
  // Add/Edit work modal on the Work(s) step: null (closed), 'add', or 'edit'.
  const [workModal, setWorkModal] = useState(null);
  const captchaRef = useRef(null);
  // Wraps the active step's fields so a failed validation can scroll/focus the first invalid
  // control instead of jumping to the top of the page.
  const stepBodyRef = useRef(null);

  // After a validation failure, move the applicant to the first field in error (parity with a
  // native form's focus-first-invalid behaviour) rather than scrolling to the top of the page.
  const scrollToFirstError = () => {
    window.requestAnimationFrame(() => {
      const root = stepBodyRef.current;
      if (!root) return;
      const invalidControl = root.querySelector('.is-invalid, [aria-invalid="true"]');
      const target = invalidControl || root.querySelector('.field-error');
      if (!target) {
        window.scrollTo({ top: 0, behavior: 'smooth' });
        return;
      }
      target.scrollIntoView({ behavior: 'smooth', block: 'center' });
      const focusable = invalidControl
        && typeof invalidControl.focus === 'function'
        && ['INPUT', 'SELECT', 'TEXTAREA', 'BUTTON'].includes(invalidControl.tagName);
      if (focusable) {
        try { invalidControl.focus({ preventScroll: true }); } catch { invalidControl.focus(); }
      }
    });
  };

  const showHazard = formData.sitehazardrequired === 'Y';

  const stepDefs = useMemo(() => {
    const defs = [
      { key: 'location', label: 'Location' },
      { key: 'applicant', label: 'Applicant' },
      { key: 'project', label: 'Project' },
      { key: 'works', label: 'Work(s)' },
    ];
    if (showHazard) defs.push({ key: 'hazard', label: 'Site Hazard' });
    defs.push({ key: 'payment', label: 'Payment' });
    defs.push({ key: 'verify', label: 'Verify' });
    return defs;
  }, [showHazard]);

  const stepCount = stepDefs.length;
  const safeStep = Math.min(currentStep, stepCount - 1);
  const currentKey = stepDefs[safeStep].key;

  useEffect(() => {
    if (currentStep > stepCount - 1) setCurrentStep(stepCount - 1);
  }, [currentStep, stepCount]);

  // When the 40-minute inactivity timeout fires (see useApplication) the saved application is wiped;
  // drop the applicant back to the first step to complete the fresh-session illusion.
  useEffect(() => {
    if (sessionExpired) setCurrentStep(0);
  }, [sessionExpired]);

  // Persist the active step so it can be restored on the next visit (see the lazy initializer
  // above). Cleared together with the form data by reset() on a successful submission.
  useEffect(() => {
    try {
      window.localStorage.setItem(STEP_STORAGE_KEY, String(safeStep));
    } catch {
      // Ignore storage failures (private mode / quota exceeded).
    }
  }, [safeStep]);

  useEffect(() => {
    async function loadOptions() {
      const next = {
        states: STATE_CODES,
        cities: [],
        paymentTypes: [],
        workCategories: WORK_CATEGORIES,
        drillMethods: DRILL_METHODS,
        wellUseTypes: WELL_USE_TYPES,
      };
      const results = await Promise.allSettled([
        getStates(),
        getCities(),
        getPaymentTypes(),
        getWorkCategories(),
        getDrillMethods(),
        getWellUseTypes(),
      ]);
      const [states, cities, paymentTypes, workCategories, drillMethods, wellUseTypes] = results;
      if (states.status === 'fulfilled') {
        const norm = normalizeOptions(states.value, ['code', 'stateCode', 'state_code'], ['label', 'stateName', 'state_name']);
        if (norm.length) next.states = norm;
      }
      if (cities.status === 'fulfilled') {
        next.cities = normalizeOptions(cities.value, ['code', 'cityCode', 'city_code'], ['label', 'cityName', 'city_name']);
      }
      if (paymentTypes.status === 'fulfilled') {
        next.paymentTypes = normalizeOptions(paymentTypes.value, ['code', 'paymentType', 'payment_type'], ['label', 'paymentDesc', 'payment_desc']);
      }
      if (workCategories.status === 'fulfilled') {
        const norm = normalizeOptions(workCategories.value, ['code', 'workCategory', 'work_category'], ['label', 'workCatDesc', 'work_cat_desc']);
        if (norm.length) next.workCategories = norm;
      }
      if (drillMethods.status === 'fulfilled') {
        const norm = normalizeOptions(drillMethods.value, ['code', 'drillMethodType', 'drill_method_type'], ['label', 'drillMethodName', 'drill_method_name']);
        if (norm.length) next.drillMethods = norm;
      }
      if (wellUseTypes.status === 'fulfilled') {
        const norm = normalizeOptions(wellUseTypes.value, ['code', 'wellUseType', 'well_use_type'], ['label', 'wellUseDesc', 'well_use_desc']);
        if (norm.length) next.wellUseTypes = norm;
      }
      setOptions(next);
    }
    loadOptions();
  }, []);

  const stepContent = useMemo(() => {
    const shared = { formData, updateField, setFields, options, errors };
    switch (currentKey) {
      case 'location':
        return <StepLocation {...shared} />;
      case 'applicant':
        return <Step1Applicant {...shared} updateCcEmail={app.updateCcEmail} addCcEmail={app.addCcEmail} removeCcEmail={app.removeCcEmail} />;
      case 'project':
        return <Step2ProjectInfo {...shared} onChangeLocation={() => goToStep(0)} />;
      case 'works':
        return (
          <StepWorksReview
            formData={formData}
            options={options}
            amountDue={amountDue}
            onAddAnother={() => handleAddWork()}
            onEditWork={(i) => handleEditWork(i)}
            onDeleteWork={(i) => app.deleteWork(i)}
          />
        );
      case 'hazard':
        return (
          <StepHazard
            {...shared}
            updateHazContamOther={app.updateHazContamOther}
            addHazContamOther={app.addHazContamOther}
            removeHazContamOther={app.removeHazContamOther}
            updateHazSubstance={app.updateHazSubstance}
            addHazSubstance={app.addHazSubstance}
            removeHazSubstance={app.removeHazSubstance}
          />
        );
      case 'payment':
        return <Step5Payment {...shared} amountDue={amountDue} />;
      default:
        return (
          <Step6Verify
            formData={formData}
            amountDue={amountDue}
            onEdit={(key) => {
              const idx = stepDefs.findIndex((s) => s.key === key);
              if (idx >= 0) goToStep(idx);
            }}
          />
        );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentKey, formData, options, errors, amountDue]);

  const goToStep = (target, { preserveErrors = false } = {}) => {
    if (!preserveErrors) {
      setErrors({});
      setError('');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
    setCurrentStep(target);
  };

  // "Add Work Type": start a fresh draft and open the modal.
  const handleAddWork = () => {
    app.startNewWork();
    setWorkModal('add');
  };

  // "Update": load the committed work into the draft and open the modal.
  const handleEditWork = (index) => {
    app.editWork(index);
    setWorkModal('edit');
  };

  // Modal saved: saveWork() already committed the draft and cleared it; just close.
  const handleWorkSaved = () => setWorkModal(null);

  // Modal cancelled: discard the in-progress draft (committed works are untouched) and close.
  const handleWorkCancel = () => {
    app.startNewWork();
    setWorkModal(null);
  };

  const handleContinue = () => {
    const stepErrors = validateStepByKey(currentKey, formData);
    if (Object.keys(stepErrors).length > 0) {
      setErrors(stepErrors);
      setError('Please correct the highlighted fields before continuing.');
      scrollToFirstError();
      return;
    }
    goToStep(Math.min(stepCount - 1, safeStep + 1));
  };

  const handleBack = () => goToStep(Math.max(0, safeStep - 1));

  // Let users jump between steps via the progress indicator. Going backward (or to the
  // current step) is always allowed; jumping forward validates each step in between and
  // stops at the first one with missing information (parity with Continue / submit).
  const handleStepClick = (index) => {
    if (index === safeStep) return;
    if (index < safeStep) {
      goToStep(index);
      return;
    }
    for (let s = safeStep; s < index; s += 1) {
      const stepErrors = validateStepByKey(stepDefs[s].key, formData);
      if (Object.keys(stepErrors).length > 0) {
        setErrors(stepErrors);
        setError('Please correct the highlighted fields before continuing.');
        goToStep(s, { preserveErrors: true });
        scrollToFirstError();
        return;
      }
    }
    goToStep(index);
  };

  const buildPayload = () => ({
    addBy: formData.addBy || 'public-portal',

    // Applicant
    appBusinessName: formData.appBusinessName || null,
    appLastName: formData.appLastName || null,
    appFirstName: formData.appFirstName || null,
    appEmailAddr: formData.appEmail || null,
    appAddrStreet: formData.appAddr || null,
    appAddrStreet2: formData.appAddr2 || null,
    appAddrCity: formData.appCity || null,
    appAddrState: formData.appState || null,
    appAddrZip: formData.appZip || null,
    appPhone: joinPhone(formData, 'appPhone'),
    appFax: [formData.appFax1, formData.appFax2, formData.appFax3].filter(Boolean).join('-') || null,

    // Contact
    contactLastName: formData.conLastName || null,
    contactFirstName: formData.conFirstName || null,
    contactEmail: formData.conEmail || null,
    contactPhone: joinPhone(formData, 'conPhone'),
    contactCell: joinPhone(formData, 'conCell'),

    // Property owner
    ownerLastName: formData.ownLastName || null,
    ownerFirstName: formData.ownFirstName || null,
    ownerAddrStreet: formData.ownAddr || null,
    ownerAddrCity: formData.ownCity || null,
    ownerAddrState: formData.ownState || null,
    ownerAddrZip: formData.ownZip || null,
    ownerPhone: joinPhone(formData, 'ownPhone'),
    ownerEmail: formData.ownEmail || null,

    // Client
    clientLastName: formData.cliLastName || null,
    clientFirstName: formData.cliFirstName || null,
    clientAddrStreet: formData.cliAddr || null,
    clientAddrCity: formData.cliCity || null,
    clientAddrState: formData.cliState || null,
    clientAddrZip: formData.cliZip || null,
    clientPhone: joinPhone(formData, 'cliPhone'),
    clientEmail: formData.cliEmail || null,

    // Project / site
    siteCityCode: formData.siteCity || null,
    siteCityName: formData.siteCityName || null,
    siteLocation: formData.siteLoc || null,
    siteLat: formData.siteLat || null,
    siteLong: formData.siteLong || null,
    projStartDate: formData.startDate || null,
    projEndDate: formData.endDate || null,
    siteHazardRequired: formData.sitehazardrequired || 'N',
    sitemapFilename: null,

    // Works graph (category/type/driller/method/fees + per-well specs)
    works: buildWorks(formData),

    // Sub-forms
    hazard: buildHazardPayload(formData),
    documents: [],
    emailCcs: (formData.ccEmails || []).filter((e) => e && e.trim()),
    notes: [],

    // Payment
    paymentType: formData.paymentType,
    checkAcctName: formData.paymentType === 'CHECK' ? (formData.acctName || null) : null,
    checkNum: null,
  });

  const validateAll = () => {
    for (let s = 0; s < stepCount - 1; s += 1) {
      const stepErrors = validateStepByKey(stepDefs[s].key, formData);
      if (Object.keys(stepErrors).length > 0) {
        setErrors(stepErrors);
        setError('Some information is missing. Please review the highlighted step.');
        goToStep(s, { preserveErrors: true });
        scrollToFirstError();
        return false;
      }
    }
    return true;
  };

  // Creates the application record (and uploads any queued site map). Shared by every payment type.
  const createApplication = async (captchaToken) => {
    const application = await submitApplication(buildPayload(), captchaToken);
    const applicationId = application.appId || application.applicationId;
    // Upload the actual site-map bytes as multipart form-data. Defensive: a
    // missing/unavailable endpoint must not block a successful application. reCAPTCHA tokens are
    // single-use, so mint a fresh one for this second protected call.
    if (applicationId && app.documentFile) {
      try {
        const uploadToken = await captchaRef.current?.execute();
        await uploadSitemap(applicationId, app.documentFile, uploadToken);
      } catch (uploadError) {
        // Metadata already persisted with the JSON submit; ignore upload failure.
      }
    }
    return application;
  };

  const submitError = (e) =>
    setError(e.response?.data?.title || 'The application could not be submitted. Please review your entries and try again.');

  // Runs the invisible captcha gate. Returns the token on success, or false when the challenge was
  // required but not completed (bot / dismissed) so the caller can abort the submission.
  const passCaptcha = async () => {
    const token = await captchaRef.current?.execute();
    if (captchaEnabled && !token) {
      setError('Security verification could not be completed. Please try again in a moment.');
      return false;
    }
    return token ?? null;
  };

  // Check / Fee-exempt: "Submit Application" — create the record and go straight to confirmation.
  const handleSubmitApplication = async () => {
    if (!validateAll()) return;
    setLoading(true);
    setError('');
    try {
      const captchaToken = await passCaptcha();
      if (captchaToken === false) {
        setLoading(false);
        return;
      }
      const application = await createApplication(captchaToken);
      reset();
      navigate(`/confirmation/${application.appId}`, { state: { application, paymentType: formData.paymentType } });
    } catch (e) {
      submitError(e);
    } finally {
      setLoading(false);
    }
  };

  // Credit card: "Pay Now and Submit" — create the record, then reveal the IntelliPay lightbox
  // inline on the verify step (matching legacy app_verify.jsp). Card vaulting happens in the popup.
  const handlePayNowAndSubmit = async () => {
    if (!validateAll()) return;
    setLoading(true);
    setError('');
    try {
      const captchaToken = await passCaptcha();
      if (captchaToken === false) {
        setLoading(false);
        return;
      }
      const application = await createApplication(captchaToken);
      setCcPay({ appId: application.appId, amount: amountDue, application });
    } catch (e) {
      submitError(e);
    } finally {
      setLoading(false);
    }
  };

  const handleCcApproved = () => {
    const target = ccPay;
    reset();
    navigate(`/confirmation/${target.appId}`, { state: { application: target.application, paymentType: 'CC' } });
  };

  return (
    <div className="page-shell">
      <div className="page-header-block">
        <h1 className="page-title">Well Permit Application</h1>
        <p className="page-subtitle">Complete each step below to submit your permit request.</p>
        <Link className="page-help-link" to="/help#apply">
          <svg className="page-help-link__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10" /><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" /><line x1="12" y1="17" x2="12.01" y2="17" /></svg> How to apply — step-by-step help
        </Link>
      </div>

      <div className="alert alert-warning" role="note" style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" style={{ flexShrink: 0, marginTop: 1 }}>
          <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <line x1="12" y1="17" x2="12.01" y2="17" />
        </svg>
        <span>
          For your security, this application times out after {SESSION_TIMEOUT_LABEL} of inactivity.
          Please complete and submit it before then — otherwise your entered information will be
          cleared and you&apos;ll need to start over.
        </span>
      </div>

      {sessionExpired && (
        <div className="alert alert-info" role="status" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
          <span>
            Your session timed out due to inactivity, so we started a new application.
            Any information you had entered was cleared for your security.
          </span>
          <button type="button" className="btn-link" onClick={acknowledgeSessionExpiry}>Dismiss</button>
        </div>
      )}

      <nav className="wizard-steps" aria-label="Application progress">
        <ol className="wizard-steps-list">
        {stepDefs.map((step, index) => (
          <li
            key={step.key}
            className={`wizard-step${index === safeStep ? ' wizard-step--active' : ''}${index < safeStep ? ' wizard-step--complete' : ''}`}
            aria-current={index === safeStep ? 'step' : undefined}
          >
            <button
              type="button"
              className="wizard-step__button"
              onClick={() => handleStepClick(index)}
              aria-label={`Go to step ${index + 1}: ${step.label}`}
            >
              <span className="step-num" aria-hidden="true">{index + 1}</span>
              <strong className="step-label">{step.label}</strong>
              <span className="sr-only">
                {index === safeStep ? ' (current step)' : index < safeStep ? ' (completed)' : ' (not started)'}
              </span>
            </button>
          </li>
        ))}
        </ol>
      </nav>

      {error && <div className="alert alert-error" role="alert">{error}</div>}
      {loading && <Loader />}

      <div ref={stepBodyRef}>{stepContent}</div>

      {workModal && (
        <WorkFormModal
          mode={workModal}
          app={app}
          options={options}
          onSaved={handleWorkSaved}
          onCancel={handleWorkCancel}
        />
      )}

      {currentKey === 'verify' && ccPay && (
        <div className="panel intellipay-inline">
          <h2 className="page-subtitle" style={{ marginTop: 0 }}>
            Complete your card payment
          </h2>
          <p className="text-muted">
            Your application <strong>{ccPay.appId}</strong> has been recorded. Enter your card in the
            secure IntelliPay window to finish. Your card is authorized for $0.00 now and charged the
            permit fee only after staff approves your permit.
          </p>
          <IntelliPayLightbox
            appId={ccPay.appId}
            amount={ccPay.amount}
            existingCustomerId={formData.customerId}
            autoOpen
            onSuccess={handleCcApproved}
          />
        </div>
      )}

      <div className="wizard-actions">
        <Button variant="default" onClick={handleBack} disabled={safeStep === 0 || loading || !!ccPay}>
          Back
        </Button>
        {safeStep < stepCount - 1 ? (
          <Button onClick={handleContinue} disabled={loading}>
            Continue
          </Button>
        ) : formData.paymentType === 'CC' ? (
          !ccPay && (
            <Button onClick={handlePayNowAndSubmit} disabled={loading}>
              Pay Now and Submit
            </Button>
          )
        ) : (
          <Button onClick={handleSubmitApplication} disabled={loading}>
            Submit Application
          </Button>
        )}
      </div>

      <InvisibleCaptcha ref={captchaRef} />
      {captchaEnabled && (
        <p className="captcha-notice">
          This site is protected by reCAPTCHA and the Google{' '}
          <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Privacy Policy</a>{' '}
          and{' '}
          <a href="https://policies.google.com/terms" target="_blank" rel="noopener noreferrer">Terms of Service</a>{' '}
          apply.
        </p>
      )}
    </div>
  );
}

export default ApplyPage;