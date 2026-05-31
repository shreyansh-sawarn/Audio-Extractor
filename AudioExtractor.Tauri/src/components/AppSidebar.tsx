import type { AppConfig } from '../types';

interface AppSidebarProps {
  config: AppConfig;
  isConverting: boolean;
  isTauri: boolean;
  onConfigChange: (patch: Partial<AppConfig>) => void;
}

export default function AppSidebar({ config, isConverting, isTauri, onConfigChange }: AppSidebarProps) {
  return (
    <aside className="app-sidebar">
      <div className="sidebar-title">Audio Config</div>

      <div className="form-group">
        <label>Format</label>
        <select
          disabled={isConverting}
          value={config.format}
          onChange={(e) => onConfigChange({ format: e.target.value })}
        >
          <option value="MP3">MP3</option>
          <option value="M4A">M4A</option>
          <option value="WAV">WAV</option>
          <option value="FLAC">FLAC</option>
        </select>
      </div>

      <div className="form-group">
        <label>Bitrate</label>
        <select
          disabled={isConverting}
          value={config.bitrate}
          onChange={(e) => onConfigChange({ bitrate: e.target.value })}
        >
          <option value="128k">128k</option>
          <option value="256k">256k</option>
          <option value="320k">320k</option>
        </select>
      </div>

      <label className="checkbox-group">
        <input
          type="checkbox"
          disabled={isConverting}
          checked={config.isMono}
          onChange={(e) => onConfigChange({ isMono: e.target.checked })}
        />
        Force Mono
      </label>

      <label className="checkbox-group">
        <input
          type="checkbox"
          disabled={isConverting}
          checked={config.normalize}
          onChange={(e) => onConfigChange({ normalize: e.target.checked })}
        />
        Normalize Volume
      </label>

      <div className="form-group">
        <label>Parallel Tasks</label>
        <select
          disabled={isConverting || !isTauri}
          value={isTauri ? config.parallelTasks : 1}
          onChange={(e) => onConfigChange({ parallelTasks: parseInt(e.target.value, 10) })}
        >
          <option value="1">1</option>
          <option value="2">2</option>
          <option value="3">3</option>
          <option value="4">4</option>
        </select>
        {!isTauri && <span className="input-info-hint">Parallelism is limited to 1 in Web browsers</span>}
      </div>

      <div style={{ height: '1px', backgroundColor: 'var(--border-color)', margin: '4px 0' }} />

      <div className="sidebar-title">Filename Template</div>
      <div className="form-group">
        <input
          type="text"
          disabled={isConverting}
          value={config.filenameTemplate}
          onChange={(e) => onConfigChange({ filenameTemplate: e.target.value })}
        />
        <span className="template-help">Tags: [Name] [Format] [Bitrate] [Date]</span>
      </div>

      <div style={{ height: '1px', backgroundColor: 'var(--border-color)', margin: '4px 0' }} />

      <div className="sidebar-title">Post Operation</div>
      <div className="form-group">
        <label>When complete</label>
        <select
          disabled={isConverting}
          value={config.postAction}
          onChange={(e) => onConfigChange({ postAction: e.target.value })}
        >
          <option value="None">None</option>
          <option value="Play Sound">Play Sound</option>
          {isTauri && <option value="Open Folder">Open Folder</option>}
        </select>
      </div>
    </aside>
  );
}
