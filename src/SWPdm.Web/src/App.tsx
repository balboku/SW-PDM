import React from 'react';
import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom';
import { Layout } from './components/ui';
import Dashboard from './pages/Dashboard';
import UploadPage from './pages/Upload';
import { Home, UploadCloud, FileBox } from 'lucide-react';

const Sidebar = () => {
  const location = useLocation();
  const isActive = (path: string) => location.pathname === path;

  return (
    <div className="py-6 px-4 flex flex-col h-full bg-[#171717] text-white">
      <div className="mb-10 px-4">
        <h1 className="text-xl font-bold tracking-tight text-white flex items-center">
          <FileBox className="mr-2 text-[#D4AF37]" />
          SW PDM
        </h1>
        <p className="text-xs text-gray-400 mt-1 uppercase tracking-wider">Local Storage Edition</p>
      </div>

      <nav className="flex-1 space-y-2">
        <MenuLink to="/" icon={<Home size={20} />} label="系統概覽" active={isActive('/')} />
        <MenuLink to="/ingest" icon={<UploadCloud size={20} />} label="檔案入庫 (Ingest)" active={isActive('/ingest')} />
      </nav>
      
      <div className="pt-4 border-t border-gray-800 text-xs text-center text-gray-500">
        © 2026 SW PDM
      </div>
    </div>
  );
};

const MenuLink = ({ to, icon, label, active }: any) => {
  return (
    <Link
      to={to}
      className={`flex items-center space-x-3 px-4 py-3 rounded-lg transition-colors ${
        active 
          ? 'bg-[#404040] text-white font-medium' 
          : 'text-gray-400 hover:text-white hover:bg-gray-800'
      }`}
    >
      {icon}
      <span>{label}</span>
      {active && <div className="ml-auto w-1.5 h-1.5 rounded-full bg-[#D4AF37]"></div>}
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
        </Routes>
      </Layout>
    </BrowserRouter>
  );
}

export default App;
