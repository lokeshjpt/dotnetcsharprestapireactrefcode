import { createContext, useCallback, useContext, useState } from 'react';
import Toaster from './Toaster';

const ToastContext = createContext(() => {});

export function useToast() {
  return useContext(ToastContext);
}

let nextId = 0;

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const removeToast = useCallback((id) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const showToast = useCallback((message, type = 'success', duration = 3500) => {
    if (!message) return;
    const id = ++nextId;
    setToasts((current) => [...current, { id, message, type, duration }]);
  }, []);

  return (
    <ToastContext.Provider value={showToast}>
      {children}
      <div className="toaster-container">
        {toasts.map((toast) => (
          <Toaster
            key={toast.id}
            message={toast.message}
            type={toast.type}
            duration={toast.duration}
            onClose={() => removeToast(toast.id)}
          />
        ))}
      </div>
    </ToastContext.Provider>
  );
}
