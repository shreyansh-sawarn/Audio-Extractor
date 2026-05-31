import React from 'react';

interface DragDropOverlayProps {
  onDragEnter: (e: React.DragEvent) => void;
  onDragLeave: (e: React.DragEvent) => void;
  onDragOver: (e: React.DragEvent) => void;
  onDrop: (e: React.DragEvent) => void;
}

export default function DragDropOverlay({ onDragEnter, onDragLeave, onDragOver, onDrop }: DragDropOverlayProps) {
  return (
    <div
      className="drag-overlay fade-in"
      onDragEnter={onDragEnter}
      onDragLeave={onDragLeave}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <div className="drag-border">
        <span className="drag-icon">📂</span>
        <span className="drag-title">Drop Video Files to Import</span>
        <span className="drag-subtitle">Supports webm, mp4, mkv, mov, avi, m4v</span>
      </div>
    </div>
  );
}
