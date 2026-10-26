import React from 'react';

export const DocumentIntegrityPanel: React.FC<{ data: any }> = ({ data }) => {
  if (!data) return null;
  const unresolved = data.references.filter((r: any) => r.needsResolution);
  return <section className="mt-4 space-y-3 rounded-lg border border-gray-700 p-3 text-xs text-gray-200" aria-label="參照與屬性檢查">
    <h3 className="font-semibold text-white">參照與屬性檢查</h3>
    <p className="leading-5 text-gray-400">依目前版本的資料庫快照；未重新檢查實體 CAD 或設計依賴。完整套件會另外檢查副本。</p>
    {data.currentVersionMissing ? <p className="text-red-200">缺少目前版本，請先確認入庫紀錄；現在無法建立完整套件。</p>
      : unresolved.length ? <p className="text-red-200">請先確認下列 {unresolved.length} 處參照，再由出庫人於 SolidWorks 修正並入庫；不會自動修復歷史 BOM。</p>
      : <p className="text-gray-300">已儲存參照沒有待解析項目；這不代表實體 CAD 已通過完整性檢查。</p>}
    {unresolved.length > 0 && <details><summary className="cursor-pointer py-2 font-medium text-red-200">查看 {unresolved.length} 處待處理參照與下一步</summary>
    {unresolved.map((r: any) => <div key={r.bomOccurrenceId} className="mt-2 break-words rounded border border-red-900/60 bg-red-950/20 p-2">
      <p className="font-medium text-red-100">{r.fileName || '未記錄來源檔名'}</p>
      <p className="mt-1 text-red-200">{r.referenceStatus === 'Broken' ? '參照已標記為待確認，請人工核對正確文件／版本。' : '尚未確認對應版本，請補齊來源並檢查 CAD 參照。'}</p>
      {r.isSuppressed && <p className="mt-1 text-amber-200">抑制件仍需確認，不能因未顯示就略過。</p>}
      <p className="mt-1 text-gray-400">父組態 {r.parentConfigurationName || '未記錄'}／子組態 {r.childConfigurationName || '未記錄'}</p>
    </div>)}</details>}
    <p className="text-gray-400">虛擬件 {data.virtualCount} 處（保留在父檔，不當成缺檔） · 抑制件 {data.suppressedCount} 處</p>
    <details>
      <summary className="cursor-pointer rounded py-2 font-medium text-gray-100 focus-visible:ring-2 focus-visible:ring-blue-400">檢視目前版本屬性與組態（{data.properties.length} 筆）</summary>
      {data.properties.length === 0 ? <p className="mt-2">尚未儲存屬性；請在 CAD 填寫並重新入庫。</p> : <dl className="mt-2 space-y-2">
        {data.properties.map((p: any, index: number) => <div key={index} className="break-words rounded border border-gray-800 p-2">
          <dt className="font-semibold text-gray-200">{p.propertyName} <span className="font-normal text-gray-400">· {p.configurationName || '文件層'}</span></dt>
          <dd className="mt-1 whitespace-pre-wrap break-all text-gray-300">{p.propertyValue || '未填寫'}{!p.isResolved && '（原生值未解析，請確認）'}</dd>
        </div>)}
      </dl>}
    </details>
  </section>;
};
