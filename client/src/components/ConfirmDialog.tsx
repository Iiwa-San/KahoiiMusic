import React, { useEffect } from "react";
import { AlertTriangle, HelpCircle, X } from "lucide-react";

export interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: "danger" | "primary";
  showCancel?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  isOpen,
  title,
  message,
  confirmText = "Đồng ý",
  cancelText = "Hủy",
  variant = "danger",
  showCancel = true,
  onConfirm,
  onCancel,
}) => {
  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onCancel();
      } else if (e.key === "Enter") {
        e.preventDefault();
        onConfirm();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isOpen, onConfirm, onCancel]);

  if (!isOpen) return null;

  const isDanger = variant === "danger";

  return (
    <div
      onClick={onCancel}
      className="fixed inset-0 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 z-[60]"
    >
      <div
        onClick={(e) => e.stopPropagation()}
        className={`figma-panel w-full max-w-[380px] flex flex-col p-5 shadow-2xl border ${
          isDanger ? "border-red-500/45" : "border-pink-500/45"
        }`}
      >
        <div className="flex items-center justify-between pb-3 border-b border-white/10">
          <div className="flex items-center gap-2.5">
            <div
              className={`w-8 h-8 rounded-xl flex items-center justify-center ${
                isDanger
                  ? "bg-red-500/20 text-red-400 border border-red-500/30"
                  : "bg-pink-500/20 text-pink-400 border border-pink-500/30"
              }`}
            >
              {isDanger ? (
                <AlertTriangle className="w-4 h-4" />
              ) : (
                <HelpCircle className="w-4 h-4" />
              )}
            </div>
            <h3 className="text-sm font-bold text-white">{title}</h3>
          </div>
          <button
            type="button"
            onClick={onCancel}
            className="p-1 rounded-lg text-white/50 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <p className="text-xs md:text-sm text-white/80 leading-relaxed py-4">
          {message}
        </p>

        <div className="flex items-center justify-end gap-2.5 pt-2 border-t border-white/10">
          {showCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="px-4 py-2 rounded-xl bg-white/5 hover:bg-white/15 border border-white/15 text-xs font-semibold text-white/80 hover:text-white transition-all cursor-pointer"
            >
              {cancelText}
            </button>
          )}
          <button
            type="button"
            onClick={onConfirm}
            className={`px-4 py-2 rounded-xl text-xs font-bold text-white shadow-lg transition-all cursor-pointer hover:opacity-95 active:scale-95 ${
              isDanger
                ? "bg-gradient-to-r from-red-500 to-rose-600 shadow-red-500/25"
                : "bg-gradient-to-r from-pink-500 to-purple-600 shadow-pink-500/25"
            }`}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
};
