using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TcPluginBase;
using TcPluginBase.FileSystem;
using MaFi.WebShareCz.TcPlugin.UI;

namespace MaFi.WebShareCz.TcPlugin
{
    public class WsFsPlugin : FsPlugin
    {
        private static FindData _backFolder = new FindData("..", FileAttributes.Directory);
        private static FindData _virtualEmptyFile = new FindData("<$VirtualEmptyFileForDeletingOrMovingFolderOnServer$>.virtual", FileAttributes.Normal);
        private readonly TcUIProvider _uiProvider;
        private readonly WsAccountCollection _accountRepository;
        private bool _deleteInProgress = false;
        private bool _moveInProgress = false;

        private static string ADD_NEW_ACCOUNT_TITLE => $"<{Resources.TextResource.AddAccount}>.lnk";

        public WsFsPlugin(Settings pluginSettings) : base(pluginSettings)
        {
            _uiProvider = new TcUIProvider(this.Prompt);
            _accountRepository = new WsAccountCollection(_uiProvider);
            Title = "WebShare.cz";
        }

        public override ExtractIconResult ExtractCustomIcon(RemotePath remoteName, ExtractIconFlags extractFlags)
        {
            WsPath wsPath = remoteName;
            switch (wsPath.Level)
            {
                case WsPathLevel.Account when wsPath.AccountName == ADD_NEW_ACCOUNT_TITLE:
                    return ExtractIconResult.Extracted(new System.Drawing.Icon(typeof(Resources.TextResource), "Add.ico"));
                case WsPathLevel.Account when remoteName.Path.EndsWith(@"\..\") == false:
                    return ExtractIconResult.Extracted(new System.Drawing.Icon(typeof(Resources.TextResource), "Account.ico"));
                case WsPathLevel.AccessLevel when wsPath.IsPrivate == true && remoteName.Path.EndsWith(@"\..\") == false: 
                    return ExtractIconResult.Extracted(new System.Drawing.Icon(typeof(Resources.TextResource), "FolderPrivate.ico"));
                case WsPathLevel.AccessLevel when wsPath.IsPrivate == false: 
                    return ExtractIconResult.Extracted(new System.Drawing.Icon(typeof(Resources.TextResource), "FolderPublic.ico"));
                default:
                    return ExtractIconResult.UseDefault;
            }
        }

        public override ExecResult ExecuteOpen(TcWindow mainWin, RemotePath remoteName)
        {
            WsPath wsPath = remoteName;
            switch (wsPath.Level)
            {
                case WsPathLevel.Account when wsPath.AccountName == ADD_NEW_ACCOUNT_TITLE:
                    return _accountRepository.AddNewAccount();
                default:
                    return ExecResult.Yourself;
            }
        }

        public override ExecResult ExecuteProperties(TcWindow mainWin, RemotePath remoteName)
        {
            WsPath wsPath = remoteName;
            switch (wsPath.Level)
            {
                case WsPathLevel.Account when wsPath.AccountName != ADD_NEW_ACCOUNT_TITLE:
                    Prompt.MsgOk("TODO:", "Show account properties");
                    return ExecResult.Ok;
                default:
                    return ExecResult.Yourself;
            }
        }

        public override IEnumerable<FindData> GetFiles(RemotePath path)
        {
            WsPath wsPath = path;
            switch (wsPath.Level)
            {
                case WsPathLevel.Root:
                    return new[] { new FindData(ADD_NEW_ACCOUNT_TITLE, FileAttributes.Normal | FileAttributes.ReadOnly) }.Concat(_accountRepository);
                case WsPathLevel.Account:
                    return new[] 
                    { 
                        new FindData(Resources.TextResource.PublicFolder, FileAttributes.Directory),
                        new FindData(Resources.TextResource.PrivateFolder, FileAttributes.Directory)
                    };
                default:
                    // return only one virtual file for non empty indication during deletion
                    if (_deleteInProgress)
                    {
                        if (wsPath.Level == WsPathLevel.AccessLevel)
                            return new[] { _backFolder };
                        using (IDisposableEnumerable<FindData> allFiles = _accountRepository[wsPath.AccountName].GetFolderAllFilesRecursive(wsPath))
                        {
                            FindData firstItem = allFiles.FirstOrDefault();
                            if (firstItem == null)
                                firstItem = _backFolder;
                            else
                                firstItem = _virtualEmptyFile;
                            return new[] { firstItem };
                        }
                    }
                    // return only one virtual file for move all folder on server, not file by file from client
                    if (_moveInProgress)
                        return new[] { _virtualEmptyFile };
                    // standard content
                    return _accountRepository[wsPath.AccountName].GetFolderItems(wsPath)?.DefaultIfEmpty(_backFolder) ?? new FindData[0];
            }
        }

        public override int FindClose(object o)
        {
            (o as IDisposable)?.Dispose();
            return 0;
        }

        public override void StatusInfo(string remoteDir, InfoStartEnd startEnd, InfoOperation infoOperation)
        {
            if (infoOperation == InfoOperation.Delete)
                _deleteInProgress = startEnd == InfoStartEnd.Start;
            if (infoOperation == InfoOperation.RenMovMulti)
                _moveInProgress = startEnd == InfoStartEnd.Start;
            base.StatusInfo(remoteDir, startEnd, infoOperation);
        }

        public override async Task<FileSystemExitCode> GetFileAsync(RemotePath remoteName, string localName, CopyFlags copyFlags, RemoteInfo remoteInfo, Action<int> setProgress, CancellationToken cancellationToken)
        {
            WsPath wsPath = remoteName;
            if (wsPath.Level == WsPathLevel.Account && wsPath.AccountName == ADD_NEW_ACCOUNT_TITLE)
                return FileSystemExitCode.NotSupported;

            FileInfo localFileName = new FileInfo(localName);
            bool overWrite = (CopyFlags.Overwrite & copyFlags) != 0;
            bool performMove = (CopyFlags.Move & copyFlags) != 0;
            bool resume = (CopyFlags.Resume & copyFlags) != 0;

            if (resume)
                return FileSystemExitCode.NotSupported;
            if (localFileName.Exists && !overWrite)
                return FileSystemExitCode.FileExists;

            return await _accountRepository[wsPath.AccountName].DownloadFile(
                wsPath,
                localFileName,
                overWrite,
                new Progress<int>(setProgress),
                performMove,
                cancellationToken
            );
        }

        public override async Task<FileSystemExitCode> PutFileAsync(string localName, RemotePath remoteName, CopyFlags copyFlags, Action<int> setProgress, CancellationToken cancellationToken)
        {
            WsPath wsPath = remoteName;
            if (wsPath.Level <= WsPathLevel.AccessLevel)
                return FileSystemExitCode.NotSupported;

            FileInfo localFileName = new FileInfo(localName);
            bool overWrite = (CopyFlags.Overwrite & copyFlags) != 0;
            bool performMove = (CopyFlags.Move & copyFlags) != 0;
            bool resume = (CopyFlags.Resume & copyFlags) != 0;

            if (resume)
                return FileSystemExitCode.NotSupported;
            if (!localFileName.Exists)
                return FileSystemExitCode.FileNotFound;

            FileSystemExitCode result = await _accountRepository[wsPath.AccountName].UploadFile(
                localFileName,
                wsPath,
                overWrite,
                new Progress<int>(setProgress),
                cancellationToken
            );

            if (performMove && result == FileSystemExitCode.OK)
                localFileName.Delete();
            return result;
        }

        public override bool MkDir(RemotePath remoteName)
        {
            if (_moveInProgress)
                return true;
            WsPath wsPath = remoteName;
            switch (wsPath.Level)
            {
                case WsPathLevel.Root:
                    return false;
                case WsPathLevel.Account:
                    // TODO: nefunguje return true, nedojde k refresh root složky
                    return _accountRepository.AddNewAccount(wsPath.AccountName).Equals(ExecResult.Yourself) == false;
                case WsPathLevel.AccessLevel:
                    return false;
                default:
                    return _accountRepository[wsPath.AccountName].CreateFolder(wsPath) != null;
            }
        }

        public override bool RemoveDir(RemotePath remoteName)
        {
            if (_moveInProgress)
                return true;
            WsPath wsPath = remoteName;
            switch (wsPath.Level)
            {
                case WsPathLevel.Root:
                    return false;
                case WsPathLevel.Account:
                    if (_uiProvider.ShowMessage(string.Format(Resources.TextResource.UnregisterAccount, wsPath.AccountName), true))
                        return _accountRepository.UnRegisterAccount(_accountRepository[wsPath.AccountName]);
                    return true;
                case WsPathLevel.AccessLevel:
                    return false;
                default:
                    return _accountRepository[wsPath.AccountName].DeleteFolder(wsPath);
            }
        }

        public override bool DeleteFile(RemotePath remoteName)
        {
            WsPath wsPath = remoteName;
            if (wsPath.Level <= WsPathLevel.AccessLevel)
                return false;
            if (wsPath.Path.EndsWith(_virtualEmptyFile.FileName))
                return true;
            return _accountRepository[wsPath.AccountName].DeleteFile(wsPath);
        }

        public override FileSystemExitCode RenMovFile(RemotePath oldRemoteName, RemotePath newRemoteName, bool move, bool overwrite, RemoteInfo remoteSourceInfo)
        {
            WsPath sourcePath = oldRemoteName;
            WsPath targetPath = newRemoteName;
            bool sourceIsFolder = (remoteSourceInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
            if (sourcePath.Level <= WsPathLevel.AccessLevel)
                return FileSystemExitCode.NotSupported;
            if (sourcePath.Name == _virtualEmptyFile.FileName)
            {
                sourcePath = sourcePath.Parent;
                targetPath = targetPath.Parent;
                sourceIsFolder = true;
            }
            if (sourcePath.AccountName != targetPath.AccountName)
            {
                // TODO: dodělat
                // Server not support it. Use standard copy by download/upload.
                return FileSystemExitCode.NotSupported;
            }
            if (ProgressProc(oldRemoteName, newRemoteName, 0))
                return FileSystemExitCode.UserAbort;
            try
            {
                if (move)
                    return _accountRepository[sourcePath.AccountName].MoveOrRenameItem(sourcePath, targetPath, overwrite, sourceIsFolder);
                else if (sourceIsFolder == false)
                    return _accountRepository[sourcePath.AccountName].CopyFile(sourcePath, targetPath, overwrite, new CancellableProgress(this, oldRemoteName, newRemoteName));

                _uiProvider.ShowMessage(Resources.TextResource.FolderCopyInWsNotSupported, false);
                return FileSystemExitCode.NotSupported;
            }
            finally
            {
                ProgressProc(oldRemoteName, newRemoteName, 100);
            }
        }

        public override PreviewBitmapResult GetPreviewBitmap(RemotePath remoteName, int width, int height)
        {
            WsPath sourcePath = remoteName;
            if (sourcePath.Level == WsPathLevel.Folder && sourcePath.Parent != "/") // Preview for root folder not supported
                return _accountRepository[sourcePath.AccountName].GetPreviewBitmap(sourcePath, width, height);
            return base.GetPreviewBitmap(remoteName, width, height);
        }

        private sealed class CancellableProgress : IProgress<int>
        {
            private readonly WsFsPlugin _plugin;
            private readonly RemotePath _oldRemoteName;
            private readonly RemotePath _newRemoteName;

            public CancellableProgress(WsFsPlugin plugin, RemotePath oldRemoteName, RemotePath newRemoteName)
            {
                _plugin = plugin;
                _oldRemoteName = oldRemoteName;
                _newRemoteName = newRemoteName;
            }
            public void Report(int value)
            {
                // TODO: not UI refresh and cancel
                if (_plugin.ProgressProc(_oldRemoteName, _newRemoteName, value))
                    throw new TaskCanceledException();
            }
        }
    }
}
