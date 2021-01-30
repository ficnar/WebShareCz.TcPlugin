using System;
using System.IO;
using MaFi.WebShareCz.ApiClient.Entities;
using TcPluginBase.FileSystem;

namespace MaFi.WebShareCz.TcPlugin
{
    internal static class WsItemExtensins
    {
        public static FindData ConvertToFindData(this WsItem item)
        {
            if (item is WsFolder folderInfo)
                return new FindData(folderInfo.PathInfo.Name, 0, FileAttributes.Directory, folderInfo.Created);
            if (item is WsFile fileInfo)
                return new FindData(fileInfo.PathInfo.Name, (ulong)fileInfo.Size, FileAttributes.Normal, fileInfo.Created);
            throw new ArgumentException($"Not supported {typeof(WsItem).Name} of type {item.GetType().Name}");
        }
    }
}
