import './ui.css';

const VARIANT_CLASS = {
  primary: 'btn-primary',
  secondary: 'btn-default',
  default: 'btn-default',
  ghost: 'btn-link',
  link: 'btn-link',
  danger: 'btn-danger',
};

function Button({ children, variant = 'primary', type = 'button', className = '', ...props }) {
  const variantClass = VARIANT_CLASS[variant] || 'btn-primary';
  return (
    <button type={type} className={`btn ${variantClass} ${className}`.trim()} {...props}>
      {children}
    </button>
  );
}

export default Button;