import { useEffect } from 'react';
import { listen } from '@tauri-apps/api/event';
import { getCurrentWindow } from '@tauri-apps/api/window';
import { invoke } from '@tauri-apps/api/core';
import type { QueueItem } from '../types';
import { isSupportedExtension } from '../lib/timeUtils';

interface TauriListenersOptions {
  isTauri: boolean;
  isConverting: boolean;
  items: QueueItem[];
  setItems: React.Dispatch<React.SetStateAction<QueueItem[]>>;
  setIsDragOver: React.Dispatch<React.SetStateAction<boolean>>;
  setIsFfmpegAvailable: React.Dispatch<React.SetStateAction<boolean>>;
  setStatusMessage: React.Dispatch<React.SetStateAction<string>>;
}

/**
 * Registers Tauri-specific event listeners (progress updates, native drag-drop).
 * Also performs the initial FFmpeg availability check.
 * All listeners are cleaned up on unmount.
 */
export function useTauriListeners({
  isTauri,
  isConverting,
  items,
  setItems,
  setIsDragOver,
  setIsFfmpegAvailable,
  setStatusMessage,
}: TauriListenersOptions) {
  useEffect(() => {
    if (!isTauri) return;

    // Check FFmpeg availability
    invoke<boolean>('check_ffmpeg').then(avail => {
      setIsFfmpegAvailable(avail);
      if (!avail) {
        setStatusMessage('Warning: FFmpeg is missing from your system PATH.');
      }
    });

    // 1. Progress updates
    const unlistenProgress = listen<{ id: string; progress: number }>('progress', (event) => {
      const { id, progress } = event.payload;
      setItems(prev => prev.map(item => item.id === id ? { ...item, progress } : item));
    });

    // 2. Native file drops and drag alerts
    const unlistenDrop = getCurrentWindow().onDragDropEvent(async (event) => {
      if (event.payload.type === 'enter') {
        setIsDragOver(true);
      } else if (event.payload.type === 'leave') {
        setIsDragOver(false);
      } else if (event.payload.type === 'drop') {
        setIsDragOver(false);
        if (!isConverting) {
          await addLocalPathsToQueue(event.payload.paths, items, setItems, setStatusMessage);
        }
      }
    });

    return () => {
      unlistenProgress.then(un => un());
      unlistenDrop.then(un => un());
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isTauri]);
}

/** Adds native file paths (from a Tauri drag-drop or file picker) to the queue. */
export async function addLocalPathsToQueue(
  paths: string[],
  existingItems: QueueItem[],
  setItems: React.Dispatch<React.SetStateAction<QueueItem[]>>,
  setStatusMessage: React.Dispatch<React.SetStateAction<string>>,
) {
  const newItems: QueueItem[] = [];
  const duplicates: string[] = [];

  for (const path of paths) {
    const ext = path.split('.').pop()?.toLowerCase() || '';
    if (!isSupportedExtension(ext)) continue;

    if (existingItems.some(item => item.filePath === path)) {
      duplicates.push(path);
      continue;
    }

    try {
      const details = await invoke<{
        fileName: string;
        fileSize: number;
        fileExtension: string;
        totalDuration: string;
        totalDurationSeconds: number;
      }>('get_file_details', { filePath: path });

      const id = Math.random().toString(36).substring(2, 9);
      newItems.push({
        id,
        fileName: details.fileName,
        fileSize: details.fileSize,
        fileExtension: details.fileExtension,
        containerDirectory: path.substring(0, path.lastIndexOf('\\')) || 'Local',
        filePath: path,
        startTime: '00:00:00',
        endTime: details.totalDuration,
        totalDuration: details.totalDuration,
        totalDurationSeconds: details.totalDurationSeconds,
        isTimeRangeValid: true,
        timeValidationError: '',
        status: 'Queued',
        errorMessage: null,
        progress: 0,
        outputPath: null,
      });
    } catch (err) {
      console.error('Failed to query file details natively:', err);
    }
  }

  if (newItems.length > 0) {
    setItems(prev => [...prev, ...newItems]);
    setStatusMessage(`Added ${newItems.length} file(s).`);
  } else if (duplicates.length > 0) {
    setStatusMessage('Files already present in list.');
  }
}
