import React, { ReactNode, useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

/**
 * 共用外觀：Minimal 單欄式 Layout + Sidebar
 */
export const Layout = ({
  children,
  sidebar,
  fullWidth = false
}: {
  children: ReactNode,
  sidebar: ReactNode,
  fullWidth?: boolean
}) => {
  return (
    <div className="flex h-screen flex-col bg-[#F9FAFB] text-[#171717] font-sans antialiased md:flex-row">
      {/* Sidebar */}
      <aside className="w-full flex-shrink-0 border-b border-[#E5E7EB] bg-white flex flex-col md:h-full md:w-64 md:border-b-0 md:border-r">
        {sidebar}
      </aside>

      {/* Main Content Area */}
      <main className="min-h-0 min-w-0 flex-1 flex flex-col overflow-hidden">
        <div className="flex-1 overflow-y-auto no-scrollbar p-4 md:p-8">
          <div className={fullWidth ? 'w-full' : 'max-w-7xl mx-auto'}>
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
  const base = "inline-flex items-center justify-center px-4 py-2 text-sm font-medium rounded-md transition-colors duration-200 cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed";
  
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

type OverlayProps = { isOpen: boolean; onClose: () => void; title: string; children: ReactNode; contentKey?: number };
const overlayStack: string[] = [];

const Overlay = ({ isOpen, onClose, title, children, contentKey, drawer = false }: OverlayProps & { drawer?: boolean }) => {
  const id = useId();
  const panel = useRef<HTMLDivElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const close = useRef(onClose);
  close.current = onClose;
  const [smallScreen, setSmallScreen] = useState(() => window.matchMedia('(max-width: 1023px)').matches);
  const isModal = !drawer || smallScreen;
  const modalRef = useRef(isModal);
  modalRef.current = isModal;

  useEffect(() => {
    const media = window.matchMedia('(max-width: 1023px)');
    const update = () => setSmallScreen(media.matches);
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);

  useEffect(() => {
    if (!isOpen) return;
    const previous = document.activeElement as HTMLElement | null;
    overlayStack.push(id);
    const focusable = () => Array.from(panel.current?.querySelectorAll<HTMLElement>(
      'button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), summary, [tabindex="0"]'
    ) ?? []).filter(element => element.getClientRects().length > 0);
    const frame = requestAnimationFrame(() => focusable()[0]?.focus());
    const observer = new MutationObserver(() => {
      if (modalRef.current && overlayStack[overlayStack.length - 1] === id && !panel.current?.contains(document.activeElement)) {
        (focusable()[0] ?? panel.current)?.focus();
      }
    });
    if (panel.current) observer.observe(panel.current, { subtree: true, childList: true, attributes: true, attributeFilter: ['disabled'] });
    const keydown = (event: KeyboardEvent) => {
      if (overlayStack[overlayStack.length - 1] !== id) return;
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopImmediatePropagation();
        close.current();
      }
      if (event.key === 'Tab' && modalRef.current) {
        const items = focusable();
        const first = items[0];
        const last = items[items.length - 1];
        if (!first) { event.preventDefault(); panel.current?.focus(); }
        else if (event.shiftKey && (document.activeElement === first || !panel.current?.contains(document.activeElement))) {
          event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !panel.current?.contains(document.activeElement))) {
          event.preventDefault(); first.focus();
        }
      }
    };
    const focusin = (event: FocusEvent) => {
      if (modalRef.current && overlayStack[overlayStack.length - 1] === id && !panel.current?.contains(event.target as Node)) {
        (focusable()[0] ?? panel.current)?.focus();
      }
    };
    const outside = (event: PointerEvent) => {
      if (drawer && !modalRef.current && overlayStack[overlayStack.length - 1] === id && !panel.current?.contains(event.target as Node)) {
        // Selecting another row switches the open detail without a close/reopen step.
        if (!(event.target as HTMLElement).closest('[data-document-row]')) close.current();
      }
    };
    document.addEventListener('keydown', keydown, true);
    document.addEventListener('focusin', focusin);
    document.addEventListener('pointerdown', outside);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
      overlayStack.splice(overlayStack.indexOf(id), 1);
      document.removeEventListener('keydown', keydown, true);
      document.removeEventListener('focusin', focusin);
      document.removeEventListener('pointerdown', outside);
      if (previous?.isConnected) previous.focus({ preventScroll: true });
      else document.querySelector<HTMLElement>('[role="dialog"] button')?.focus({ preventScroll: true });
    };
  }, [isOpen, id, drawer]);

  useEffect(() => { if (body.current) body.current.scrollTop = 0; }, [contentKey]);
  // Async states can remove/disable the focused control without a focusin event.
  useEffect(() => {
    if (isOpen && isModal && overlayStack[overlayStack.length - 1] === id && !panel.current?.contains(document.activeElement)) {
      panel.current?.querySelector<HTMLElement>('button:not(:disabled)')?.focus();
    }
  });

  if (!isOpen) return null;

  return createPortal(
    <div className={drawer
      ? 'pointer-events-none fixed inset-0 z-40'
      : 'fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 backdrop-blur-sm sm:p-4'}>
      {drawer && smallScreen && <div aria-hidden="true" className="pointer-events-auto absolute inset-0 bg-black/50" onClick={onClose} />}
      <div
        ref={panel}
        tabIndex={-1}
        className={drawer
          ? 'pointer-events-auto absolute inset-y-0 right-0 flex w-full flex-col overflow-hidden border-l border-gray-700 bg-[#121212] text-gray-200 shadow-2xl lg:inset-y-3 lg:right-3 lg:w-[min(38rem,calc(100vw-38rem))] lg:rounded-xl'
          : 'flex max-h-[calc(100dvh-1.5rem)] w-full max-w-lg flex-col overflow-hidden rounded-xl border border-gray-700 bg-[#121212] text-gray-200 shadow-2xl'}
        role="dialog"
        aria-modal={isModal || undefined}
        aria-labelledby={id}
      >
        <div className="flex shrink-0 items-center justify-between gap-3 border-b border-gray-700 px-4 py-3">
          <h3 id={id} className="text-lg font-semibold text-white">{title}</h3>
          <button type="button" onClick={onClose} aria-label={drawer ? '關閉明細並返回清單' : '關閉視窗'} className="flex min-h-[44px] shrink-0 items-center gap-2 rounded px-2 text-sm text-gray-300 hover:bg-gray-800 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-400">
            {drawer && <span>返回清單</span>}
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div ref={body} className={`min-h-0 overflow-y-auto overscroll-contain [scrollbar-gutter:stable] ${drawer ? '' : 'p-4 sm:p-6'}`}>
          {children}
        </div>
      </div>
    </div>, document.body
  );
};

export const Modal = (props: OverlayProps) => <Overlay {...props} />;
export const Drawer = (props: OverlayProps) => <Overlay {...props} drawer />;
