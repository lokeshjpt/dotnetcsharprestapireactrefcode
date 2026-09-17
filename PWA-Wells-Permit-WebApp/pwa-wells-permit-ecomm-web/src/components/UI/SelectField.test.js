import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import SelectField from './SelectField';

const options = [
  { code: 'CA', label: 'California' },
  { code: 'NV', label: 'Nevada' },
];

describe('SelectField', () => {
  test('renders label, placeholder and options', () => {
    render(<SelectField id="state" label="State" options={options} placeholder="Select..." />);
    const select = screen.getByLabelText(/State/);
    expect(select.tagName).toBe('SELECT');
    expect(screen.getByRole('option', { name: 'Select...' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'California' })).toBeInTheDocument();
  });

  test('required renders aria-required', () => {
    render(<SelectField id="state" label="State" options={options} required />);
    expect(screen.getByLabelText(/State/)).toHaveAttribute('aria-required', 'true');
  });

  test('error renders role=alert, aria-invalid and aria-describedby', () => {
    render(<SelectField id="state" label="State" options={options} required error="State is required." />);
    const select = screen.getByLabelText(/State/);
    expect(select).toHaveAttribute('aria-invalid', 'true');
    expect(select).toHaveAttribute('aria-describedby', 'state-error');
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('State is required.');
    expect(alert.id).toBe('state-error');
  });

  test('has no axe a11y violations (valid)', async () => {
    const { container } = render(<SelectField id="state" label="State" options={options} required placeholder="Select..." />);
    expect(await axe(container)).toHaveNoViolations();
  });

  test('has no axe a11y violations (error state)', async () => {
    const { container } = render(
      <SelectField id="state" label="State" options={options} required error="State is required." placeholder="Select..." />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
