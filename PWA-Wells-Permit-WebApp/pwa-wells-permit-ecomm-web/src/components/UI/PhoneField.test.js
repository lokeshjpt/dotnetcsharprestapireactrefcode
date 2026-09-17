import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import PhoneField from './PhoneField';

function renderPhone(props = {}) {
  const formData = props.formData || {};
  const updateField = props.updateField || jest.fn();
  return render(
    <PhoneField
      label="Phone"
      prefix="appPhone"
      formData={formData}
      updateField={updateField}
      {...props}
    />,
  );
}

describe('PhoneField', () => {
  test('renders a role=group labelled by the field label', () => {
    renderPhone();
    const group = screen.getByRole('group', { name: /Phone/ });
    expect(group).toBeInTheDocument();
  });

  test('renders per-segment aria-labels', () => {
    renderPhone();
    expect(screen.getByLabelText('Phone area code')).toBeInTheDocument();
    expect(screen.getByLabelText('Phone prefix')).toBeInTheDocument();
    expect(screen.getByLabelText('Phone line number')).toBeInTheDocument();
  });

  test('renders extension segment when withExt', () => {
    renderPhone({ withExt: true });
    expect(screen.getByLabelText('Phone extension')).toBeInTheDocument();
  });

  test('required renders visible required indicator', () => {
    renderPhone({ required: true });
    expect(screen.getByText('(required)')).toBeInTheDocument();
  });

  test('error renders role=alert, marks segments aria-invalid, group described-by', () => {
    renderPhone({ errors: { appPhone1: 'Phone area code must be 3 digits.' } });
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Phone area code must be 3 digits.');
    expect(alert.id).toBe('appPhone-error');
    expect(screen.getByLabelText('Phone area code')).toHaveAttribute('aria-invalid', 'true');
    const group = screen.getByRole('group', { name: /Phone/ });
    expect(group).toHaveAttribute('aria-describedby', 'appPhone-error');
  });

  test('reflects existing values', () => {
    renderPhone({ formData: { appPhone1: '510', appPhone2: '555', appPhone3: '1212' } });
    expect(screen.getByLabelText('Phone area code')).toHaveValue('510');
    expect(screen.getByLabelText('Phone line number')).toHaveValue('1212');
  });

  test('has no axe a11y violations (valid)', async () => {
    const { container } = renderPhone({ required: true, withExt: true });
    expect(await axe(container)).toHaveNoViolations();
  });

  test('has no axe a11y violations (error state)', async () => {
    const { container } = renderPhone({ errors: { appPhone1: 'Phone area code must be 3 digits.' } });
    expect(await axe(container)).toHaveNoViolations();
  });
});
