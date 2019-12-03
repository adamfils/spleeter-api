﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SpleeterAPI.Youtube
{
    public static class YoutubeHelper
    {
        private static string Youtube_User = Startup.Configuration["Youtube:User"];
        private static string Youtube_Pass = Startup.Configuration["Youtube:Pass"];
        private static string Output_Root = Startup.Configuration["Spleeter:OutputFolder"];
        private static string Cache_Root = Startup.Configuration["Spleeter:CacheFolder"];
        private static ConcurrentDictionary<string, YoutubeVideoInfo> _videoInfoCache = new ConcurrentDictionary<string, YoutubeVideoInfo>();

        public static Dictionary<string, string[]> FormatMapSub { get; } = new Dictionary<string, string[]>()
        {
            { "2stems", new [] { "accompaniment", "vocals" } },
            { "4stems", new [] { "bass", "drums", "other", "vocals" } },
            { "5stems", new [] { "bass", "drums", "other", "piano", "vocals" } }
        };
        public static Regex ValidVidRegex { get; } = new Regex(@"^[a-zA-Z0-9_-]{11}$");

        public static YoutubeVideoInfo GetVideoInfo(string vid)
        {
            if (_videoInfoCache.TryGetValue(vid, out YoutubeVideoInfo cachedInfo))
            {
                return cachedInfo;
            }
            var cmd = @$"youtube-dl -s --get-filename --get-duration --no-check-certificate ""https://youtu.be/{vid}""";
            var shellResult = ShellHelper.Execute(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl -s exited with code {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            var dataArray = shellResult.Output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            if (dataArray.Length < 2)
            {
                throw new Exception($"youtube-dl -s returned unformatted data {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            var info = new YoutubeVideoInfo()
            {
                Filename = dataArray[0].Trim(),
                Duration = dataArray[1]
            };
            if (info.Filename.Contains('.'))
            {
                info.Filename = ShellHelper.SanitizeFilename(info.Filename.Substring(0, info.Filename.LastIndexOf('.')));
            }
            var dur = info.Duration.Split(':');
            if (dur.Length == 1)
            {
                info.DurationSeconds = int.Parse(dur[0]);
            }
            else if (dur.Length == 2)
            {
                info.DurationSeconds = int.Parse(dur[0]) * 60 + int.Parse(dur[1]);
            }
            else if (dur.Length == 3)
            {
                info.DurationSeconds = int.Parse(dur[0]) * 3600 + int.Parse(dur[1]) * 60 + int.Parse(dur[2]);
            }
            _videoInfoCache[vid] = info;
            return info;
        }

        public static YoutubeAudioResponse DownloadAudio(string vid)
        {
            var fileName = GetAudioFilename(vid);
            var result = new YoutubeAudioResponse();
            if (File.Exists(fileName))
            {
                result.AudioFileFullPath = fileName;
                return result;
            }
            var userPassParams = string.IsNullOrWhiteSpace(Youtube_User) ? "" : @$"-u ""{Youtube_User}"" -p ""{Youtube_Pass}""";
            var cmd = @$"youtube-dl -f bestaudio --no-playlist {userPassParams} -o ""{fileName}"" --no-check-certificate ""https://youtu.be/{vid}""";
            var shellResult = ShellHelper.Execute(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl audio exited with code {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            if (!File.Exists(fileName))
            {
                throw new Exception($"Video filename {fileName} not found after youtube-dl");
            }
            result.AudioFileFullPath = fileName;
            return result;
        }

        public static string DownloadAudioMp3(string vid)
        {
            var filePathTemplate = $"{Output_Root}/yt/{vid}.%(ext)s";
            
            var cmd = $@"youtube-dl -f bestaudio --max-filesize 100M --extract-audio --audio-format mp3 --audio-quality 0 --no-check-certificate -o ""{filePathTemplate}"" ""https://youtu.be/{vid}""";
            var shellResult = ShellHelper.Execute(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl audio exited with code {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            var outputFilePath = $"{Output_Root}/yt/{vid}.mp3";
            if (!File.Exists(outputFilePath))
            {
                throw new Exception($"Audio filename {outputFilePath} not found after youtube-dl");
            }
            return outputFilePath;
        }

        public static YoutubeVideoResponse DownloadVideo(string vid, bool includeSubtitles)
        {
            var result = new YoutubeVideoResponse();
            var fileName = GetVideoFilename(vid);
            if (File.Exists(fileName))
            {
                result.VideoFileFullPath = fileName;
                return result;
            }
            var userPassParams = string.IsNullOrWhiteSpace(Youtube_User) ? "" : @$"-u ""{Youtube_User}"" -p ""{Youtube_Pass}"" ";
            var embedSubs = includeSubtitles ? "--write-sub --embed-subs " : "";
            var cmd = @$"youtube-dl -f ""bestvideo[height<=720][ext=mp4]"" --max-filesize 100M {userPassParams}-o ""{fileName}"" {embedSubs}--no-check-certificate ""https://youtu.be/{vid}""";

            var shellResult = ShellHelper.Execute(cmd);
            if (shellResult.ExitCode != 0)
            {
                throw new Exception($"youtube-dl video exited with code {shellResult.ExitCode}.\n{shellResult.Output}");
            }
            if (!File.Exists(fileName))
            {
                throw new Exception($"Audio filename {fileName} not found after youtube-dl");
            }
            result.VideoFileFullPath = fileName;
            return result;
        }

        public static bool AudioExists(string vid)
        {
            var fileName = GetAudioFilename(vid);
            return File.Exists(fileName);
        }

        private static string GetAudioFilename(string vid)
        {
            return $"{Cache_Root}/yt/download.audio/{vid}";
        }

        private static string GetVideoFilename(string vid)
        {
            return $"{Cache_Root}/yt/download.video/{vid}.mp4";
        }
    }
}
