import React from 'react';
import type { QueueItem, AppConfig } from '../types';

interface AppFooterProps {
  config: AppConfig;
  items: QueueItem[];
  isConverting: boolean;
  isTauri: boolean;
  statusMessage: string;
  audioRef: React.MutableRefObject<HTMLAudioElement | null>;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  onSetItems: React.Dispatch<React.SetStateAction<QueueItem[]>>;
  onSetPlayingItemId: (id: string | null) => void;
  onBrowseFolder: () => void;
  onAddFiles: () => void;
  onStartConversions: () => void;
  onStopConversion: () => void;
  onFilesSelected: (files: FileList) => void;
}

export default function AppFooter({
  config,
  items,
  isConverting,
  isTauri,
  statusMessage,
  audioRef,
  fileInputRef,
  onSetItems,
  onSetPlayingItemId,
  onBrowseFolder,
  onAddFiles,
  onStartConversions,
  onStopConversion,
  onFilesSelected,
}: AppFooterProps) {
  const handleClear = () => {
    if (audioRef.current) {
      audioRef.current.pause();
    }
    onSetPlayingItemId(null);
    onSetItems([]);
  };

  const handleClearCompleted = () => {
    onSetItems(prev => prev.filter(i => i.status !== 'Done'));
  };

  return (
    <footer className="app-controls">
      <div className="info-section">
        {isTauri ? (
          <>
            <span>Target folder:</span>
            <span className="target-folder-text">{config.targetFolderPath}</span>
          </>
        ) : (
          <span>Target folder: <b>Browser Downloads</b> (Files download directly client-side)</span>
        )}
        <span style={{ fontSize: '11px', marginTop: '2px' }}>{statusMessage}</span>
      </div>

      <div className="button-section">
        {isTauri && (
          <button className="btn" onClick={onBrowseFolder}>Browse</button>
        )}

        <input
          type="file"
          multiple
          ref={fileInputRef}
          style={{ display: 'none' }}
          onChange={(e) => e.target.files && onFilesSelected(e.target.files)}
        />
        <button disabled={isConverting} className="btn" onClick={onAddFiles}>
          Add Files
        </button>

        <button
          disabled={isConverting || items.length === 0}
          className="btn"
          onClick={handleClear}
        >
          Clear
        </button>

        <button
          disabled={isConverting || !items.some(i => i.status === 'Done')}
          className="btn"
          onClick={handleClearCompleted}
        >
          Clear Completed
        </button>

        {isConverting ? (
          <button className="btn btn-primary btn-danger" onClick={onStopConversion}>Stop</button>
        ) : (
          <button className="btn btn-primary" disabled={items.length === 0} onClick={onStartConversions}>Start</button>
        )}
      </div>
    </footer>
  );
}
