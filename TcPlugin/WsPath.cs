using MaFi.WebShareCz.ApiClient.Entities;
using System;
using System.Linq;
using TcPluginBase.FileSystem;

namespace MaFi.WebShareCz.TcPlugin
{
    internal struct WsPath
    {
        public WsPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Segments = new string[0];
                return;
            }

            var separators = new[] { '\\', '/' };

            path = path.Trim();

            if (!separators.Contains(path[0]))
            {
                throw new NotSupportedException($"Relative paths are not supported! path: '{path}'");
            }

            var substring = path.Substring(1);
            Segments = string.IsNullOrEmpty(substring)
                ? new string[0]
                : substring.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        public string[] Segments { get; }

        public string Path => $"/{string.Join("/", Segments.Skip(2))}";

        public WsPath Parent => $"/{string.Join("/", Segments.Take(Segments.Length - 1))}";

        public string Name => Segments[Segments.Length - 1];

        public WsPathLevel Level => ((WsPathLevel)Segments.Length) <= WsPathLevel.Folder ? (WsPathLevel)Segments.Length : WsPathLevel.Folder;

        public string AccountName => GetSegment(1) ?? string.Empty;

        public bool IsPrivate => GetSegment(2) == Resources.TextResource.PublicFolder ? false : true;

        public WsFolderPath GetFolderPath() => new WsFolderPath(Path, IsPrivate);

        public WsFilePath GetFilePath() => new WsFilePath(Path, IsPrivate);

        public static implicit operator string(WsPath path)
        {
            return path.ToString();
        }

        public static implicit operator WsPath(string path)
        {
            return new WsPath(path);
        }

        public static implicit operator WsPath(RemotePath path)
        {
            return new WsPath(path);
        }

        public static implicit operator RemotePath(WsPath path)
        {
            return new RemotePath(path);
        }

        public override string ToString()
        {
            return Path;
        }

        private string GetSegment(int level)
        {
            var index = level - 1;
            if (index < 0)
            {
                return null;
            }
            return index < Segments.Length ? Segments[index] : null;
        }

    }
}
