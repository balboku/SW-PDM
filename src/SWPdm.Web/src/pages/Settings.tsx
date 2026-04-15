import React, { useState, useEffect } from 'react';
import { Card, Button } from '../components/ui';
import { Settings as SettingsIcon, Save } from 'lucide-react';
import { getNumberingRules, updateNumberingRule } from '../lib/api';

export default function Settings() {
  const [partPattern, setPartPattern] = useState('PRT-{YYMM}-{SEQ:4}');
  const [assemblyPattern, setAssemblyPattern] = useState('ASM-{YYMM}-{SEQ:4}');
  const [drawingPattern, setDrawingPattern] = useState('DRW-{YYMM}-{SEQ:4}');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    loadRules();
  }, []);

  const loadRules = async () => {
    try {
      const rules = await getNumberingRules();
      if (Array.isArray(rules)) {
        rules.forEach(r => {
          if (r.documentType === 'Part') setPartPattern(r.pattern);
          if (r.documentType === 'Assembly') setAssemblyPattern(r.pattern);
          if (r.documentType === 'Drawing') setDrawingPattern(r.pattern);
        });
      }
    } catch (err) {
      console.error('Failed to load rules', err);
    }
  };

  const saveRules = async () => {
    setSaving(true);
    setMessage('');
    try {
      await updateNumberingRule('Part', partPattern);
      await updateNumberingRule('Assembly', assemblyPattern);
      await updateNumberingRule('Drawing', drawingPattern);
      setMessage('設定已成功儲存');
    } catch (err) {
      console.error(err);
      setMessage('儲存失敗');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight text-[#171717] flex items-center">
          <SettingsIcon className="mr-3 text-gray-500" />
          系統設定 (Settings)
        </h1>
        <p className="mt-2 text-[#404040]">維護自動派發圖號的編碼規則。</p>
      </div>

      <Card className="p-8 space-y-6">
        <div>
          <h2 className="text-xl font-medium mb-4">編碼規則 (Numbering Rules)</h2>
          <p className="text-sm text-gray-500 mb-6">
            可用變數：<code>{`{YYMM}`}</code> (西元年後兩碼與月份)、<code>{`{SEQ:N}`}</code> (不足 N 位補零的流水號)。<br/>
            例如：<code>PRT-{`{YYMM}`}-{`{SEQ:4}`}</code> 將產生 <code>PRT-2604-0001</code>。
          </p>

          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">零件 (Part) 規則</label>
              <input 
                type="text" 
                value={partPattern}
                onChange={(e) => setPartPattern(e.target.value)}
                className="w-full sm:w-1/2 p-2 border border-gray-300 rounded-md focus:ring-accent focus:border-accent"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">組合件 (Assembly) 規則</label>
              <input 
                type="text" 
                value={assemblyPattern}
                onChange={(e) => setAssemblyPattern(e.target.value)}
                className="w-full sm:w-1/2 p-2 border border-gray-300 rounded-md focus:ring-accent focus:border-accent"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">工程圖 (Drawing) 規則</label>
              <input 
                type="text" 
                value={drawingPattern}
                onChange={(e) => setDrawingPattern(e.target.value)}
                className="w-full sm:w-1/2 p-2 border border-gray-300 rounded-md focus:ring-accent focus:border-accent"
              />
            </div>
          </div>
        </div>

        <div className="pt-4 flex items-center space-x-4">
          <Button onClick={saveRules} disabled={saving}>
            <Save className="w-4 h-4 mr-2" />
            {saving ? '儲存中...' : '儲存設定'}
          </Button>
          {message && (
            <span className={`text-sm ${message.includes('失敗') ? 'text-red-500' : 'text-green-600'}`}>
              {message}
            </span>
          )}
        </div>
      </Card>
    </div>
  );
}
