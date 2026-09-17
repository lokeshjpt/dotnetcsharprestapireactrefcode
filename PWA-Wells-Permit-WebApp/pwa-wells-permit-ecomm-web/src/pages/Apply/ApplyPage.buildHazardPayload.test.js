import { buildHazardPayload } from './ApplyPage';

// ApplyPage transitively imports axios (api layer) and react-router-dom.
// Mock both so these pure-function tests need no network or router wiring.
// (react-router-dom v7's subpath exports don't resolve under CRA's jest-resolve.)
jest.mock('axios');
jest.mock('react-router-dom', () => ({
  useNavigate: () => jest.fn(),
}));

describe('buildHazardPayload', () => {
  test('present flag reflects sitehazardrequired === Y', () => {
    expect(buildHazardPayload({ sitehazardrequired: 'Y' }).present).toBe(true);
    expect(buildHazardPayload({ sitehazardrequired: 'N' }).present).toBe(false);
    expect(buildHazardPayload({}).present).toBe(false);
  });

  test('PPE checkbox booleans map to Y/N', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazPpeA: true,
      hazPpeB: false,
      hazPpeC: true,
      hazPpeD: undefined,
    });
    expect(payload.ppeLevelA).toBe('Y');
    expect(payload.ppeLevelB).toBe('N');
    expect(payload.ppeLevelC).toBe('Y');
    expect(payload.ppeLevelD).toBe('N');
  });

  test('equipment flags pass through (or null when empty)', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazEquipHardHat: 'R',
      hazEquipSafetyShoes: 'A',
      hazEquipClothing: 'R',
      hazEquipClothingDesc: 'Tyvek suit',
    });
    expect(payload.equipHardHatFlag).toBe('R');
    expect(payload.equipSafetyShoesFlag).toBe('A');
    expect(payload.equipClothingFlag).toBe('R');
    expect(payload.equipClothingDesc).toBe('Tyvek suit');
    // untouched equipment defaults to null
    expect(payload.equipOrangeVestFlag).toBeNull();
    expect(payload.equipGlovesDesc).toBeNull();
  });

  test('acknowledgement coerced to boolean', () => {
    expect(buildHazardPayload({ hazAcknowledgement: true }).acknowledgement).toBe(true);
    expect(buildHazardPayload({ hazAcknowledgement: 'yes' }).acknowledgement).toBe(true);
    expect(buildHazardPayload({}).acknowledgement).toBe(false);
  });

  test('provider phone joined with dashes and extension', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazProviderPhone1: '510',
      hazProviderPhone2: '555',
      hazProviderPhone3: '1212',
      hazProviderPhoneX: '77',
    });
    expect(payload.infoProvidedByPhone).toBe('510-555-1212 x77');
  });

  test('provider phone without extension omits x', () => {
    const payload = buildHazardPayload({
      hazProviderPhone1: '510',
      hazProviderPhone2: '555',
      hazProviderPhone3: '1212',
    });
    expect(payload.infoProvidedByPhone).toBe('510-555-1212');
  });

  test('empty provider phone becomes null', () => {
    expect(buildHazardPayload({}).infoProvidedByPhone).toBeNull();
  });

  test('consultant/safety phones joined (empty string when absent)', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazConsultantPhone1: '415',
      hazConsultantPhone2: '222',
      hazConsultantPhone3: '3333',
    });
    expect(payload.consultantPhone).toBe('415-222-3333');
    expect(payload.safetyOfficerPhone).toBe('');
  });

  test('contaminants array built from checkbox flags plus others', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazContamGasoline: true,
      hazContamDiesel: false,
      hazContamWasteOil: true,
      hazContamOthers: ['Benzene', '  ', 'MTBE'],
    });
    expect(payload.contaminants).toEqual(['Gasoline', 'Waste Oil', 'Benzene', 'MTBE']);
  });

  test('contaminants empty when nothing selected', () => {
    expect(buildHazardPayload({}).contaminants).toEqual([]);
  });

  test('substances filtered to rows with any value', () => {
    const payload = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazSubstances: [
        { concentration: '', pelPpm: '', healthEffects: '' },
        { concentration: '5', pelPpm: '1', healthEffects: 'irritant' },
      ],
    });
    expect(payload.substances).toEqual([
      { concentration: '5', pelPpm: '1', healthEffects: 'irritant' },
    ]);
  });

  test('meeting time only assembled when hazards present', () => {
    const present = buildHazardPayload({
      sitehazardrequired: 'Y',
      hazMeetingHour: '09',
      hazMeetingMinute: '30',
      hazMeetingShift: 'AM',
    });
    expect(present.siteSafetyMeetingTime).toBe('09:30 AM');
    const absent = buildHazardPayload({ sitehazardrequired: 'N' });
    expect(absent.siteSafetyMeetingTime).toBe('');
  });
});
