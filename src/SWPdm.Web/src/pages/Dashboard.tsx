import React, { useEffect, useState } from 'react';
import { Database, Server, RefreshCw, AlertCircle, UploadCloud, Search } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Card, Button } from '../components/ui';
import { getSystemStatus, getDatabaseStatus } from '../lib/api';

export default function Dashboard() {
  const [status, setStatus] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [database, setDatabase] = useState<any>(null);
  const [error, setError] = useState(false);

  const loadStatus = async () => {
    setLoading(true);
    setError(false);
    try {
      const [configuration, connection] = await Promise.all([
        getSystemStatus(), getDatabaseStatus().catch(() => null)
      ]);
      setStatus(configuration);
      setDatabase(connection);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadStatus();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight text-[#171717] sm:text-3xl">系統概覽</h1>
        <Button variant="secondary" disabled={loading} onClick={loadStatus}>
          <RefreshCw size={16} className={`mr-2 ${loading ? 'animate-spin' : ''}`} />重新檢查
        </Button>
      </div>

      <Card className="p-5">
        <h2 className="mb-2 text-lg font-semibold">開始管理圖檔</h2>
        <p className="mb-4 text-sm text-gray-600">新圖檔請先匯入；已出庫修改的圖檔，請回圖檔中心選取原文件再入庫。</p>
        <div className="flex flex-col gap-3 sm:flex-row">
          <Link to="/documents" className="inline-flex items-center justify-center rounded-md bg-[#171717] px-4 py-3 text-sm font-medium text-white focus-visible:ring-2 focus-visible:ring-blue-500">
            <Search size={17} className="mr-2" />開啟圖檔中心
          </Link>
          <Link to="/ingest" className="inline-flex items-center justify-center rounded-md border border-gray-300 px-4 py-3 text-sm font-medium focus-visible:ring-2 focus-visible:ring-blue-500">
            <UploadCloud size={17} className="mr-2" />首次匯入 CAD
          </Link>
        </div>
      </Card>

      {loading && <p role="status" className="text-sm text-gray-600">正在檢查系統狀態，請稍候…</p>}
      {error && <div role="alert" className="rounded-lg border border-red-300 bg-red-50 p-4 text-sm text-red-800">
        <p className="flex items-center gap-2 font-semibold"><AlertCircle size={18} />系統狀態尚未載入，請按「重新檢查」。</p>
        <p className="mt-1">請確認 PDM 服務可連線；您仍可前往圖檔中心，已儲存的圖檔不會因此刪除。</p>
      </div>}
      {!loading && !error && status && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Database className="w-5 h-5 text-accent" />
                <span className="font-medium">資料庫連線</span>
              </div>
              <div className="text-xl font-semibold">{!status.isDatabaseConfigured ? '尚未設定' : database?.canConnect ? '連線檢測通過' : database ? '目前無法連線' : '已設定，連線待確認'}</div>
              <p className="mt-2 text-sm text-gray-600">{database?.canConnect ? '本次已實際測試資料庫連線。' : '可按「重新檢查」再測；持續失敗請檢查資料庫服務。'}</p>
              <div className="text-sm mt-2 text-gray-500">Provider: {status?.databaseProvider}</div>
            </Card>

            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Server className="w-5 h-5 text-accent" />
                <span className="font-medium">本機儲存 (Vault)</span>
              </div>
              <div className="text-xl font-semibold">{status.isLocalStorageConfigured ? '儲存路徑已設定' : '尚未設定'}</div>
              <p className="mt-2 text-sm text-gray-600">此狀態不代表圖檔可讀寫。</p>
              <details className="mt-2 text-sm text-gray-600"><summary className="cursor-pointer">檢視儲存路徑</summary><p className="mt-2 break-all">{status.localStorageVaultPath || '未設定'}</p></details>
            </Card>

            <Card className="p-6 flex flex-col justify-between">
              <div className="flex items-center space-x-3 text-gray-500 mb-2">
                <Server className="w-5 h-5 text-accent" />
                <span className="font-medium">SolidWorks 解析</span>
              </div>
              <div className="text-xl font-semibold">{status.isSolidWorksDocumentManagerConfigured ? '解析金鑰已設定' : '尚未設定金鑰'}</div>
              <p className="mt-2 text-sm text-gray-600">實際解析結果請在匯入時確認。</p>
            </Card>
          </div>

        </>
      )}
    </div>
  );
}
