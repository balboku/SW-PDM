import React, { useState, useEffect } from 'react';
import { ChevronRight, ChevronDown, File, Archive, Loader2 } from 'lucide-react';
import { getVersionChildren } from '../lib/api';

interface BomNode {
  childVersionId: number;
  childOriginalFileName: string;
  quantity: number;
  childDocumentType?: string; // 根據實際 API 回傳欄位
}

interface BomTreeViewProps {
  rootVersionId: number;
}

const BomTreeNode: React.FC<{ 
  node: BomNode, 
  depth: number 
}> = ({ node, depth }) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const [children, setChildren] = useState<BomNode[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasLoaded, setHasLoaded] = useState(false);

  // 判斷是否為組合件 (透過附檔名或 API 欄位)
  const isAssembly = node.childOriginalFileName?.toLowerCase().endsWith('.sldasm') || node.childDocumentType === 'Assembly';

  const toggleExpand = async () => {
    if (!isAssembly) return;

    if (!isExpanded && !hasLoaded) {
      setIsLoading(true);
      try {
        const data = await getVersionChildren(node.childVersionId);
        setChildren(data);
        setHasLoaded(true);
      } catch (error) {
        console.error('Failed to load children', error);
      } finally {
        setIsLoading(false);
      }
    }
    
    setIsExpanded(!isExpanded);
  };

  return (
    <div>
      <div 
        className={`flex items-center py-1.5 hover:bg-gray-800 transition-colors cursor-pointer text-sm`}
        style={{ paddingLeft: `${depth * 20 + 8}px` }}
        onClick={toggleExpand}
      >
        <div className="w-5 flex justify-center items-center mr-1 text-gray-400">
          {isAssembly ? (
            isLoading ? (
              <Loader2 size={14} className="animate-spin" />
            ) : isExpanded ? (
              <ChevronDown size={14} />
            ) : (
              <ChevronRight size={14} />
            )
          ) : (
            <span className="w-3.5" /> // Empty space alignment
          )}
        </div>
        
        <div className="mr-2 text-gray-400">
          {isAssembly ? <Archive size={16} className="text-yellow-500" /> : <File size={16} className="text-blue-400" />}
        </div>
        
        <span className="flex-1 font-medium text-gray-200 truncate" title={node.childOriginalFileName}>
          {node.childOriginalFileName}
        </span>
        
        {node.quantity > 0 && (
          <span className="text-gray-500 bg-gray-800 px-2 py-0.5 rounded text-xs ml-3 font-mono">
            Qty: {node.quantity}
          </span>
        )}
      </div>

      {isExpanded && children.length > 0 && (
        <div className="flex flex-col relative before:absolute before:left-3 before:top-0 before:bottom-0 before:w-px before:bg-gray-700">
          {children.map((child, idx) => (
            <BomTreeNode 
              key={`${child.childVersionId}-${idx}`} 
              node={child} 
              depth={depth + 1} 
            />
          ))}
        </div>
      )}
      
      {isExpanded && hasLoaded && children.length === 0 && (
        <div style={{ paddingLeft: `${(depth + 1) * 20 + 8}px` }} className="text-xs text-gray-500 py-1 italic">
          No items found
        </div>
      )}
    </div>
  );
};

export const BomTreeView: React.FC<BomTreeViewProps> = ({ rootVersionId }) => {
  const [rootChildren, setRootChildren] = useState<BomNode[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const fetchRootChildren = async () => {
      setIsLoading(true);
      try {
        const data = await getVersionChildren(rootVersionId);
        setRootChildren(data);
      } catch (error) {
        console.error('Failed to load root children', error);
      } finally {
        setIsLoading(false);
      }
    };

    if (rootVersionId) {
      fetchRootChildren();
    }
  }, [rootVersionId]);

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-gray-400">
        <Loader2 size={24} className="animate-spin mb-3 text-[#D4AF37]" />
        <span className="text-sm">載入 BOM 結構中...</span>
      </div>
    );
  }

  if (rootChildren.length === 0) {
    return <div className="p-4 text-sm text-gray-500 text-center bg-gray-800/20 rounded-lg border border-gray-800">無子零件或尚未解析 BOM。</div>;
  }

  return (
    <div className="border border-gray-800 rounded-lg bg-[#1a1a1a] overflow-hidden">
      <div className="px-4 py-2 border-b border-gray-800 bg-gray-800/30 text-xs font-semibold text-gray-400 tracking-wider">
        Structure
      </div>
      <div className="py-2">
        {rootChildren.map((child, idx) => (
          <BomTreeNode 
            key={`${child.childVersionId}-${idx}`} 
            node={child} 
            depth={0} 
          />
        ))}
      </div>
    </div>
  );
};
