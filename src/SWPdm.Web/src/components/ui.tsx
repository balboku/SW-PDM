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
