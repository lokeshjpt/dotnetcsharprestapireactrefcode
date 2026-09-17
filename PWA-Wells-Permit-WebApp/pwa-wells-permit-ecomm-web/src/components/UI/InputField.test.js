import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import InputField from './InputField';

describe('InputField', () => {
  test('renders label and associates it with the input', () => {
    render(<InputField id="fname" label="First Name" />);
    const input = screen.getByLabelText(/First Name/);
    expect(input).toBeInTheDocument();
    expect(input.id).toBe('fname');
  });

  test('required renders aria-required and required attribute', () => {
    render(<InputField id="fname" label="First Name" required />);
    const input = screen.getByLabelText(/First Name/);
    expect(input).toHaveAttribute('aria-required', 'true');
    expect(input).toBeRequired();
  });

  test('error renders role=alert, aria-invalid and aria-describedby link', () => {
    render(<InputField id="fname" label="First Name" required error="First Name is required." />);
    const input = screen.getByLabelText(/First Name/);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('First Name is required.');
    expect(alert.id).toBe('fname-error');
    expect(input).toHaveAttribute('aria-describedby', expect.stringContaining('fname-error'));
  });

  test('hint is described-by when no error', () => {
    render(<InputField id="fname" label="First Name" hint="As on ID" />);
    const input = screen.getByLabelText(/First Name/);
    expect(input).toHaveAttribute('aria-describedby', 'fname-hint');
  });

  test('no aria-invalid when valid', () => {
    render(<InputField id="fname" label="First Name" />);
    expect(screen.getByLabelText(/First Name/)).not.toHaveAttribute('aria-invalid');
  });

  test('has no axe a11y violations (valid)', async () => {
    const { container } = render(<InputField id="fname" label="First Name" required />);
    expect(await axe(container)).toHaveNoViolations();
  });

  test('has no axe a11y violations (error state)', async () => {
    const { container } = render(
      <InputField id="fname" label="First Name" required error="First Name is required." />,
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
