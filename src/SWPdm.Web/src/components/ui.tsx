import React, { ReactNode } from 'react';

/**
 * 共用外觀：Minimal 單欄式 Layout + Sidebar
 */
export const Layout = ({ children, sidebar }: { children: ReactNode, sidebar: ReactNode }) => {
  return (
    <div className="flex h-screen bg-[#F9FAFB] text-[#171717] font-sans antialiased">
      {/* Sidebar */}
      <aside className="w-64 flex-shrink-0 border-r border-[#E5E7EB] bg-white flex flex-col">
        {sidebar}
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col h-full overflow-hidden">
        <div className="flex-1 overflow-y-auto no-scrollbar p-8">
          <div className="max-w-7xl mx-auto">
            {children}
          </div>
        </div>
      </main>
    </div>
  );
};

export const Card = ({ children, className = '' }: { children: ReactNode, className?: string }) => (
  <div className={`bg-white border border-[#E5E7EB] rounded-lg shadow-sm ${className}`}>
    {children}
  </div>
);

export const Button = ({ children, onClick, variant = 'primary', className = '', ...props }: any) => {
  const base = "inline-flex items-center justify-center px-4 py-2 text-sm font-medium rounded-md transition-colors duration-200 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed";
  
  const variants = {
    primary: "bg-[#171717] text-white hover:bg-[#404040]",
    secondary: "bg-white text-[#171717] border border-[#E5E7EB] hover:bg-[#F3F4F6]",
    accent: "bg-[#D4AF37] text-[#171717] hover:bg-[#C19B2E]",
  };

  return (
    <button onClick={onClick} className={`${base} ${variants[variant as keyof typeof variants]} ${className}`} {...props}>
      {children}
    </button>
  );
};

export const Modal = ({ isOpen, onClose, title, children }: { isOpen: boolean, onClose: () => void, title: string, children: React.ReactNode }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
      <div 
        className="bg-[#121212] border border-gray-800 rounded-xl shadow-2xl w-full max-w-lg overflow-hidden animate-in zoom-in-95 duration-200"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between items-center px-6 py-4 border-b border-gray-800">
          <h3 className="text-lg font-semibold text-white">{title}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-white transition-colors">
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div className="p-6">
          {children}
        </div>
      </div>
    </div>
  );
};
