import type { QueueItem } from '../types';
import { SUPPORTED_EXTENSIONS } from '../types';

// Format seconds to hh:mm:ss
export function formatSeconds(totalSeconds: number): string {
  const hrs = Math.floor(totalSeconds / 3600);
  const mins = Math.floor((totalSeconds % 3600) / 60);
  const secs = Math.floor(totalSeconds % 60);
  return `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

// Parse hh:mm:ss, mm:ss, or plain seconds into a number. Returns null on bad input.
export function tryParseTime(input: string): number | null {
  const trimmed = input.trim();
  if (!trimmed) return null;

  if (!isNaN(Number(trimmed))) {
    const val = Number(trimmed);
    return val >= 0 ? val : null;
  }

  const parts = trimmed.split(':');
  if (parts.length === 2) {
    const min = parseInt(parts[0], 10);
    const sec = parseFloat(parts[1]);
    if (min >= 0 && sec >= 0 && sec < 60) {
      return min * 60 + sec;
    }
  } else if (parts.length === 3) {
    const hr = parseInt(parts[0], 10);
    const min = parseInt(parts[1], 10);
    const sec = parseFloat(parts[2]);
    if (hr >= 0 && min >= 0 && min < 60 && sec >= 0 && sec < 60) {
      return hr * 3600 + min * 60 + sec;
    }
  }

  return null;
}

// Live validation of a start/end trim pair against the total media duration.
export function validateItemTime(
  item: QueueItem,
  start: string,
  end: string,
  totalSec: number
): Partial<QueueItem> {
  const startSec = tryParseTime(start);
  if (start && startSec === null) {
    return {
      isTimeRangeValid: false,
      timeValidationError: 'Start time format invalid (use hh:mm:ss, mm:ss, or seconds).',
    };
  }

  const endSec = tryParseTime(end);
  if (end && endSec === null) {
    return {
      isTimeRangeValid: false,
      timeValidationError: 'End time format invalid (use hh:mm:ss, mm:ss, or seconds).',
    };
  }

  const finalStart = startSec || 0;
  const finalEnd = endSec !== null ? endSec : totalSec >= 0 ? totalSec : Infinity;

  if (finalStart > finalEnd) {
    return {
      isTimeRangeValid: false,
      timeValidationError: 'Start time cannot be after End time.',
    };
  }

  if (totalSec >= 0) {
    if (finalStart > totalSec) {
      return {
        isTimeRangeValid: false,
        timeValidationError: `Start time exceeds media length (${item.totalDuration}).`,
      };
    }
    if (finalEnd > totalSec && endSec !== null) {
      return {
        isTimeRangeValid: false,
        timeValidationError: `End time exceeds media length (${item.totalDuration}).`,
      };
    }
  }

  return {
    isTimeRangeValid: true,
    timeValidationError: '',
  };
}

// Check whether a file extension is in the supported list.
export function isSupportedExtension(ext: string): boolean {
  return (SUPPORTED_EXTENSIONS as readonly string[]).includes(ext.toLowerCase());
}

// Retrieve duration of a browser File using an HTML5 video element.
export function fetchWebVideoDuration(file: File): Promise<number> {
  return new Promise((resolve) => {
    const video = document.createElement('video');
    video.preload = 'metadata';
    video.onloadedmetadata = () => { resolve(video.duration); };
    video.onerror = () => { resolve(0); };
    video.src = URL.createObjectURL(file);
  });
}
