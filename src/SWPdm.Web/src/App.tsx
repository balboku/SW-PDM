import React from 'react';
import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom';
import { Layout } from './components/ui';
import Dashboard from './pages/Dashboard';
import UploadPage from './pages/Upload';
import Documents from './pages/Documents';
import { Home, UploadCloud, FileBox, Search } from 'lucide-react';

const Sidebar = () => {
  const location = useLocation();
  const isActive = (path: string) => location.pathname === path;

  return (
    <div className="flex h-full flex-col bg-[#171717] px-3 py-3 text-white md:px-4 md:py-6">
      <div className="mb-3 px-1 md:mb-10 md:px-4">
        <h1 className="flex items-center text-lg font-bold tracking-tight text-white md:text-xl">
          <FileBox className="mr-2 text-[#D4AF37]" />
          SW PDM
        </h1>
        <p className="mt-1 hidden text-xs uppercase tracking-wider text-gray-400 sm:block">Local Storage Edition</p>
      </div>

      <nav className="flex min-w-0 flex-1 gap-1 md:block md:space-y-2">
        <MenuLink to="/" icon={<Home size={20} />} label="系統概覽" active={isActive('/')} />
        <MenuLink to="/ingest" icon={<UploadCloud size={20} />} label="檔案入庫 (Ingest)" active={isActive('/ingest')} />
        <MenuLink to="/documents" icon={<Search size={20} />} label="圖檔搜尋" active={isActive('/documents')} />
      </nav>
      
      <div className="hidden pt-4 border-t border-gray-800 text-xs text-center text-gray-500 md:block">
        © 2026 SW PDM
      </div>
    </div>
  );
};

const MenuLink = ({ to, icon, label, active }: any) => {
  return (
    <Link
      to={to}
      className={`flex min-w-0 flex-1 items-center justify-center space-x-2 rounded-lg px-2 py-2.5 transition-colors md:flex-none md:justify-start md:space-x-3 md:px-4 md:py-3 ${
        active 
          ? 'bg-[#404040] text-white font-medium' 
          : 'text-gray-400 hover:text-white hover:bg-gray-800'
      }`}
    >
      <span className="flex-none">{icon}</span>
      <span className="truncate text-xs sm:text-sm md:text-base">{label}</span>
      {active && <div className="ml-auto hidden w-1.5 h-1.5 rounded-full bg-[#D4AF37] md:block"></div>}
    </Link>
  );
};

function App() {
  return (
    <BrowserRouter>
      <Layout sidebar={<Sidebar />}>
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/ingest" element={<UploadPage />} />
          <Route path="/documents" element={<Documents />} />
        </Routes>
      </Layout>
    </BrowserRouter>
  );
}

export default App;
