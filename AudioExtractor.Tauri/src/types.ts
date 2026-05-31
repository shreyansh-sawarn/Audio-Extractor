export interface QueueItem {
  id: string;
  file?: File; // Web File reference
  fileName: string;
  fileSize: number;
  fileExtension: string;
  containerDirectory: string;
  filePath: string;
  startTime: string;
  endTime: string;
  totalDuration: string;
  totalDurationSeconds: number;
  isTimeRangeValid: boolean;
  timeValidationError: string;
  status: 'Queued' | 'Converting' | 'Done' | 'Failed' | 'Cancelled';
  errorMessage: string | null;
  progress: number;
  outputPath: string | null;
}

export interface AppConfig {
  format: string;
  bitrate: string;
  isMono: boolean;
  normalize: boolean;
  parallelTasks: number;
  filenameTemplate: string;
  postAction: string;
  targetFolderPath: string;
  isDarkMode: boolean;
}

/// Canonical list of supported input container formats — mirrors MediaConverter.SupportedExtensions in Core.
export const SUPPORTED_EXTENSIONS = ['webm', 'mp4', 'mkv', 'mov', 'avi', 'm4v'] as const;
