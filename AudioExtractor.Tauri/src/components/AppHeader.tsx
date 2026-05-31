
interface AppHeaderProps {
  isDarkMode: boolean;
  onToggleDarkMode: () => void;
}

export default function AppHeader({ isDarkMode, onToggleDarkMode }: AppHeaderProps) {
  return (
    <header className="app-header">
      <div className="logo-section">
        <span className="logo-text">Audio Extractor &amp; Converter</span>
        <span className="version-badge">v2.0</span>
      </div>
      <div className="theme-toggle-section">
        <span>Dark Mode</span>
        <label className="theme-switch">
          <input
            type="checkbox"
            checked={isDarkMode}
            onChange={onToggleDarkMode}
          />
          <span className="slider"></span>
        </label>
      </div>
    </header>
  );
}
