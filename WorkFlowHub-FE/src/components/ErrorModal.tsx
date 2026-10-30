import React from 'react';
import { ShieldAlert, X, AlertTriangle } from 'lucide-react';

interface ErrorModalProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  message: string;
  code?: string;
}

export const ErrorModal: React.FC<ErrorModalProps> = ({
  isOpen,
  onClose,
  title = 'Error Notification',
  message,
  code,
}) => {
  if (!isOpen) return null;

  const isForbidden = code === 'FORBIDDEN' || code === '403';

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-in fade-in">
      <div 
        className="bg-white rounded-3xl max-w-md w-full p-6 sm:p-7 shadow-2xl relative border border-gray-100 text-center animate-in zoom-in-95 duration-200"
        role="dialog"
        aria-modal="true"
      >
        {/* Close button top right */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 p-1.5 text-gray-400 hover:text-gray-700 hover:bg-gray-100 rounded-xl transition-colors cursor-pointer"
          title="Close"
        >
          <X size={18} />
        </button>

        {/* Warning Icon */}
        <div className={`w-14 h-14 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-xs ring-8 ${
          isForbidden 
            ? 'bg-amber-100/90 text-amber-700 ring-amber-50' 
            : 'bg-red-100/90 text-red-600 ring-red-50'
        }`}>
          {isForbidden ? <ShieldAlert size={28} /> : <AlertTriangle size={28} />}
        </div>

        {/* Error Code Badge */}
        <div className="mb-2">
          <span className={`inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider ${
            isForbidden 
              ? 'bg-amber-100 text-amber-800 border border-amber-200' 
              : 'bg-red-100 text-red-800 border border-red-200'
          }`}>
            {code || (isForbidden ? '403 FORBIDDEN' : 'OPERATION ERROR')}
          </span>
        </div>

        {/* Title */}
        <h3 className="text-base sm:text-lg font-bold text-gray-900 mb-2">
          {title}
        </h3>

        {/* Error Message */}
        <div className="bg-gray-50/80 rounded-2xl p-3.5 mb-5 border border-gray-100">
          <p className="text-xs sm:text-sm text-gray-700 leading-relaxed font-medium">
            {message}
          </p>
        </div>

        {/* Confirm button */}
        <button
          onClick={onClose}
          className="w-full py-2.5 px-4 bg-emerald-600 hover:bg-emerald-700 text-white text-xs sm:text-sm font-semibold rounded-xl shadow-xs transition-all cursor-pointer"
        >
          Got it
        </button>
      </div>
    </div>
  );
};
