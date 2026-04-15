import React, { useEffect, useState } from 'react';
import { Database, Download, Server } from 'lucide-react';
import { Card, Button } from '../components/ui';
import { getSystemStatus, downloadAssemblyZip } from '../lib/api';

export default function Dashboard() {
  const [status, setStatus] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getSystemStatus().then((data) => {
      setStatus(data);
      setLoading(false);
    }).catch(console.error);
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717]">系統概覽</h1>
      </div>

      {loading ? (
        <div className="animate-pulse space-y-4">
          <div className="h-32 bg-gray-200 rounded-lg"></div>
          <div className="h-64 bg-gray-200 rounded-lg"></div>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Database className="w-5 h-5 text-accent" />
                <span className="font-medium">資料庫連線</span>
              </div>
              <div className="text-2xl font-semibold">{status?.isDatabaseConfigured ? "連線正常" : "尚未設定"}</div>
              <div className="text-sm mt-2 text-gray-500">Provider: {status?.databaseProvider}</div>
            </Card>

            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Server className="w-5 h-5 text-accent" />
                <span className="font-medium">本機儲存 (Vault)</span>
              </div>
              <div className="text-2xl font-semibold">{status?.isLocalStorageConfigured ? "已啟用" : "未啟用"}</div>
              <div className="text-sm mt-2 text-gray-500 truncate" title={status?.localStorageVaultPath}>
                Path: {status?.localStorageVaultPath}
              </div>
            </Card>

            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Server className="w-5 h-5 text-accent" />
                <span className="font-medium">SolidWorks 解析</span>
              </div>
              <div className="text-2xl font-semibold">{status?.isSolidWorksDocumentManagerConfigured ? "已啟用" : "未設定金鑰"}</div>
            </Card>
          </div>

          <Card className="p-6">
            <h2 className="text-lg font-semibold mb-4">功能測試區</h2>
            <p className="text-gray-600 mb-6">您可以點擊側邊欄的「📁 檔案入庫 (Ingest)」進行 CAD 檔案解析與寫入。<br/>或是點擊下方按鈕測試直接封裝下載 RootVersionId = 1 的組合件。</p>
            
            <Button variant="secondary" onClick={() => downloadAssemblyZip(1)}>
              <Download className="w-4 h-4 mr-2" />
              測試下載組合件 (ID: 1)
            </Button>
          </Card>
        </>
      )}
    </div>
  );
}
