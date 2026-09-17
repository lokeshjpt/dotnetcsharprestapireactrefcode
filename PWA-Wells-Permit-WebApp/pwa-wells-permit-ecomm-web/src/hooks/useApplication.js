import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  makeWellSpec,
  blankDraftWork,
  extractDraftWork,
  worksTotal,
} from '../pages/Apply/works';

const STORAGE_KEY = 'pwa-permits-ecomm';
// Persisted in localStorage (not sessionStorage) so an in-progress application survives closing the
// tab/window; the applicant can reopen the site later and resume from the exact step they left off.
export const STEP_STORAGE_KEY = 'pwa-permits-ecomm-step';
// Timestamp (ms) of the applicant's last interaction. Used to enforce a sliding inactivity timeout
// that mirrors the legacy JBoss 40-minute session: if the saved application is left untouched longer
// than SESSION_TIMEOUT_MS, it is discarded on the next load/tick and treated as a brand-new session.
const ACTIVITY_KEY = 'pwa-permits-ecomm-activity';
// Defaults to 40 minutes (the server session-timeout). Override with REACT_APP_SESSION_TIMEOUT_MIN
// (e.g. "1") to simulate/verify the expiry quickly in dev.
export const SESSION_TIMEOUT_MS =
  Math.max(0.1, Number(process.env.REACT_APP_SESSION_TIMEOUT_MIN) || 40) * 60 * 1000;

function makeHazSubstance() {
  return {
    concentration: '',
    pelPpm: '',
    healthEffects: '',
  };
}

const defaultState = {
  addBy: 'public-portal',

  // ---- Applicant ----
  appBusinessName: '',
  appLastName: '',
  appFirstName: '',
  appAddr: '',
  appAddr2: '',
  appCity: '',
  appState: 'CA',
  appZip: '',
  appPhone1: '',
  appPhone2: '',
  appPhone3: '',
  appPhoneX: '',
  appFax1: '',
  appFax2: '',
  appFax3: '',
  appEmail: '',
  conLastName: '',
  conFirstName: '',
  conPhone1: '',
  conPhone2: '',
  conPhone3: '',
  conPhoneX: '',
  conCell1: '',
  conCell2: '',
  conCell3: '',
  conEmail: '',
  ccEmails: [''],

  // ---- Project / Location ----
  siteLoc: '',
  siteCity: '',
  siteCityName: '',
  siteLat: '',
  siteLong: '',
  startDate: '',
  endDate: '',
  sitehazardrequired: 'Y',

  // ---- Property Owner ----
  ownLastName: '',
  ownFirstName: '',
  ownAddr: '',
  ownCity: '',
  ownState: 'CA',
  ownZip: '',
  ownPhone1: '',
  ownPhone2: '',
  ownPhone3: '',
  ownPhoneX: '',
  ownEmail: '',

  // ---- Client ----
  cliLastName: '',
  cliFirstName: '',
  cliAddr: '',
  cliCity: '',
  cliState: 'CA',
  cliZip: '',
  cliPhone1: '',
  cliPhone2: '',
  cliPhone3: '',
  cliPhoneX: '',
  cliEmail: '',

  // ---- Work type ----
  workCat: '',
  workType: '',
  workDesc: '',
  workFeeRate: 0,
  workFeeUnit: 'EA',
  workSiteMax: 0,
  workSiteExtraRate: 0,

  // ---- Work information ----
  wUse: '',
  wUseDesc: '',
  drillerName: '',
  drillerLic: '',
  dmeth: '',
  dmethName: '',
  dmethOth: '',
  numbore: '',
  holediam: '',
  maxdepth: '',
  wellSpecs: [makeWellSpec()],

  // ---- Committed works (multi-work applications, legacy "Add Another Type of Work to Project") ----
  // The flat work-type/work-information fields above are the "draft" for the work currently being
  // edited; completed works are pushed here. workEditIndex is the slot the draft maps to (null = new).
  works: [],
  workEditIndex: null,

  // ---- Payment ----
  paymentType: 'CC',
  acctName: '',
  customerId: '',

  // ---- Site Hazard (only used when sitehazardrequired === 'Y') ----
  hazConsultantFirstName: '',
  hazConsultantLastName: '',
  hazConsultantPhone1: '',
  hazConsultantPhone2: '',
  hazConsultantPhone3: '',
  hazConsultantPhoneX: '',
  hazConsultantCell1: '',
  hazConsultantCell2: '',
  hazConsultantCell3: '',
  hazSafetyFirstName: '',
  hazSafetyLastName: '',
  hazSafetyPhone1: '',
  hazSafetyPhone2: '',
  hazSafetyPhone3: '',
  hazSafetyPhoneX: '',
  hazSafetyCell1: '',
  hazSafetyCell2: '',
  hazSafetyCell3: '',
  hazFacilityType: '',
  hazMeetingDate: '',
  hazMeetingHour: '00',
  hazMeetingMinute: '00',
  hazMeetingShift: 'AM',
  hazPpeA: false,
  hazPpeB: false,
  hazPpeC: false,
  hazPpeD: false,
  hazEquipHardHat: '',
  hazEquipSafetyShoes: '',
  hazEquipOrangeVest: '',
  hazEquipHearing: '',
  hazEquipEyewear: '',
  hazEquipClothing: '',
  hazEquipClothingDesc: '',
  hazEquipRespirator: '',
  hazEquipRespiratorDesc: '',
  hazEquipCartridge: '',
  hazEquipCartridgeDesc: '',
  hazEquipGloves: '',
  hazEquipGlovesDesc: '',
  hazEquipOther: '',
  hazEquipOtherDesc: '',
  hazContamGasoline: false,
  hazContamDiesel: false,
  hazContamWasteOil: false,
  hazContamOthers: [''],
  hazSubstances: [makeHazSubstance()],
  hazProviderLastName: '',
  hazProviderFirstName: '',
  hazProviderTitle: '',
  hazProviderPhone1: '',
  hazProviderPhone2: '',
  hazProviderPhone3: '',
  hazProviderPhoneX: '',
  hazAcknowledgement: false,

  // ---- Site-map upload ----
  document: null,
};

function readLastActivity() {
  try {
    const ts = parseInt(window.localStorage.getItem(ACTIVITY_KEY), 10);
    return Number.isFinite(ts) ? ts : null;
  } catch {
    return null;
  }
}

// True once the saved application has sat untouched past the inactivity window.
function isSessionExpired() {
  const ts = readLastActivity();
  return ts != null && Date.now() - ts > SESSION_TIMEOUT_MS;
}

function markActivity() {
  try {
    window.localStorage.setItem(ACTIVITY_KEY, String(Date.now()));
  } catch {
    // Ignore storage failures (private mode / quota exceeded).
  }
}

function clearStoredApplication() {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
    window.localStorage.removeItem(STEP_STORAGE_KEY);
    window.localStorage.removeItem(ACTIVITY_KEY);
  } catch {
    // Ignore storage failures.
  }
}

function loadInitial() {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (!saved) return defaultState;
    // Expired sessions are wiped so a returning applicant starts a brand-new application.
    if (isSessionExpired()) {
      clearStoredApplication();
      return defaultState;
    }
    return { ...defaultState, ...JSON.parse(saved) };
  } catch {
    return defaultState;
  }
}

function useApplication() {
  const [formData, setFormData] = useState(loadInitial);
  const [documentFile, setDocumentFile] = useState(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  // Persist the form and refresh the inactivity window on every change (editing counts as activity).
  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(formData));
      markActivity();
    } catch {
      // Ignore storage failures (private mode / quota exceeded).
    }
  }, [formData]);

  // Enforce the sliding inactivity timeout while the tab stays open: any on-screen activity (mouse
  // move, click, key, scroll, touch, field input) refreshes the timestamp; if nothing happens for
  // SESSION_TIMEOUT_MS the saved application is wiped and the in-memory form cleared, dropping the
  // applicant back to a fresh session (ApplyPage returns to step 1 and shows a notice). Also
  // re-checked whenever the tab regains focus.
  useEffect(() => {
    // Re-stamp at most this often so constant activity (e.g. mousemove) isn't a write storm, while
    // staying well under the timeout window so active users are never expired (throttle << window).
    const throttleMs = Math.max(250, Math.min(5000, Math.floor(SESSION_TIMEOUT_MS / 10)));
    let lastMark = Date.now(); // mount already stamped via the persist effect
    const onActivity = () => {
      const now = Date.now();
      if (now - lastMark >= throttleMs) {
        lastMark = now;
        markActivity();
      }
    };
    const activityEvents = [
      'mousemove', 'mousedown', 'click', 'keydown', 'wheel', 'scroll', 'touchstart', 'pointerdown', 'input', 'change', 'focusin',
    ];
    activityEvents.forEach((e) => window.addEventListener(e, onActivity, { passive: true }));

    const checkExpiry = () => {
      if (isSessionExpired()) {
        clearStoredApplication();
        setFormData(defaultState);
        setDocumentFile(null);
        setSessionExpired(true);
      }
    };
    const interval = window.setInterval(checkExpiry, Math.max(1000, Math.min(30000, Math.floor(SESSION_TIMEOUT_MS / 4))));
    const onVisible = () => {
      if (document.visibilityState === 'visible') checkExpiry();
    };
    document.addEventListener('visibilitychange', onVisible);

    return () => {
      activityEvents.forEach((e) => window.removeEventListener(e, onActivity));
      window.clearInterval(interval);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);

  const updateField = useCallback((field, value) => {
    setFormData((current) => ({ ...current, [field]: value }));
  }, []);

  const setFields = useCallback((partial) => {
    setFormData((current) => ({ ...current, ...partial }));
  }, []);

  const reset = useCallback(() => {
    clearStoredApplication();
    setFormData(defaultState);
    setDocumentFile(null);
    setSessionExpired(false);
  }, []);

  const acknowledgeSessionExpiry = useCallback(() => setSessionExpired(false), []);

  // ---- Hazard contaminant (Other) helpers ----
  const updateHazContamOther = useCallback((index, value) => {
    setFormData((current) => {
      const others = [...(current.hazContamOthers || [])];
      others[index] = value;
      return { ...current, hazContamOthers: others };
    });
  }, []);

  const addHazContamOther = useCallback(() => {
    setFormData((current) => ({ ...current, hazContamOthers: [...(current.hazContamOthers || []), ''] }));
  }, []);

  const removeHazContamOther = useCallback((index) => {
    setFormData((current) => {
      const others = (current.hazContamOthers || []).filter((_, i) => i !== index);
      return { ...current, hazContamOthers: others.length ? others : [''] };
    });
  }, []);

  // ---- Hazard substance helpers ----
  const updateHazSubstance = useCallback((index, field, value) => {
    setFormData((current) => {
      const substances = [...(current.hazSubstances || [])];
      substances[index] = { ...substances[index], [field]: value };
      return { ...current, hazSubstances: substances };
    });
  }, []);

  const addHazSubstance = useCallback(() => {
    setFormData((current) => ({ ...current, hazSubstances: [...(current.hazSubstances || []), makeHazSubstance()] }));
  }, []);

  const removeHazSubstance = useCallback((index) => {
    setFormData((current) => {
      const substances = (current.hazSubstances || []).filter((_, i) => i !== index);
      return { ...current, hazSubstances: substances.length ? substances : [makeHazSubstance()] };
    });
  }, []);

  // ---- Site-map document helpers ----
  const setDocument = useCallback((file, documentType = 'SITEMAP') => {
    setDocumentFile(file);
    setFormData((current) => ({
      ...current,
      document: file
        ? { fileName: file.name, fileSize: file.size, contentType: file.type || 'application/octet-stream', documentType }
        : null,
    }));
  }, []);

  const removeDocument = useCallback(() => {
    setDocumentFile(null);
    setFormData((current) => ({ ...current, document: null }));
  }, []);

  // ---- CC email helpers ----
  const updateCcEmail = useCallback((index, value) => {
    setFormData((current) => {
      const ccEmails = [...(current.ccEmails || [])];
      ccEmails[index] = value;
      return { ...current, ccEmails };
    });
  }, []);

  const addCcEmail = useCallback(() => {
    setFormData((current) => ({ ...current, ccEmails: [...(current.ccEmails || []), ''] }));
  }, []);

  const removeCcEmail = useCallback((index) => {
    setFormData((current) => {
      const ccEmails = (current.ccEmails || []).filter((_, i) => i !== index);
      return { ...current, ccEmails: ccEmails.length ? ccEmails : [''] };
    });
  }, []);

  // ---- Well spec helpers ----
  const updateWellSpec = useCallback((index, field, value) => {
    setFormData((current) => {
      const wellSpecs = [...(current.wellSpecs || [])];
      wellSpecs[index] = { ...wellSpecs[index], [field]: value };
      return { ...current, wellSpecs };
    });
  }, []);

  const addWellSpec = useCallback(() => {
    setFormData((current) => {
      const spec = makeWellSpec();
      // New wells default to the project site coordinates so the map pin/coords are preset.
      if (current.siteLat) spec.latitude = String(current.siteLat);
      if (current.siteLong) spec.longitude = String(current.siteLong);
      return { ...current, wellSpecs: [...(current.wellSpecs || []), spec] };
    });
  }, []);

  // Presets each well-spec row's empty latitude/longitude to the project site coordinates
  // (matching legacy app_work_wellsmap.jsp, which centers each well on the project site lat/long).
  const seedWellSpecCoords = useCallback((lat, lng) => {
    if (!lat && !lng) return;
    setFormData((current) => {
      const rows = current.wellSpecs || [];
      let changed = false;
      const wellSpecs = rows.map((row) => {
        const next = { ...row };
        if (!next.latitude && lat) { next.latitude = String(lat); changed = true; }
        if (!next.longitude && lng) { next.longitude = String(lng); changed = true; }
        return next;
      });
      return changed ? { ...current, wellSpecs } : current;
    });
  }, []);

  const removeWellSpec = useCallback((index) => {
    setFormData((current) => {
      const wellSpecs = (current.wellSpecs || []).filter((_, i) => i !== index);
      return { ...current, wellSpecs: wellSpecs.length ? wellSpecs : [makeWellSpec()] };
    });
  }, []);

  // ---- Multi-work helpers (legacy "Add Another Type of Work to Project") ----
  // Commit the current draft (flat work fields) into the works list: append a new work, or replace
  // the one being edited. Called when leaving the Work Info step.
  const commitDraftWork = useCallback(() => {
    setFormData((current) => {
      const draft = extractDraftWork(current);
      if (!draft.workCat || !draft.workType) return current;
      const works = [...(current.works || [])];
      let idx = current.workEditIndex;
      if (idx == null || idx < 0 || idx >= works.length) {
        works.push(draft);
        idx = works.length - 1;
      } else {
        works[idx] = draft;
      }
      return { ...current, works, workEditIndex: idx };
    });
  }, []);

  // Clear the draft so the applicant can enter a brand-new work type.
  const startNewWork = useCallback(() => {
    setFormData((current) => ({ ...current, ...blankDraftWork(), workEditIndex: null }));
  }, []);

  // Save the modal's work: commit the draft (append new or replace the edited slot) AND clear the
  // draft back to blank in a single atomic update, so between modal sessions the draft never
  // pollutes the works list / running total. Used by the "Works" step Add/Update modal.
  const saveWork = useCallback(() => {
    setFormData((current) => {
      const draft = extractDraftWork(current);
      if (!draft.workCat || !draft.workType) return current;
      const works = [...(current.works || [])];
      const idx = current.workEditIndex;
      if (idx == null || idx < 0 || idx >= works.length) works.push(draft);
      else works[idx] = draft;
      return { ...current, ...blankDraftWork(), works, workEditIndex: null };
    });
  }, []);

  // Load a committed work back into the draft fields for editing.
  const editWork = useCallback((index) => {
    setFormData((current) => {
      const work = (current.works || [])[index];
      if (!work) return current;
      return {
        ...current,
        ...work,
        wellSpecs: (work.wellSpecs || []).map((s) => ({ ...s })),
        workEditIndex: index,
      };
    });
  }, []);

  // Remove a committed work; reset the draft when it was the one being edited.
  const deleteWork = useCallback((index) => {
    setFormData((current) => {
      const works = (current.works || []).filter((_, i) => i !== index);
      let editIndex = current.workEditIndex;
      let draftReset = {};
      if (editIndex === index) {
        editIndex = null;
        draftReset = blankDraftWork();
      } else if (editIndex != null && editIndex > index) {
        editIndex -= 1;
      }
      return { ...current, ...draftReset, works, workEditIndex: editIndex };
    });
  }, []);

  // ---- Fee computation ----
  // Sum of every work on the application (committed works + the in-progress draft). Mirrors the
  // backend FeeCalculator / legacy BeanApp.getAuthAmt so the Amount Due shown equals the stored
  // auth_amount at submit.
  const amountDue = useMemo(() => worksTotal(formData), [formData]);

  const summary = useMemo(
    () => ({
      applicant: `${formData.appFirstName} ${formData.appLastName}`.trim(),
      business: formData.appBusinessName,
      email: formData.appEmail,
      site: formData.siteLoc,
      city: formData.siteCityName,
      workType: formData.workDesc || formData.workType,
      paymentType: formData.paymentType,
      amountDue,
    }),
    [formData, amountDue]
  );

  return {
    formData,
    updateField,
    setFields,
    reset,
    sessionExpired,
    acknowledgeSessionExpiry,
    updateCcEmail,
    addCcEmail,
    removeCcEmail,
    updateWellSpec,
    addWellSpec,
    removeWellSpec,
    seedWellSpecCoords,
    commitDraftWork,
    startNewWork,
    saveWork,
    editWork,
    deleteWork,
    updateHazContamOther,
    addHazContamOther,
    removeHazContamOther,
    updateHazSubstance,
    addHazSubstance,
    removeHazSubstance,
    documentFile,
    setDocument,
    removeDocument,
    amountDue,
    summary,
  };
}

export default useApplication;