using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AudioExtractor
{
    class MediaConversionEventArgs
    {
        public string FileName { get; private set; }
        public bool Successful { get; private set; }

        public MediaConversionEventArgs(string fileName)
        {
            FileName = fileName;
            Successful = false;
        }

        public MediaConversionEventArgs(string fileName, bool successful)
        {
            FileName = fileName;
            Successful = successful;
        }
    }

    public class ConversionResult
    {
        public bool Successful { get; set; }
    }

    class MediaConverter : DependencyObject
    {
        public delegate void ConversionStartedEvent(object sender, MediaConversionEventArgs e);
        public delegate void ConversionFinishedEvent(object sender, MediaConversionEventArgs e);

        public event ConversionStartedEvent ConversionStarted;
        public event ConversionFinishedEvent ConversionFinished;

        private readonly string m_targetDirectory;
        private readonly string m_ffmpegPath;

        public MediaConverter(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
                throw new ArgumentException("Target directory doesn't exist");

            m_targetDirectory = targetDirectory;

            m_ffmpegPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), 
                "3rdparty", 
                "ffmpeg.exe"
            );
        }

        public async Task<ConversionResult[]> ConvertFiles(string[] fileList)
        {
            var tasks = fileList.Select((path) => ConvertFile(path));
            return await Task.WhenAll(tasks);
        }

        public async Task<ConversionResult> ConvertFile(string path)
        {
            var task = new Task<ConversionResult>(() =>
            {
                var targetPath = Path.Combine(m_targetDirectory, Path.GetFileNameWithoutExtension(path));
                var arguments = $"-i \"{path}\" -vn -c:a libmp3lame -b:a 256k \"{targetPath}.mp3\"";

                var startInfo = new ProcessStartInfo()
                {
                    FileName = m_ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var ffmpeg = new Process()
                {
                    StartInfo = startInfo
                };

                try
                {
                    ffmpeg.Start();
                }
                catch (Exception)
                {
                    return new ConversionResult();
                }

                var result = new ConversionResult()
                {
                    Successful = true
                };

                ffmpeg.WaitForExit();

                return result;
            });

            task.Start();

            return await task;
        }
    }
}
