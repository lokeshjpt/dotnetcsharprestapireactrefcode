import {
  stepValidators,
  validateStepByKey,
} from './stepValidators';
import {
  isWeekend,
  isCountyHoliday,
  addDays,
  startOfToday,
  VALID_START_MIN_DAYS,
} from './countyHolidays';

// Helper to run a validator and get the raw (unfiltered) error object.
const run = (key, fd) => stepValidators[key](fd);

const toYmd = (d) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

// First weekday that is not a County holiday on or after the given date. Mirrors the scheduling
// window rules so date fixtures never flake by landing on a weekend/holiday.
const nextBusinessDay = (d) => {
  let x = d;
  while (isWeekend(x) || isCountyHoliday(x)) x = addDays(x, 1);
  return x;
};

describe('stepValidators - applicantErrors', () => {
  const validApplicant = {
    appBusinessName: 'Acme Drilling',
    appLastName: 'Smith',
    appFirstName: 'John',
    appAddr: '123 Main St',
    appCity: 'Oakland',
    appState: 'CA',
    appZip: '94607',
    appPhone1: '510',
    appPhone2: '555',
    appPhone3: '1212',
    appEmail: 'john@example.com',
  };

  test('valid applicant produces no truthy errors', () => {
    const errors = validateStepByKey('applicant', validApplicant);
    expect(errors).toEqual({});
  });

  test('required fields flagged when empty', () => {
    const e = run('applicant', {});
    expect(e.appBusinessName).toMatch(/required/);
    expect(e.appLastName).toMatch(/required/);
    expect(e.appFirstName).toMatch(/required/);
    expect(e.appAddr).toMatch(/required/);
    expect(e.appCity).toMatch(/required/);
    expect(e.appState).toMatch(/required/);
    expect(e.appZip).toMatch(/required/);
    expect(e.appEmail).toMatch(/required/);
    // phone required
    expect(e.appPhone1).toMatch(/required/);
  });

  test('optional address line 2 not required', () => {
    const e = run('applicant', validApplicant);
    expect(e.appAddr2).toBeNull();
  });

  test('business name maxLen 100 enforced', () => {
    const e = run('applicant', { ...validApplicant, appBusinessName: 'x'.repeat(101) });
    expect(e.appBusinessName).toMatch(/100 characters/);
  });

  test('last name maxLen 50 enforced', () => {
    const e = run('applicant', { ...validApplicant, appLastName: 'x'.repeat(51) });
    expect(e.appLastName).toMatch(/50 characters/);
  });

  test('invalid email flagged', () => {
    const e = run('applicant', { ...validApplicant, appEmail: 'not-an-email' });
    expect(e.appEmail).toMatch(/valid email/);
  });

  test('bad zip flagged', () => {
    const e = run('applicant', { ...validApplicant, appZip: '123' });
    expect(e.appZip).toMatch(/5 digits/);
  });

  test('partial phone flags the missing segment', () => {
    const e = run('applicant', { ...validApplicant, appPhone1: '51', appPhone2: '', appPhone3: '' });
    expect(e.appPhone1).toMatch(/area code must be 3 digits/);
  });

  test('phone prefix segment error when area code ok but prefix bad', () => {
    const e = run('applicant', { ...validApplicant, appPhone2: '55' });
    expect(e.appPhone2).toMatch(/prefix must be 3 digits/);
  });

  test('phone line number segment error when others ok', () => {
    const e = run('applicant', { ...validApplicant, appPhone3: '121' });
    expect(e.appPhone3).toMatch(/line number must be 4 digits/);
  });

  test('extension must be numeric when provided', () => {
    const e = run('applicant', { ...validApplicant, appPhoneX: 'abc' });
    expect(e.appPhoneX).toMatch(/numeric/);
  });

  test('optional contact email validated when present', () => {
    const e = run('applicant', { ...validApplicant, conEmail: 'bad' });
    expect(e.conEmail).toMatch(/valid email/);
  });

  test('cc emails validated per index', () => {
    const e = run('applicant', { ...validApplicant, ccEmails: ['good@x.com', 'bad'] });
    expect(e['ccEmails.0']).toBeUndefined();
    expect(e['ccEmails.1']).toMatch(/valid email/);
  });
});

describe('stepValidators - locationErrors', () => {
  const validLocation = {
    siteLoc: '4825 Gleason Dr',
    siteCity: 'ALA',
    siteLat: 37.7024,
    siteLong: -121.9282,
  };

  test('valid location produces no errors', () => {
    expect(validateStepByKey('location', validLocation)).toEqual({});
  });

  test('required fields flagged when empty', () => {
    const e = run('location', {});
    expect(e.siteLoc).toMatch(/required/);
    expect(e.siteCity).toMatch(/select the location city/i);
    expect(e.siteLat).toMatch(/identify the project location/i);
  });

  test('siteLoc maxLen 200 enforced', () => {
    const e = run('location', { ...validLocation, siteLoc: 'x'.repeat(201) });
    expect(e.siteLoc).toMatch(/200 characters/);
  });

  test('missing coordinates flagged', () => {
    const e = run('location', { ...validLocation, siteLat: '', siteLong: '' });
    expect(e.siteLat).toMatch(/identify the project location/i);
  });
});

describe('stepValidators - projectErrors', () => {
  const startD = nextBusinessDay(addDays(startOfToday(), VALID_START_MIN_DAYS + 3));
  const endD = nextBusinessDay(addDays(startD, 5));
  const future = toYmd(startD);
  const laterFuture = toYmd(endD);
  const validProject = {
    siteLoc: 'North lot',
    siteCity: 'OAK',
    startDate: future,
    endDate: laterFuture,
    sitehazardrequired: 'N',
    ownLastName: 'Doe',
    ownFirstName: 'Jane',
    ownAddr: '1 A St',
    ownCity: 'Oakland',
    ownState: 'CA',
    ownZip: '94607',
    cliLastName: 'Roe',
    cliFirstName: 'Rick',
    cliAddr: '2 B St',
    cliCity: 'Oakland',
    cliState: 'CA',
    cliZip: '94607',
  };

  test('valid project produces no errors', () => {
    expect(validateStepByKey('project', validProject)).toEqual({});
  });

  test('required fields flagged when empty', () => {
    const e = run('project', {});
    expect(e.startDate).toMatch(/required/);
    expect(e.sitehazardrequired).toMatch(/site hazards/);
    expect(e.ownLastName).toMatch(/required/);
    // Client is optional when no Client Last Name is entered (matches Java "same as Property Owner").
    expect(e.cliLastName).toBeNull();
    expect(e.cliFirstName).toBeNull();
    expect(e.cliAddr).toBeNull();
  });

  test('client fields become required once a client last name is entered', () => {
    const e = run('project', { cliLastName: 'Roe' });
    expect(e.cliLastName).toBeNull();
    expect(e.cliFirstName).toMatch(/required/);
    expect(e.cliAddr).toMatch(/required/);
    expect(e.cliCity).toMatch(/required/);
    expect(e.cliState).toMatch(/required/);
    expect(e.cliZip).toMatch(/required/);
  });

  test('start date must be within the valid scheduling window', () => {
    const past = toYmd(addDays(startOfToday(), -1));
    const e = run('project', { ...validProject, startDate: past });
    expect(e.startDate).toMatch(/must be between/i);
  });

  test('end date must be after start date', () => {
    const e = run('project', { ...validProject, endDate: future, startDate: laterFuture });
    expect(e.endDate).toMatch(/on or after the Project Start Date/i);
  });

  test('optional owner email validated when present', () => {
    const e = run('project', { ...validProject, ownEmail: 'bad' });
    expect(e.ownEmail).toMatch(/valid email/);
  });
});

describe('stepValidators - workTypeErrors', () => {
  test('flags missing category and type', () => {
    const e = run('workType', {});
    expect(e.workCat).toMatch(/work category/);
    expect(e.workType).toMatch(/work type/);
  });

  test('valid selection has no errors', () => {
    expect(validateStepByKey('workType', { workCat: 'CON', workType: 'X' })).toEqual({});
  });
});

describe('stepValidators - workInfoErrors', () => {
  const base = {
    drillerName: 'Bob',
    drillerLic: 'LIC123',
    dmeth: 'MUD',
    wellSpecs: [
      { owellnum: 'W1', holediam: '8', casediam: '6', sealdepth: '20', maxdepth: '100' },
    ],
  };

  test('valid construction work info has no errors', () => {
    expect(validateStepByKey('workInfo', { ...base, workCat: 'CON', wUse: 'DOM' })).toEqual({});
  });

  test('construction requires well use', () => {
    const e = run('workInfo', { ...base, workCat: 'Construction' });
    expect(e.wUse).toMatch(/Well Use is required/);
  });

  test('other drill method requires description', () => {
    const e = run('workInfo', { ...base, dmeth: 'OTH' });
    expect(e.dmethOth).toMatch(/required/);
  });

  test('investigation category requires bore/diam/depth', () => {
    const e = run('workInfo', { ...base, workCat: 'Investigation' });
    expect(e.numbore).toMatch(/required/);
    expect(e.holediam).toMatch(/required/);
    expect(e.maxdepth).toMatch(/required/);
  });

  test('empty well specs flagged', () => {
    const e = run('workInfo', { ...base, wellSpecs: [] });
    expect(e.wellSpecs).toMatch(/At least one well specification/);
  });

  test('well spec row required fields validated by index', () => {
    const e = run('workInfo', { ...base, wellSpecs: [{ owellnum: '', holediam: 'x', casediam: '', sealdepth: '', maxdepth: '' }] });
    expect(e['wellSpecs.0.owellnum']).toMatch(/required/);
    expect(e['wellSpecs.0.holediam']).toMatch(/number/);
    expect(e['wellSpecs.0.casediam']).toMatch(/required/);
  });
});

describe('stepValidators - paymentErrors', () => {
  test('requires payment type', () => {
    const e = run('payment', {});
    expect(e.paymentType).toMatch(/payment type/);
  });

  test('check requires name on account', () => {
    const e = run('payment', { paymentType: 'CHECK' });
    expect(e.acctName).toMatch(/required/);
  });

  test('check with account name valid', () => {
    expect(validateStepByKey('payment', { paymentType: 'CHECK', acctName: 'John Smith' })).toEqual({});
  });

  test('non-check type has no account name requirement', () => {
    expect(validateStepByKey('payment', { paymentType: 'CC' })).toEqual({});
  });

  test('fee-exempt type has no additional field requirement', () => {
    expect(validateStepByKey('payment', { paymentType: 'EXMPT' })).toEqual({});
  });
});

describe('stepValidators - hazardErrors (NEW hazard validation)', () => {
  const validHaz = {
    sitehazardrequired: 'Y',
    hazConsultantFirstName: 'Amy',
    hazConsultantLastName: 'Lee',
    hazSafetyFirstName: 'Sam',
    hazSafetyLastName: 'Ng',
    hazProviderFirstName: 'Pat',
    hazProviderLastName: 'Prov',
    hazPpeD: true,
    hazContamGasoline: true,
    hazAcknowledgement: true,
  };

  test('no hazard validation when sitehazardrequired !== Y', () => {
    expect(run('hazard', { sitehazardrequired: 'N' })).toEqual({});
    expect(run('hazard', {})).toEqual({});
  });

  test('acknowledgement required (falsy) => error when hazards present', () => {
    const e = run('hazard', { ...validHaz, hazAcknowledgement: false });
    expect(e.hazAcknowledgement).toMatch(/acknowledge the hazardous-materials certification/);
  });

  test('acknowledgement undefined => error present', () => {
    const { hazAcknowledgement, ...noAck } = validHaz;
    const e = run('hazard', noAck);
    expect(e.hazAcknowledgement).toMatch(/acknowledge/);
  });

  test('acknowledgement truthy => no acknowledgement error', () => {
    const e = run('hazard', validHaz);
    expect(e.hazAcknowledgement).toBeNull();
  });

  test('valid hazard section produces no truthy errors', () => {
    expect(validateStepByKey('hazard', validHaz)).toEqual({});
  });

  test('consultant names required', () => {
    const e = run('hazard', { sitehazardrequired: 'Y', hazAcknowledgement: true });
    expect(e.hazConsultantFirstName).toMatch(/required/);
    expect(e.hazConsultantLastName).toMatch(/required/);
  });

  test('safety officer names required', () => {
    const e = run('hazard', { sitehazardrequired: 'Y', hazAcknowledgement: true });
    expect(e.hazSafetyFirstName).toMatch(/required/);
    expect(e.hazSafetyLastName).toMatch(/required/);
  });

  test('preparer (information provided by) names required', () => {
    const e = run('hazard', { sitehazardrequired: 'Y', hazAcknowledgement: true });
    expect(e.hazProviderFirstName).toMatch(/required/);
    expect(e.hazProviderLastName).toMatch(/required/);
  });

  test('at least one PPE level required', () => {
    const { hazPpeD, ...noPpe } = validHaz;
    const e = run('hazard', noPpe);
    expect(e.hazPpe).toMatch(/at least one level of protection/i);
  });

  test('any single PPE level satisfies the requirement', () => {
    const { hazPpeD, ...noPpe } = validHaz;
    const e = run('hazard', { ...noPpe, hazPpeB: true });
    expect(e.hazPpe).toBeNull();
  });

  test('at least one contaminant required', () => {
    const { hazContamGasoline, ...noContam } = validHaz;
    const e = run('hazard', noContam);
    expect(e.hazContam).toMatch(/at least one contaminant/i);
  });

  test('an "other" contaminant satisfies the requirement', () => {
    const { hazContamGasoline, ...noContam } = validHaz;
    const e = run('hazard', { ...noContam, hazContamOthers: ['Benzene'] });
    expect(e.hazContam).toBeNull();
  });

  test('equipment description maxLen 150 enforced', () => {
    const e = run('hazard', { ...validHaz, hazEquipClothingDesc: 'x'.repeat(151) });
    expect(e.hazEquipClothingDesc).toMatch(/150 characters/);
  });

  test('equipment description within 150 chars ok', () => {
    const e = run('hazard', { ...validHaz, hazEquipRespiratorDesc: 'x'.repeat(150) });
    expect(e.hazEquipRespiratorDesc).toBeNull();
  });

  test('provider phone optional (not required when empty)', () => {
    const e = run('hazard', validHaz);
    expect(e.hazProviderPhone1).toBeUndefined();
  });

  test('provider phone validated when partially entered', () => {
    const e = run('hazard', { ...validHaz, hazProviderPhone1: '51' });
    expect(e.hazProviderPhone1).toMatch(/area code must be 3 digits/);
  });

  test('consultant phone optional when empty', () => {
    const e = run('hazard', validHaz);
    expect(e.hazConsultantPhone1).toBeUndefined();
  });

  test('other contaminants validated by index', () => {
    const e = run('hazard', { ...validHaz, hazContamOthers: ['ok', 'x'.repeat(101)] });
    expect(e['hazContamOthers.0']).toBeUndefined();
    expect(e['hazContamOthers.1']).toMatch(/100 characters/);
  });

  test('substance rows validated by index', () => {
    const e = run('hazard', {
      ...validHaz,
      hazSubstances: [{ concentration: 'x'.repeat(101), pelPpm: '', healthEffects: '' }],
    });
    expect(e['hazSubstances.0.concentration']).toMatch(/100 characters/);
  });

  test('meeting date validated when present', () => {
    const e = run('hazard', { ...validHaz, hazMeetingDate: 'not-a-date' });
    expect(e.hazMeetingDate).toMatch(/valid date/);
  });
});

describe('stepValidators - misc steps', () => {
  test('verify always empty', () => {
    expect(validateStepByKey('verify', {})).toEqual({});
  });

  test('unknown key returns empty errors', () => {
    expect(validateStepByKey('nonexistent', {})).toEqual({});
  });

  test('validateStepByKey filters out null/falsy entries', () => {
    const errors = validateStepByKey('applicant', {
      appBusinessName: 'Acme',
      appLastName: 'Smith',
      appFirstName: 'John',
      appAddr: '123 Main',
      appCity: 'Oakland',
      appState: 'CA',
      appZip: '94607',
      appPhone1: '510',
      appPhone2: '555',
      appPhone3: '1212',
      appEmail: 'john@example.com',
    });
    // Every value in the filtered result must be truthy.
    Object.values(errors).forEach((v) => expect(v).toBeTruthy());
  });
});
