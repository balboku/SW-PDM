import React, { useState, useEffect } from 'react';
import { Search, Filter, Loader2, Download, PackageOpen, Server, FileText } from 'lucide-react';
import { searchDocuments, downloadAssemblyZip } from '../lib/api';
import { BomTreeView } from '../components/BomTreeView';

export default function Documents() {
  const [query, setQuery] = useState('');
  const [documents, setDocuments] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedDoc, setSelectedDoc] = useState<any>(null);

  const fetchDocuments = async (searchQuery: string = '') => {
    setIsLoading(true);
    try {
      const data = await searchDocuments(searchQuery);
      setDocuments(data);
    } catch (error) {
      console.error('Failed to search documents', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDocuments();
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchDocuments(query);
  };

  return (
    <div className="flex h-full flex-col p-6 animate-in fade-in duration-500">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight flex items-center">
            <Server className="mr-3 text-[#D4AF37]" size={28} />
            圖檔中心 (Vault)
          </h1>
          <p className="text-gray-400 mt-2 text-sm">搜尋、檢視與管理伺服器上的設計圖檔</p>
        </div>
      </div>

      <div className="flex flex-col lg:flex-row gap-6 flex-1 min-h-0">
        {/* 左側列表區 */}
        <div className="flex-1 flex flex-col min-h-0 bg-[#121212] border border-gray-800 rounded-xl overflow-hidden shadow-2xl">
          <div className="p-4 border-b border-gray-800 bg-gray-900/50">
            <form onSubmit={handleSearch} className="flex gap-2">
              <div className="relative flex-1">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-500">
                  <Search size={18} />
                </div>
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="搜尋檔名、料號..."
                  className="block w-full pl-10 pr-3 py-2 border border-gray-700 rounded-lg leading-5 bg-gray-800/50 text-gray-100 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-[#D4AF37] focus:border-[#D4AF37] sm:text-sm transition-all"
                />
              </div>
              <button
                type="submit"
                className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-lg text-white bg-[#D4AF37] hover:bg-[#c2a033] shadow-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-[#D4AF37] disabled:opacity-50"
                disabled={isLoading}
              >
                {isLoading ? <Loader2 size={16} className="animate-spin mr-2" /> : <Filter size={16} className="mr-2" />}
                篩選
              </button>
            </form>
          </div>

          <div className="flex-1 overflow-auto">
            <table className="min-w-full divide-y divide-gray-800 text-sm">
              <thead className="bg-[#1a1a1a] sticky top-0 z-10 w-full">
                <tr>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">檔名</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">料號</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">類型</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">版次</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-400 tracking-wide">更新時間</th>
                </tr>
              </thead>
              <tbody className="bg-[#121212] divide-y divide-gray-800/50">
                {documents.length === 0 && !isLoading && (
                  <tr>
                    <td colSpan={5} className="px-6 py-10 text-center text-gray-500">
                      查無圖檔資料
                    </td>
                  </tr>
                )}
                
                {documents.map((doc) => (
                  <tr 
                    key={doc.documentId} 
                    onClick={() => setSelectedDoc(doc)}
                    className={`cursor-pointer transition-colors ${selectedDoc?.documentId === doc.documentId ? 'bg-gray-800/80 border-l-2 border-[#D4AF37]' : 'hover:bg-gray-800/40 border-l-2 border-transparent'}`}
                  >
                    <td className="px-6 py-4 whitespace-nowrap text-gray-200 font-medium flex items-center">
                      <FileText size={16} className="mr-2 text-gray-500" />
                      {doc.fileName}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-400">{doc.partNumber || '-'}</td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`px-2 py-1 inline-flex text-xs leading-5 font-semibold rounded-md ${
                        doc.documentType === 'Assembly' ? 'bg-yellow-900/40 text-yellow-500' : 'bg-blue-900/40 text-blue-400'
                      }`}>
                        {doc.documentType}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-400 text-center">{doc.revisionLabel}</td>
                    <td className="px-6 py-4 whitespace-nowrap text-gray-500 text-xs">
                      {new Date(doc.updatedAt).toLocaleString('zh-TW', {
                         year: 'numeric', month: '2-digit', day: '2-digit',
                         hour: '2-digit', minute: '2-digit'
                      })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* 右側預覽區 (Bom Tree / Info) */}
        {selectedDoc && (
          <div className="w-full lg:w-96 flex flex-col min-h-0 bg-[#121212] border border-gray-800 rounded-xl shadow-2xl animate-in slide-in-from-right-4 duration-300">
            <div className="p-4 border-b border-gray-800 bg-[#1a1a1a]">
              <div className="flex justify-between items-start">
                <h3 className="text-sm border border-gray-700 bg-gray-800 px-2 py-0.5 rounded text-gray-400 font-mono mb-2 inline-block">
                  {selectedDoc.partNumber || 'No Part Number'}
                </h3>
              </div>
              <h2 className="text-xl font-bold text-white break-all mb-1">{selectedDoc.fileName}</h2>
              <div className="flex items-center text-xs text-gray-400 mt-2 space-x-4">
                <span>Rev: <span className="text-gray-200 font-medium">{selectedDoc.revisionLabel}</span></span>
                <span>Type: <span className="text-gray-200">{selectedDoc.documentType}</span></span>
              </div>
            </div>

            <div className="p-4 flex-1 overflow-auto">
              {selectedDoc.documentType === 'Assembly' && selectedDoc.currentVersionNo ? (
                <div className="h-full flex flex-col">
                  <div className="flex justify-between items-center mb-3">
                    <h4 className="text-white font-medium flex items-center">
                      <PackageOpen size={16} className="mr-2 text-[#D4AF37]" />
                      BOM 結構預覽
                    </h4>
                    <button 
                      onClick={() => downloadAssemblyZip(selectedDoc.currentVersionNo)}
                      className="text-xs flex items-center text-gray-400 hover:text-white bg-gray-800 hover:bg-gray-700 px-2 py-1 rounded transition-colors"
                    >
                      <Download size={12} className="mr-1" /> Pack & Go
                    </button>
                  </div>
                  
                  <BomTreeView rootVersionId={selectedDoc.currentVersionNo} />
                </div>
              ) : (
                <div className="h-full flex flex-col items-center justify-center text-gray-500 bg-gray-900/30 rounded-lg border border-gray-800/50 p-6">
                  <FileText size={48} className="mb-4 text-gray-700" />
                  <p className="text-center text-sm">此為零件檔案，無 BOM 結構可顯示。</p>
                  <p className="text-center text-xs mt-2 text-gray-600">BOM 檢視僅支援組合件 (Assembly)</p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
