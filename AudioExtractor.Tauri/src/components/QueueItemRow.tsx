import React from 'react';
import type { QueueItem, AppConfig } from '../types';

interface QueueItemRowProps {
  item: QueueItem;
  isConverting: boolean;
  isTauri: boolean;
  isPlaying: boolean;
  config: AppConfig;
  onRemove: (id: string) => void;
  onUpdateStartTime: (id: string, val: string) => void;
  onUpdateEndTime: (id: string, val: string) => void;
  onPlay: (item: QueueItem) => void;
  onOpenFolder: (item: QueueItem) => void;
  onDragStart: (e: React.DragEvent, id: string) => void;
  onDragEnd: () => void;
  onDragOver: (e: React.DragEvent, id: string) => void;
  onDrop: (e: React.DragEvent, id: string) => void;
}

export default function QueueItemRow({
  item,
  isConverting,
  isTauri,
  isPlaying,
  config,
  onRemove,
  onUpdateStartTime,
  onUpdateEndTime,
  onPlay,
  onOpenFolder,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDrop,
}: QueueItemRowProps) {
  const statusColor =
    item.status === 'Done' ? 'var(--success)' :
    item.status === 'Failed' ? 'var(--error)' :
    item.status === 'Converting' ? 'var(--accent)' : 'var(--text-primary)';

  return (
    <li
      className="queue-item"
      draggable={!isConverting ? 'true' : 'false'}
      onDragStart={(e) => onDragStart(e, item.id)}
      onDragEnd={onDragEnd}
      onDragOver={(e) => onDragOver(e, item.id)}
      onDrop={(e) => onDrop(e, item.id)}
    >
      <div className="ext-badge">{item.fileExtension}</div>

      <div className="item-details">
        <span className="item-name">{item.fileName}</span>
        <span className="item-meta">
          Size: {(item.fileSize / (1024 * 1024)).toFixed(2)} MB | Duration: {item.totalDuration}
        </span>
        <div className="trim-fields" draggable={false} onDragStart={e => e.preventDefault()}>
          <span>Trim Start:</span>
          <div className="tooltip-container">
            <input
              type="text"
              disabled={isConverting}
              className={item.isTimeRangeValid ? '' : 'input-invalid'}
              value={item.startTime}
              onChange={(e) => onUpdateStartTime(item.id, e.target.value)}
            />
            {!item.isTimeRangeValid && (
              <span className="tooltip-text">{item.timeValidationError}</span>
            )}
          </div>
          <span>End:</span>
          <div className="tooltip-container">
            <input
              type="text"
              disabled={isConverting}
              className={item.isTimeRangeValid ? '' : 'input-invalid'}
              value={item.endTime}
              onChange={(e) => onUpdateEndTime(item.id, e.target.value)}
            />
            {!item.isTimeRangeValid && (
              <span className="tooltip-text">{item.timeValidationError}</span>
            )}
          </div>
        </div>
      </div>

      <div className="item-status-panel">
        <span className="status-text" style={{ color: statusColor }}>{item.status}</span>

        {item.status === 'Converting' && (
          <div className="item-progress-bar">
            <div className="progress-fill" style={{ width: `${item.progress}%` }}></div>
            <span className="progress-percent">{item.progress}%</span>
          </div>
        )}

        {item.status === 'Done' && (
          <div className="row-buttons">
            <button className="row-btn" onClick={() => onPlay(item)}>
              {!isTauri && isPlaying ? 'Pause' : 'Play'}
            </button>
            {isTauri ? (
              <button className="row-btn row-btn-secondary" onClick={() => onOpenFolder(item)}>Open Folder</button>
            ) : (
              <a
                className="row-btn row-btn-secondary download-link"
                href={item.outputPath || '#'}
                download={`${item.fileName}.${config.format.toLowerCase()}`}
                style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', textDecoration: 'none' }}
              >
                Download
              </a>
            )}
          </div>
        )}

        {item.status === 'Failed' && item.errorMessage && (
          <span className="error-hint" title={item.errorMessage}>
            ⚠️ Info
          </span>
        )}
      </div>

      <button
        disabled={isConverting}
        className="remove-btn"
        onClick={() => onRemove(item.id)}
      >
        ✕
      </button>
    </li>
  );
}
