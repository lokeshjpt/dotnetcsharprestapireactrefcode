import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import StepHazard from './StepHazard';

function renderStep(overrides = {}) {
  const formData = {
    hazContamOthers: [''],
    hazSubstances: [{ concentration: '', pelPpm: '', healthEffects: '' }],
    ...(overrides.formData || {}),
  };
  const props = {
    formData,
    updateField: jest.fn(),
    errors: {},
    updateHazContamOther: jest.fn(),
    addHazContamOther: jest.fn(),
    removeHazContamOther: jest.fn(),
    updateHazSubstance: jest.fn(),
    addHazSubstance: jest.fn(),
    removeHazSubstance: jest.fn(),
    ...overrides,
    formData,
  };
  return render(<StepHazard {...props} />);
}

describe('StepHazard', () => {
  test('renders the four PPE level checkboxes', () => {
    renderStep();
    expect(screen.getByRole('checkbox', { name: /A \(Highest\)/ })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /B \(High\)/ })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /C \(Medium\)/ })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /D \(Low\)/ })).toBeInTheDocument();
  });

  test('renders the 10 equipment fieldsets with Required/Available checkboxes', () => {
    renderStep();
    const legends = [
      'Hard Hat', 'Safety Shoes', 'Orange Traffic Vest', 'Hearing Protection',
      'Safety Eye Wear', 'Clothing', 'Respirator', 'Cartridge', 'Gloves', 'Other',
    ];
    legends.forEach((label) => {
      expect(screen.getByRole('checkbox', { name: `${label} required` })).toBeInTheDocument();
      expect(screen.getByRole('checkbox', { name: `${label} available on site` })).toBeInTheDocument();
    });
    // 10 "required" + 10 "available" equipment checkboxes
    const requiredBoxes = screen.getAllByRole('checkbox', { name: /required$/ });
    expect(requiredBoxes).toHaveLength(10);
  });

  test('renders equipment description inputs for description-bearing rows', () => {
    renderStep();
    expect(screen.getByLabelText('Clothing description')).toBeInTheDocument();
    expect(screen.getByLabelText('Respirator description')).toBeInTheDocument();
    expect(screen.getByLabelText('Other description')).toBeInTheDocument();
  });

  test('renders provider name/title inputs', () => {
    renderStep();
    // Provider section uses ids so target by id-associated labels within group
    expect(document.getElementById('hazProviderLastName')).toBeInTheDocument();
    expect(document.getElementById('hazProviderFirstName')).toBeInTheDocument();
    expect(document.getElementById('hazProviderTitle')).toBeInTheDocument();
  });

  test('renders the required Acknowledgement checkbox', () => {
    renderStep();
    const ack = screen.getByRole('checkbox', { name: /I certify that the above hazardous-materials information is accurate/ });
    expect(ack).toBeInTheDocument();
  });

  test('acknowledgement error surfaces role=alert with aria wiring', () => {
    renderStep({ errors: { hazAcknowledgement: 'You must acknowledge the hazardous-materials certification.' } });
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(/must acknowledge/);
    expect(alert.id).toBe('hazAcknowledgement-error');
    const ack = screen.getByRole('checkbox', { name: /I certify/ });
    expect(ack).toHaveAttribute('aria-invalid', 'true');
    expect(ack).toHaveAttribute('aria-describedby', 'hazAcknowledgement-error');
  });

  test('renders consultant and safety officer phone groups', () => {
    renderStep();
    // Multiple phone groups (consultant, cell, safety, provider) each expose an area-code segment.
    expect(screen.getAllByLabelText('Phone area code').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByRole('group').length).toBeGreaterThanOrEqual(4);
  });

  // NOTE: StepHazard contains PRE-EXISTING a11y gaps in production markup that are
  // out of scope to fix here (constraint: report, don't change markup). axe reports:
  //   - "label": the "Anticipated Hazardous Substances" table inputs have no <label>
  //   - "select-name": the meeting-time minute <select> has no accessible name
  //   - "empty-table-header": the trailing (actions) <th></th> in the substances table
  // These tests characterize those known violations and assert no OTHER (regression)
  // violations are introduced by the ADA-hardened parts of the step.
  const KNOWN_PREEXISTING = ['empty-table-header', 'label', 'select-name'];

  test('a11y: only known pre-existing violations, no regressions (valid state)', async () => {
    const { container } = renderStep();
    const results = await axe(container);
    const unexpected = results.violations.filter((v) => !KNOWN_PREEXISTING.includes(v.id));
    expect(unexpected).toEqual([]);
  });

  test('a11y: only known pre-existing violations, no regressions (error state)', async () => {
    const { container } = renderStep({
      errors: {
        hazConsultantFirstName: 'Site Consultant First Name is required.',
        hazAcknowledgement: 'You must acknowledge the hazardous-materials certification.',
      },
    });
    const results = await axe(container);
    const unexpected = results.violations.filter((v) => !KNOWN_PREEXISTING.includes(v.id));
    expect(unexpected).toEqual([]);
  });
});
